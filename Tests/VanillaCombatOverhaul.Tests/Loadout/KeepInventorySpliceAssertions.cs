using System.Collections.Generic;
using System.Reflection;
using System.Reflection.Emit;
using HarmonyLib;
using Verse;

namespace VanillaCombatOverhaul
{
    /// <summary>Runs the kept-inventory splice on synthetic foreach and for loops over innerContainer, the two shapes the game compiles.</summary>
    public static class KeepInventorySpliceAssertions
    {
        private static readonly FieldInfo InnerContainer =
            AccessTools.Field(typeof(Pawn_InventoryTracker), nameof(Pawn_InventoryTracker.innerContainer));
        private static readonly MethodInfo AddKeptItems =
            AccessTools.Method(typeof(LoadoutUtility), nameof(LoadoutUtility.AddKeptItems));

        public static List<AssertionResult> Tests()
        {
            var results = new List<AssertionResult>();
            var il = new DynamicMethod("VCO_SpliceTest", null, null).GetILGenerator();

            var foreachLoop = new List<CodeInstruction>
            {
                new CodeInstruction(OpCodes.Ldarg_0),
                new CodeInstruction(OpCodes.Ldfld, InnerContainer),
                new CodeInstruction(OpCodes.Callvirt, AccessTools.Method(typeof(ThingOwner<Thing>), nameof(ThingOwner<Thing>.GetEnumerator))),
                new CodeInstruction(OpCodes.Pop),
                new CodeInstruction(OpCodes.Ret)
            };
            var spliced = Patch_Pawn_InventoryTracker_FirstUnloadableThing.Splice(foreachLoop);
            results.Add(Check("keep splice finds a foreach loop", spliced && CallsAddKeptItemsAt(foreachLoop, 3),
                spliced ? "call misplaced" : "not found"));

            // A drug-policy style loop first, which must be skipped, then the loop over innerContainer.
            var drugCondition = il.DefineLabel();
            var drugBody = il.DefineLabel();
            var itemCondition = il.DefineLabel();
            var itemBody = il.DefineLabel();
            var forLoop = new List<CodeInstruction>
            {
                new CodeInstruction(OpCodes.Ldc_I4_0),
                new CodeInstruction(OpCodes.Stloc_0),
                new CodeInstruction(OpCodes.Br_S, drugCondition),
                new CodeInstruction(OpCodes.Nop) { labels = { drugBody } },
                new CodeInstruction(OpCodes.Ldloc_0) { labels = { drugCondition } },
                new CodeInstruction(OpCodes.Ldc_I4_5),
                new CodeInstruction(OpCodes.Blt_S, drugBody),
                new CodeInstruction(OpCodes.Ldc_I4_0),
                new CodeInstruction(OpCodes.Stloc_1),
                new CodeInstruction(OpCodes.Br_S, itemCondition),
                new CodeInstruction(OpCodes.Nop) { labels = { itemBody } },
                new CodeInstruction(OpCodes.Ldloc_1) { labels = { itemCondition } },
                new CodeInstruction(OpCodes.Ldarg_0),
                new CodeInstruction(OpCodes.Ldfld, InnerContainer),
                new CodeInstruction(OpCodes.Callvirt, AccessTools.PropertyGetter(typeof(ThingOwner), nameof(ThingOwner.Count))),
                new CodeInstruction(OpCodes.Blt_S, itemBody),
                new CodeInstruction(OpCodes.Ret)
            };
            spliced = Patch_Pawn_InventoryTracker_FirstUnloadableThing.Splice(forLoop);
            results.Add(Check("keep splice finds a for loop and skips the drug loop", spliced && CallsAddKeptItemsAt(forLoop, 10),
                spliced ? "call misplaced" : "not found"));

            var noLoop = new List<CodeInstruction> { new CodeInstruction(OpCodes.Ret) };
            results.Add(Check("keep splice leaves code without the loop alone",
                !Patch_Pawn_InventoryTracker_FirstUnloadableThing.Splice(noLoop) && noLoop.Count == 1, $"{noLoop.Count} instructions"));
            return results;
        }

        private static bool CallsAddKeptItemsAt(List<CodeInstruction> codes, int index) =>
            index < codes.Count && codes[index].Calls(AddKeptItems);

        private static AssertionResult Check(string name, bool passed, string detail) =>
            new AssertionResult { Name = name, Passed = passed, Detail = detail };
    }
}
