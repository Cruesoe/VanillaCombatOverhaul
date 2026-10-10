using System.Collections.Generic;
using System.Linq;
using System.Text;
using Verse;

namespace VanillaCombatOverhaul
{
    /// <summary>
    /// Counters and sampled values for how often each mechanic fires, recorded only with verbose
    /// logging on (the test suite turns it on). Dumped on an interval or from the debug actions.
    /// </summary>
    public static class VCODiagnostics
    {
        private static readonly Dictionary<string, long> Counters = new Dictionary<string, long>();
        private static readonly Dictionary<string, Reading> Samples = new Dictionary<string, Reading>();

        public struct Reading
        {
            public long Count;
            public double Total;
            public float Min;
            public float Max;

            public double Average => Count > 0 ? Total / Count : 0d;
        }

        /// <summary>Raw counter values, for the test harness to assert against.</summary>
        public static Dictionary<string, long> SnapshotCounters() =>
            new Dictionary<string, long>(Counters);

        /// <summary>Raw sampled readings, for the test harness to assert against.</summary>
        public static Dictionary<string, Reading> SnapshotReadings() =>
            new Dictionary<string, Reading>(Samples);

        /// <summary>Cheap gate so instrumentation costs nothing when switched off.</summary>
        public static bool Enabled => VCOMod.Settings?.verboseLogging ?? false;

        /// <summary>Limits pawn-attributed events to the pawns under test; null records everything.</summary>
        public static System.Func<Pawn, bool> SubjectFilter;

        /// <summary>Test hook called with the parry formula's inputs at each roll; null in normal play.</summary>
        public static System.Action<Pawn, Pawn, float> ParryChanceProbe;

        public static void ProbeParryChance(Pawn defender, Pawn attacker, float directionFactor)
        {
            ParryChanceProbe?.Invoke(defender, attacker, directionFactor);
        }


        private static bool Accepts(Pawn subject) =>
            SubjectFilter == null || subject == null || SubjectFilter(subject);

        /// <summary>Counts an event attributed to a specific pawn, honouring the subject filter.</summary>
        public static void CountFor(Pawn subject, string key, long amount = 1)
        {
            if (!Enabled || !Accepts(subject))
            {
                return;
            }
            Count(key, amount);
        }

        /// <summary>Samples a value attributed to a specific pawn, honouring the subject filter.</summary>
        public static void SampleFor(Pawn subject, string key, float value)
        {
            if (!Enabled || !Accepts(subject))
            {
                return;
            }
            Sample(key, value);
        }

        /// <summary>Counter keys for each enum value (e.g. "parry.facing.Front"), indexed by the value, so hot paths avoid string building.</summary>
        public static string[] KeyTable<TEnum>(string prefix) where TEnum : struct
        {
            var values = (TEnum[])System.Enum.GetValues(typeof(TEnum));
            var max = 0;
            foreach (var value in values)
            {
                max = System.Math.Max(max, System.Convert.ToInt32(value));
            }

            var keys = new string[max + 1];
            foreach (var value in values)
            {
                keys[System.Convert.ToInt32(value)] = prefix + value;
            }
            return keys;
        }

        public static void Count(string key, long amount = 1)
        {
            if (!Enabled)
            {
                return;
            }
            Counters.TryGetValue(key, out var current);
            Counters[key] = current + amount;
        }

        /// <summary>Records a value so we can report its average and range, e.g. rolled parry chance.</summary>
        public static void Sample(string key, float value)
        {
            if (!Enabled)
            {
                return;
            }
            if (Samples.TryGetValue(key, out var s))
            {
                s.Count++;
                s.Total += value;
                if (value < s.Min) { s.Min = value; }
                if (value > s.Max) { s.Max = value; }
                Samples[key] = s;
            }
            else
            {
                Samples[key] = new Reading { Count = 1, Total = value, Min = value, Max = value };
            }
        }

        public static long Get(string key)
        {
            Counters.TryGetValue(key, out var v);
            return v;
        }

        public static bool HasData => Counters.Count > 0 || Samples.Count > 0;

        public static void Reset()
        {
            Counters.Clear();
            Samples.Clear();
        }

        /// <summary>Ordered key/value pairs for display in the settings window.</summary>
        public static IEnumerable<KeyValuePair<string, string>> Lines()
        {
            foreach (var kv in Counters.OrderBy(k => k.Key))
            {
                yield return new KeyValuePair<string, string>(kv.Key, kv.Value.ToString("N0"));
            }
            foreach (var kv in Samples.OrderBy(k => k.Key))
            {
                var s = kv.Value;
                var avg = s.Count > 0 ? s.Total / s.Count : 0d;
                yield return new KeyValuePair<string, string>(
                    kv.Key,
                    $"avg {avg:P1}   min {s.Min:P1}   max {s.Max:P1}   n={s.Count:N0}");
            }
        }

        public static string BuildReport()
        {
            if (!HasData)
            {
                return "[VCO] Diagnostics: nothing recorded yet.";
            }

            var sb = new StringBuilder();
            sb.AppendLine("[VCO] Diagnostics summary");

            var width = Counters.Keys.Concat(Samples.Keys).Max(k => k.Length);
            foreach (var line in Lines())
            {
                sb.AppendLine("    " + line.Key.PadRight(width) + "  " + line.Value);
            }

            // Derived rates.
            var attempts = Get("parry.attempt");
            if (attempts > 0)
            {
                sb.AppendLine($"    -> parry success rate: {(double)Get("parry.success") / attempts:P1} " +
                              $"of {attempts:N0} eligible melee attacks");
            }
            var dirCalls = Get("directional.considered");
            if (dirCalls > 0)
            {
                sb.AppendLine($"    -> directional replaced hit part: {(double)Get("directional.replaced") / dirCalls:P1} " +
                              $"of {dirCalls:N0} damage instances");
            }
            var mitigations = Get("accuracy.mitigation.applied");
            if (mitigations > 0)
            {
                sb.AppendLine($"    -> accuracy mitigation applied: {mitigations:N0} shot reports");
            }
            var evasionApplied = Get("evasion.applied");
            if (evasionApplied > 0)
            {
                sb.AppendLine($"    -> evasion applied: {evasionApplied:N0} aim checks");
            }
            var armorApplied = Get("armor.applied");
            if (armorApplied > 0)
            {
                sb.AppendLine($"    -> armor leftover stretch: {armorApplied:N0} layers, " +
                              $"always-block {Get("armor.alwaysBlock"):N0}");
            }
            return sb.ToString().TrimEnd();
        }

        public static void WriteReport()
        {
            Log.Message(BuildReport());
        }
    }
}
