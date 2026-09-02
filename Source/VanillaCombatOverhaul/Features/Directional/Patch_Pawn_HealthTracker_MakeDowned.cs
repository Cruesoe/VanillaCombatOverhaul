using HarmonyLib;
using Verse;

namespace VanillaCombatOverhaul
{
    [HarmonyPatch(typeof(Pawn_HealthTracker), "MakeDowned")]
    public static class Patch_Pawn_HealthTracker_MakeDowned
    {
        public static void Postfix(Pawn ___pawn)
        {
            if (___pawn != null && ___pawn.Downed)
            {
                HeightTargetingUtility.Reset(___pawn);
            }
        }
    }
}
