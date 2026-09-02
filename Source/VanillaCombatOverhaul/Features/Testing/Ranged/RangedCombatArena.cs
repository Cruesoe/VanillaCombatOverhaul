using System.Collections.Generic;
using RimWorld;
using UnityEngine;
using Verse;
using Verse.AI;

namespace VanillaCombatOverhaul
{
    public static class RangedCombatArena
    {
        public static List<RangedArenaResult> RunMatrix(Map map, int seed = 0)
        {
            var results = new List<RangedArenaResult>();
            foreach (var spec in RangedAssertions.DefaultMatrix())
            {
                if (seed != 0)
                {
                    spec.seed = seed;
                }
                results.Add(Run(spec, map));
            }
            return results;
        }

        public static RangedArenaResult Run(RangedArenaSpec spec, Map map)
        {
            var result = new RangedArenaResult { Spec = spec };

            if (map == null)
            {
                result.Assertions.Add(new AssertionResult
                {
                    Name = "map loaded",
                    Passed = false,
                    Detail = "ranged arena needs a loaded map"
                });
                return result;
            }

            var weapon = ResolveWeapon(spec.weaponDef);
            if (weapon == null)
            {
                result.Assertions.Add(new AssertionResult
                {
                    Name = "weapon resolved",
                    Passed = false,
                    Detail = "could not resolve " + spec.weaponDef
                });
                return result;
            }

            var seeded = spec.seed != 0;
            if (seeded)
            {
                Rand.PushState(spec.seed);
            }

            Pawn shooter = null;
            Pawn target = null;

            try
            {
                var origin = map.Center;
                shooter = SpawnShooter(map, origin + IntVec3.West * 2, spec.shooterSkill, weapon);
                target = SpawnTarget(map, origin + IntVec3.East * spec.distance, spec.targetMoving);

                if (shooter == null || target == null)
                {
                    result.Assertions.Add(new AssertionResult
                    {
                        Name = "pawns spawned",
                        Passed = false,
                        Detail = "failed to spawn shooter or target"
                    });
                    return result;
                }

                var verb = shooter.equipment?.PrimaryEq?.PrimaryVerb;
                if (verb == null)
                {
                    result.Assertions.Add(new AssertionResult
                    {
                        Name = "ranged verb",
                        Passed = false,
                        Detail = "shooter has no ranged verb"
                    });
                    return result;
                }

                var settings = VCOMod.Settings;
                var advancedWasOn = settings.enableAdvancedAccuracy;
                var evasionWasOn = settings.enableEvasion;
                settings.enableAdvancedAccuracy = false;
                settings.enableEvasion = false;

                var baseline = ShotReport.HitReportFor(shooter, verb, target);
                result.EquipmentFactor = ShotReportAccess.GetEquipmentFactor(ref baseline);

                settings.enableAdvancedAccuracy = advancedWasOn;
                settings.enableEvasion = evasionWasOn;

                var mitigated = ShotReport.HitReportFor(shooter, verb, target);
                result.MitigatedEquipmentFactor = ShotReportAccess.GetEquipmentFactor(ref mitigated);
                result.WeatherFactor = ShotReportAccess.GetWeatherFactor(ref mitigated);
                result.AimOnTarget = mitigated.AimOnTargetChance_IgnoringPosture;

                var skill = StatDefOf.ShootingAccuracyPawn.Worker.GetValue(StatRequest.For(shooter), false);
                result.ExpectedMitigatedEquipment = PenaltyMitigationUtility.MitigatePenalty(
                    result.EquipmentFactor, shooter, settings.accuracyScale);
                result.EvasionMultiplier = EvasionUtility.HitChanceMultiplier(target, shooter);

                RangedAssertions.Evaluate(result);
            }
            finally
            {
                shooter?.Destroy(DestroyMode.Vanish);
                target?.Destroy(DestroyMode.Vanish);
                if (seeded)
                {
                    Rand.PopState();
                }
            }

            return result;
        }

        private static ThingDef ResolveWeapon(string defName)
        {
            var def = DefDatabase<ThingDef>.GetNamedSilentFail(defName);
            return def?.IsWeapon ?? false ? def : null;
        }

        private static Pawn SpawnShooter(Map map, IntVec3 cell, int skill, ThingDef weapon)
        {
            var pawn = PawnGenerator.GeneratePawn(PawnKindDefOf.Colonist, Faction.OfPlayer);
            if (pawn?.skills != null)
            {
                pawn.skills.GetSkill(SkillDefOf.Shooting).Level = skill;
            }

            GenSpawn.Spawn(pawn, cell, map);
            var item = ThingMaker.MakeThing(weapon);
            pawn.equipment?.AddEquipment((ThingWithComps)item);
            return pawn;
        }

        private static Pawn SpawnTarget(Map map, IntVec3 cell, bool moving)
        {
            var pawn = PawnGenerator.GeneratePawn(PawnKindDefOf.Colonist, Faction.OfPlayer);
            GenSpawn.Spawn(pawn, cell, map);

                if (moving && pawn.pather != null)
                {
                    var dest = cell + IntVec3.North * 20;
                    if (dest.InBounds(map))
                    {
                        pawn.pather.StartPath(dest, PathEndMode.OnCell);
                        for (var i = 0; i < 30; i++)
                        {
                            pawn.pather.PatherTick();
                        }
                    }
                }

            return pawn;
        }
    }
}
