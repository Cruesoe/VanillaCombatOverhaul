using HarmonyLib;
using Verse;

namespace VanillaCombatOverhaul
{
    /// <summary>Ends burst tracking when a burst is abandoned: stun, despawn, job change.</summary>
    [HarmonyPatch(typeof(Verb), nameof(Verb.Reset))]
    public static class Patch_Verb_Reset_PointBlank
    {
        public static void Postfix(Verb __instance) => PointBlankUtility.NotifyBurstProgress(__instance);
    }
}
