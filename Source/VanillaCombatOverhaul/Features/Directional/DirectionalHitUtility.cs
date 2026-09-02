using System.Linq;
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
                VCODiagnostics.CountFor(pawn, "directional.facing." + facing);
                group = GroupFor(facing);
                if (group == null)
                {
                    VCODiagnostics.CountFor(pawn, "directional.keep.frontal");
                }
            }

            var height = BodyPartHeight.Undefined;
            if (heightActive)
            {
                height = HeightTargeting.GetTargetHeight(dinfo.Instigator);
                if (height != BodyPartHeight.Undefined)
                {
                    var land = HeightTargeting.ChanceToLand(
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

        public static bool TryPick(Pawn pawn, DamageInfo dinfo, BodyPartGroupDef group,
                                   BodyPartHeight height, out BodyPartRecord result)
        {
            var candidates = pawn.health.hediffSet
                .GetNotMissingParts(height, dinfo.Depth)
                .Where(p => group == null || (p.groups != null && p.groups.Contains(group)));

            return candidates.TryRandomElementByWeight(
                p => p.coverageAbs * p.def.GetHitChanceFactorFor(dinfo.Def),
                out result);
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
