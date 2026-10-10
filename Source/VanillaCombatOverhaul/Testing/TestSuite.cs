using System.Collections.Generic;
using System.Text;
using Verse;

namespace VanillaCombatOverhaul
{
    /// <summary>Scenario matrices and report formatting, shared by the debug actions and the headless run.</summary>
    public static class TestSuite
    {
        /// <summary>Even fights, a mismatch each way, an unarmed control, crowd scenarios and the point-blank matrix.</summary>
        public static IEnumerable<MeleeArenaSpec> DefaultMatrix()
        {
            yield return new MeleeArenaSpec { label = "even-novice",   attackerMeleeSkill = 0,  defenderMeleeSkill = 0 };
            yield return new MeleeArenaSpec { label = "even-skilled",  attackerMeleeSkill = 10, defenderMeleeSkill = 10 };
            yield return new MeleeArenaSpec { label = "even-master",   attackerMeleeSkill = 20, defenderMeleeSkill = 20 };
            yield return new MeleeArenaSpec { label = "weak-attacker", attackerMeleeSkill = 0,  defenderMeleeSkill = 20 };
            yield return new MeleeArenaSpec { label = "weak-defender", attackerMeleeSkill = 20, defenderMeleeSkill = 0 };
            yield return new MeleeArenaSpec { label = "unarmed-control", attackerMeleeSkill = 10, defenderMeleeSkill = 10,
                                         defenderUnarmed = true };

            // Crowd scenarios put the parry budget under pressure; they refresh every 15 ticks so downed defenders are replaced quickly.
            yield return new MeleeArenaSpec { label = "outnumbered-3v1", attackerMeleeSkill = 10, defenderMeleeSkill = 10,
                                         attackersPerDefender = 3, pairs = 12, refreshEveryTicks = 15 };

            // Six attackers with and without the budget, so its effect can be separated from facing.
            yield return new MeleeArenaSpec { label = "outnumbered-6v1", attackerMeleeSkill = 10, defenderMeleeSkill = 10,
                                         attackersPerDefender = 6, pairs = 14, refreshEveryTicks = 15, ticks = 16000 };
            yield return new MeleeArenaSpec { label = "outnumbered-6v1-nobudget", attackerMeleeSkill = 10, defenderMeleeSkill = 10,
                                         attackersPerDefender = 6, pairs = 14, refreshEveryTicks = 15, ticks = 16000,
                                         parryBudgetOverride = 9999 };

            foreach (var spec in PointBlankMatrix())
            {
                yield return spec;
            }
        }

        /// <summary>Point-blank: the skill threshold, the chance table's low, middle and top, burst and bow weapons, and cases that must never shoot.</summary>
        public static IEnumerable<MeleeArenaSpec> PointBlankMatrix()
        {
            MeleeArenaSpec Shooter(string label, int shooting, string weapon = "Gun_Autopistol", int ticks = 6000) =>
                new MeleeArenaSpec
                {
                    label = label, attackerMeleeSkill = 10, defenderMeleeSkill = 10,
                    attackerWeapon = weapon, attackerShootingSkill = shooting,
                    pointBlankOverride = 1, ticks = ticks
                };

            yield return Shooter("pointblank-shoot9", 9);
            yield return Shooter("pointblank-shoot10", 10, ticks: 16000);
            yield return Shooter("pointblank-shoot15", 15);
            yield return Shooter("pointblank-shoot20", 20);
            yield return Shooter("pointblank-shoot20-burst", 20, "Gun_AssaultRifle");
            yield return Shooter("pointblank-shoot20-bow", 20, "Bow_Short");

            var disabled = Shooter("pointblank-disabled", 20);
            disabled.pointBlankOverride = -1;
            yield return disabled;

            var ordered = Shooter("pointblank-player-orders", 20);
            ordered.pointBlankHonourOrders = true;
            yield return ordered;
        }

        public static List<MeleeArenaResult> RunMatrix(Map map, int seed = 0)
        {
            var results = new List<MeleeArenaResult>();
            foreach (var spec in DefaultMatrix())
            {
                if (seed != 0)
                {
                    spec.seed = seed;
                }
                results.Add(MeleeCombatArena.Run(spec, map));
            }
            return results;
        }

        // ------------------------------------------------------------------ report

        public static string FormatReport(List<MeleeArenaResult> results, List<AssertionResult> facing,
                                          List<AssertionResult> armor = null)
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

            if (armor != null)
            {
                sb.AppendLine();
                sb.AppendLine("-- armor formula checks --");
                foreach (var a in armor)
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
                sb.AppendLine($"  chance: curve predicts {MeleeAssertions.PredictParryChance(r.Spec):P1}, " +
                              $"measured inputs predict {MeleeAssertions.ExpectedFromMeasuredInputs(r):P1}, " +
                              $"rolled {r.ReadingAverage("parry.chanceRolled"):P1}");
                if (r.Spec.IsPointBlank)
                {
                    sb.AppendLine($"  point-blank: {r.Spec.attackerWeapon}, shooting {r.Spec.attackerShootingSkill}, " +
                                  $"{r.Counter("pointblank.success"):N0} shots from {r.Counter("pointblank.roll"):N0} rolls, " +
                                  $"table {PointBlankAssertions.ExpectedChance(r.Spec.attackerShootingSkill):P0}; " +
                                  $"cooldown ticks ranged {r.ReadingAverage("pointblank.rangedCooldownTicks"):0} " +
                                  $"vs melee {r.ReadingAverage("pointblank.meleeCooldownTicks"):0}");
                }

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

        public static string FormatChecks(string title, List<AssertionResult> checks)
        {
            var sb = new StringBuilder();
            sb.AppendLine("=== VCO " + title + " ===");
            var passed = 0;
            var total = 0;
            foreach (var a in checks)
            {
                sb.AppendLine("  " + a);
                total++;
                if (a.Passed) { passed++; }
            }
            sb.AppendLine($"=== {passed} of {total} checks passed ===");
            return sb.ToString();
        }

        public static string FormatRangedReport(List<RangedArenaResult> results, List<AssertionResult> selfTests)
        {
            var sb = new StringBuilder();
            sb.AppendLine("=== VCO ranged accuracy test ===");

            var passed = 0;
            var total = 0;

            if (selfTests != null)
            {
                sb.AppendLine();
                sb.AppendLine("-- ranged formula checks --");
                foreach (var a in selfTests)
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
                sb.AppendLine($"  shooter skill {r.Spec.shooterSkill}, distance {r.Spec.distance}, " +
                              $"target moving {r.Spec.targetMoving}");
                sb.AppendLine($"  weapon factor raw {r.EquipmentFactor:P3}, mitigated {r.MitigatedEquipmentFactor:P3}, " +
                              $"expected {r.ExpectedMitigatedEquipment:P3}");
                sb.AppendLine($"  evasion multiplier {r.EvasionMultiplier:P3}, aim {r.AimOnTarget:P1}");

                foreach (var a in r.Assertions)
                {
                    sb.AppendLine("  " + a);
                    total++;
                    if (a.Passed) { passed++; }
                }
            }

            sb.AppendLine();
            sb.AppendLine($"=== {passed} of {total} ranged checks passed ===");
            return sb.ToString();
        }

        public static List<RangedArenaResult> RunRangedMatrix(Map map, int seed = 0) =>
            RangedCombatArena.RunMatrix(map, seed);
    }
}
