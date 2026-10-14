using HarmonyLib;
using Verse;

namespace VanillaCombatOverhaul
{
    /// <summary>A run-and-gun shot's aim and cooldown do not hold the pawn in place while it walks.</summary>
    [HarmonyPatch(typeof(Stance_Busy), nameof(Stance_Busy.StanceBusy), MethodType.Getter)]
    public static class Patch_Stance_Busy_StanceBusy
    {
        public static void Postfix(Stance_Busy __instance, ref bool __result)
        {
            if (__result && RunAndGunUtility.AnyActive && __instance.stanceTracker?.pawn is Pawn pawn
                && RunAndGunUtility.ShootingOnTheMove(pawn, __instance.verb))
            {
                __result = false;
            }
        }
    }
}
