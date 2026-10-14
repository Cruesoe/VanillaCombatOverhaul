using HarmonyLib;
using Verse;

namespace VanillaCombatOverhaul
{
    [HarmonyPatch(typeof(Verb), nameof(Verb.BurstShotCount), MethodType.Getter)]
    public static class Patch_Verb_BurstShotCount
    {
        public static void Postfix(Verb __instance, ref int __result)
        {
            var current = Patch_Verb_WarmupComplete.Current;
            if (current != __instance)
            {
                return;
            }
            // Cleared while the mode is resolved, so nothing it reads can re-enter here.
            Patch_Verb_WarmupComplete.Current = null;
            try
            {
                __result = FireModeUtility.BurstShotCountFor(__instance, __result);
            }
            finally
            {
                Patch_Verb_WarmupComplete.Current = current;
            }
        }
    }
}
