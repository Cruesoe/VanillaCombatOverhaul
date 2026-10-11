using HarmonyLib;
using Verse;

namespace VanillaCombatOverhaul
{
    /// <summary>Verb.Available as a non-virtual call, which C# cannot express directly.</summary>
    [HarmonyPatch(typeof(Verb), nameof(Verb.Available))]
    public static class VerbBaseAvailable
    {
        [HarmonyReversePatch]
        public static bool Available(Verb instance) =>
            throw new System.NotImplementedException("Replaced by Harmony at startup.");
    }
}
