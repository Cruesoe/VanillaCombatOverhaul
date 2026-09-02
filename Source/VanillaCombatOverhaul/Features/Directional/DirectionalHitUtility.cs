using System.Linq;
using RimWorld;
using Verse;

namespace VanillaCombatOverhaul
{
    /// <summary>
    /// Chooses which body part an attack lands on based on the side it arrives from, so
    /// flanking exposes different parts than a frontal attack does.
    /// </summary>
    public static class DirectionalHitUtility
    {
        /// <summary>
        /// Returns a replacement hit part, or null to keep whatever vanilla chose.
        ///
        /// Returning null on every failure path is deliberate. Vanilla Combat Reloaded's
        /// equivalent could return null from a *success* path -- if its coverage roll passed
        /// but no part existed at the requested height, it fell through to null and the arrow
        /// damage worker dereferenced it. Here null only ever means "no opinion", and the
        /// caller keeps a known-good vanilla result, so there is no path that produces one.
        /// </summary>
        public static BodyPartRecord TryPickDirectional(DamageInfo dinfo, Pawn pawn)
        {
            var settings = VCOMod.Settings;
            if (settings == null || !settings.enableDirectionalDamage)
            {
                return null;
            }
            if (IsMeleeDamage(dinfo) && !settings.enableMeleeFlanking)
            {
                VCODiagnostics.CountFor(pawn, "directional.skip.meleeDisabled");
                return null;
            }
            if (pawn?.health?.hediffSet == null)
            {
                return null;
            }

            VCODiagnostics.CountFor(pawn, "directional.considered");
            VCODiagnostics.Count(IsMeleeDamage(dinfo) ? "directional.source.melee" : "directional.source.ranged");

            var facing = FacingFor(dinfo, pawn);
            VCODiagnostics.CountFor(pawn, "directional.facing." + facing);

            var group = GroupFor(facing);
            if (group == null)
            {
                // Frontal attacks are unrestricted; everything is exposed.
                VCODiagnostics.CountFor(pawn, "directional.keep.frontal");
                return null;
            }

            // One weighted pass over the parts on the exposed side. Vanilla Combat Reloaded
            // computed a coverage ratio instead, which meant two full enumerations and a
            // division that returned NaN whenever a side had no coverage left.
            var candidates = pawn.health.hediffSet
                .GetNotMissingParts(dinfo.Height, dinfo.Depth)
                .Where(p => p.groups != null && p.groups.Contains(group));

            if (candidates.TryRandomElementByWeight(
                    p => p.coverageAbs * p.def.GetHitChanceFactorFor(dinfo.Def),
                    out var result))
            {
                VCODiagnostics.CountFor(pawn, "directional.replaced");
                return result;
            }

            // No part left on that side. Vanilla's choice stands; if this counter is ever
            // non-trivial it means the group seeder is under-covering some body shape.
            VCODiagnostics.CountFor(pawn, "directional.keep.noPartsOnSide");
            return null;
        }

        private static AttackFacing FacingFor(DamageInfo dinfo, Pawn pawn)
        {
            // The instigator's position is the more reliable signal when it is still on the
            // map; dinfo.Angle covers damage whose source has already despawned or died.
            var instigator = dinfo.Instigator;
            if (instigator != null && instigator.Spawned && instigator.Map == pawn.Map)
            {
                return FacingUtility.Relative(instigator.Position, pawn);
            }
            return FacingUtility.FromTravelAngle(dinfo.Angle, pawn);
        }

        private static BodyPartGroupDef GroupFor(AttackFacing facing)
        {
            switch (facing)
            {
                case AttackFacing.Left:
                    return VCO_BodyPartGroupDefOf.VCO_Left;
                case AttackFacing.Right:
                    return VCO_BodyPartGroupDefOf.VCO_Right;
                case AttackFacing.Rear:
                    // From behind, the midline is what presents itself.
                    return VCO_BodyPartGroupDefOf.VCO_Center;
                default:
                    return null;
            }
        }

        /// <summary>
        /// Melee and ranged are toggled separately, so they have to be told apart at the point
        /// damage resolves. A tool means a melee attack; unarmed strikes carry no weapon at all.
        /// </summary>
        private static bool IsMeleeDamage(DamageInfo dinfo) =>
            dinfo.Tool != null || dinfo.Weapon == null || dinfo.Weapon.IsMeleeWeapon;
    }
}
