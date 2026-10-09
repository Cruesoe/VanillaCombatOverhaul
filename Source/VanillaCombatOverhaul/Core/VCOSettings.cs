using System.Collections.Generic;
using Verse;

namespace VanillaCombatOverhaul
{
    public enum SettingsTab
    {
        Combat,
        Equipment
    }

    /// <summary>
    /// Defaults are set per feature, not by one global switch.
    ///
    /// A feature defaults ON only once it is built and verified by the arena suite. Work in
    /// progress is kept out of the settings screen until it is ready for players.
    ///
    /// Verbose logging is internal diagnostic scaffolding and ships off. A player's log should
    /// hold their own mod list's problems, not a per-hour dump of our counters. These values are
    /// retained for developer configuration but are not exposed in the player settings window.
    /// </summary>
    public class VCOSettings : ModSettings
    {
        // Built, verified by the arena suite, safe to ship enabled.
        private const bool Shipped = true;

        public SettingsTab CurrentTab = SettingsTab.Combat;

        // ---- Melee -------------------------------------------------------------
        public bool enableParry = Shipped;
        // Exponent divisors, not linear multipliers: higher means easier to parry.
        // Front keeps Vanilla Combat Reloaded's 1.5. Side is deliberately lower so
        // gaining a flank weakens a defender's parry without denying it outright.
        public float parryFrontFactor = 1.5f;
        public float parrySideFactor = 1.25f;
        // Measured at 2: negligible in a duel (0 rejections), light at 3v1 (2.4% of
        // attacks), and material at 6v1 (9.9%). See the balance notes in README.md.
        public int parryBudgetPerWindow = 2;
        public int parryWindowTicks = 60;
        public bool enableCounterAttack = Shipped;
        public bool enablePointBlank = Shipped;

        // ---- Damage ------------------------------------------------------------
        public bool enableDirectionalDamage = Shipped;
        public bool enableMeleeFlanking = Shipped;
        public bool enableHeightTargeting = Shipped;

        // ---- Projectile wounds (VCR damage workers, without swapping workerClass) ----
        public bool enableBulletWorker = Shipped;
        public float bulletStoppingPowerCap = 10f;
        public bool enableArrowWorker = Shipped;

        // ---- Ranged ------------------------------------------------------------
        public bool enableAdvancedAccuracy = Shipped;
        public float accuracyScale = 5f;
        public bool enableEvasion = Shipped;
        // Per speed unit above minSpeed; lower means more evasion. VCR default 0.8.
        public float evasionFactor = 0.8f;
        public float evasionMinSpeed = 2.5f;
        public bool evasionSkillContest = true;
        public bool enableFiringArc = Shipped;
        public float firingArcDegrees = 45f;
        // Vanilla Combat Reloaded ships six miss-spread distributions; 0 is the default.
        public int firingArcType = 0;
        public bool enableVisibleTracers = Shipped;
        // One knob, not two: the streak has one right look, and length and width only ever
        // wanted to move together. 1 is the shipped size, and the range is the useful span
        // either side of it rather than everything the draw code can survive.
        public float tracerScale = 1f;

        // ---- Fire modes --------------------------------------------------------
        public bool enableFireModes = Shipped;
        // Non-player pawns always choose by distance; this turns that off.
        public bool fireModesForNpcs = true;
        // Auto selection: Suppression below the short burst range, Short Burst up to the
        // precision range, Precision beyond it. Matches Vanilla Fire Modes' 12 and 25.
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
        // Leftover stretch and AP compensation. Defaults match Vanilla Combat Reloaded:
        // always-block at 100% leftover (armorScale 2), weapons show 2x AP.
        public bool enableAdvancedArmor = Shipped;
        public float armorScale = 2f;
        public float penetrationScale = 2f;

        // ---- Loadouts ----------------------------------------------------------
        // Saved under its original name so existing settings files keep their choice.
        public bool enableAutoEquip = Shipped;
        // A replacement must be this much better than an allowed current weapon.
        // The margin prevents pawns oscillating between near-identical choices.
        public float autoEquipUpgradeThreshold = 1.10f;

        // ---- Apparel / coverage (VCR XML pack) ---------------------------------
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
        // Ticks between automatic diagnostic dumps to the log. 2500 ticks is about one
        // in-game hour. Zero disables the periodic dump without losing the counters.
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

        /// <summary>
        /// Settings consumed by XML PatchOperations during def load. These cannot change
        /// without a restart, so the UI marks them and they are snapshotted once.
        /// </summary>
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
