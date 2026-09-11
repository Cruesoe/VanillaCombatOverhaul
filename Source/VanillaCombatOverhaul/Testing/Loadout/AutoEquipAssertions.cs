using System;
using System.Collections.Generic;

namespace VanillaCombatOverhaul
{
    public static class AutoEquipAssertions
    {
        private const float Tolerance = 0.0005f;

        public static List<AssertionResult> SelfTests()
        {
            var results = new List<AssertionResult>();
            var priorityMethod = typeof(JobGiver_AutoEquipPrimary).GetMethod(
                nameof(JobGiver_AutoEquipPrimary.GetPriority));
            results.Add(Check("automatic weapon job supplies sorter priority",
                priorityMethod?.DeclaringType == typeof(JobGiver_AutoEquipPrimary),
                priorityMethod?.DeclaringType?.Name ?? "missing"));

            var baseline = WeaponScoreUtility.RangedScore(10f, 1, 2f, 0.5f, 0f);
            results.Add(Check("ranged score is cycle damage times accuracy",
                Approx(baseline, 2.5f), baseline.ToString("F3")));

            var burst = WeaponScoreUtility.RangedScore(10f, 3, 3f, 0.5f, 0f);
            results.Add(Check("burst damage is counted across the full cycle",
                Approx(burst, 5f), burst.ToString("F3")));

            var penetrating = WeaponScoreUtility.RangedScore(10f, 1, 2f, 0.5f, 0.5f);
            results.Add(Check("armor penetration improves weapon score",
                penetrating > baseline, $"{baseline:F3} -> {penetrating:F3}"));

            var specialist = WeaponScoreUtility.SkillMultiplier(16, 4);
            var mismatch = WeaponScoreUtility.SkillMultiplier(4, 16);
            results.Add(Check("weapon type follows the pawn's stronger skill",
                specialist > mismatch, $"{specialist:F3} vs {mismatch:F3}"));

            results.Add(Check("upgrade margin rejects a small improvement",
                !WeaponScoreUtility.IsUpgrade(100f, 109f, 1.10f, true), "109 < 110"));
            results.Add(Check("upgrade margin accepts its boundary",
                WeaponScoreUtility.IsUpgrade(100f, 110f, 1.10f, true), "110 >= 110"));
            results.Add(Check("a disallowed current weapon may be replaced",
                WeaponScoreUtility.IsUpgrade(100f, 60f, 1.10f, false), "policy takes priority"));
            results.Add(Check("a weaker allowed weapon is never an upgrade",
                !WeaponScoreUtility.IsUpgrade(100f, 90f, 1.10f, true), "90 < 110"));
            return results;
        }

        private static bool Approx(float actual, float expected) =>
            Math.Abs(actual - expected) <= Tolerance;

        private static AssertionResult Check(string name, bool passed, string detail) =>
            new AssertionResult { Name = name, Passed = passed, Detail = detail };
    }
}
