using System.Collections.Generic;
using RimWorld;
using UnityEngine;
using Verse;

namespace VanillaCombatOverhaul
{
    [StaticConstructorOnStartup]
    public static class FireModeUtility
    {
        public static readonly FireMode[] Modes =
        {
            FireMode.Default,
            FireMode.Precision,
            FireMode.ShortBurst,
            FireMode.Suppression
        };

        // Vanilla's floor in ShotReport.HitFactorFromShooter; a mode never goes below it.
        public const float MinShooterFactor = 0.0201f;

        public static readonly Texture2D IconAuto =
            ContentFinder<Texture2D>.Get("UI/Commands/VCO_FireAuto");
        private static readonly Texture2D IconDefault =
            ContentFinder<Texture2D>.Get("UI/Commands/VCO_FireDefault");
        private static readonly Texture2D IconPrecision =
            ContentFinder<Texture2D>.Get("UI/Commands/VCO_FirePrecision");
        private static readonly Texture2D IconShortBurst =
            ContentFinder<Texture2D>.Get("UI/Commands/VCO_FireShortBurst");
        private static readonly Texture2D IconSuppression =
            ContentFinder<Texture2D>.Get("UI/Commands/VCO_FireSuppression");

        static FireModeUtility()
        {
            foreach (var def in DefDatabase<ThingDef>.AllDefs)
            {
                if (def.race == null || (!def.race.Humanlike && !def.race.ToolUser))
                {
                    continue;
                }
                if (def.HasComp(typeof(CompFireMode)))
                {
                    continue;
                }
                if (def.comps == null)
                {
                    def.comps = new List<CompProperties>();
                }
                def.comps.Add(new CompProperties_FireMode());
            }
        }

        public static bool Enabled => VCOMod.Settings?.enableFireModes ?? false;

        public static Verb PrimaryVerb(Pawn pawn) => pawn?.equipment?.PrimaryEq?.PrimaryVerb;

        /// <summary>
        /// Whether a mode can apply to this verb at all: a ranged verb of the pawn's own
        /// primary weapon. Abilities, apparel verbs, melee, explosives and one-use launchers
        /// always fire as Default.
        /// </summary>
        public static bool IsModeVerb(Pawn pawn, Verb verb)
        {
            if (pawn == null || verb?.verbProps == null)
            {
                return false;
            }
            if (verb.verbProps.IsMeleeAttack || verb.verbProps.CausesExplosion || verb is Verb_ShootOneUse)
            {
                return false;
            }
            var primary = pawn.equipment?.Primary;
            return primary != null && verb.EquipmentSource == primary && primary.def.IsRangedWeapon;
        }

        /// <summary>
        /// Burst modes need a burst to change. Beam weapons are excluded too: their
        /// WarmupComplete does not call the base method, and they size the beam path from the
        /// shot count in more than one place.
        /// </summary>
        public static bool CanChangeBurst(Verb verb, int baseBurst) =>
            baseBurst > 1 && !(verb is Verb_ShootBeam);

        public static bool IsBurstMode(FireMode mode) =>
            mode == FireMode.ShortBurst || mode == FireMode.Suppression;

        /// <summary>Whether the menu should list this mode for the pawn's current weapon.</summary>
        public static bool Offers(Pawn pawn, FireMode mode)
        {
            var verb = PrimaryVerb(pawn);
            if (!IsModeVerb(pawn, verb))
            {
                return false;
            }
            return !IsBurstMode(mode) || CanChangeBurst(verb, verb.BurstShotCount);
        }

        /// <summary>
        /// The mode in force for this pawn firing this verb, or Default.
        ///
        /// Colonists use their mode only while drafted, so hunting and undrafted self-defence
        /// fire normally. Everyone else picks by distance when NPC fire modes are on.
        /// <paramref name="distance"/> lets auto mode answer for a target it has not started
        /// shooting at yet, which is what a hover tooltip needs.
        /// <paramref name="baseBurst"/> is passed by the burst patch, which cannot read
        /// BurstShotCount again without re-entering itself.
        /// </summary>
        public static FireMode ActiveMode(Pawn pawn, Verb verb, float? distance = null, int baseBurst = -1)
        {
            var settings = VCOMod.Settings;
            if (settings == null || !settings.enableFireModes || !IsModeVerb(pawn, verb))
            {
                return FireMode.Default;
            }
            var comp = pawn.TryGetComp<CompFireMode>();
            if (comp == null)
            {
                return FireMode.Default;
            }

            FireMode mode;
            if (pawn.Faction == Faction.OfPlayer)
            {
                if (!pawn.Drafted)
                {
                    return FireMode.Default;
                }
                mode = comp.AutoSelect ? AutoMode(comp, verb, distance) : comp.Mode;
            }
            else
            {
                if (!settings.fireModesForNpcs)
                {
                    return FireMode.Default;
                }
                mode = AutoMode(comp, verb, distance);
            }

            if (IsBurstMode(mode) && !CanChangeBurst(verb, baseBurst >= 0 ? baseBurst : verb.BurstShotCount))
            {
                return FireMode.Default;
            }
            return mode;
        }

        // A burst in progress keeps the mode it started with, even if the target moves.
        private static FireMode AutoMode(CompFireMode comp, Verb verb, float? distance) =>
            distance.HasValue && !verb.Bursting ? ModeForDistance(distance.Value) : comp.AutoMode;

        public static FireMode ModeForDistance(float distance)
        {
            var settings = VCOMod.Settings;
            if (settings == null)
            {
                return FireMode.Default;
            }
            if (distance >= settings.fireModePrecisionRange)
            {
                return FireMode.Precision;
            }
            return distance >= settings.fireModeShortBurstRange ? FireMode.ShortBurst : FireMode.Suppression;
        }

        /// <summary>Records the distance-picked mode as a cast starts.</summary>
        public static void NoteTarget(Pawn pawn, LocalTargetInfo target)
        {
            if (!target.IsValid)
            {
                return;
            }
            var comp = pawn.TryGetComp<CompFireMode>();
            comp?.NoteAutoMode(ModeForDistance(pawn.Position.DistanceTo(target.Cell)));
        }

        public static FireModeTuning TuningFor(FireMode mode) => VCOMod.Settings?.TuningFor(mode);

        /// <summary>
        /// hit^(1/accuracy): shooting as if from distance/accuracy. Accuracy above 1 raises the
        /// factor and below 1 lowers it; certainty stays certain and vanilla's floor holds.
        /// </summary>
        public static float AdjustHitFactor(float factor, float accuracy)
        {
            if (accuracy <= 0f || Mathf.Approximately(accuracy, 1f) || factor >= 1f)
            {
                return factor;
            }
            return Mathf.Max(Mathf.Pow(Mathf.Clamp01(factor), 1f / accuracy), MinShooterFactor);
        }

        /// <summary>
        /// Scales a burst, rounding half up, then limits the change to maxChange shots either
        /// way. Single shots are left alone.
        /// </summary>
        public static int AdjustBurst(int baseBurst, float factor, int maxChange)
        {
            if (baseBurst <= 1)
            {
                return baseBurst;
            }
            var scaled = Mathf.FloorToInt(baseBurst * factor + 0.5f);
            var limit = Mathf.Max(0, maxChange);
            scaled = Mathf.Clamp(scaled, baseBurst - limit, baseBurst + limit);
            return Mathf.Max(1, scaled);
        }

        public static int BurstShotCountFor(Verb verb, int baseBurst)
        {
            var pawn = verb.CasterPawn;
            var mode = ActiveMode(pawn, verb, baseBurst: baseBurst);
            var tuning = mode == FireMode.Default ? null : TuningFor(mode);
            if (tuning == null)
            {
                return baseBurst;
            }
            var adjusted = AdjustBurst(baseBurst, tuning.burstFactor, tuning.burstMaxChange);
            if (adjusted != baseBurst)
            {
                VCODiagnostics.Count("fireMode.burst.adjusted");
            }
            return adjusted;
        }

        /// <summary>Applies the shooter's mode to a freshly built shot report.</summary>
        public static void ApplyToShotReport(ref ShotReport report, Thing caster, Verb verb)
        {
            if (!(caster is Pawn pawn) || !Enabled)
            {
                return;
            }
            var mode = ActiveMode(pawn, verb, ShotReportAccess.GetDistance(ref report));
            var tuning = mode == FireMode.Default ? null : TuningFor(mode);
            if (tuning == null)
            {
                return;
            }
            var factor = ShotReportAccess.GetShooterFactor(ref report);
            var adjusted = AdjustHitFactor(factor, tuning.accuracy);
            if (!Mathf.Approximately(factor, adjusted))
            {
                VCODiagnostics.Count("fireMode.hit.adjusted");
                ShotReportAccess.SetShooterFactor(ref report, adjusted);
            }
        }

        public static string LabelFor(FireMode mode)
        {
            switch (mode)
            {
                case FireMode.Precision:
                    return "VCO_FireMode_Precision".Translate();
                case FireMode.ShortBurst:
                    return "VCO_FireMode_ShortBurst".Translate();
                case FireMode.Suppression:
                    return "VCO_FireMode_Suppression".Translate();
                default:
                    return "VCO_FireMode_Default".Translate();
            }
        }

        public static Texture2D IconFor(FireMode mode)
        {
            switch (mode)
            {
                case FireMode.Precision:
                    return IconPrecision;
                case FireMode.ShortBurst:
                    return IconShortBurst;
                case FireMode.Suppression:
                    return IconSuppression;
                default:
                    return IconDefault;
            }
        }

        public static Command CommandFor(CompFireMode comp)
        {
            var label = comp.AutoSelect
                ? "VCO_FireMode_Auto".Translate().ToString()
                : LabelFor(comp.Mode);
            return new Command_SetFireMode
            {
                icon = comp.AutoSelect ? IconAuto : IconFor(comp.Mode),
                defaultLabel = "VCO_CommandSetFireMode".Translate(label),
                defaultDesc = "VCO_CommandSetFireMode_Tip".Translate(),
                comp = comp
            };
        }
    }
}
