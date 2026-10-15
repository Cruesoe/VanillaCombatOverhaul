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
        /// <summary>Aiming needs Shooting 10 for shots and Melee 10 for melee attacks; needs a game for pawn generation.</summary>
        public static List<AssertionResult> AimSkillTests()
        {
            var results = new List<AssertionResult>();
            var pawn = PawnGenerator.GeneratePawn(new PawnGenerationRequest(PawnKindDefOf.Colonist, Faction.OfPlayer,
                mustBeCapableOfViolence: true));
            var comp = pawn.TryGetComp<CompHeightTarget>();
            var shooting = pawn.skills?.GetSkill(SkillDefOf.Shooting);
            var melee = pawn.skills?.GetSkill(SkillDefOf.Melee);
            if (comp == null || shooting == null || melee == null)
            {
                results.Add(Check("aim test pawn has the height comp and both skills", false,
                    $"comp {comp != null}, shooting {shooting != null}, melee {melee != null}"));
                return results;
            }
            comp.SetTargetingMode(BodyPartHeight.Top);

            shooting.Level = 0;
            melee.Level = 12;
            var meleeOnly = (melee: HeightTargetingUtility.GetTargetHeight(pawn, melee: true),
                ranged: HeightTargetingUtility.GetTargetHeight(pawn, melee: false),
                any: HeightTargetingUtility.CanAimAny(pawn));
            results.Add(Check("Melee 12 with Shooting 0 aims melee attacks but not shots",
                meleeOnly.melee == BodyPartHeight.Top && meleeOnly.ranged == BodyPartHeight.Undefined && meleeOnly.any,
                $"melee {meleeOnly.melee}, ranged {meleeOnly.ranged}, button {meleeOnly.any}"));

            shooting.Level = 12;
            melee.Level = 0;
            var shootingOnly = (melee: HeightTargetingUtility.GetTargetHeight(pawn, melee: true),
                any: HeightTargetingUtility.CanAimAny(pawn),
                ranged: HeightTargetingUtility.MeetsSkillRequirement(pawn, melee: false));
            results.Add(Check("Shooting 12 with Melee 0 qualifies for shots but not melee attacks",
                shootingOnly.melee == BodyPartHeight.Undefined && shootingOnly.ranged && shootingOnly.any,
                $"melee {shootingOnly.melee}, ranged skill {shootingOnly.ranged}, button {shootingOnly.any}"));

            shooting.Level = 9;
            melee.Level = 9;
            results.Add(Check("below 10 in both skills there is no aim button",
                !HeightTargetingUtility.CanAimAny(pawn), "shooting 9, melee 9"));
            return results;
        }

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
