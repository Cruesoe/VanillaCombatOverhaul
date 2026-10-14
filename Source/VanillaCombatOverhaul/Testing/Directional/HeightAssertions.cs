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
                    HeightTargetingUtility.ChanceToLand(null, null, null, BodyPartHeight.Undefined, null, false),
                    1f),
                "expected 1"));

            var failOpen = HeightTargetingUtility.ChanceToLand(null, null, null, BodyPartHeight.Top, null, false);
            results.Add(Check(
                "no pawn fails open rather than NaN",
                Mathf.Approximately(failOpen, 1f) && !float.IsNaN(failOpen),
                failOpen.ToString()));
            return results;
        }

        /// <summary>CoveragePair must match the two-pass Coverage on real bodies, which relies on vanilla's height filter.</summary>
        public static IEnumerable<AssertionResult> CoverageFusionTests(Pawn pawn)
        {
            if (pawn?.health?.hediffSet == null)
            {
                // A check that cannot run fails rather than disappearing.
                yield return Check("fused coverage had a body to check against", false,
                                   "no humanlike pawn on the map");
                yield break;
            }

            var sides = new[]
            {
                null, VCO_BodyPartGroupDefOf.VCO_Left,
                VCO_BodyPartGroupDefOf.VCO_Right, VCO_BodyPartGroupDefOf.VCO_Center
            };

            foreach (var side in sides)
            {
                foreach (var height in new[]
                         { BodyPartHeight.Bottom, BodyPartHeight.Middle, BodyPartHeight.Top })
                {
                    HeightTargetingUtility.CoveragePair(
                        pawn, side, DamageDefOf.Bullet, height, out var at, out var any);

                    var refAt = HeightTargetingUtility.Coverage(pawn, side, DamageDefOf.Bullet, height);
                    var refAny = HeightTargetingUtility.Coverage(
                        pawn, side, DamageDefOf.Bullet, BodyPartHeight.Undefined);

                    var sideName = side == null ? "any side" : side.defName;
                    yield return Check(
                        $"fused coverage matches two-pass ({sideName}, {height})",
                        Mathf.Approximately(at, refAt) && Mathf.Approximately(any, refAny),
                        $"fused {at:F4}/{any:F4}, reference {refAt:F4}/{refAny:F4}");
                }
            }
        }

        private static AssertionResult Check(string name, bool passed, string detail) =>
            new AssertionResult { Name = name, Passed = passed, Detail = detail };
    }
}
