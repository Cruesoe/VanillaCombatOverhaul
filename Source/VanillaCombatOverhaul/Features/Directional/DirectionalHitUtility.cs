using System;
using System.Collections.Generic;
using RimWorld;
using Verse;

namespace VanillaCombatOverhaul
{
    /// <summary>
    /// Chooses which body part an attack lands on based on the side it arrives from and,
    /// optionally, the attacker's targeted height.
    /// </summary>
    public static class DirectionalHitUtility
    {
        // Built once at startup; see VCODiagnostics.KeyTable for why this is not concatenated
        // at the call site. This runs on every damage instance in the game.
        private static readonly string[] FacingKeys =
            VCODiagnostics.KeyTable<AttackFacing>("directional.facing.");

        // Scratch for the weighted pick below. Never read outside the call that fills it --
        // a reused buffer, not shared context, so this does not breach the no-static-state
        // rule that CombatContext exists to enforce.
        [ThreadStatic] private static List<BodyPartRecord> pickParts;
        [ThreadStatic] private static List<float> pickWeights;

        /// <summary>
        /// Returns a replacement hit part, or null to keep whatever vanilla chose.
        ///
        /// Null only ever means "no opinion". Vanilla Combat Reloaded could return null from a
        /// success path when no part existed at the requested height; the caller then
        /// dereferenced it. If the targeted height has no coverage we fall back to any height
        /// on that side, and if that also fails we leave vanilla's result alone.
        /// </summary>
        public static BodyPartRecord TryPickDirectional(DamageInfo dinfo, Pawn pawn)
        {
            var settings = VCOMod.Settings;
            if (settings == null)
            {
                return null;
            }

            var melee = IsMeleeDamage(dinfo);
            var sideActive = settings.enableDirectionalDamage && (!melee || settings.enableMeleeFlanking);
            var heightActive = settings.enableHeightTargeting;
            if (!sideActive && !heightActive)
            {
                return null;
            }
            if (melee && !settings.enableMeleeFlanking && !heightActive)
            {
                VCODiagnostics.CountFor(pawn, "directional.skip.meleeDisabled");
                return null;
            }
            if (pawn?.health?.hediffSet == null)
            {
                return null;
            }

            VCODiagnostics.CountFor(pawn, "directional.considered");
            VCODiagnostics.Count(melee ? "directional.source.melee" : "directional.source.ranged");

            BodyPartGroupDef group = null;
            if (sideActive)
            {
                var facing = FacingFor(dinfo, pawn);
                VCODiagnostics.CountFor(pawn, FacingKeys[(int)facing]);
                group = GroupFor(facing);
                if (group == null)
                {
                    VCODiagnostics.CountFor(pawn, "directional.keep.frontal");
                }
            }

            var height = BodyPartHeight.Undefined;
            if (heightActive)
            {
                height = HeightTargetingUtility.GetTargetHeight(dinfo.Instigator);
                if (height != BodyPartHeight.Undefined)
                {
                    var land = HeightTargetingUtility.ChanceToLand(
                        dinfo.Instigator, pawn, group, height, dinfo.Def, melee);
                    if (!Rand.Chance(land))
                    {
                        VCODiagnostics.CountFor(pawn, "directional.height.missedBand");
                        height = BodyPartHeight.Undefined;
                    }
                    else
                    {
                        VCODiagnostics.CountFor(pawn, "directional.height.applied");
                    }
                }
            }

            if (group == null && height == BodyPartHeight.Undefined)
            {
                return null;
            }

            if (TryPick(pawn, dinfo, group, height, out var result))
            {
                VCODiagnostics.CountFor(pawn, "directional.replaced");
                return result;
            }

            if (height != BodyPartHeight.Undefined
                && TryPick(pawn, dinfo, group, BodyPartHeight.Undefined, out result))
            {
                VCODiagnostics.CountFor(pawn, "directional.height.fallback");
                return result;
            }

            VCODiagnostics.CountFor(pawn, "directional.keep.noPartsOnSide");
            return null;
        }

        /// <summary>
        /// Weighted choice of a hit part on the given side and height.
        ///
        /// Was a LINQ Where feeding TryRandomElementByWeight. That takes an IEnumerable and
        /// ships no IList overload, so it cannot know the total weight without consuming the
        /// sequence: the predicate ran over the whole body more than once per damage instance,
        /// allocating two display classes and two iterators to do it. RimWorld still owns the
        /// not-missing, height and depth filtering; only the selection is done by hand.
        /// </summary>
        public static bool TryPick(Pawn pawn, DamageInfo dinfo, BodyPartGroupDef group,
                                   BodyPartHeight height, out BodyPartRecord result)
        {
            var parts = pickParts ?? (pickParts = new List<BodyPartRecord>(96));
            var weights = pickWeights ?? (pickWeights = new List<float>(96));
            parts.Clear();
            weights.Clear();

            var total = 0f;
            foreach (var part in pawn.health.hediffSet.GetNotMissingParts(height, dinfo.Depth))
            {
                if (group != null && (part.groups == null || !part.groups.Contains(group)))
                {
                    continue;
                }
                var weight = part.coverageAbs * part.def.GetHitChanceFactorFor(dinfo.Def);
                if (weight <= 0f)
                {
                    continue;   // A zero-weight part could never be selected anyway.
                }
                parts.Add(part);
                weights.Add(weight);
                total += weight;
            }

            if (total <= 0f)
            {
                result = null;
                return false;
            }

            var roll = Rand.Value * total;
            for (var i = 0; i < parts.Count; i++)
            {
                if (roll < weights[i])
                {
                    result = parts[i];
                    return true;
                }
                roll -= weights[i];
            }

            result = parts[parts.Count - 1];   // Float drift only; the roll is below the total.
            return true;
        }

        public static AttackFacing FacingFor(DamageInfo dinfo, Pawn pawn)
        {
            var instigator = dinfo.Instigator;
            if (instigator != null && instigator.Spawned && instigator.Map == pawn.Map)
            {
                return FacingUtility.Relative(instigator.Position, pawn);
            }
            return FacingUtility.FromTravelAngle(dinfo.Angle, pawn);
        }

        public static BodyPartGroupDef GroupFor(AttackFacing facing)
        {
            switch (facing)
            {
                case AttackFacing.Left:
                    return VCO_BodyPartGroupDefOf.VCO_Left;
                case AttackFacing.Right:
                    return VCO_BodyPartGroupDefOf.VCO_Right;
                case AttackFacing.Rear:
                    return VCO_BodyPartGroupDefOf.VCO_Center;
                default:
                    return null;
            }
        }

        private static bool IsMeleeDamage(DamageInfo dinfo) =>
            dinfo.Tool != null || dinfo.Weapon == null || dinfo.Weapon.IsMeleeWeapon;
    }
}
