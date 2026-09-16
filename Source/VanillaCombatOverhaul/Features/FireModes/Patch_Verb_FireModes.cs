using System;
using System.Reflection;
using HarmonyLib;
using Verse;

namespace VanillaCombatOverhaul
{
    /// <summary>
    /// Records the distance-picked mode before warmup is read, and pushes shot context so
    /// the aim time stat part knows which verb is being cast. Context is pushed for every
    /// pawn cast, not just gun shots: without it an ability would be judged by the primary
    /// weapon and slowed by its mode.
    /// </summary>
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

    /// <summary>
    /// Pushes shot context around each shot, so the cooldown stat read at the end of a burst
    /// knows which verb fired.
    /// </summary>
    [HarmonyPatch(typeof(Verb), "TryCastNextBurstShot")]
    public static class Patch_Verb_TryCastNextBurstShot
    {
        public static void Prefix(Verb __instance, ref CombatContext.Scope __state)
        {
            __state = default;
            var pawn = __instance.CasterPawn;
            if (pawn == null || !FireModeUtility.Enabled)
            {
                return;
            }
            __state = CombatContext.PushShot(pawn, __instance);
        }

        public static void Finalizer(ref CombatContext.Scope __state) => __state.Dispose();
    }

    /// <summary>
    /// Marks the one moment a burst is sized. Vanilla reads the shot count once, here, into
    /// burstShotsLeft; scoping the change to it leaves weapon info cards, DPS readouts and
    /// other mods reading BurstShotCount untouched.
    /// </summary>
    [HarmonyPatch(typeof(Verb), nameof(Verb.WarmupComplete))]
    public static class Patch_Verb_WarmupComplete
    {
        internal static Verb Current;

        private static readonly MethodInfo ShotsPerBurstGetter =
            AccessTools.PropertyGetter(typeof(Verb), "ShotsPerBurst");

        public static void Prefix(Verb __instance, out Verb __state)
        {
            __state = Current;
            Current = __instance;
        }

        public static void Finalizer(Verb __state) => Current = __state;

        /// <summary>The burst size a warmup would give this verb right now. For the arena.</summary>
        internal static int ShotsPerBurstFor(Verb verb)
        {
            var previous = Current;
            Current = verb;
            try
            {
                return (int)ShotsPerBurstGetter.Invoke(verb, Array.Empty<object>());
            }
            finally
            {
                Current = previous;
            }
        }
    }

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
