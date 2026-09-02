using System;
using System.Collections.Generic;
using RimWorld;
using UnityEngine;
using Verse;

namespace VanillaCombatOverhaul
{
    /// <summary>
    /// Turns arena counters into pass/fail checks.
    ///
    /// A harness that only prints numbers cannot regress; something has to be able to fail.
    /// The expected parry chance is derived from the vanilla MeleeHitChance curve read out of
    /// the def at runtime, not from a constant copied into this file, so the prediction stays
    /// honest if Ludeon retunes the curve.
    /// </summary>
    public static class ArenaAssertions
    {
        /// <summary>Below this many attempts, rates are noise and the run proves nothing.</summary>
        private const long MinimumAttempts = 200;

        private const double ChanceTolerance = 0.03;
        private const double RateTolerance = 0.06;

        /// <summary>Share of attacks a duel may lose to the parry budget before it counts as throttled.</summary>
        private const double DuelBudgetShare = 0.01;

        public static void Evaluate(ArenaResult r)
        {
            var attempts = r.Counter("parry.attempt");

            r.Assertions.Add(Check(
                "sample size",
                attempts >= MinimumAttempts,
                $"{attempts:N0} eligible attacks (need >= {MinimumAttempts})"));

            if (attempts < MinimumAttempts)
            {
                // Everything below divides by these numbers; stop rather than report noise.
                return;
            }

            AssertCombatantsMatchSpec(r);
            AssertRearNeverParries(r);
            AssertSeederCoverage(r);
            AssertBudgetRespected(r);
            AssertBudgetScopedToBeingOutnumbered(r);

            if (r.Spec.defenderUnarmed)
            {
                // An unarmed defender is gated out before any roll is taken, so there is no
                // chance to compare against. Zero rolls is the pass condition here, not a gap.
                AssertUnarmedCannotParry(r);
                return;
            }

            AssertChanceMatchesCurve(r);
            AssertRollHonoursChance(r);
        }

        /// <summary>
        /// The combatants must be the ones the scenario named.
        ///
        /// Added after a run fielded attackers with no skills tracker at all, so pinning melee
        /// skill did nothing and a skill-20 scenario measured roughly skill 7. Every other
        /// assertion passed, because they check the formula against the inputs it was fed
        /// rather than against the inputs the spec asked for. Nothing downstream can catch a
        /// wrong matchup; only this can.
        /// </summary>
        private static void AssertCombatantsMatchSpec(ArenaResult r)
        {
            CheckSkillPinned(r, "attacker", "measured.attackerSkill", r.Spec.attackerMeleeSkill);
            CheckSkillPinned(r, "defender", "measured.defenderSkill", r.Spec.defenderMeleeSkill);
        }

        private static void CheckSkillPinned(ArenaResult r, string who, string key, int expected)
        {
            if (!r.Readings.TryGetValue(key, out var reading) || reading.Count == 0)
            {
                r.Assertions.Add(Check($"{who} skill pinned to spec", false, "no skill samples captured"));
                return;
            }

            // Min and max, not the mean: an average can sit on target while individuals drift
            // either side of it, and -1 marks a combatant with no skills tracker.
            //
            // One level of slack, because combat grants melee XP continuously while the
            // re-pin only runs on the refresh tick, so a sample can legitimately catch a pawn
            // between drifting and being corrected. The arena.skillRepinned counter shows how
            // often that happens. A wrong matchup is off by far more than one level -- the
            // failure this exists to catch read 0 against an expected 20.
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
        private static void AssertRearNeverParries(ArenaResult r)
        {
            var rear = r.Counter("parry.facing.Rear");
            var rejected = r.Counter("parry.reject.fromBehind");
            r.Assertions.Add(Check(
                "rear attacks never parried",
                rear == rejected,
                $"{rejected:N0} rejected of {rear:N0} rear-facing attacks"));
        }

        /// <summary>
        /// Non-zero means the body-part group seeder left some body shape without parts on a
        /// side, which is the failure mode the XPath fallbacks exist to prevent.
        /// </summary>
        private static void AssertSeederCoverage(ArenaResult r)
        {
            var gaps = r.Counter("directional.keep.noPartsOnSide");
            r.Assertions.Add(Check(
                "directional seeder covers every body",
                gaps == 0,
                gaps == 0 ? "no gaps" : $"{gaps:N0} damage instances found no part on the exposed side"));
        }

        /// <summary>
        /// The cap must actually hold. A pawn is never allowed to bank more parries in one
        /// window than the budget permits, however many attackers are on it.
        /// </summary>
        private static void AssertBudgetRespected(ArenaResult r)
        {
            var overruns = r.Counter("parry.budget.overrun");
            r.Assertions.Add(Check(
                "parry budget cap holds",
                overruns == 0,
                overruns == 0
                    ? $"no defender exceeded {VCOMod.Settings?.parryBudgetPerWindow} per window"
                    : $"{overruns:N0} parries were banked past the cap"));
        }

        /// <summary>
        /// The budget is meant to punish being surrounded, not to throttle an ordinary duel.
        /// A lone attacker cannot swing often enough to spend a budget of two per window, so
        /// any rejection in a one-on-one fight means the window or the cap is mistuned.
        /// </summary>
        private static void AssertBudgetScopedToBeingOutnumbered(ArenaResult r)
        {
            var spent = r.Counter("parry.reject.budgetSpent");
            var attempts = r.Counter("parry.attempt");

            if (r.Spec.attackersPerDefender <= 1)
            {
                // Not exactly zero. A lone attacker occasionally lands two swings inside one
                // 60-tick window, so the cap can bite about once in several hundred attacks.
                // The contract is that it stays negligible in a duel, not that it never fires
                // -- measured at 10% of attacks against six attackers, so a duel sitting under
                // 1% is a wide separation.
                var share = (double)spent / attempts;
                r.Assertions.Add(Check(
                    "budget negligible in a duel",
                    share <= DuelBudgetShare,
                    $"{spent:N0} of {attempts:N0} attacks denied for budget ({share:P2}, " +
                    $"limit {DuelBudgetShare:P0})"));
                return;
            }

            // Nothing is asserted for an outnumbered defender. Whether three attackers swing
            // often enough to spend the budget is a balance question this run is measuring,
            // not a contract it may assume -- an assertion that cannot fail is not a test. The
            // count reaches the report through the counter dump.
        }

        private static void AssertUnarmedCannotParry(ArenaResult r)
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

        /// <summary>
        /// The chance actually rolled should match the documented formula for these two skill
        /// levels. Front and side factors are equal by default, so every sampled roll shares
        /// one expected value.
        /// </summary>
        private static void AssertChanceMatchesCurve(ArenaResult r)
        {
            // Built from the stat values measured during the run rather than from the skill
            // curve alone. Light level and injuries both move MeleeHitChance, so a curve-only
            // prediction sits a few points above the live one and the assertion would be
            // testing the environment instead of the formula.
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

        /// <summary>
        /// The parry formula, reimplemented here rather than called from ParryUtility. Calling
        /// the production method would make the assertion compare a value to itself; writing it
        /// out separately means a wrong exponent, direction factor or clamp in the
        /// implementation shows up as a mismatch.
        /// </summary>
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

        /// <summary>
        /// Average of the formula evaluated per pair during the run.
        ///
        /// Evaluated per pair and then averaged, never the other way round. The formula is
        /// strongly non-linear in the attacker's ability, so averaging the inputs first and
        /// evaluating once gives a materially different answer whenever the stats vary --
        /// which is what left this assertion four points out while everything else matched.
        /// </summary>
        public static double ExpectedFromMeasuredInputs(ArenaResult r) =>
            r.Readings.ContainsKey("measured.expectedChance")
                ? r.ReadingAverage("measured.expectedChance")
                : -1;

        /// <summary>
        /// Verifies the RNG actually honours the chance: of the rolls taken, the proportion
        /// that succeeded should track the average chance offered.
        /// </summary>
        private static void AssertRollHonoursChance(ArenaResult r)
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
            r.Assertions.Add(Check(
                "roll honours chance",
                delta <= RateTolerance,
                $"{success:N0}/{rolls:N0} = {observedRate:P1} against an offered {offered:P1} (delta {delta:P1})"));
        }

        /// <summary>
        /// Independent prediction of parry chance, reading the vanilla curve from the def
        /// rather than repeating numbers from the README.
        /// </summary>
        public static double PredictParryChance(ArenaSpec spec)
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

        /// <summary>
        /// Direct check of the facing maths, needing no combat at all. Fast, deterministic,
        /// and it isolates a bug in FacingUtility from a bug in how combat reaches it.
        /// </summary>
        public static List<AssertionResult> FacingSelfTest()
        {
            var results = new List<AssertionResult>();

            // A target facing north is looking north, so an attacker standing to its north is
            // in front of it and one to the south is behind it. The first version of this
            // table had that the wrong way round, which is exactly the sort of thing a unit
            // check is for.
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
