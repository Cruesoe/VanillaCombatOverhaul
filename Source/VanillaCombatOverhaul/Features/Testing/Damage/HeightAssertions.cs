using System.Collections.Generic;
using RimWorld;
using UnityEngine;
using Verse;

namespace VanillaCombatOverhaul
{
    public static class HeightAssertions
    {
        public static List<AssertionResult> SelfTests()
        {
            var results = new List<AssertionResult>();
            results.Add(Check(
                "undefined height always lands",
                Mathf.Approximately(
                    HeightTargeting.ChanceToLand(null, null, null, BodyPartHeight.Undefined, null, false),
                    1f),
                "expected 1"));

            var failOpen = HeightTargeting.ChanceToLand(null, null, null, BodyPartHeight.Top, null, false);
            results.Add(Check(
                "no pawn fails open rather than NaN",
                Mathf.Approximately(failOpen, 1f) && !float.IsNaN(failOpen),
                failOpen.ToString()));
            return results;
        }

        private static AssertionResult Check(string name, bool passed, string detail) =>
            new AssertionResult { Name = name, Passed = passed, Detail = detail };
    }
}
