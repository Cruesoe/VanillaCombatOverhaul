using HarmonyLib;
using Verse;

namespace VanillaCombatOverhaul
{
    /// <summary>
    /// Pins the RNG across map generation for a seeded autotest run, and does nothing at all
    /// otherwise.
    ///
    /// Seeding the call that *asks* for a map is not enough: PageUtility.InitGameStart queues
    /// the generation rather than performing it, so a Rand state pushed around that call is
    /// popped again long before any terrain exists. The world seed pinned fine -- the world is
    /// built inline -- which is exactly why this was easy to miss: two runs reported the same
    /// world seed and the same tile while quietly building maps that differed by hundreds of
    /// things, and every thingIDNumber shifted with them.
    ///
    /// This narrows the variance but does NOT make generation reproducible, and it is worth
    /// being precise about why: RimWorld 1.6 generates parts of a map in parallel, and
    /// Verse.Rand is a shared static rather than [ThreadStatic], so worker threads interleave
    /// draws from one generator in an order decided by the scheduler. Measured with this patch
    /// confirmed live, three runs of one seed still produced 58, 73 and 82 pawns. Nothing a mod
    /// can do fixes that; the suite is therefore treated as statistical, not reproducible, and
    /// its tolerances are set from sample size accordingly.
    ///
    /// Scoped to generation only. Ordinary play never sees this because no trigger file exists,
    /// and an unseeded run (seed 0) is left alone deliberately.
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

        // A finalizer rather than a postfix: generation throwing must not leave the state
        // stack unbalanced, or every later Rand consumer inherits a stuck seed.
        public static void Finalizer(bool __state)
        {
            if (__state)
            {
                Rand.PopState();
            }
        }
    }
}
