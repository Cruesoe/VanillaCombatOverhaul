using System;
using System.Collections.Generic;
using RimWorld;
using UnityEngine;
using Verse;

namespace VanillaCombatOverhaul
{
    /// <summary>Pass/fail checks from the melee arena's counters; expected parry chances use the live MeleeHitChance curve.</summary>
    public static class MeleeAssertions
    {
        /// <summary>Below this many attempts, rates are noise and the run proves nothing.</summary>
        private const long MinimumAttempts = 200;

        private const double ChanceTolerance = 0.03;
        private const double RateTolerance = 0.06;

        // Four standard errors: a false failure about once in 15,000 checks.
        private const double SigmaTolerance = 4d;

        /// <summary>Share of attacks a duel may lose to the parry budget before it counts as throttled.</summary>
        private const double DuelBudgetShare = 0.01;

        public static void Evaluate(MeleeArenaResult r)
        {
            if (r.Spec.IsPointBlank)
            {
                // Point-blank scenarios are checked by PointBlankAssertions only.
                PointBlankAssertions.Evaluate(r);
                return;
            }

            var attempts = r.Counter("parry.attempt");

            r.Assertions.Add(Check(
                "sample size",
                attempts >= MinimumAttempts,
                $"{attempts:N0} eligible attacks (need >= {MinimumAttempts})"));

            if (attempts < MinimumAttempts)
            {
                // Too few samples for the rate checks below.
                return;
            }

            AssertCombatantsMatchSpec(r);
            AssertRearNeverParries(r);
            AssertSeederCoverage(r);
            AssertBudgetRespected(r);
            AssertBudgetScopedToBeingOutnumbered(r);

            if (r.Spec.defenderUnarmed)
            {
                // An unarmed defender must never roll.
                AssertUnarmedCannotParry(r);
                return;
            }

            AssertChanceMatchesCurve(r);
            AssertRollHonoursChance(r);
        }

        /// <summary>The combatants' melee skills must match the scenario.</summary>
        private static void AssertCombatantsMatchSpec(MeleeArenaResult r)
        {
            CheckSkillPinned(r, "attacker", "measured.attackerSkill", r.Spec.attackerMeleeSkill);
            CheckSkillPinned(r, "defender", "measured.defenderSkill", r.Spec.defenderMeleeSkill);
        }

        private static void CheckSkillPinned(MeleeArenaResult r, string who, string key, int expected)
        {
            if (!r.Readings.TryGetValue(key, out var reading) || reading.Count == 0)
            {
                r.Assertions.Add(Check($"{who} skill pinned to spec", false, "no skill samples captured"));
                return;
            }

            // Min and max must sit within one level (XP gained between re-pins); -1 means no skills tracker.
            const float Slack = 1.01f;
            var pinned = reading.Min >= expected - Slack && reading.Max <= expected + Slack;
            r.Assertions.Add(Check(
                $"{who} skill pinned to spec",
                pinned,
                pinned
                    ? $"samples within one level of melee {expected}"
                    : $"expected melee {expected}, saw {reading.Min:0.#} to {reading.Max:0.#}" +
                      (reading.Min < 0 ? " (-1 means the pawn has no skills tracker)" : "")));
        }

        /// <summary>Every attack from behind must be rejected by the facing gate, with none slipping past.</summary>
        private static void AssertRearNeverParries(MeleeArenaResult r)
        {
            var rear = r.Counter("parry.facing.Rear");
            var rejected = r.Counter("parry.reject.fromBehind");
            r.Assertions.Add(Check(
                "rear attacks never parried",
                rear == rejected,
                $"{rejected:N0} rejected of {rear:N0} rear-facing attacks"));
        }

        /// <summary>Every body must have parts on each side after the group seeder runs.</summary>
        private static void AssertSeederCoverage(MeleeArenaResult r)
        {
            var gaps = r.Counter("directional.keep.noPartsOnSide");
            r.Assertions.Add(Check(
                "directional seeder covers every body",
                gaps == 0,
                gaps == 0 ? "no gaps" : $"{gaps:N0} damage instances found no part on the exposed side"));
        }

        /// <summary>No defender may parry more than the budget in one window.</summary>
        private static void AssertBudgetRespected(MeleeArenaResult r)
        {
            var overruns = r.Counter("parry.budget.overrun");
            r.Assertions.Add(Check(
                "parry budget cap holds",
                overruns == 0,
                overruns == 0
                    ? $"no defender exceeded {VCOMod.Settings?.parryBudgetPerWindow} per window"
                    : $"{overruns:N0} parries were banked past the cap"));
        }

        /// <summary>The budget must be negligible in a duel.</summary>
        private static void AssertBudgetScopedToBeingOutnumbered(MeleeArenaResult r)
        {
            var spent = r.Counter("parry.reject.budgetSpent");
            var attempts = r.Counter("parry.attempt");

            if (r.Spec.attackersPerDefender <= 1)
            {
                // A lone attacker occasionally lands two swings in one window, so a small share is allowed.
                var share = (double)spent / attempts;
                r.Assertions.Add(Check(
                    "budget negligible in a duel",
                    share <= DuelBudgetShare,
                    $"{spent:N0} of {attempts:N0} attacks denied for budget ({share:P2}, " +
                    $"limit {DuelBudgetShare:P0})"));
                return;
            }

            // Outnumbered defenders are reported through the counters, not asserted.
        }

        private static void AssertUnarmedCannotParry(MeleeArenaResult r)
        {
            if (!r.Spec.defenderUnarmed)
            {
                return;
            }
            var successes = r.Counter("parry.success");
            r.Assertions.Add(Check(
                "unarmed defender never parries",
                successes == 0,
                $"{successes:N0} parries by an unarmed defender"));
        }

        /// <summary>The chance rolled must match the formula for the measured inputs.</summary>
        private static void AssertChanceMatchesCurve(MeleeArenaResult r)
        {
            // From stat values measured during the run, since light and injuries move MeleeHitChance.
            var expected = ExpectedFromMeasuredInputs(r);
            if (expected < 0)
            {
                r.Assertions.Add(Check("parry chance matches formula", false,
                    "no measured stat inputs were captured"));
                return;
            }

            var observed = r.ReadingAverage("parry.chanceRolled");
            var delta = Math.Abs(observed - expected);
            r.Assertions.Add(Check(
                "parry chance matches formula",
                delta <= ChanceTolerance,
                $"expected {expected:P1} from measured inputs, rolled {observed:P1} (delta {delta:P1})"));
        }

        /// <summary>The parry formula, written out independently of ParryUtility.</summary>
        public static double FormulaFor(double aptitude, double attackerMelee, double directionFactor)
        {
            if (directionFactor <= 0d)
            {
                return 0d;
            }
            aptitude = Math.Min(aptitude, 0.999);
            if (aptitude <= 0d)
            {
                return 0d;
            }
            attackerMelee = Math.Min(Math.Max(attackerMelee, 0d), 0.999);
            return Math.Pow(aptitude, (1d / directionFactor) / (1d - attackerMelee));
        }

        /// <summary>Average of the formula evaluated per pair (the formula is non-linear, so inputs are not averaged first).</summary>
        public static double ExpectedFromMeasuredInputs(MeleeArenaResult r) =>
            r.Readings.ContainsKey("measured.expectedChance")
                ? r.ReadingAverage("measured.expectedChance")
                : -1;

        /// <summary>The success rate of the rolls must track the average chance offered.</summary>
        private static void AssertRollHonoursChance(MeleeArenaResult r)
        {
            var success = r.Counter("parry.success");
            var failed = r.Counter("parry.reject.rollFailed");
            var rolls = success + failed;
            if (rolls == 0)
            {
                r.Assertions.Add(Check("roll honours chance", false, "no parry rolls were taken"));
                return;
            }

            var observedRate = (double)success / rolls;
            var offered = r.ReadingAverage("parry.chanceRolled");
            var delta = Math.Abs(observedRate - offered);

            // Tolerance is SigmaTolerance binomial standard errors, with a flat floor for large samples.
            var sigma = Math.Sqrt(Math.Max(offered * (1d - offered), 1e-9) / rolls);
            var tolerance = Math.Max(RateTolerance, SigmaTolerance * sigma);

            r.Assertions.Add(Check(
                "roll honours chance",
                delta <= tolerance,
                $"{success:N0}/{rolls:N0} = {observedRate:P1} against an offered {offered:P1} "
                + $"(delta {delta:P1}, allowed {tolerance:P1} = max of {RateTolerance:P1} "
                + $"and {SigmaTolerance:F0}x sigma {sigma:P1})"));
        }

        /// <summary>Parry chance predicted from the vanilla MeleeHitChance curve read from the def.</summary>
        public static double PredictParryChance(MeleeArenaSpec spec)
        {
            var curve = StatDefOf.MeleeHitChance?.postProcessCurve;
            if (curve == null)
            {
                return -1;
            }

            var settings = VCOMod.Settings;
            var d = settings?.parryFrontFactor ?? 1.5f;
            if (d <= 0f)
            {
                return -1;
            }

            var aptitude = Mathf.Min(curve.Evaluate(spec.defenderMeleeSkill), 0.999f);
            var attacker = Mathf.Clamp(curve.Evaluate(spec.attackerMeleeSkill), 0f, 0.999f);

            return Math.Pow(aptitude, (1f / d) / (1f - attacker));
        }

        private static AssertionResult Check(string name, bool passed, string detail) =>
            new AssertionResult { Name = name, Passed = passed, Detail = detail };

        // ------------------------------------------------------------- unit checks

        /// <summary>Direct checks of the facing maths, without combat.</summary>
        public static List<AssertionResult> FacingSelfTest()
        {
            var results = new List<AssertionResult>();

            // A target facing north has an attacker to its north in front and one to its south behind.
            var cases = new (Rot4 targetFacing, IntVec3 attackerOffset, AttackFacing expected)[]
            {
                (Rot4.North, IntVec3.North, AttackFacing.Front),
                (Rot4.North, IntVec3.South, AttackFacing.Rear),
                (Rot4.North, IntVec3.East,  AttackFacing.Right),
                (Rot4.North, IntVec3.West,  AttackFacing.Left),
                (Rot4.East,  IntVec3.East,  AttackFacing.Front),
                (Rot4.East,  IntVec3.West,  AttackFacing.Rear),
                (Rot4.East,  IntVec3.South, AttackFacing.Right),
                (Rot4.East,  IntVec3.North, AttackFacing.Left),
                (Rot4.South, IntVec3.South, AttackFacing.Front),
                (Rot4.South, IntVec3.North, AttackFacing.Rear),
                (Rot4.West,  IntVec3.West,  AttackFacing.Front),
                (Rot4.West,  IntVec3.East,  AttackFacing.Rear)
            };

            foreach (var c in cases)
            {
                // The attack travels from the attacker's cell toward the target at origin.
                var travel = -c.attackerOffset.ToVector3();
                var angle = UnityEngine.Quaternion.LookRotation(travel).eulerAngles.y;
                var actual = FacingUtility.FromTravelAngleFor(angle, c.targetFacing);

                results.Add(new AssertionResult
                {
                    Name = $"facing: target {c.targetFacing.ToStringHuman()}, attacker {c.attackerOffset}",
                    Passed = actual == c.expected,
                    Detail = $"expected {c.expected}, got {actual}"
                });
            }
            return results;
        }
    }
}
