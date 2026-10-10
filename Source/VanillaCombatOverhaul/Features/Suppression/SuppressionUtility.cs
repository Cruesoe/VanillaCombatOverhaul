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
    /// weapons need no patches. Amount, armour reduction and fading follow Combat Extended's rules.
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
        // Combat Extended's fly-by distance, and the radius around the shooter where fly-bys do not count.
        public const float FlybyRadius = 3f;
        public const float MuzzleExclusionRadius = 3f;
        // Combat Extended's per-hit multiplier on damage.
        public const float DamageFactor = 2f;
        public const float ExplosionFactor = 2f;
        public const float SuppressionModeFactor = 1.5f;
        // Combat Extended's fading: starts 30 ticks after the last hit, 4 per tick.
        public const int DecayDelayTicks = 30;
        public const float DecayPerSecond = 240f;
        // Weight of overall sharp armour against raw armour penetration; Combat Extended's 0.5 rescaled for vanilla's units.
        public const float ArmorWeight = 0.1f;
        public const int ArmorCacheTicks = 60;
        public const float MinAccuracy = 0.5f;
        public const float SuppressedAccuracy = 0.85f;
        public const float SuppressedAimTime = 1.15f;
        public const float MaxAimTime = 1.5f;
        // Suppression (Continued)'s prone-stage move speed factor.
        public const float CrawlSpeedFactor = 0.65f;

        private static readonly HashSet<Pawn> HitThisImpact = new HashSet<Pawn>();
        private static readonly Dictionary<int, KeyValuePair<int, float>> ArmorCache = new Dictionary<int, KeyValuePair<int, float>>();

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

        /// <summary>Combat Extended's armour reduction: 1 - clamp(armour * weight / penetration); no penetration means no suppression.</summary>
        public static float ArmorFactor(float overallSharpArmor, float rawPenetration)
        {
            if (rawPenetration <= 0f)
            {
                return 0f;
            }
            return 1f - Mathf.Clamp01(overallSharpArmor * ArmorWeight / rawPenetration);
        }

        /// <summary>Overall sharp armour as the Gear tab shows it (0 to 2), cached briefly per pawn.</summary>
        public static float OverallSharpArmor(Pawn pawn)
        {
            var now = Find.TickManager.TicksGame;
            if (ArmorCache.TryGetValue(pawn.thingIDNumber, out var cached) && now - cached.Key < ArmorCacheTicks)
            {
                return cached.Value;
            }
            var value = ComputeOverallSharpArmor(pawn);
            if (ArmorCache.Count > 1024)
            {
                ArmorCache.Clear();
            }
            ArmorCache[pawn.thingIDNumber] = new KeyValuePair<int, float>(now, value);
            return value;
        }

        // Matches ITab_Pawn_Gear.TryDrawOverallArmor.
        private static float ComputeOverallSharpArmor(Pawn pawn)
        {
            var stat = StatDefOf.ArmorRating_Sharp;
            var natural = Mathf.Clamp01(pawn.GetStatValue(stat) / 2f);
            var parts = pawn.RaceProps.body.AllParts;
            var worn = pawn.apparel?.WornApparel;
            var total = 0f;
            for (var i = 0; i < parts.Count; i++)
            {
                var unprotected = 1f - natural;
                if (worn != null)
                {
                    for (var j = 0; j < worn.Count; j++)
                    {
                        if (worn[j].def.apparel.CoversBodyPart(parts[i]))
                        {
                            unprotected *= 1f - Mathf.Clamp01(worn[j].GetStatValue(stat) / 2f);
                        }
                    }
                }
                total += parts[i].coverageAbs * (1f - unprotected);
            }
            return Mathf.Clamp(total * 2f, 0f, 2f);
        }

        /// <summary>The projectile's armour penetration before this mod's penetrationScale.</summary>
        public static float RawPenetration(Projectile projectile) => RawPenetration(projectile.ArmorPenetration);

        /// <summary>Armour penetration as shown on the weapon, with penetrationScale removed.</summary>
        public static float RawPenetration(float shown)
        {
            var settings = VCOMod.Settings;
            if (settings != null && settings.enableAdvancedArmor && settings.penetrationScale > 0f)
            {
                return shown / settings.penetrationScale;
            }
            return shown;
        }

        public static CompSuppression CompFor(Pawn pawn) => pawn?.TryGetComp<CompSuppression>();

        /// <summary>A standing, conscious humanlike that is pinned with pinning switched on; it crawls whenever it moves.</summary>
        public static bool CrawlsWhenMoving(Pawn pawn)
        {
            var settings = VCOMod.Settings;
            if (settings == null || !settings.enableSuppression || !settings.enableSuppressionPinning)
            {
                return false;
            }
            if (!pawn.RaceProps.Humanlike || pawn.Downed || pawn.GetPosture() != PawnPosture.Standing)
            {
                return false;
            }
            return CompFor(pawn)?.Pinned == true && pawn.health.CanCrawl;
        }

        /// <summary>Crawling right now: pinned and on the move. Vanilla ends jobs of crawlers that cannot crawl, so CanCrawl is required.</summary>
        public static bool IsCrawling(Pawn pawn) =>
            pawn.Spawned && pawn.pather != null && pawn.pather.Moving && CrawlsWhenMoving(pawn);

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

            var baseDamage = (float)projectile.DamageAmount;
            if (baseDamage <= 0f)
            {
                return;
            }
            baseDamage *= DamageFactor * settings.suppressionStrength * FireModeFactor(launcher);
            var damage = baseDamage;
            var radius = BaseRadius;
            if (explosion)
            {
                damage *= ExplosionFactor;
                radius = Mathf.Max(radius, props.explosionRadius + ExplosionRadiusBonus);
            }
            // Armour only resists bullets, as in Combat Extended.
            var penetration = RawPenetration(projectile);

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
                    if (!explosion)
                    {
                        amount *= ArmorFactor(OverallSharpArmor(pawn), penetration);
                    }
                    if (amount > 0f)
                    {
                        CompFor(pawn)?.AddSuppression(amount, source);
                    }
                }
            }
            if (!props.flyOverhead)
            {
                ApplyFlyby(map, ProjectileAccess.Origin(projectile), position, baseDamage, penetration, launcher);
            }
            HitThisImpact.Clear();
        }

        /// <summary>
        /// Suppresses pawns the projectile passed within FlybyRadius of, as Combat Extended does, except
        /// pawns near the shooter or already suppressed by the impact.
        /// </summary>
        private static void ApplyFlyby(Map map, Vector3 origin, Vector3 end, float damage, float penetration, Thing launcher)
        {
            var path = (end - origin).Yto0();
            var lengthSquared = path.sqrMagnitude;
            if (lengthSquared <= MuzzleExclusionRadius * MuzzleExclusionRadius)
            {
                return;
            }
            var minX = Mathf.Min(origin.x, end.x) - FlybyRadius;
            var maxX = Mathf.Max(origin.x, end.x) + FlybyRadius;
            var minZ = Mathf.Min(origin.z, end.z) - FlybyRadius;
            var maxZ = Mathf.Max(origin.z, end.z) + FlybyRadius;
            var source = launcher.Position;
            var pawns = map.mapPawns.AllPawnsSpawned;
            for (var i = 0; i < pawns.Count; i++)
            {
                var pawn = pawns[i];
                var pos = pawn.DrawPos;
                if (pos.x < minX || pos.x > maxX || pos.z < minZ || pos.z > maxZ || HitThisImpact.Contains(pawn))
                {
                    continue;
                }
                if ((pos - origin).MagnitudeHorizontalSquared() <= MuzzleExclusionRadius * MuzzleExclusionRadius)
                {
                    continue;
                }
                var distance = DistanceToSegment(pos, origin, path, lengthSquared);
                if (distance > FlybyRadius || !CanBeSuppressedBy(pawn, launcher))
                {
                    continue;
                }
                var amount = damage * Falloff(distance, FlybyRadius)
                             * pawn.GetStatValue(VCO_StatDefOf.VCO_Suppressability, true, 60)
                             * ArmorFactor(OverallSharpArmor(pawn), penetration);
                if (amount > 0f)
                {
                    VCODiagnostics.CountFor(pawn, "suppression.flyby");
                    CompFor(pawn)?.AddSuppression(amount, source);
                }
            }
        }

        /// <summary>Horizontal distance from a point to the segment starting at origin along path.</summary>
        public static float DistanceToSegment(Vector3 point, Vector3 origin, Vector3 path, float lengthSquared)
        {
            var offset = (point - origin).Yto0();
            var t = lengthSquared > 0f ? Mathf.Clamp01(Vector3.Dot(offset, path) / lengthSquared) : 0f;
            return (offset - path * t).MagnitudeHorizontal();
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
