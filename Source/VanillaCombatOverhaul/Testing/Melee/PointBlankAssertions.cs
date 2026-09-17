using System;

namespace VanillaCombatOverhaul
{
    /// <summary>
    /// Pass/fail checks for point-blank scenarios.
    ///
    /// The expected chances are the agreed design table, written out again here rather than
    /// read from PointBlankUtility, so a typo in the mod's table fails instead of being
    /// compared against itself.
    /// </summary>
    public static class PointBlankAssertions
    {
        private static readonly double[] DesignTable =
        {
            0, 0, 0, 0, 0, 0, 0, 0, 0, 0,
            0.05, 0.06, 0.08, 0.10, 0.13, 0.17, 0.22, 0.28, 0.35, 0.43, 0.50
        };

        private const long MinimumRolls = 200;
        private const long MinimumMeleeAttacks = 200;
        private const double SigmaTolerance = 4d;

        /// <summary>
        /// Share of passed rolls allowed to fall back to melee because the shot would not
        /// start. Adjacent targets always have a line of fire, so this should be near zero;
        /// a high value means the start path is refusing shots it should take.
        /// </summary>
        private const double MaxFallbackShare = 0.05;

        public static double ExpectedChance(int shootingSkill) =>
            shootingSkill < 0 ? 0d : DesignTable[Math.Min(shootingSkill, DesignTable.Length - 1)];

        public static void Evaluate(MeleeArenaResult r)
        {
            var spec = r.Spec;

            // Parry attempts are the defenders' count of melee attacks that actually landed a
            // swing, independent of point-blank bookkeeping, so they prove the fight happened.
            var melee = r.Counter("parry.attempt");
            var opportunities = r.Counter("pointblank.opportunity");
            r.Assertions.Add(Check(
                "fight took place",
                melee + opportunities >= MinimumMeleeAttacks,
                $"{melee:N0} melee swings reached defenders, {opportunities:N0} point-blank opportunities"));

            if (spec.pointBlankOverride < 0)
            {
                // Disabled: the feature must not even look at an attack.
                r.Assertions.Add(Check(
                    "disabled feature is inert",
                    opportunities == 0 && r.Counter("pointblank.success") == 0,
                    $"{opportunities:N0} opportunities, {r.Counter("pointblank.success"):N0} shots"));
                return;
            }

            AssertShootingPinned(r);
            AssertOpportunitiesAccounted(r);

            var rolls = r.Counter("pointblank.roll");
            var expected = ExpectedChance(spec.attackerShootingSkill);

            if (spec.pointBlankHonourOrders)
            {
                // Rolls still happen here: once an arena order expires, the pawn's own AI
                // melees under a job the player never gave, and that attack is eligible. The
                // claim is that no roll is ever taken while a player order is running.
                var onOrder = r.Counter("pointblank.roll.onPlayerOrder");
                var refused = r.Counter("pointblank.reject.context.playerOrder");
                r.Assertions.Add(Check(
                    "player melee orders never become shots",
                    onOrder == 0 && refused > 0,
                    $"{onOrder:N0} rolls under a player order, {refused:N0} ordered attacks refused, " +
                    $"{rolls:N0} rolls on the AI's own attacks"));
                return;
            }

            if (expected <= 0d)
            {
                r.Assertions.Add(Check(
                    "below Shooting 10 never shoots",
                    rolls == 0 && r.Counter("pointblank.success") == 0
                               && r.Counter("pointblank.reject.skill") >= MinimumRolls,
                    $"{rolls:N0} rolls, {r.Counter("pointblank.reject.skill"):N0} rejected on skill"));
                return;
            }

            r.Assertions.Add(Check(
                "sample size",
                rolls >= MinimumRolls,
                $"{rolls:N0} rolls (need >= {MinimumRolls})"));
            if (rolls < MinimumRolls)
            {
                return;
            }

            AssertChanceMatchesTable(r, expected);
            AssertRollHonoursChance(r, rolls, expected);
            AssertPassedRollsFire(r, rolls);
        }

        private static void AssertShootingPinned(MeleeArenaResult r)
        {
            if (!r.Readings.TryGetValue("measured.attackerShootingSkill", out var reading) || reading.Count == 0)
            {
                r.Assertions.Add(Check("attacker shooting pinned to spec", false, "no skill samples captured"));
                return;
            }
            // One level of slack for XP gained between refreshes, as with melee.
            var expected = r.Spec.attackerShootingSkill;
            var pinned = reading.Min >= expected - 1.01f && reading.Max <= expected + 1.01f;
            r.Assertions.Add(Check(
                "attacker shooting pinned to spec",
                pinned,
                $"expected shooting {expected}, saw {reading.Min:0.#} to {reading.Max:0.#}"));
        }

        /// <summary>
        /// Every opportunity must end in exactly one recorded outcome. An exit path that
        /// forgets to count would show up here as a gap.
        /// </summary>
        private static void AssertOpportunitiesAccounted(MeleeArenaResult r)
        {
            var opportunities = r.Counter("pointblank.opportunity");
            var outcomes = r.Counter("pointblank.reject.notHumanlike")
                           + r.Counter("pointblank.reject.target")
                           + r.Counter("pointblank.reject.context")
                           + r.Counter("pointblank.reject.skill")
                           + r.Counter("pointblank.reject.weapon")
                           + r.Counter("pointblank.roll");
            var context = r.Counter("pointblank.reject.context");
            var reasons = r.Counter("pointblank.reject.context.counter")
                          + r.Counter("pointblank.reject.context.socialFight")
                          + r.Counter("pointblank.reject.context.duel")
                          + r.Counter("pointblank.reject.context.playerOrder");
            r.Assertions.Add(Check(
                "opportunities accounted for",
                opportunities == outcomes && context == reasons,
                $"{opportunities:N0} opportunities, {outcomes:N0} gate outcomes; " +
                $"{context:N0} context rejections, {reasons:N0} with a reason"));
        }

        /// <summary>The chance offered must be the table value exactly, not approximately.</summary>
        private static void AssertChanceMatchesTable(MeleeArenaResult r, double expected)
        {
            r.Readings.TryGetValue("pointblank.chanceRolled", out var reading);
            var exact = reading.Count > 0
                        && Math.Abs(reading.Min - expected) < 1e-4
                        && Math.Abs(reading.Max - expected) < 1e-4;
            r.Assertions.Add(Check(
                "chance matches design table",
                exact,
                $"expected {expected:P0} at shooting {r.Spec.attackerShootingSkill}, " +
                $"offered {reading.Min:P1} to {reading.Max:P1}"));
        }

        /// <summary>
        /// Passed rolls against the table, within four binomial standard errors. There is no
        /// flat floor as the parry check has: at 5% a floor that size would pass a roll that
        /// never succeeded at all.
        /// </summary>
        private static void AssertRollHonoursChance(MeleeArenaResult r, long rolls, double expected)
        {
            var passed = rolls - r.Counter("pointblank.reject.rollFailed");
            var observed = (double)passed / rolls;
            var sigma = Math.Sqrt(expected * (1d - expected) / rolls);
            var tolerance = SigmaTolerance * sigma;
            var delta = Math.Abs(observed - expected);
            r.Assertions.Add(Check(
                "roll honours chance",
                delta <= tolerance,
                $"{passed:N0}/{rolls:N0} = {observed:P1} against {expected:P1} " +
                $"(delta {delta:P1}, allowed {tolerance:P1})"));
        }

        private static void AssertPassedRollsFire(MeleeArenaResult r, long rolls)
        {
            var passed = rolls - r.Counter("pointblank.reject.rollFailed");
            var fired = r.Counter("pointblank.success");
            var fallback = r.Counter("pointblank.reject.cannotStart") + r.Counter("pointblank.reject.notFired");
            var share = passed > 0 ? (double)fallback / passed : 0d;
            r.Assertions.Add(Check(
                "passed rolls fire",
                fired > 0 && fired + fallback == passed && share <= MaxFallbackShare,
                $"{fired:N0} fired, {fallback:N0} fell back to melee, of {passed:N0} passed rolls"));
        }

        private static AssertionResult Check(string name, bool passed, string detail) =>
            new AssertionResult { Name = name, Passed = passed, Detail = detail };
    }
}
