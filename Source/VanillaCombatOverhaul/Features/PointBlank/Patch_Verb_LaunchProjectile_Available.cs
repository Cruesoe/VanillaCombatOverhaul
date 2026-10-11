using HarmonyLib;
using Verse;

namespace VanillaCombatOverhaul
{
    /// <summary>Lifts the adjacent-threat block on launching projectiles for point-blank casts; fuel, charges and roles still apply.</summary>
    [HarmonyPatch(typeof(Verb_LaunchProjectile), nameof(Verb_LaunchProjectile.Available))]
    public static class Patch_Verb_LaunchProjectile_Available
    {
        public static void Postfix(Verb_LaunchProjectile __instance, ref bool __result)
        {
            if (__result || !PointBlankUtility.IsPointBlankCast(__instance))
            {
                return;
            }
            __result = VerbBaseAvailable.Available(__instance) && __instance.Projectile != null;
        }
    }
}
