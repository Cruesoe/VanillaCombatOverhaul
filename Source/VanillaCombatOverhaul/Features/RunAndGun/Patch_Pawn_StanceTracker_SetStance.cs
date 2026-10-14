using HarmonyLib;
using Verse;

namespace VanillaCombatOverhaul
{
    /// <summary>Ends run-and-gun tracking once the shot is over.</summary>
    [HarmonyPatch(typeof(Pawn_StanceTracker), nameof(Pawn_StanceTracker.SetStance))]
    public static class Patch_Pawn_StanceTracker_SetStance
    {
        public static void Postfix(Pawn_StanceTracker __instance, Stance newStance)
        {
            if (RunAndGunUtility.AnyActive)
            {
                RunAndGunUtility.NotifyStanceChanged(__instance.pawn, newStance);
            }
        }
    }
}
