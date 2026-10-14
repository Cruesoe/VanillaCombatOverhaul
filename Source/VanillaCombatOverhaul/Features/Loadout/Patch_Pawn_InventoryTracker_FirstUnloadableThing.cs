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
        private static readonly MethodInfo GetEnumerator =
            AccessTools.Method(typeof(ThingOwner<Thing>), nameof(ThingOwner<Thing>.GetEnumerator));
        private static readonly MethodInfo AddKeptItems =
            AccessTools.Method(typeof(LoadoutUtility), nameof(LoadoutUtility.AddKeptItems));

        public static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions)
        {
            var codes = new List<CodeInstruction>(instructions);
            for (var i = 2; i < codes.Count; i++)
            {
                // The foreach over innerContainer starts with ldarg.0; ldfld innerContainer; callvirt GetEnumerator.
                if (!codes[i].Calls(GetEnumerator) || !codes[i - 1].LoadsField(InnerContainer)
                    || codes[i - 2].opcode != OpCodes.Ldarg_0)
                {
                    continue;
                }
                var start = codes[i - 2];
                var call = new[]
                {
                    new CodeInstruction(OpCodes.Ldarg_0),
                    new CodeInstruction(OpCodes.Ldfld, PawnField),
                    new CodeInstruction(OpCodes.Ldsfld, ItemsToKeep),
                    new CodeInstruction(OpCodes.Call, AddKeptItems)
                };
                // The insertion point follows a finally block, so its labels and block markers move with it.
                start.MoveLabelsTo(call[0]);
                start.MoveBlocksTo(call[0]);
                codes.InsertRange(i - 2, call);
                Guard.Spliced();
                break;
            }
            return codes;
        }
    }
}
