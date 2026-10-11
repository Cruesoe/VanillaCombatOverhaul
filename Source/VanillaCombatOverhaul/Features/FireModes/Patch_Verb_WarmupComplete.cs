using System;
using System.Reflection;
using HarmonyLib;
using Verse;

namespace VanillaCombatOverhaul
{
    /// <summary>Marks the warmup, where vanilla sizes the burst, so only that read of BurstShotCount is changed.</summary>
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
}
