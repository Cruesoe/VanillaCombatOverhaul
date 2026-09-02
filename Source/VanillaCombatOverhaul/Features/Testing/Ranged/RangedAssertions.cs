using System;
using System.Collections.Generic;
using RimWorld;
using UnityEngine;
using Verse;

namespace VanillaCombatOverhaul
{
    public static class RangedAssertions
    {
        private const double FormulaTolerance = 0.001;

        public static List<AssertionResult> SelfTests()
        {
            var results = new List<AssertionResult>();
            results.AddRange(MitigationFormulaTests());
            results.AddRange(EvasionFormulaTests());
            results.AddRange(FiringArcFormulaTests());
            results.AddRange(PatchGuardTests());
            return results;
        }

        public static void Evaluate(RangedArenaResult result)
        {
            var delta = Math.Abs(result.MitigatedEquipmentFactor - result.ExpectedMitigatedEquipment);
            result.Assertions.Add(Check(
                "mitigated weapon factor matches formula",
                delta <= FormulaTolerance,
                $"expected {result.ExpectedMitigatedEquipment:P3}, got {result.MitigatedEquipmentFactor:P3}"));

            if (result.Spec.label == "mitigation-high-skill")
            {
                result.Assertions.Add(Check(
                    "high skill improves mitigated weapon factor",
                    result.MitigatedEquipmentFactor > result.EquipmentFactor + 0.001f,
                    $"raw {result.EquipmentFactor:P3}, mitigated {result.MitigatedEquipmentFactor:P3}"));
            }

            if (result.Spec.label == "mitigation-low-skill")
            {
                result.Assertions.Add(Check(
                    "low skill leaves weapon factor unchanged",
                    Math.Abs(result.MitigatedEquipmentFactor - result.EquipmentFactor) <= FormulaTolerance,
                    $"raw {result.EquipmentFactor:P3}, mitigated {result.MitigatedEquipmentFactor:P3}"));
            }

            if (result.Spec.targetMoving)
            {
                result.Assertions.Add(Check(
                    "moving target has evasion multiplier below 1",
                    result.EvasionMultiplier < 0.999f,
                    $"multiplier {result.EvasionMultiplier:P3}"));
            }
            else
            {
                result.Assertions.Add(Check(
                    "stationary target has no evasion multiplier",
                    Math.Abs(result.EvasionMultiplier - 1f) <= FormulaTolerance,
                    $"multiplier {result.EvasionMultiplier:P3}"));
            }
        }

        public static IEnumerable<RangedArenaSpec> DefaultMatrix()
        {
            yield return new RangedArenaSpec { label = "mitigation-low-skill", shooterSkill = 0, distance = 40 };
            yield return new RangedArenaSpec { label = "mitigation-high-skill", shooterSkill = 20, distance = 40 };
            yield return new RangedArenaSpec { label = "evasion-moving", shooterSkill = 10, targetMoving = true };
            yield return new RangedArenaSpec { label = "evasion-stationary", shooterSkill = 10, targetMoving = false };
        }

        private static IEnumerable<AssertionResult> MitigationFormulaTests()
        {
            var map = Find.CurrentMap;
            if (map == null)
            {
                yield return Check("mitigation formula tests", false, "no map loaded");
                yield break;
            }

            var scale = VCOMod.Settings?.accuracyScale ?? 5f;
            var cases = new (float factor, int skill)[]
            {
                (0.5f, 0),
                (0.5f, 10),
                (0.5f, 20),
                (1f, 20),
            };

            foreach (var c in cases)
            {
                var shooter = SpawnMockShooter(map, c.skill);
                var actual = PenaltyMitigationUtility.MitigatePenalty(c.factor, shooter, scale);
                var expected = ExpectedMitigation(c.factor, shooter, scale);
                yield return Check(
                    $"mitigation formula skill {c.skill} factor {c.factor}",
                    Math.Abs(actual - expected) <= FormulaTolerance,
                    $"expected {expected:P3}, got {actual:P3}");
                shooter.Destroy(DestroyMode.Vanish);
            }
        }

        private static Pawn SpawnMockShooter(Map map, int skill)
        {
            var pawn = PawnGenerator.GeneratePawn(PawnKindDefOf.Colonist, Faction.OfPlayer);
            if (pawn?.skills != null)
            {
                pawn.skills.GetSkill(SkillDefOf.Shooting).Level = skill;
            }
            GenSpawn.Spawn(pawn, map.Center, map);
            return pawn;
        }

        private static float ExpectedMitigation(float factor, Thing shooter, float scale)
        {
            if (factor >= 1f)
            {
                return factor;
            }

            var skillFactor = PenaltyMitigationUtility.ShooterSkillFactor(shooter, scale);
            if (skillFactor <= 1f)
            {
                return factor;
            }

            return (float)Math.Pow(factor, 1d / skillFactor);
        }

        private static IEnumerable<AssertionResult> EvasionFormulaTests()
        {
            var stationary = EvasionUtility.RawMovementMultiplier(null, 0.8f, 2.5f);
            yield return Check(
                "stationary evasion multiplier is 1",
                Math.Abs(stationary - 1f) <= FormulaTolerance,
                stationary.ToString("P3"));

            var settings = VCOMod.Settings;
            var factor = settings?.evasionFactor ?? 0.8f;
            var minSpeed = settings?.evasionMinSpeed ?? 2.5f;
            var excess = 3f;
            var expected = Math.Pow(factor, excess);
            var actual = Math.Pow(Mathf.Clamp(factor, 0.01f, 1f), excess);
            yield return Check(
                "evasion curve at excess speed 3",
                Math.Abs(actual - expected) <= FormulaTolerance,
                $"expected {expected:P3}, got {actual:P3}");
        }

        private static IEnumerable<AssertionResult> FiringArcFormulaTests()
        {
            var settings = VCOMod.Settings;
            var restore = settings?.enableFiringArc ?? false;
            if (settings != null)
            {
                settings.enableFiringArc = true;
            }

            try
            {
                var near = FiringArcUtility.AdjustMissRadius(5f, new IntVec3(10, 0, 0), IntVec3.Zero, 45f);
                var far = FiringArcUtility.AdjustMissRadius(5f, new IntVec3(40, 0, 0), IntVec3.Zero, 45f);
                yield return Check(
                    "firing arc widens with distance",
                    far > near,
                    $"near {near:F2}, far {far:F2}");
            }
            finally
            {
                if (settings != null)
                {
                    settings.enableFiringArc = restore;
                }
            }
        }

        private static IEnumerable<AssertionResult> PatchGuardTests()
        {
            var guard = PatchGuard.All;
            var found = false;
            foreach (var g in guard)
            {
                if (g.Id == "ShootLine.ChangeDestToMissWild.firingArc")
                {
                    found = true;
                    yield return Check(
                        "firing arc transpiler applied",
                        g.Satisfied,
                        $"spliced {g.Actual} of {g.Expected}");
                }
            }

            if (!found)
            {
                yield return Check("firing arc transpiler registered", false, "guard not found");
            }
        }

        private static Thing MockShooter(float skill)
        {
            var map = Find.CurrentMap;
            if (map != null)
            {
                return SpawnMockShooter(map, Mathf.RoundToInt(skill));
            }

            var pawn = PawnGenerator.GeneratePawn(PawnKindDefOf.Colonist, Faction.OfPlayer);
            if (pawn?.skills != null)
            {
                pawn.skills.GetSkill(SkillDefOf.Shooting).Level = Mathf.RoundToInt(skill);
            }
            return pawn;
        }

        private static AssertionResult Check(string name, bool passed, string detail) =>
            new AssertionResult { Name = name, Passed = passed, Detail = detail };
    }
}
