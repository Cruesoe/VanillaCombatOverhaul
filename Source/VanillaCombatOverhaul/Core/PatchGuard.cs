using System.Collections.Generic;
using System.Linq;
using Verse;

namespace VanillaCombatOverhaul
{
    /// <summary>Expected splice count for a transpiler; checked after patching so a transpiler that finds no target logs an error.</summary>
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

        /// <summary>Declares the expected splice count for a named transpiler, from a static field of its patch class.</summary>
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

        /// <summary>Logs an error for each transpiler that did not splice the expected number of times; call after PatchAll.</summary>
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
