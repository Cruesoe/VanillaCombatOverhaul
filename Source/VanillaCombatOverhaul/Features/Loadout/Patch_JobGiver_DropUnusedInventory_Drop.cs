using HarmonyLib;
using RimWorld;
using UnityEngine;
using Verse;

namespace VanillaCombatOverhaul
{
    /// <summary>
    /// Vanilla drops old raw food and drugs the drug policy does not carry; items the loadout keeps are dropped
    /// only beyond the loadout's count.
    /// </summary>
    [HarmonyPatch(typeof(JobGiver_DropUnusedInventory), "Drop")]
    public static class Patch_JobGiver_DropUnusedInventory_Drop
    {
        public static bool Prefix(Pawn pawn, Thing thing)
        {
            var keep = LoadoutUtility.KeptCount(pawn, thing.def);
            if (keep <= 0)
            {
                return true;
            }
            var excess = Mathf.Min(pawn.inventory.Count(thing.def) - keep, thing.stackCount);
            if (excess > 0)
            {
                pawn.inventory.innerContainer.TryDrop(thing, pawn.Position, pawn.Map, ThingPlaceMode.Near, excess, out _);
            }
            return false;
        }
    }
}
