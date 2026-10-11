using HarmonyLib;
using RimWorld;
using Verse;

namespace VanillaCombatOverhaul
{
    /// <summary>Replaces the melee cast with a point-blank shot when the roll succeeds; the melee verb then does not run.</summary>
    [HarmonyPatch(typeof(Verb), nameof(Verb.TryStartCastOn), new[]
    {
        typeof(LocalTargetInfo), typeof(LocalTargetInfo), typeof(bool), typeof(bool), typeof(bool), typeof(bool)
    })]
    public static class Patch_Verb_TryStartCastOn_PointBlank
    {
        public static bool Prefix(Verb __instance, LocalTargetInfo castTarg, ref bool __result)
        {
            if (!(__instance is Verb_MeleeAttack) || !PointBlankUtility.Enabled)
            {
                return true;
            }
            if (!PointBlankUtility.TryReplace(__instance, castTarg))
            {
                return true;
            }
            __result = true;
            return false;
        }
    }
}
