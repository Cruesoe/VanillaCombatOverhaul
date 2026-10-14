using System.Collections.Generic;
using System.Linq;
using RimWorld;
using UnityEngine;
using Verse;

namespace VanillaCombatOverhaul
{
    [StaticConstructorOnStartup]
    public static class HeightTargetingUtility
    {
        public const int MinimumShootingSkill = 10;

        public static readonly BodyPartHeight[] Modes =
        {
            BodyPartHeight.Undefined,
            BodyPartHeight.Bottom,
            BodyPartHeight.Middle,
            BodyPartHeight.Top
        };

        private static readonly Texture2D IconNone =
            ContentFinder<Texture2D>.Get("UI/Commands/VCO_AimNone");
        private static readonly Texture2D IconBottom =
            ContentFinder<Texture2D>.Get("UI/Commands/VCO_AimLegs");
        private static readonly Texture2D IconMiddle =
            ContentFinder<Texture2D>.Get("UI/Commands/VCO_AimTorso");
        private static readonly Texture2D IconTop =
            ContentFinder<Texture2D>.Get("UI/Commands/VCO_AimHead");

        /// <summary>The height band this attacker is aiming for, or Undefined.</summary>
        public static BodyPartHeight GetTargetHeight(Thing instigator)
        {
            var comp = instigator?.TryGetComp<CompHeightTarget>();
            if (comp == null || comp.TargetingMode == BodyPartHeight.Undefined)
            {
                return BodyPartHeight.Undefined;
            }
            return CanUse(instigator) ? comp.TargetingMode : BodyPartHeight.Undefined;
        }

        public static bool CanUse(Thing instigator)
        {
            if (instigator == null || !instigator.def.HasComp(typeof(CompHeightTarget)))
            {
                return false;
            }
            if (instigator is Pawn pawn
                && (!MeetsShootingRequirement(pawn)
                    || (pawn.CurrentEffectiveVerb?.verbProps.CausesExplosion ?? true)))
            {
                return false;
            }
            return true;
        }

        public static bool MeetsShootingRequirement(Pawn pawn)
        {
            var shooting = pawn?.skills?.GetSkill(SkillDefOf.Shooting);
            return shooting != null
                   && !shooting.TotallyDisabled
                   && shooting.Level >= MinimumShootingSkill;
        }

        public static void AssignRandom(Pawn pawn)
        {
            var comp = pawn?.TryGetComp<CompHeightTarget>();
            if (comp == null || !MeetsShootingRequirement(pawn))
            {
                comp?.SetTargetingMode(BodyPartHeight.Undefined);
                return;
            }
            comp.SetTargetingMode((BodyPartHeight)Rand.RangeInclusive(0, 3));
        }

        public static void Reset(Pawn pawn) =>
            pawn?.TryGetComp<CompHeightTarget>()?.SetTargetingMode(BodyPartHeight.Undefined);

        public static string LabelFor(BodyPartHeight height)
        {
            switch (height)
            {
                case BodyPartHeight.Top:
                    return "VCO_Height_Top".Translate();
                case BodyPartHeight.Middle:
                    return "VCO_Height_Middle".Translate();
                case BodyPartHeight.Bottom:
                    return "VCO_Height_Bottom".Translate();
                default:
                    return "VCO_Height_None".Translate();
            }
        }

        public static Texture2D IconFor(BodyPartHeight height)
        {
            switch (height)
            {
                case BodyPartHeight.Top:
                    return IconTop;
                case BodyPartHeight.Middle:
                    return IconMiddle;
                case BodyPartHeight.Bottom:
                    return IconBottom;
                default:
                    return IconNone;
            }
        }

        public static Command CommandFor(CompHeightTarget comp) =>
            new Command_SetHeightTarget
            {
                icon = IconFor(comp.TargetingMode),
                defaultLabel = "VCO_CommandSetHeight".Translate(LabelFor(comp.TargetingMode)),
                defaultDesc = "VCO_CommandSetHeight_Tip".Translate(),
                comp = comp
            };

        /// <summary>Chance the targeted height band lands, from its share of coverage on that side, raised by skill with advanced accuracy on.</summary>
        public static float ChanceToLand(Thing caster, Pawn target, BodyPartGroupDef side,
                                         BodyPartHeight height, DamageDef damage, bool melee)
        {
            if (target?.health?.hediffSet == null || height == BodyPartHeight.Undefined)
            {
                return 1f;
            }

            CoveragePair(target, side, damage, height, out var atHeight, out var anyHeight);
            if (anyHeight <= 0f)
            {
                return 0f;
            }

            var relative = atHeight / anyHeight;
            var settings = VCOMod.Settings;
            if (settings == null || !settings.enableAdvancedAccuracy || caster == null)
            {
                return Mathf.Clamp01(relative);
            }

            float skill;
            if (melee)
            {
                skill = Mathf.Max(1f, StatDefOf.MeleeHitChance.Worker.GetValue(StatRequest.For(caster), false)
                                      / settings.accuracyScale);
            }
            else
            {
                skill = PenaltyMitigationUtility.ShooterSkillFactor(caster, settings.accuracyScale);
            }

            return Mathf.Clamp01(1f - Mathf.Pow(1f - relative, skill));
        }

        /// <summary>Weighted coverage on one side at one height; the reference the tests check <see cref="CoveragePair"/> against.</summary>
        public static float Coverage(Pawn target, BodyPartGroupDef side, DamageDef damage,
                                     BodyPartHeight height)
        {
            var total = 0f;
            foreach (var part in target.health.hediffSet.GetNotMissingParts(height))
            {
                if (side != null && (part.groups == null || !part.groups.Contains(side)))
                {
                    continue;
                }
                total += part.coverageAbs * part.def.GetHitChanceFactorFor(damage);
            }
            return total;
        }

        /// <summary>Coverage at the height and at any height on one side, from a single walk of the body.</summary>
        public static void CoveragePair(Pawn target, BodyPartGroupDef side, DamageDef damage,
                                        BodyPartHeight height, out float atHeight, out float anyHeight)
        {
            atHeight = 0f;
            anyHeight = 0f;
            foreach (var part in target.health.hediffSet.GetNotMissingParts(BodyPartHeight.Undefined))
            {
                if (side != null && (part.groups == null || !part.groups.Contains(side)))
                {
                    continue;
                }
                var weight = part.coverageAbs * part.def.GetHitChanceFactorFor(damage);
                anyHeight += weight;
                if (part.height == height)
                {
                    atHeight += weight;
                }
            }
        }

    }
}
