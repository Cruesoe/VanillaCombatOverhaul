using HarmonyLib;
using Verse;

namespace VanillaCombatOverhaul
{
    // Prefixes run before the projectile destroys itself, while its map and position are still valid.
    [HarmonyPatch(typeof(Projectile), "Impact")]
    public static class Patch_Projectile_Impact
    {
        public static void Prefix(Projectile __instance) =>
            SuppressionUtility.ApplyImpact(__instance, __instance.Map, __instance.ExactPosition, explosion: false);
    }

    // Explosives override Impact without calling the base method.
    [HarmonyPatch(typeof(Projectile_Explosive), "Explode")]
    public static class Patch_Projectile_Explosive_Explode
    {
        public static void Prefix(Projectile_Explosive __instance) =>
            SuppressionUtility.ApplyImpact(__instance, __instance.Map, __instance.ExactPosition, explosion: true);
    }
}
