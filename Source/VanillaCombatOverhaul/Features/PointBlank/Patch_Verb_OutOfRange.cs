using HarmonyLib;
using Verse;

namespace VanillaCombatOverhaul
{
    /// <summary>Lifts vanilla's adjacent-target minimum range for point-blank casts; the weapon's own minimum range still applies.</summary>
    [HarmonyPatch(typeof(Verb), nameof(Verb.OutOfRange))]
    public static class Patch_Verb_OutOfRange
    {
        public static void Postfix(Verb __instance, IntVec3 root, CellRect occupiedRect, ref bool __result)
        {
            if (!__result || !PointBlankUtility.IsPointBlankCast(__instance))
            {
                return;
            }
            var minRange = __instance.verbProps.EffectiveMinRange(allowAdjacentShot: true);
            var range = __instance.EffectiveRange;
            float distSquared = occupiedRect.ClosestDistSquaredTo(root);
            __result = distSquared > range * range || distSquared < minRange * minRange;
        }
    }
}
