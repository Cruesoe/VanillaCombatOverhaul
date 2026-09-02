using System.Collections.Generic;
using System.Text;
using Verse;

namespace VanillaCombatOverhaul
{
    /// <summary>
    /// The scenario matrix, shared by the manual dev-menu runs and the automated headless run
    /// so both exercise exactly the same thing.
    /// </summary>
    public static class TestSuite
    {
        /// <summary>
        /// Skill pairings chosen to hit the corners and the middle of the balance table: an
        /// even fight, a mismatch in each direction, plus a control that must produce no
        /// parries at all.
        /// </summary>
        public static IEnumerable<ArenaSpec> DefaultMatrix()
        {
            yield return new ArenaSpec { label = "even-novice",   attackerMeleeSkill = 0,  defenderMeleeSkill = 0 };
            yield return new ArenaSpec { label = "even-skilled",  attackerMeleeSkill = 10, defenderMeleeSkill = 10 };
            yield return new ArenaSpec { label = "even-master",   attackerMeleeSkill = 20, defenderMeleeSkill = 20 };
            yield return new ArenaSpec { label = "weak-attacker", attackerMeleeSkill = 0,  defenderMeleeSkill = 20 };
            yield return new ArenaSpec { label = "weak-defender", attackerMeleeSkill = 20, defenderMeleeSkill = 0 };
            yield return new ArenaSpec { label = "unarmed-control", attackerMeleeSkill = 10, defenderMeleeSkill = 10,
                                         defenderUnarmed = true };

            // The only scenario that puts the parry budget under any pressure. Every matchup
            // above is a duel, and a lone attacker cannot swing fast enough to spend a budget
            // of two per window, so the one mechanic with no equivalent in Vanilla Combat
            // Reloaded went entirely unexercised until this was added. Fewer groups because
            // each one now costs four pawns and needs a clear ring of ground to stand on.
            // A crowd puts a defender down well inside the 120-tick refresh the duels use, and
            // most of the window is then spent with nothing attacking a downed pawn -- six
            // attackers produced fewer samples than three until this dropped to 30. The three
            // crowd scenarios share a cadence so they can be compared with each other.
            yield return new ArenaSpec { label = "outnumbered-3v1", attackerMeleeSkill = 10, defenderMeleeSkill = 10,
                                         attackersPerDefender = 3, pairs = 12, refreshEveryTicks = 15 };

            // Six attackers, run twice: once as configured, once with the cap lifted. The pair
            // is the experiment -- the difference between them is exactly what the budget
            // contributes, and everything else in the drop belongs to the facing gate. Without
            // the control the two effects are impossible to tell apart from one number.
            yield return new ArenaSpec { label = "outnumbered-6v1", attackerMeleeSkill = 10, defenderMeleeSkill = 10,
                                         attackersPerDefender = 6, pairs = 14, refreshEveryTicks = 15, ticks = 16000 };
            yield return new ArenaSpec { label = "outnumbered-6v1-nobudget", attackerMeleeSkill = 10, defenderMeleeSkill = 10,
                                         attackersPerDefender = 6, pairs = 14, refreshEveryTicks = 15, ticks = 16000,
                                         parryBudgetOverride = 9999 };
        }

        public static List<ArenaResult> RunMatrix(Map map, int seed = 0)
        {
            var results = new List<ArenaResult>();
            foreach (var spec in DefaultMatrix())
            {
                if (seed != 0)
                {
                    spec.seed = seed;
                }
                results.Add(CombatArena.Run(spec, map));
            }
            return results;
        }

        // ------------------------------------------------------------------ report

        public static string FormatReport(List<ArenaResult> results, List<AssertionResult> facing)
        {
            var sb = new StringBuilder();
            sb.AppendLine("=== VCO combat test ===");

            var passed = 0;
            var total = 0;

            if (facing != null)
            {
                sb.AppendLine();
                sb.AppendLine("-- facing unit checks --");
                foreach (var a in facing)
                {
                    sb.AppendLine("  " + a);
                    total++;
                    if (a.Passed) { passed++; }
                }
            }

            foreach (var r in results)
            {
                sb.AppendLine();
                sb.AppendLine($"-- scenario: {r.Spec.label} --");
                sb.AppendLine($"  attacker melee {r.Spec.attackerMeleeSkill}, defender melee {r.Spec.defenderMeleeSkill}" +
                              (r.Spec.defenderUnarmed ? ", defender unarmed" : "") +
                              $", {r.AttackersSpawned} attackers on {r.DefendersSpawned} defenders" +
                              $" ({r.Spec.attackersPerDefender} each), {r.Spec.ticks} ticks, seed {r.Spec.seed}");
                sb.AppendLine($"  attrition: {r.PawnsDied} of {r.PawnsSpawned} combatants died" +
                              " (dead pawns are never revived, so this caps the sample)");

                var attempts = r.Counter("parry.attempt");
                var success = r.Counter("parry.success");
                sb.AppendLine($"  parry: {success:N0} of {attempts:N0} eligible attacks" +
                              (attempts > 0 ? $" ({(double)success / attempts:P1})" : ""));
                sb.AppendLine($"  chance: curve predicts {ArenaAssertions.PredictParryChance(r.Spec):P1}, " +
                              $"measured inputs predict {ArenaAssertions.ExpectedFromMeasuredInputs(r):P1}, " +
                              $"rolled {r.ReadingAverage("parry.chanceRolled"):P1}");

                sb.AppendLine("  counters:");
                foreach (var kv in r.Counters)
                {
                    sb.AppendLine($"      {kv.Key,-40} {kv.Value,10:N0}");
                }

                foreach (var a in r.Assertions)
                {
                    sb.AppendLine("  " + a);
                    total++;
                    if (a.Passed) { passed++; }
                }
            }

            sb.AppendLine();
            sb.AppendLine($"=== {passed} of {total} checks passed ===");
            return sb.ToString();
        }
    }
}
