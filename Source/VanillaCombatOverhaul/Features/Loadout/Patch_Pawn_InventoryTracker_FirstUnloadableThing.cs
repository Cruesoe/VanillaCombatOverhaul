using System.Collections.Generic;
using System.Reflection;
using System.Reflection.Emit;
using HarmonyLib;
using Verse;

namespace VanillaCombatOverhaul
{
    /// <summary>
    /// Adds the loadout's items, sidearm and swapped-out primary to the list of things a pawn keeps
    /// when unloading its inventory, beside vanilla's drug policy and medicine entries.
    /// </summary>
    [HarmonyPatch(typeof(Pawn_InventoryTracker), nameof(Pawn_InventoryTracker.FirstUnloadableThing), MethodType.Getter)]
    public static class Patch_Pawn_InventoryTracker_FirstUnloadableThing
    {
        private static readonly TranspilerGuard Guard = PatchGuard.Declare("Loadout.KeepInventory");

        private static readonly FieldInfo InnerContainer =
            AccessTools.Field(typeof(Pawn_InventoryTracker), nameof(Pawn_InventoryTracker.innerContainer));
        private static readonly FieldInfo PawnField =
            AccessTools.Field(typeof(Pawn_InventoryTracker), nameof(Pawn_InventoryTracker.pawn));
        private static readonly FieldInfo ItemsToKeep =
            AccessTools.Field(typeof(Pawn_InventoryTracker), "tmpItemsToKeep");
        private static readonly MethodInfo AddKeptItems =
            AccessTools.Method(typeof(LoadoutUtility), nameof(LoadoutUtility.AddKeptItems));

        public static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions)
        {
            var codes = new List<CodeInstruction>(instructions);
            if (Splice(codes))
            {
                Guard.Spliced();
            }
            return codes;
        }

        public static void Postfix(Pawn_InventoryTracker __instance, ref ThingCount __result) =>
            LoadoutUtility.AvoidUnloadingKeptWeapon(__instance.pawn, ref __result);

        /// <summary>Inserts the AddKeptItems call before the loop over innerContainer; returns false if no loop was found.</summary>
        internal static bool Splice(List<CodeInstruction> codes)
        {
            var index = FindItemLoopStart(codes);
            if (index < 0)
            {
                return false;
            }
            var start = codes[index];
            var call = new[]
            {
                new CodeInstruction(OpCodes.Ldarg_0),
                new CodeInstruction(OpCodes.Ldfld, PawnField),
                new CodeInstruction(OpCodes.Ldsfld, ItemsToKeep),
                new CodeInstruction(OpCodes.Call, AddKeptItems)
            };
            // The insertion point can follow a finally block, so its labels and block markers move with it.
            start.MoveLabelsTo(call[0]);
            start.MoveBlocksTo(call[0]);
            codes.InsertRange(index, call);
            return true;
        }

        /// <summary>Finds the first instruction of the loop over innerContainer, compiled as a foreach or an indexed for loop.</summary>
        private static int FindItemLoopStart(List<CodeInstruction> codes)
        {
            for (var i = 0; i < codes.Count; i++)
            {
                // foreach: ldarg.0; ldfld innerContainer; callvirt GetEnumerator.
                if (i + 2 < codes.Count && codes[i].opcode == OpCodes.Ldarg_0 && codes[i + 1].LoadsField(InnerContainer)
                    && IsContainerCall(codes[i + 2], "GetEnumerator"))
                {
                    return i;
                }
                // for: ldc.i4.0; stloc; br COND, where COND compares the index with innerContainer.Count.
                if (i + 2 < codes.Count && codes[i].opcode == OpCodes.Ldc_I4_0 && codes[i + 1].IsStloc()
                    && (codes[i + 2].opcode == OpCodes.Br || codes[i + 2].opcode == OpCodes.Br_S)
                    && codes[i + 2].operand is Label condition && ConditionReadsContainerCount(codes, condition))
                {
                    return i;
                }
            }
            return -1;
        }

        private static bool ConditionReadsContainerCount(List<CodeInstruction> codes, Label condition)
        {
            var target = codes.FindIndex(c => c.labels.Contains(condition));
            if (target < 0 || target + 3 >= codes.Count)
            {
                return false;
            }
            return codes[target].IsLdloc() && codes[target + 1].opcode == OpCodes.Ldarg_0
                && codes[target + 2].LoadsField(InnerContainer) && IsContainerCall(codes[target + 3], "get_Count");
        }

        private static bool IsContainerCall(CodeInstruction code, string name) =>
            (code.opcode == OpCodes.Callvirt || code.opcode == OpCodes.Call) && code.operand is MethodInfo method
            && method.Name == name && typeof(ThingOwner).IsAssignableFrom(method.DeclaringType);
    }
}
