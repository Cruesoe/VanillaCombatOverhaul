using System.Collections.Generic;
using System.Linq;
using System.Text;
using Verse;

namespace VanillaCombatOverhaul
{
    /// <summary>
    /// Counters for how often each part of the mod actually fires.
    ///
    /// A clean startup log proves nothing crashed; it says nothing about whether pawns are
    /// parrying, how often a rejection reason bites, or whether directional damage is
    /// replacing hit parts at all. These counters answer that.
    ///
    /// Deliberately a summary rather than a line per event: a single fight produces hundreds
    /// of damage instances, and per-event logging would bury anything useful. Counters
    /// accumulate and are dumped on an interval or on demand through developer debug actions.
    ///
    /// This is scaffolding for tuning, not a shipping feature. Remove it, along with the
    /// Count/Sample calls it is paired with, once the numbers stop being interesting.
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

        /// <summary>
        /// Optional restriction on whose events are recorded. Null in normal play, so
        /// everything counts.
        ///
        /// The test arena sets this to the pawns under test, because a parry produces a
        /// counter-attack, and that counter is itself a melee attack the original attacker can
        /// parry. Without a filter an asymmetric matchup records both directions and reports
        /// their mean, which is exactly what made two scenarios look broken when they were not.
        /// </summary>
        public static System.Func<Pawn, bool> SubjectFilter;

        /// <summary>
        /// Test-only hook, invoked when a parry chance is computed. Null in normal play.
        ///
        /// The arena needs the formula's inputs as they were at the moment of the attempt.
        /// Sampling them on the refresh tick instead measures freshly healed pawns, and under
        /// six attackers a defender is rarely healthy when the roll actually happens -- worth
        /// 4.4 points of apparent error that belonged to the harness, not the mod.
        /// </summary>
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

        /// <summary>
        /// Pre-built counter keys for an enum-valued suffix, e.g. "parry.facing.Front".
        ///
        /// C# evaluates arguments before the call, so <c>Count(prefix + facing)</c> pays a
        /// string concat and a reflective Enum.ToString on every hit -- even with counting
        /// switched off, which defeats the gate below. Measured at ~180ns against ~0.2ns for
        /// an array index, on a path that runs once per damage instance.
        ///
        /// Built once at startup from the enum itself and indexed by the underlying value, so
        /// adding or renumbering a case cannot desynchronise the table.
        /// </summary>
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

            // Derived rates, which are the numbers actually worth reading.
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
