using HarmonyLib;
using Verse;

namespace VanillaCombatOverhaul
{
    /// <summary>Pushes shot context around each shot, for vanilla's hit roll and the cooldown stat read at the end of a burst.</summary>
    [HarmonyPatch(typeof(Verb), "TryCastNextBurstShot")]
    public static class Patch_Verb_TryCastNextBurstShot
    {
        public static void Prefix(Verb __instance, ref CombatContext.Scope __state)
        {
            __state = __instance.caster == null ? default : CombatContext.PushShot(__instance.caster, __instance);
        }

        public static void Finalizer(ref CombatContext.Scope __state) => __state.Dispose();
    }
}
