using System.Collections.Generic;
using Verse;

namespace VanillaCombatOverhaul
{
    public enum SettingsTab
    {
        Combat,
        Equipment
    }

    /// <summary>Mod settings. Values without a settings control keep their defaults unless edited in the settings file.</summary>
    public class VCOSettings : ModSettings
    {
        // Default for features covered by the test suite.
        private const bool Shipped = true;

        public SettingsTab CurrentTab = SettingsTab.Combat;

        // ---- Melee -------------------------------------------------------------
        public bool enableParry = Shipped;
        // Exponent divisors in the parry formula: higher is easier to parry.
        public float parryFrontFactor = 1.5f;
        public float parrySideFactor = 1.25f;
        // Parries allowed per window before further attacks get through.
        public int parryBudgetPerWindow = 2;
        public int parryWindowTicks = 60;
        public bool enableCounterAttack = Shipped;
        public bool enablePointBlank = Shipped;

        // ---- Damage ------------------------------------------------------------
        public bool enableDirectionalDamage = Shipped;
        public bool enableMeleeFlanking = Shipped;
        public bool enableHeightTargeting = Shipped;

        // ---- Projectile wounds ------------------------------------------------
        public bool enableBulletWorker = Shipped;
        public float bulletStoppingPowerCap = 10f;
        public bool enableArrowWorker = Shipped;

        // ---- Ranged ------------------------------------------------------------
        public bool enableAdvancedAccuracy = Shipped;
        public float accuracyScale = 5f;
        public bool enableEvasion = Shipped;
        // Hit chance factor per speed unit above evasionMinSpeed; lower means more evasion.
        public float evasionFactor = 0.8f;
        public float evasionMinSpeed = 2.5f;
        public bool evasionSkillContest = true;
        public bool enableFiringArc = Shipped;
        public float firingArcDegrees = 45f;
        // Miss-spread distribution, 0 to 5 (see FiringArcUtility.RadiusFor).
        public int firingArcType = 0;
        public bool enableVisibleTracers = Shipped;
        // Scales tracer length and width together.
        public float tracerScale = 1f;

        // ---- Fire modes --------------------------------------------------------
        public bool enableFireModes = Shipped;
        // Non-player pawns always choose by distance; this turns that off.
        public bool fireModesForNpcs = true;
        // Auto: Suppression below the short burst range, Short Burst up to the precision range, Precision beyond.
        public float fireModeShortBurstRange = 12f;
        public float fireModePrecisionRange = 25f;
        public FireModeTuning precisionTuning = FireModeTuning.PrecisionDefaults();
        public FireModeTuning shortBurstTuning = FireModeTuning.ShortBurstDefaults();
        public FireModeTuning suppressionTuning = FireModeTuning.SuppressionDefaults();

        // ---- Suppression -------------------------------------------------------
        public bool enableSuppression = Shipped;
        public float suppressionStrength = 1f;
        // Non-player pawns take cover when pinned; player pawns only take the penalties.
        public bool enableSuppressionPinning = Shipped;

        // ---- Armor -------------------------------------------------------------
        // armorScale 2 always blocks at 100% leftover armour; penetrationScale multiplies weapon AP.
        public bool enableAdvancedArmor = Shipped;
        public float armorScale = 2f;
        public float penetrationScale = 2f;

        // ---- Loadouts ----------------------------------------------------------
        // Saved under its original name so existing settings files keep their choice.
        public bool enableAutoEquip = Shipped;
        // A replacement must score this many times the allowed current weapon.
        public float autoEquipUpgradeThreshold = 1.10f;

        // ---- Apparel coverage -------------------------------------------------
        public bool enableHandFeetPatch = Shipped;
        public bool enableAcidHeatPatch = Shipped;
        public bool enableThumpBluntPatch = Shipped;
        public bool enableGlassesHelmetPatch = Shipped;
        public bool enableNoseMouthPatch = Shipped;
        public bool enableMaskPatch = Shipped;
        public bool enableHeadsetPatch = Shipped;
        public bool enableArrayHeadsetPatch = Shipped;
        public bool enableApparelTweaks = Shipped;

        // ---- Internal diagnostics (not shown in player settings) ---------------
        public bool verboseLogging = false;
        // Ticks between diagnostic dumps to the log; 0 turns the periodic dump off.
        public int diagnosticDumpIntervalTicks = 2500;

        public override void ExposeData()
        {
            base.ExposeData();

            Scribe_Values.Look(ref enableParry, nameof(enableParry), Shipped);
            Scribe_Values.Look(ref parryFrontFactor, nameof(parryFrontFactor), 1.5f);
            Scribe_Values.Look(ref parrySideFactor, nameof(parrySideFactor), 1.25f);
            Scribe_Values.Look(ref parryBudgetPerWindow, nameof(parryBudgetPerWindow), 2);
            Scribe_Values.Look(ref parryWindowTicks, nameof(parryWindowTicks), 60);
            Scribe_Values.Look(ref enableCounterAttack, nameof(enableCounterAttack), Shipped);
            Scribe_Values.Look(ref enablePointBlank, nameof(enablePointBlank), Shipped);

            Scribe_Values.Look(ref enableDirectionalDamage, nameof(enableDirectionalDamage), Shipped);
            Scribe_Values.Look(ref enableMeleeFlanking, nameof(enableMeleeFlanking), Shipped);
            Scribe_Values.Look(ref enableHeightTargeting, nameof(enableHeightTargeting), Shipped);

            Scribe_Values.Look(ref enableBulletWorker, nameof(enableBulletWorker), Shipped);
            Scribe_Values.Look(ref bulletStoppingPowerCap, nameof(bulletStoppingPowerCap), 10f);
            Scribe_Values.Look(ref enableArrowWorker, nameof(enableArrowWorker), Shipped);

            Scribe_Values.Look(ref enableAdvancedAccuracy, nameof(enableAdvancedAccuracy), Shipped);
            Scribe_Values.Look(ref accuracyScale, nameof(accuracyScale), 5f);
            Scribe_Values.Look(ref enableEvasion, nameof(enableEvasion), Shipped);
            Scribe_Values.Look(ref evasionFactor, nameof(evasionFactor), 0.8f);
            Scribe_Values.Look(ref evasionMinSpeed, nameof(evasionMinSpeed), 2.5f);
            Scribe_Values.Look(ref evasionSkillContest, nameof(evasionSkillContest), true);
            Scribe_Values.Look(ref enableFiringArc, nameof(enableFiringArc), Shipped);
            Scribe_Values.Look(ref firingArcDegrees, nameof(firingArcDegrees), 45f);
            Scribe_Values.Look(ref firingArcType, nameof(firingArcType), 0);
            Scribe_Values.Look(ref enableVisibleTracers, nameof(enableVisibleTracers), Shipped);
            Scribe_Values.Look(ref tracerScale, nameof(tracerScale), 1f);

            Scribe_Values.Look(ref enableFireModes, nameof(enableFireModes), Shipped);
            Scribe_Values.Look(ref fireModesForNpcs, nameof(fireModesForNpcs), true);
            Scribe_Values.Look(ref fireModeShortBurstRange, nameof(fireModeShortBurstRange), 12f);
            Scribe_Values.Look(ref fireModePrecisionRange, nameof(fireModePrecisionRange), 25f);
            Scribe_Deep.Look(ref precisionTuning, nameof(precisionTuning));
            Scribe_Deep.Look(ref shortBurstTuning, nameof(shortBurstTuning));
            Scribe_Deep.Look(ref suppressionTuning, nameof(suppressionTuning));
            if (Scribe.mode != LoadSaveMode.Saving)
            {
                // A settings file from before fire modes has no tuning nodes, which loads as null.
                precisionTuning ??= FireModeTuning.PrecisionDefaults();
                shortBurstTuning ??= FireModeTuning.ShortBurstDefaults();
                suppressionTuning ??= FireModeTuning.SuppressionDefaults();
            }

            Scribe_Values.Look(ref enableSuppression, nameof(enableSuppression), Shipped);
            Scribe_Values.Look(ref suppressionStrength, nameof(suppressionStrength), 1f);
            Scribe_Values.Look(ref enableSuppressionPinning, nameof(enableSuppressionPinning), Shipped);

            Scribe_Values.Look(ref enableAdvancedArmor, nameof(enableAdvancedArmor), Shipped);
            Scribe_Values.Look(ref armorScale, nameof(armorScale), 2f);
            Scribe_Values.Look(ref penetrationScale, nameof(penetrationScale), 2f);

            Scribe_Values.Look(ref enableAutoEquip, nameof(enableAutoEquip), Shipped);
            Scribe_Values.Look(ref autoEquipUpgradeThreshold, nameof(autoEquipUpgradeThreshold), 1.10f);

            Scribe_Values.Look(ref enableHandFeetPatch, nameof(enableHandFeetPatch), Shipped);
            Scribe_Values.Look(ref enableAcidHeatPatch, nameof(enableAcidHeatPatch), Shipped);
            Scribe_Values.Look(ref enableThumpBluntPatch, nameof(enableThumpBluntPatch), Shipped);
            Scribe_Values.Look(ref enableGlassesHelmetPatch, nameof(enableGlassesHelmetPatch), Shipped);
            Scribe_Values.Look(ref enableNoseMouthPatch, nameof(enableNoseMouthPatch), Shipped);
            Scribe_Values.Look(ref enableMaskPatch, nameof(enableMaskPatch), Shipped);
            Scribe_Values.Look(ref enableHeadsetPatch, nameof(enableHeadsetPatch), Shipped);
            Scribe_Values.Look(ref enableArrayHeadsetPatch, nameof(enableArrayHeadsetPatch), Shipped);
            Scribe_Values.Look(ref enableApparelTweaks, nameof(enableApparelTweaks), Shipped);

            Scribe_Values.Look(ref verboseLogging, nameof(verboseLogging), false);
            Scribe_Values.Look(ref diagnosticDumpIntervalTicks, nameof(diagnosticDumpIntervalTicks), 2500);

        }

        public FireModeTuning TuningFor(FireMode mode)
        {
            switch (mode)
            {
                case FireMode.Precision:
                    return precisionTuning;
                case FireMode.ShortBurst:
                    return shortBurstTuning;
                case FireMode.Suppression:
                    return suppressionTuning;
                default:
                    return null;
            }
        }

        public void ResetFireModes()
        {
            fireModesForNpcs = true;
            fireModeShortBurstRange = 12f;
            fireModePrecisionRange = 25f;
            precisionTuning = FireModeTuning.PrecisionDefaults();
            shortBurstTuning = FireModeTuning.ShortBurstDefaults();
            suppressionTuning = FireModeTuning.SuppressionDefaults();
        }

        /// <summary>Flags read by XML patches at def load; changing them needs a restart.</summary>
        public IEnumerable<string> ActiveXmlFlags()
        {
            if (enableDirectionalDamage)
            {
                yield return "DirectionalDamage";
            }
            if (enableAutoEquip)
            {
                yield return "Loadouts";
            }
            if (enableHandFeetPatch)
            {
                yield return "HandFeetPatch";
            }
            if (enableAcidHeatPatch)
            {
                yield return "AcidHeatPatch";
            }
            if (enableThumpBluntPatch)
            {
                yield return "ThumpBluntPatch";
            }
            if (enableGlassesHelmetPatch)
            {
                yield return "GlassesHelmetPatch";
            }
            if (enableNoseMouthPatch)
            {
                yield return "NoseMouthPatch";
            }
            if (enableMaskPatch)
            {
                yield return "MaskPatch";
            }
            if (enableHeadsetPatch)
            {
                yield return "HeadsetPatch";
            }
            if (enableArrayHeadsetPatch)
            {
                yield return "ArrayHeadsetPatch";
            }
        }
    }
}
