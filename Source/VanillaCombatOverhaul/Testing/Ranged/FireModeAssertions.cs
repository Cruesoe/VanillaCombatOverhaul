using System;
using System.Collections.Generic;
using System.Linq;
using RimWorld;
using UnityEngine;
using Verse;

namespace VanillaCombatOverhaul
{
    /// <summary>Fire mode formula checks, then a drafted shooter run through every mode against a real target.</summary>
    public static class FireModeAssertions
    {
        private const double Tolerance = 0.001;
        private const string BurstWeapon = "Gun_AssaultRifle";
        private const string SingleShotWeapon = "Gun_BoltActionRifle";
        private const int ShooterSkill = 10;
        // The arena puts the shooter two cells west of centre, so this is 20 cells apart.
        private const int TargetOffset = 18;

        public static List<AssertionResult> SelfTests()
        {
            var results = new List<AssertionResult>();
            results.AddRange(HitFormulaTests());
            results.AddRange(BurstFormulaTests());
            results.AddRange(PatchTests());

            var map = Find.CurrentMap;
            if (map == null)
            {
                results.Add(Check("fire mode arena", false, "no map loaded"));
                return results;
            }

            var settings = VCOMod.Settings;
            var wasEnabled = settings.enableFireModes;
            settings.enableFireModes = true;
            try
            {
                results.AddRange(ArenaTests(map));
            }
            finally
            {
                settings.enableFireModes = wasEnabled;
            }
            return results;
        }

        private static IEnumerable<AssertionResult> HitFormulaTests()
        {
            var precision = FireModeUtility.AdjustHitFactor(0.6f, 1.5f);
            yield return Check("precision raises a 60% shooter factor",
                               precision > 0.6f && Math.Abs(precision - Math.Pow(0.6, 1 / 1.5)) <= Tolerance,
                               precision.ToString("P2"));

            var suppression = FireModeUtility.AdjustHitFactor(0.9f, 0.5f);
            yield return Check("suppression lowers a 90% shooter factor to 81%",
                               Math.Abs(suppression - 0.81f) <= Tolerance, suppression.ToString("P2"));

            yield return Check("a certain shot stays certain",
                               Mathf.Approximately(FireModeUtility.AdjustHitFactor(1f, 1.5f), 1f)
                               && Mathf.Approximately(FireModeUtility.AdjustHitFactor(1f, 0.5f), 1f),
                               "1.0 in, 1.0 out");

            var floor = FireModeUtility.AdjustHitFactor(FireModeUtility.MinShooterFactor, 0.25f);
            yield return Check("vanilla's shooter floor holds",
                               floor >= FireModeUtility.MinShooterFactor - 0.0001f, floor.ToString("P2"));

            var neverAbove = true;
            for (var f = 0.05f; f < 1f; f += 0.05f)
            {
                var v = FireModeUtility.AdjustHitFactor(f, 3f);
                neverAbove &= v <= 1f && v >= f;
            }
            yield return Check("better accuracy never exceeds certainty or lowers a shot", neverAbove, "0.05..0.95 at x3");
        }

        private static IEnumerable<AssertionResult> BurstFormulaTests()
        {
            var p = FireModeTuning.PrecisionDefaults();
            var b = FireModeTuning.ShortBurstDefaults();
            var s = FireModeTuning.SuppressionDefaults();
            var cases = new (string name, int baseBurst, FireModeTuning t, int expected)[]
            {
                ("precision, assault rifle", 3, p, 2),
                ("short burst, assault rifle", 3, b, 5),
                ("suppression, assault rifle", 3, s, 6),
                ("suppression, LMG", 6, s, 12),
                ("precision, minigun", 25, p, 17),
                ("short burst, minigun (capped)", 25, b, 28),
                ("suppression, minigun (capped)", 25, s, 35),
                ("suppression, single shot", 1, s, 1),
                ("precision, two-round burst", 2, p, 1),
            };
            foreach (var c in cases)
            {
                var actual = FireModeUtility.AdjustBurst(c.baseBurst, c.t.burstFactor, c.t.burstMaxChange);
                yield return Check($"burst {c.name}: {c.baseBurst} -> {c.expected}", actual == c.expected,
                                   "got " + actual);
            }
        }

        private static IEnumerable<AssertionResult> PatchTests()
        {
            yield return Check("aim time stat part installed",
                               StatDefOf.AimingDelayFactor.parts?.Any(x => x is StatPart_FireModeAimTime) ?? false,
                               "AimingDelayFactor.parts");
            yield return Check("cooldown stat part installed",
                               StatDefOf.RangedCooldownFactor.parts?.Any(x => x is StatPart_FireModeCooldown) ?? false,
                               "RangedCooldownFactor.parts");
            yield return Check("pawns carry the fire mode comp",
                               ThingDefOf.Human.HasComp(typeof(CompFireMode)), "Human");
        }

        private static IEnumerable<AssertionResult> ArenaTests(Map map)
        {
            var burstDef = DefDatabase<ThingDef>.GetNamedSilentFail(BurstWeapon);
            var singleDef = DefDatabase<ThingDef>.GetNamedSilentFail(SingleShotWeapon);
            if (burstDef == null || singleDef == null)
            {
                yield return Check("fire mode arena weapons", false, "missing " + BurstWeapon + " or " + SingleShotWeapon);
                yield break;
            }

            var results = new List<AssertionResult>();
            Pawn shooter = null;
            Pawn rifleman = null;
            Pawn target = null;
            try
            {
                var origin = map.Center;
                shooter = RangedCombatArena.SpawnShooter(map, origin + IntVec3.West * 2, ShooterSkill, burstDef);
                rifleman = RangedCombatArena.SpawnShooter(map, origin + IntVec3.West * 2 + IntVec3.North * 2,
                                                          ShooterSkill, singleDef);
                target = RangedCombatArena.SpawnTarget(map, origin + IntVec3.East * TargetOffset, false);
                if (shooter == null || rifleman == null || target == null)
                {
                    results.Add(Check("fire mode arena pawns spawned", false, "spawn failed"));
                }
                else
                {
                    RunArena(shooter, rifleman, target, results);
                }
            }
            finally
            {
                shooter?.Destroy(DestroyMode.Vanish);
                rifleman?.Destroy(DestroyMode.Vanish);
                target?.Destroy(DestroyMode.Vanish);
            }

            foreach (var r in results)
            {
                yield return r;
            }
        }

        private static void RunArena(Pawn shooter, Pawn rifleman, Pawn target, List<AssertionResult> results)
        {
            var settings = VCOMod.Settings;
            var verb = FireModeUtility.PrimaryVerb(shooter);
            var comp = shooter.TryGetComp<CompFireMode>();
            if (verb == null || comp == null || shooter.drafter == null)
            {
                results.Add(Check("fire mode arena shooter ready", false,
                                  $"verb {verb != null}, comp {comp != null}, drafter {shooter.drafter != null}"));
                return;
            }

            comp.SetMode(FireMode.Precision);
            shooter.drafter.Drafted = false;
            results.Add(Check("undrafted colonist fires as Default",
                              FireModeUtility.ActiveMode(shooter, verb) == FireMode.Default, "Precision chosen, undrafted"));

            shooter.drafter.Drafted = true;
            var baseBurst = verb.BurstShotCount;
            comp.SetMode(FireMode.Default);
            var baseline = Measure(shooter, verb, target);
            results.Add(Check("Default leaves the burst alone",
                              baseline.burst == baseBurst, $"{baseline.burst} vs {baseBurst}"));

            var hits = new Dictionary<FireMode, float>();
            foreach (var mode in new[] { FireMode.Precision, FireMode.ShortBurst, FireMode.Suppression })
            {
                comp.SetMode(mode);
                var t = settings.TuningFor(mode);
                var m = Measure(shooter, verb, target);
                hits[mode] = m.hit;
                var label = FireModeUtility.LabelFor(mode);

                var expectedHit = FireModeUtility.AdjustHitFactor(baseline.hit, t.accuracy);
                results.Add(Check($"{label}: shooter factor follows the formula",
                                  Math.Abs(m.hit - expectedHit) <= Tolerance,
                                  $"base {baseline.hit:P2}, expected {expectedHit:P2}, got {m.hit:P2}"));
                results.Add(Check($"{label}: aim time scaled",
                                  Math.Abs(m.aim - baseline.aim * t.aimTime) <= Tolerance,
                                  $"base {baseline.aim:F3}, got {m.aim:F3}, x{t.aimTime}"));
                results.Add(Check($"{label}: cooldown scaled",
                                  Math.Abs(m.cooldown - baseline.cooldown * t.cooldown) <= Tolerance,
                                  $"base {baseline.cooldown:F3}, got {m.cooldown:F3}, x{t.cooldown}"));
                var expectedBurst = FireModeUtility.AdjustBurst(baseBurst, t.burstFactor, t.burstMaxChange);
                results.Add(Check($"{label}: warmup sizes the burst",
                                  m.burst == expectedBurst, $"{baseBurst} -> expected {expectedBurst}, got {m.burst}"));
            }
            results.Add(Check("hit order is Precision > Default > Suppression",
                              hits[FireMode.Precision] > baseline.hit && baseline.hit > hits[FireMode.Suppression],
                              $"{hits[FireMode.Precision]:P2} / {baseline.hit:P2} / {hits[FireMode.Suppression]:P2}"));
            results.Add(Check("BurstShotCount is untouched outside warmup",
                              verb.BurstShotCount == baseBurst, verb.BurstShotCount.ToString()));

            // An ability or fist is judged by its own verb, not the gun's mode.
            comp.SetMode(FireMode.Precision);
            var otherVerb = shooter.verbTracker?.AllVerbs?.FirstOrDefault(v => v != null && v != verb);
            if (otherVerb != null)
            {
                float aimWithOther;
                using (CombatContext.PushShot(shooter, otherVerb))
                {
                    aimWithOther = StatDefOf.AimingDelayFactor.Worker.GetValue(StatRequest.For(shooter));
                }
                results.Add(Check("a non-weapon cast ignores the mode",
                                  Math.Abs(aimWithOther - baseline.aim) <= Tolerance,
                                  $"base {baseline.aim:F3}, got {aimWithOther:F3}"));
            }

            comp.SetAuto();
            var near = settings.fireModeShortBurstRange - 1f;
            var mid = (settings.fireModeShortBurstRange + settings.fireModePrecisionRange) / 2f;
            var far = settings.fireModePrecisionRange + 1f;
            results.Add(Check("auto picks by distance",
                              FireModeUtility.ActiveMode(shooter, verb, near) == FireMode.Suppression
                              && FireModeUtility.ActiveMode(shooter, verb, mid) == FireMode.ShortBurst
                              && FireModeUtility.ActiveMode(shooter, verb, far) == FireMode.Precision,
                              $"{near} / {mid} / {far} cells"));

            settings.enableFireModes = false;
            var off = Measure(shooter, verb, target);
            settings.enableFireModes = true;
            results.Add(Check("disabled fire modes change nothing",
                              Math.Abs(off.hit - baseline.hit) <= Tolerance && off.burst == baseBurst,
                              $"hit {off.hit:P2}, burst {off.burst}"));
            shooter.drafter.Drafted = false;

            var rifleVerb = FireModeUtility.PrimaryVerb(rifleman);
            var rifleComp = rifleman.TryGetComp<CompFireMode>();
            if (rifleVerb == null || rifleComp == null || rifleman.drafter == null)
            {
                results.Add(Check("single-shot rifleman ready", false, "missing verb, comp or drafter"));
                return;
            }
            rifleman.drafter.Drafted = true;
            rifleComp.SetMode(FireMode.Suppression);
            results.Add(Check("single-shot weapon falls back from Suppression",
                              FireModeUtility.ActiveMode(rifleman, rifleVerb) == FireMode.Default,
                              FireModeUtility.ActiveMode(rifleman, rifleVerb).ToString()));
            results.Add(Check("single-shot weapon offers Precision only",
                              FireModeUtility.Offers(rifleman, FireMode.Precision)
                              && !FireModeUtility.Offers(rifleman, FireMode.ShortBurst)
                              && !FireModeUtility.Offers(rifleman, FireMode.Suppression),
                              "menu options"));
            rifleComp.SetMode(FireMode.Precision);
            results.Add(Check("single-shot weapon still uses Precision",
                              FireModeUtility.ActiveMode(rifleman, rifleVerb) == FireMode.Precision,
                              FireModeUtility.ActiveMode(rifleman, rifleVerb).ToString()));
            rifleman.drafter.Drafted = false;
        }

        private static (float hit, float aim, float cooldown, int burst) Measure(Pawn shooter, Verb verb, Pawn target)
        {
            var report = ShotReport.HitReportFor(shooter, verb, target);
            var hit = ShotReportAccess.GetShooterFactor(ref report);
            var aim = StatDefOf.AimingDelayFactor.Worker.GetValue(StatRequest.For(shooter));
            var cooldown = StatDefOf.RangedCooldownFactor.Worker.GetValue(StatRequest.For(shooter));
            var burst = Patch_Verb_WarmupComplete.ShotsPerBurstFor(verb);
            return (hit, aim, cooldown, burst);
        }

        private static AssertionResult Check(string name, bool passed, string detail) =>
            new AssertionResult { Name = name, Passed = passed, Detail = detail };
    }
}
