using System.Collections.Generic;
using RimWorld;
using UnityEngine;
using Verse;
using Verse.AI;

namespace VanillaCombatOverhaul
{
    /// <summary>
    /// Incoming fire builds suppression on nearby hostile humanlikes. Suppressed pawns aim worse and
    /// slower; pinned non-player pawns take cover. Works from any projectile's damage, so modded
    /// weapons need no patches.
    /// </summary>
    [StaticConstructorOnStartup]
    public static class SuppressionUtility
    {
        public const float SuppressedLevel = 30f;
        public const float PinnedLevel = 90f;
        public const float MaxLevel = 150f;
        public const float BaseRadius = 2.9f;
        public const float ExplosionRadiusBonus = 2f;
        public const float EdgeFalloff = 0.5f;
        public const float ExplosionFactor = 2f;
        public const float SuppressionModeFactor = 1.5f;
        public const int DecayDelayTicks = 60;
        public const float DecayPerSecond = 20f;
        public const float MinAccuracy = 0.5f;
        public const float SuppressedAccuracy = 0.85f;
        public const float SuppressedAimTime = 1.15f;
        public const float MaxAimTime = 1.5f;

        private static readonly HashSet<Pawn> HitThisImpact = new HashSet<Pawn>();

        static SuppressionUtility()
        {
            foreach (var def in DefDatabase<ThingDef>.AllDefs)
            {
                if (def.race == null || !def.race.Humanlike || def.HasComp(typeof(CompSuppression)))
                {
                    continue;
                }
                if (def.comps == null)
                {
                    def.comps = new List<CompProperties>();
                }
                def.comps.Add(new CompProperties_Suppression());
            }
        }

        public static bool Enabled => VCOMod.Settings?.enableSuppression ?? false;

        /// <summary>0 below the suppressed level, rising to 1 at the pinned level.</summary>
        public static float Severity(float level)
        {
            if (level < SuppressedLevel)
            {
                return 0f;
            }
            return Mathf.Clamp01((level - SuppressedLevel) / (PinnedLevel - SuppressedLevel));
        }

        /// <summary>Accuracy divisor in the fire mode sense: 1 is unaffected.</summary>
        public static float AccuracyFor(float level) =>
            level < SuppressedLevel ? 1f : Mathf.Lerp(SuppressedAccuracy, MinAccuracy, Severity(level));

        public static float AimTimeFor(float level) =>
            level < SuppressedLevel ? 1f : Mathf.Lerp(SuppressedAimTime, MaxAimTime, Severity(level));

        /// <summary>Full strength at the impact cell, falling to EdgeFalloff at the radius.</summary>
        public static float Falloff(float distance, float radius)
        {
            if (radius <= 0f || distance > radius)
            {
                return 0f;
            }
            return Mathf.Lerp(1f, EdgeFalloff, distance / radius);
        }

        public static float Decay(float level, int ticks) =>
            Mathf.Max(0f, level - DecayPerSecond * ticks / 60f);

        public static CompSuppression CompFor(Pawn pawn) => pawn?.TryGetComp<CompSuppression>();

        public static float LevelOf(Pawn pawn)
        {
            if (!Enabled)
            {
                return 0f;
            }
            return CompFor(pawn)?.Level ?? 0f;
        }

        /// <summary>Spreads suppression from a projectile landing or exploding at a point.</summary>
        public static void ApplyImpact(Projectile projectile, Map map, Vector3 position, bool explosion)
        {
            var settings = VCOMod.Settings;
            if (settings == null || !settings.enableSuppression || map == null)
            {
                return;
            }
            var props = projectile.def?.projectile;
            var launcher = projectile.Launcher;
            if (props?.damageDef == null || !props.damageDef.harmsHealth || launcher == null)
            {
                return;
            }

            var damage = (float)projectile.DamageAmount;
            if (damage <= 0f)
            {
                return;
            }
            var radius = BaseRadius;
            if (explosion)
            {
                damage *= ExplosionFactor;
                radius = Mathf.Max(radius, props.explosionRadius + ExplosionRadiusBonus);
            }
            damage *= settings.suppressionStrength * FireModeFactor(launcher);

            var center = position.ToIntVec3();
            var source = launcher.Position;
            var cells = GenRadial.NumCellsInRadius(radius);
            HitThisImpact.Clear();
            for (var i = 0; i < cells; i++)
            {
                var cell = center + GenRadial.RadialPattern[i];
                if (!cell.InBounds(map))
                {
                    continue;
                }
                var things = map.thingGrid.ThingsListAtFast(cell);
                for (var j = 0; j < things.Count; j++)
                {
                    if (!(things[j] is Pawn pawn) || !HitThisImpact.Add(pawn) || !CanBeSuppressedBy(pawn, launcher))
                    {
                        continue;
                    }
                    var falloff = Falloff((pawn.DrawPos - position).MagnitudeHorizontal(), radius);
                    var amount = damage * falloff * pawn.GetStatValue(VCO_StatDefOf.VCO_Suppressability, true, 60);
                    if (amount > 0f)
                    {
                        CompFor(pawn)?.AddSuppression(amount, source);
                    }
                }
            }
            HitThisImpact.Clear();
        }

        public static bool CanBeSuppressedBy(Pawn pawn, Thing launcher)
        {
            if (pawn.Dead || pawn.Downed || !pawn.Spawned || !pawn.RaceProps.Humanlike)
            {
                return false;
            }
            if (pawn.InAggroMentalState || !pawn.HostileTo(launcher) || HasActiveShield(pawn))
            {
                return false;
            }
            return true;
        }

        private static bool HasActiveShield(Pawn pawn)
        {
            var worn = pawn.apparel?.WornApparel;
            if (worn == null)
            {
                return false;
            }
            for (var i = 0; i < worn.Count; i++)
            {
                var shield = worn[i].GetComp<CompShield>();
                if (shield != null && shield.ShieldState == ShieldState.Active)
                {
                    return true;
                }
            }
            return false;
        }

        private static float FireModeFactor(Thing launcher)
        {
            if (!(launcher is Pawn shooter))
            {
                return 1f;
            }
            var mode = FireModeUtility.ActiveMode(shooter, FireModeUtility.PrimaryVerb(shooter));
            return mode == FireMode.Suppression ? SuppressionModeFactor : 1f;
        }

        /// <summary>Applies the shooter's suppression to a freshly built shot report.</summary>
        public static void ApplyToShotReport(ref ShotReport report, Thing caster)
        {
            var level = LevelOf(caster as Pawn);
            if (level < SuppressedLevel)
            {
                return;
            }
            var factor = ShotReportAccess.GetShooterFactor(ref report);
            var adjusted = FireModeUtility.AdjustHitFactor(factor, AccuracyFor(level));
            if (!Mathf.Approximately(factor, adjusted))
            {
                VCODiagnostics.Count("suppression.hit.adjusted");
                ShotReportAccess.SetShooterFactor(ref report, adjusted);
            }
        }

        /// <summary>Best nearby cell for cover from the source, or the pawn's own cell.</summary>
        public static IntVec3 FindCover(Pawn pawn, IntVec3 source, float radius)
        {
            var map = pawn.Map;
            var best = pawn.Position;
            var bestScore = CoverUtility.CalculateOverallBlockChance(pawn.Position, source, map) + 0.1f;
            var cells = GenRadial.NumCellsInRadius(radius);
            for (var i = 1; i < cells; i++)
            {
                var cell = pawn.Position + GenRadial.RadialPattern[i];
                if (!cell.InBounds(map) || !cell.Standable(map) || cell.GetFirstPawn(map) != null)
                {
                    continue;
                }
                var score = CoverUtility.CalculateOverallBlockChance(cell, source, map)
                            - 0.02f * GenRadial.RadialPattern[i].LengthHorizontal;
                if (score <= bestScore || !pawn.CanReach(cell, PathEndMode.OnCell, Danger.Some))
                {
                    continue;
                }
                best = cell;
                bestScore = score;
            }
            return best;
        }
    }
}
