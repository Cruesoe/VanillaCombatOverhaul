using HarmonyLib;
using Verse;

namespace VanillaCombatOverhaul
{
    /// <summary>Ends burst tracking once a point-blank burst has fired its last shot.</summary>
    [HarmonyPatch(typeof(Verb), "TryCastNextBurstShot")]
    public static class Patch_Verb_TryCastNextBurstShot_PointBlank
    {
        public static void Postfix(Verb __instance) => PointBlankUtility.NotifyBurstProgress(__instance);
    }
}
