using HarmonyLib;
using Verse;

namespace VanillaCombatOverhaul
{
    /// <summary>
    /// Pins Rand across map generation in a seeded test run; inert otherwise. Parallel parts of map
    /// generation still vary between runs, so the suite's tolerances are statistical.
    /// </summary>
    [HarmonyPatch(typeof(MapGenerator), nameof(MapGenerator.GenerateMap))]
    public static class Patch_MapGenerator_GenerateMap
    {
        public static void Prefix(out bool __state)
        {
            __state = false;
            if (!AutoTest.Requested)
            {
                return;
            }

            var seed = AutoTest.Seed;
            if (seed == 0)
            {
                return;
            }

            Rand.PushState(seed);
            __state = true;
            Log.Message("[VCO] Map generation pinned to seed " + seed + " (serial portion only).");
        }

        // A finalizer, so the state is popped even if generation throws.
        public static void Finalizer(bool __state)
        {
            if (__state)
            {
                Rand.PopState();
            }
        }
    }
}
