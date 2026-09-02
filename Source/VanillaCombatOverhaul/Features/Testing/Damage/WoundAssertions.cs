using System.Collections.Generic;
using UnityEngine;
using Verse;

namespace VanillaCombatOverhaul
{
    public static class WoundAssertions
    {
        public static List<AssertionResult> SelfTests()
        {
            var results = new List<AssertionResult>();
            results.Add(Check(
                "stopping power below 1 fragments",
                ProjectileWoundUtility.KindFor(0.5f) == BulletWoundKind.Fragment,
                ProjectileWoundUtility.KindFor(0.5f).ToString()));
            results.Add(Check(
                "stopping power 1 passes through",
                ProjectileWoundUtility.KindFor(1f) == BulletWoundKind.PassThrough,
                ProjectileWoundUtility.KindFor(1f).ToString()));
            results.Add(Check(
                "stopping power 1.5 still passes through",
                ProjectileWoundUtility.KindFor(1.5f) == BulletWoundKind.PassThrough,
                ProjectileWoundUtility.KindFor(1.5f).ToString()));
            results.Add(Check(
                "stopping power above 1.5 mushrooms",
                ProjectileWoundUtility.KindFor(2f) == BulletWoundKind.Mushroom,
                ProjectileWoundUtility.KindFor(2f).ToString()));

            results.Add(Check(
                "fragment curve is zero at 0",
                Mathf.Approximately(ProjectileWoundUtility.FragmentTargets.Evaluate(0f), 0f),
                ProjectileWoundUtility.FragmentTargets.Evaluate(0f).ToString("F2")));
            results.Add(Check(
                "fragment curve is 3 at 1",
                Mathf.Approximately(ProjectileWoundUtility.FragmentTargets.Evaluate(1f), 3f),
                ProjectileWoundUtility.FragmentTargets.Evaluate(1f).ToString("F2")));
            results.Add(Check(
                "arrow scratch split is Reloaded's 0.67",
                Mathf.Approximately(ProjectileWoundUtility.ArrowScratchSplit, 0.67f),
                ProjectileWoundUtility.ArrowScratchSplit.ToString("F2")));
            return results;
        }

        private static AssertionResult Check(string name, bool passed, string detail) =>
            new AssertionResult { Name = name, Passed = passed, Detail = detail };
    }
}
