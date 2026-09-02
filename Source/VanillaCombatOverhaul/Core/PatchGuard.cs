using System.Collections.Generic;
using System.Linq;
using Verse;

namespace VanillaCombatOverhaul
{
    /// <summary>
    /// A Harmony transpiler that fails to find its target emits the original method and
    /// reports no error, so the feature silently stops working after a RimWorld update.
    /// Every transpiler in this mod declares a guard and reports its splices; the guards
    /// are checked once after patching so a broken patch is loud instead of invisible.
    /// </summary>
    public sealed class TranspilerGuard
    {
        public readonly string Id;
        public readonly int Expected;
        public int Actual { get; private set; }

        internal TranspilerGuard(string id, int expected)
        {
            Id = id;
            Expected = expected;
        }

        /// <summary>Call once per successful splice inside the transpiler body.</summary>
        public void Spliced() => Actual++;

        public bool Satisfied => Actual == Expected;
    }

    public static class PatchGuard
    {
        private static readonly Dictionary<string, TranspilerGuard> Guards =
            new Dictionary<string, TranspilerGuard>();

        /// <summary>
        /// Declares an expected number of IL splices for a named patch. Call this from the
        /// transpiler's declaring type so the guard exists before Harmony runs the patch.
        /// </summary>
        public static TranspilerGuard Declare(string id, int expected = 1)
        {
            if (Guards.TryGetValue(id, out var existing))
            {
                return existing;
            }
            var guard = new TranspilerGuard(id, expected);
            Guards[id] = guard;
            return guard;
        }

        /// <summary>
        /// Reports any transpiler that did not splice the expected number of times.
        /// Harmony applies transpilers during PatchAll, so this is accurate immediately after.
        /// </summary>
        public static void VerifyAll()
        {
            var broken = Guards.Values.Where(g => !g.Satisfied).ToList();
            if (broken.Count == 0)
            {
                if (VCOMod.Settings?.verboseLogging ?? false)
                {
                    Log.Message($"[VCO] All {Guards.Count} guarded patches applied cleanly.");
                }
                return;
            }

            foreach (var g in broken)
            {
                Log.Error(
                    $"[VCO] Patch '{g.Id}' spliced {g.Actual} time(s) but expected {g.Expected}. " +
                    "The feature it backs is disabled or degraded. This usually means a RimWorld " +
                    "update changed the target method, or another mod replaced it.");
            }
        }

        /// <summary>Snapshot for the in-game diagnostics readout.</summary>
        public static IEnumerable<TranspilerGuard> All => Guards.Values;
    }
}
