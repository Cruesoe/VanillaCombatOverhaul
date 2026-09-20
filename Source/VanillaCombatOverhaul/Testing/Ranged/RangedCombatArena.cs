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

                // Checked before any mechanic is measured, so a scenario that could not set
                // itself up says exactly that. Without this the same two failures read as
                // "high skill did not improve the weapon factor" and "a moving target had no
                // evasion" -- both of which point at the mod rather than at the harness.
                var pinned = shooter.skills?.GetSkill(SkillDefOf.Shooting);
                result.Assertions.Add(new AssertionResult
                {
                    Name = "shooter skill pinned to spec",
                    Passed = pinned != null && pinned.Level == spec.shooterSkill,
                    Detail = "asked " + spec.shooterSkill + ", got " + (pinned?.Level.ToString() ?? "no skills tracker")
                });

                if (spec.targetMoving)
                {
                    result.Assertions.Add(new AssertionResult
                    {
                        Name = "target is actually moving",
                        Passed = IsMoving(target),
                        Detail = "velocity " + EvasionUtility.MovementVelocity(target).ToString("F2")
                                 + " c/s, movingNow " + (target.pather?.MovingNow ?? false)
                    });
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

        // Matches the melee arena. A generated pawn can have Shooting disabled by its
        // backstory or shifted by an aptitude gene, so unsuitable ones are discarded.
        private const int GenerationAttempts = 40;

        /// <summary>
        /// Assigning SkillRecord.Level is not enough on its own.
        ///
        /// The getter adds an aptitude offset on top of the stored value, and on a pawn whose
        /// backstory disables Shooting the setter does nothing at all. The melee arena learned
        /// this and reads the value back; this one did not, which is how a scenario asking for
        /// skill 20 could quietly run at whatever the generator felt like and report that high
        /// skill had failed to improve anything.
        /// </summary>
        private static bool TryPinShootingSkill(Pawn pawn, int level)
        {
            var shooting = pawn.skills?.GetSkill(SkillDefOf.Shooting);
            if (shooting == null || shooting.TotallyDisabled)
            {
                return false;
            }
            shooting.Level = level;
            shooting.passion = Passion.None;
            shooting.xpSinceLastLevel = 0;
            return shooting.Level == level;
        }

        /// <summary>
        /// A pawn stripped of everything that would move ShootingAccuracyPawn or MoveSpeed
        /// around between runs -- hediffs, traits and apparel -- so the only variables left
        /// are the ones the spec sets.
        /// </summary>
        private static Pawn MakeCleanPawn(int shootingSkill, bool requireSkill)
        {
            for (var attempt = 0; attempt < GenerationAttempts; attempt++)
            {
                var candidate = PawnGenerator.GeneratePawn(PawnKindDefOf.Colonist, Faction.OfPlayer);
                if (candidate == null)
                {
                    continue;
                }

                candidate.health.RemoveAllHediffs();
                candidate.story?.traits?.allTraits?.Clear();
                candidate.apparel?.DestroyAll();

                if (!requireSkill || TryPinShootingSkill(candidate, shootingSkill))
                {
                    return candidate;
                }
                candidate.Destroy(DestroyMode.Vanish);
            }
            return null;
        }

        internal static Pawn SpawnShooter(Map map, IntVec3 cell, int skill, ThingDef weapon)
        {
            var pawn = MakeCleanPawn(skill, requireSkill: true);
            if (pawn == null)
            {
                return null;
            }

            GenSpawn.Spawn(pawn, cell, map);
            var item = ThingMaker.MakeThing(weapon);
            pawn.equipment?.AddEquipment((ThingWithComps)item);
            return pawn;
        }

        internal static Pawn SpawnTarget(Map map, IntVec3 cell, bool moving)
        {
            var pawn = MakeCleanPawn(0, requireSkill: false);
            if (pawn == null)
            {
                return null;
            }
            GenSpawn.Spawn(pawn, cell, map);

            if (moving && pawn.pather != null)
            {
                StartMoving(pawn, map, cell);
            }
            return pawn;
        }

        /// <summary>
        /// Gets the target genuinely moving, trying each direction until one takes.
        ///
        /// A single hardcoded destination can be unreachable -- water, rock, the map edge --
        /// in which case the pawn just stands there, evasion reads 1.0, and the scenario
        /// reports that a moving target had no evasion. That is a harness failure wearing a
        /// mechanic's clothes. The caller checks IsMoving and says so plainly instead.
        /// </summary>
        private static void StartMoving(Pawn pawn, Map map, IntVec3 from)
        {
            foreach (var dir in new[] { IntVec3.North, IntVec3.South, IntVec3.East, IntVec3.West })
            {
                var dest = from + dir * 20;
                if (!dest.InBounds(map) || !dest.Walkable(map))
                {
                    continue;
                }

                pawn.pather.StartPath(dest, PathEndMode.OnCell);
                for (var i = 0; i < 30; i++)
                {
                    pawn.pather.PatherTick();
                }
                if (IsMoving(pawn))
                {
                    return;
                }
            }
        }

        /// <summary>True once the pawn is actually under way at a speed evasion can see.</summary>
        public static bool IsMoving(Pawn pawn) =>
            pawn?.pather != null && pawn.pather.MovingNow
            && EvasionUtility.MovementVelocity(pawn) > 0f;
    }
}
