using HarmonyLib;
using Verse;

namespace VanillaCombatOverhaul
{
    /// <summary>Re-applies weapon-matched stock (Progression: Ammunition) when a colonist's primary weapon changes.</summary>
    [HarmonyPatch(typeof(Pawn_EquipmentTracker), nameof(Pawn_EquipmentTracker.Notify_EquipmentAdded))]
    public static class Patch_Pawn_EquipmentTracker_Notify_EquipmentAdded
    {
        public static void Postfix(Pawn_EquipmentTracker __instance, ThingWithComps eq)
        {
            // A sidearm swap is skipped: the stowed primary still decides the ammunition. Pawns generated before
            // the game starts are skipped too: reading their loadout would create one before the starting loadouts.
            if (!LoadoutStockUtility.AmmunitionEnabled || !LoadoutUtility.Enabled || LoadoutUtility.Swapping
                || Current.ProgramState != ProgramState.Playing)
            {
                return;
            }
            var pawn = __instance.pawn;
            if (pawn == null || !pawn.IsColonist || pawn.equipment?.Primary != eq)
            {
                return;
            }
            var comp = LoadoutUtility.CompFor(pawn);
            if (comp?.SwappedPrimary == null)
            {
                LoadoutStockUtility.ApplyWeaponMatch(pawn, comp?.Loadout);
            }
        }
    }
}
