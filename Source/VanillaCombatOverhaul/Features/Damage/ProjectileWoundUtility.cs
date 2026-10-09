using System.Collections.Generic;
using RimWorld;
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
    /// Vanilla Combat Reloaded's bullet and arrow wounds, applied by intercepting
    /// <see cref="DamageWorker_AddInjury"/>. Armour that reduced the hit cancels them.
    /// </summary>
    public static class ProjectileWoundUtility
    {
        /// <summary>Reloaded's cut-extra-targets curve, used when stopping power is below 1.</summary>
        public static readonly SimpleCurve FragmentTargets = new SimpleCurve
        {
            new CurvePoint(0f, 0f),
            new CurvePoint(0.6f, 1f),
            new CurvePoint(0.9f, 2f),
            new CurvePoint(1f, 3f)
        };

        public const float ArrowScratchSplit = 0.67f;
        public const float PassThroughCeiling = 1.5f;

        private static readonly Dictionary<ThingDef, float> WeaponStoppingPower = new Dictionary<ThingDef, float>();
        private static DamageDef arrowDef;
        private static DamageDef rangedStabDef;
        private static bool arrowDefsResolved;

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
        /// Stopping power of the round that produced this <see cref="DamageInfo"/>, cached per weapon.
        /// Multi-projectile weapons only expose the first verb's projectile.
        /// </summary>
        public static float StoppingPowerFor(DamageInfo dinfo)
        {
            var weapon = dinfo.Weapon;
            if (weapon != null)
            {
                if (!WeaponStoppingPower.TryGetValue(weapon, out var cached))
                {
                    var verbs = weapon.Verbs;
                    cached = verbs != null && verbs.Count > 0
                        ? verbs[0].defaultProjectile?.projectile?.stoppingPower ?? 0f
                        : 0f;
                    WeaponStoppingPower[weapon] = cached;
                }
                if (cached > 0f)
                {
                    return cached;
                }
            }
            return dinfo.Def?.defaultStoppingPower ?? 0f;
        }

        public static bool IsBulletDef(DamageDef def) => def != null && def == DamageDefOf.Bullet;

        public static bool IsArrowDef(DamageDef def)
        {
            if (def == null)
            {
                return false;
            }
            if (!arrowDefsResolved)
            {
                arrowDef = DefDatabase<DamageDef>.GetNamedSilentFail("Arrow");
                rangedStabDef = DefDatabase<DamageDef>.GetNamedSilentFail("RangedStab");
                arrowDefsResolved = true;
            }
            return def == arrowDef || def == rangedStabDef;
        }

        /// <summary>Adds the hit part's children, parent and siblings, excluding the hit part and conceptual parts.</summary>
        public static void AddNearbyParts(BodyPartRecord hit, List<BodyPartRecord> into)
        {
            AddChildren(hit, hit, into);
            var parent = hit.parent;
            if (parent == null)
            {
                return;
            }
            if (!parent.def.conceptual)
            {
                into.Add(parent);
            }
            if (parent.parent != null)
            {
                AddChildren(parent, hit, into);
            }
        }

        private static void AddChildren(BodyPartRecord part, BodyPartRecord hit, List<BodyPartRecord> into)
        {
            var children = part.parts;
            for (var i = 0; i < children.Count; i++)
            {
                var child = children[i];
                if (child != hit && !child.def.conceptual && !into.Contains(child))
                {
                    into.Add(child);
                }
            }
        }

        /// <summary>Adds nearby outside parts the pawn still has, for an arrow's second wound.</summary>
        public static void AddNearbyExternalParts(Pawn pawn, BodyPartRecord hit, List<BodyPartRecord> into)
        {
            var start = into.Count;
            AddNearbyParts(hit, into);
            for (var i = into.Count - 1; i >= start; i--)
            {
                var part = into[i];
                if (part.depth != BodyPartDepth.Outside || pawn.health.hediffSet.PartIsMissing(part))
                {
                    into.RemoveAt(i);
                }
            }
        }

        /// <summary>Adds the hit part and its parents out to the first outside part.</summary>
        public static void AddPassThroughChain(BodyPartRecord hit, List<BodyPartRecord> into)
        {
            for (var part = hit; part != null; part = part.parent)
            {
                into.Add(part);
                if (part.depth == BodyPartDepth.Outside)
                {
                    return;
                }
            }
        }

        /// <summary>Moves <paramref name="count"/> randomly chosen entries to the front of the list.</summary>
        public static void ShuffleFront(List<BodyPartRecord> list, int count)
        {
            for (var i = 0; i < count && i < list.Count; i++)
            {
                var j = Rand.RangeInclusive(i, list.Count - 1);
                var swap = list[i];
                list[i] = list[j];
                list[j] = swap;
            }
        }
    }
}
