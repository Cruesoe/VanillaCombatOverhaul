using HarmonyLib;
using Verse;

namespace VanillaCombatOverhaul
{
    /// <summary>Makes a parry's counter-attack after the parried swing has finished.</summary>
    [HarmonyPatch(typeof(Verb), "TryCastNextBurstShot")]
    public static class Patch_Verb_TryCastNextBurstShot_Parry
    {
        public static void Finalizer(Verb __instance) => ParryUtility.ResolvePendingCounter(__instance);
    }
}
