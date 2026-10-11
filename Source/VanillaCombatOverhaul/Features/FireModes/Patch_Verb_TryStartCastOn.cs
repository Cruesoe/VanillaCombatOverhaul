using HarmonyLib;
using Verse;

namespace VanillaCombatOverhaul
{
    /// <summary>Records the Auto mode for the target and pushes shot context for every pawn cast, so stat parts see the verb being cast.</summary>
    [HarmonyPatch(typeof(Verb), nameof(Verb.TryStartCastOn), new[]
    {
        typeof(LocalTargetInfo), typeof(LocalTargetInfo), typeof(bool), typeof(bool), typeof(bool), typeof(bool)
    })]
    public static class Patch_Verb_TryStartCastOn
    {
        public static void Prefix(Verb __instance, LocalTargetInfo castTarg, ref CombatContext.Scope __state)
        {
            __state = default;
            var pawn = __instance.CasterPawn;
            if (pawn == null || !FireModeUtility.Enabled)
            {
                return;
            }
            if (FireModeUtility.IsModeVerb(pawn, __instance))
            {
                FireModeUtility.NoteTarget(pawn, castTarg);
            }
            __state = CombatContext.PushShot(pawn, __instance);
        }

        public static void Finalizer(ref CombatContext.Scope __state) => __state.Dispose();
    }
}
