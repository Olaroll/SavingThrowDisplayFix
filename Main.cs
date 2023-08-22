using System;
using System.Linq;
using System.Collections.Generic;
using System.Reflection;
using System.Reflection.Emit;
using UnityModManagerNet;
using HarmonyLib;
using Kingmaker.RuleSystem.Rules;
using Kingmaker.RuleSystem;
using Kingmaker.UI._ConsoleUI.Overtips;
using Kingmaker.RuleSystem.Rules.Damage;
using Kingmaker.UI.CombatText;
using Kingmaker.Utility;

namespace SavingThrowDisplayFix
{
    static class Main
    {
        public static string ModId;

        static void Log(string str)
        {
            UnityModManager.Logger.Log(str, $"[{ModId}] ");
        }

        public static bool Load(UnityModManager.ModEntry modEntry)
        {
            ModId = modEntry.Info.Id;

            var harmony = new Harmony(ModId);
            harmony.PatchAll(Assembly.GetExecutingAssembly());

            return true;
        }

        [HarmonyPatch(typeof(OvertipsVM), nameof(OvertipsVM.OnEventDidTrigger), new Type[] { typeof(RuleSavingThrow) })]
        public class OvertipsVM_OnEventDidTrigger_RuleSavingThrow_Patch
        {
            [HarmonyPrefix]
            static bool Prefix(RuleSavingThrow evt, OvertipsVM __instance)
            {
                OnEventDidTrigger(evt, __instance);
                return false; // Always skip the original code
            }

            static void OnEventDidTrigger(RuleSavingThrow evt, OvertipsVM __instance)
            {
                EntityOvertipVM entityOvertipVM;
                if (Rulebook.CurrentContext.CurrentEvent == null || !__instance.OvertipVms.TryGetValue(Rulebook.CurrentContext.CurrentEvent.Initiator, out entityOvertipVM))
                {
                    return;
                }
                if (Rulebook.CurrentContext.AllEvents.Any((RulebookEvent rulebookEvent) => rulebookEvent.Initiator == evt.Initiator && rulebookEvent is RuleDealDamage && !evt.IsPassed))
                {
                    return;
                }
                entityOvertipVM.CombatMessage.Execute(new EntityOvertipVM.CombatMessageSavingThrow
                {
                    Passed = evt.IsPassed,
                    Reason = evt.Reason.Name,
                    Sprite = evt.Reason.Icon,
                    StatType = evt.StatType,
                    Roll = evt.D20,
                    // DC = evt.DifficultyClass - evt.SuccessBonus
                    DC = evt.DifficultyClass - evt.SuccessBonus - evt.StatValue
                });
            }
        }

        [HarmonyPatch(typeof(CombatTextManager), nameof(CombatTextManager.OnEventDidTrigger), new Type[] { typeof(RuleSavingThrow) })]
        static class CombatTextManager_OnEventDidTrigger_RuleSavingThrow_Patch
        {
            // Using a transpiler here because I couldn't figure out runtime reflection to access private properties and methods

            [HarmonyTranspiler]
            static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions)
            {
                var code = new List<CodeInstruction>(instructions);
                //  text = UIStrings.Instance.CombatTexts.GetTbmCombatText(text, evt.D20, evt.DifficultyClass - evt.SuccessBonus - evt.StatValue);
                //* 0x000000C6 280A980006   */ IL_00C6: call      class Kingmaker.Blueprints.Root.Strings.UIStrings Kingmaker.Blueprints.Root.Strings.UIStrings::get_Instance()
                //* 0x000000CB 7BEC630004   */ IL_00CB: ldfld     class Kingmaker.Blueprints.Root.Strings.UICombatTexts Kingmaker.Blueprints.Root.Strings.UIStrings::CombatTexts
                //* 0x000000D0 07           */ IL_00D0: ldloc.1
                //* 0x000000D1 06           */ IL_00D1: ldloc.0
                //* 0x000000D2 7B????????   */ IL_00D2: ldfld     class Kingmaker.RuleSystem.Rules.RuleSavingThrow Kingmaker.UI.CombatText.CombatTextManager/'<>c__DisplayClass15_0'::evt
                //* 0x000000D7 6F06730006   */ IL_00D7: callvirt  instance valuetype Kingmaker.RuleSystem.RulebookEvent/RollEntry Kingmaker.RuleSystem.Rules.RuleSavingThrow::get_D20()
                //* 0x000000DC 2879BA0006   */ IL_00DC: call      int32 Kingmaker.RuleSystem.RulebookEvent/RollEntry::op_Implicit(valuetype Kingmaker.RuleSystem.RulebookEvent/RollEntry)
                //* 0x000000E1 06           */ IL_00E1: ldloc.0
                //* 0x000000E2 7B????????   */ IL_00E2: ldfld     class Kingmaker.RuleSystem.Rules.RuleSavingThrow Kingmaker.UI.CombatText.CombatTextManager/'<>c__DisplayClass15_0'::evt
                //* 0x000000E7 7B6E4B0004   */ IL_00E7: ldfld     int32 Kingmaker.RuleSystem.Rules.RuleSavingThrow::DifficultyClass
                //* 0x000000EC 06           */ IL_00EC: ldloc.0
                //* 0x000000ED 7B????????   */ IL_00ED: ldfld     class Kingmaker.RuleSystem.Rules.RuleSavingThrow Kingmaker.UI.CombatText.CombatTextManager/'<>c__DisplayClass15_0'::evt
                //* 0x000000F2 6F10730006   */ IL_00F2: callvirt  instance int32 Kingmaker.RuleSystem.Rules.RuleSavingThrow::get_SuccessBonus()
                //* 0x000000F7 59           */ IL_00F7: sub
                // ----- NEW CODE START -----
                //* 0x000000F8 06           */ IL_00F8: ldloc.0
                //* 0x000000F9 7B????????   */ IL_00F9: ldfld     class Kingmaker.RuleSystem.Rules.RuleSavingThrow Kingmaker.UI.CombatText.CombatTextManager/'<>c__DisplayClass15_0'::evt
                //* 0x000000FE 6F17730006   */ IL_00FE: callvirt  instance int32 Kingmaker.RuleSystem.Rules.RuleSavingThrow::get_StatValue()
                //* 0x00000103 59           */ IL_0103: sub
                // ----- NEW CODE END -----
                //* 0x00000104 6F30980006   */ IL_0104: callvirt  instance string Kingmaker.Blueprints.Root.Strings.UICombatTexts::GetTbmCombatText(string, int32, int32) <--- INSERT INDEX
                //* 0x00000109 0B           */ IL_0109: stloc.1

                int foundIndex = -1;
                for (int i = 0; i < code.Count - 1; i++) // -1 since we will be checking i + 1
                {
                    if (code[i].opcode == OpCodes.Call
                        && code[i + 1].opcode == OpCodes.Ldfld
                        && code[i + 2].opcode == OpCodes.Ldloc_1
                        && code[i + 3].opcode == OpCodes.Ldloc_0
                        && code[i + 4].opcode == OpCodes.Ldfld
                        && code[i + 5].opcode == OpCodes.Callvirt
                        && code[i + 6].opcode == OpCodes.Call
                        && code[i + 7].opcode == OpCodes.Ldloc_0
                        && code[i + 8].opcode == OpCodes.Ldfld
                        && code[i + 9].opcode == OpCodes.Ldfld
                        && code[i + 10].opcode == OpCodes.Ldloc_0
                        && code[i + 11].opcode == OpCodes.Ldfld
                        && code[i + 12].opcode == OpCodes.Callvirt
                        && code[i + 13].opcode == OpCodes.Sub
                        // CODE WILL BE INSERTED HERE
                        && code[i + 14].opcode == OpCodes.Callvirt // <--- INSERT INDEX
                        && code[i + 15].opcode == OpCodes.Stloc_1
                    )
                    {
                        foundIndex = i;
                        break;
                    }
                }

                if (foundIndex == -1)
                {
                    Log("Transpiler patch couldn't find the right code to modify, mod might need updating");
                    throw new Exception("Transpiler patch couldn't find the right code to modify");
                }

                object ldfldOperand = code[foundIndex+11].operand;
                int insertionIndex = foundIndex + 14;

                var instructionsToInsert = new List<CodeInstruction>();

                instructionsToInsert.Add(new CodeInstruction(OpCodes.Ldloc_0));
                instructionsToInsert.Add(new CodeInstruction(OpCodes.Ldfld, ldfldOperand));
                instructionsToInsert.Add(new CodeInstruction(OpCodes.Callvirt, AccessTools.PropertyGetter(typeof(RuleSavingThrow), nameof(RuleSavingThrow.StatValue))));
                instructionsToInsert.Add(new CodeInstruction(OpCodes.Sub));

                code.InsertRange(insertionIndex, instructionsToInsert);

                return code;
            }
        }
    }
}
