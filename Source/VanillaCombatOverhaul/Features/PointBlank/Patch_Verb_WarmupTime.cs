using HarmonyLib;
using Verse;

namespace VanillaCombatOverhaul
{
    /// <summary>Zero warmup for a point-blank cast only; everything else reads the weapon's real warmup.</summary>
    [HarmonyPatch(typeof(Verb), nameof(Verb.WarmupTime), MethodType.Getter)]
    public static class Patch_Verb_WarmupTime
    {
        public static void Postfix(Verb __instance, ref float __result)
        {
            if (__result > 0f && PointBlankUtility.IsPointBlankCast(__instance))
            {
                __result = 0f;
            }
        }
    }
}
