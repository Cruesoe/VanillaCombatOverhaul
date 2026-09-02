using System.Collections.Generic;
using System.Linq;
using RimWorld;
using UnityEngine;
using Verse;

namespace VanillaCombatOverhaul
{
    public enum BulletWoundKind
    {
        Fragment,
        PassThrough,
        Mushroom
    }

    /// <summary>
    /// Vanilla Combat Reloaded's bullet and arrow damage workers, applied as a Harmony
    /// intercept on <see cref="DamageWorker_AddInjury"/> rather than by replacing the
    /// DamageDef workerClass. Gameplay is the same; any other mod can still own Bullet/Arrow.
    ///
    /// Armor that already reduced the hit (<c>result.diminished</c>) cancels the extra
    /// behaviour, matching Reloaded: a vest that catches the round also stops fragmentation,
    /// pass-through and mushrooming.
    /// </summary>
    public static class ProjectileWoundUtility
    {
        /// <summary>
        /// Reloaded's cut-extra-targets curve, used when stopping power is below 1.
        /// </summary>
        public static readonly SimpleCurve FragmentTargets = new SimpleCurve
        {
            new CurvePoint(0f, 0f),
            new CurvePoint(0.6f, 1f),
            new CurvePoint(0.9f, 2f),
            new CurvePoint(1f, 3f)
        };

        public const float ArrowScratchSplit = 0.67f;
        public const float PassThroughCeiling = 1.5f;

        public static BulletWoundKind KindFor(float stoppingPower)
        {
            if (stoppingPower < 1f)
            {
                return BulletWoundKind.Fragment;
            }
            return stoppingPower <= PassThroughCeiling
                ? BulletWoundKind.PassThrough
                : BulletWoundKind.Mushroom;
        }

        /// <summary>
        /// Stopping power of the round that produced this <see cref="DamageInfo"/>.
        /// Multi-projectile weapons only expose the first verb's projectile, same as Reloaded.
        /// </summary>
        public static float StoppingPowerFor(DamageInfo dinfo)
        {
            var fromWeapon = dinfo.Weapon?.Verbs?
                .FirstOrDefault()?.defaultProjectile?.projectile?.stoppingPower;
            if (fromWeapon.HasValue && fromWeapon.Value > 0f)
            {
                return fromWeapon.Value;
            }
            return dinfo.Def?.defaultStoppingPower ?? 0f;
        }

        public static bool IsBulletDef(DamageDef def) =>
            def != null && (def == DamageDefOf.Bullet || def.defName == "Bullet");

        public static bool IsArrowDef(DamageDef def) =>
            def != null && (def.defName == "Arrow" || def.defName == "RangedStab");

        public static IEnumerable<BodyPartRecord> NearbyExternalParts(Pawn pawn, BodyPartRecord hit)
        {
            if (pawn?.health?.hediffSet == null || hit == null)
            {
                yield break;
            }

            IEnumerable<BodyPartRecord> nearby = hit.GetDirectChildParts();
            if (hit.parent != null)
            {
                nearby = nearby.Concat(hit.parent);
                if (hit.parent.parent != null)
                {
                    nearby = nearby.Concat(hit.parent.GetDirectChildParts());
                }
            }

            foreach (var part in nearby)
            {
                if (part != hit
                    && !part.def.conceptual
                    && part.depth == BodyPartDepth.Outside
                    && !pawn.health.hediffSet.PartIsMissing(part))
                {
                    yield return part;
                }
            }
        }

        public static IEnumerable<BodyPartRecord> PassThroughChain(BodyPartRecord hit)
        {
            for (var part = hit; part != null; part = part.parent)
            {
                yield return part;
                if (part.depth == BodyPartDepth.Outside)
                {
                    yield break;
                }
            }
        }
    }
}
