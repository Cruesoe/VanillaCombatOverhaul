using HarmonyLib;
using Verse;
using Verse.AI;

namespace VanillaCombatOverhaul
{
    /// <summary>Gives moving pawns a chance to start a shot every CheckInterval ticks.</summary>
    [HarmonyPatch(typeof(Pawn_PathFollower), nameof(Pawn_PathFollower.PatherTick))]
    public static class Patch_Pawn_PathFollower_PatherTick
    {
        public static void Postfix(Pawn_PathFollower __instance, Pawn ___pawn)
        {
            if (__instance.Moving && ___pawn.IsHashIntervalTick(RunAndGunUtility.CheckInterval))
            {
                RunAndGunUtility.TryShoot(___pawn);
            }
        }
    }
}
