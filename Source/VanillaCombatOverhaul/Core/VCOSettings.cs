using System.Collections.Generic;
using Verse;

namespace VanillaCombatOverhaul
{
    public enum SettingsTab
    {
        Combat,
        Equipment,
        Diagnostics
    }

    /// <summary>
    /// Defaults are set per feature, not by one global switch.
    ///
    /// A feature defaults ON only once it is built and verified by the arena suite. Everything
    /// still on the roadmap defaults OFF and is locked off in the UI, so a switch can never
    /// imply an effect that does not exist -- a toggle a player can enable to no effect is
    /// worse than no toggle at all, because it makes the mod look broken rather than unfinished.
    ///
    /// Verbose logging is the exception: it is diagnostic scaffolding, on for the test build so
    /// tester reports come with counters attached, and it comes out before public release along
    /// with VCODiagnostics itself.
    /// </summary>
    public class VCOSettings : ModSettings
    {
        // Built, verified by the arena suite, safe to ship enabled.
        private const bool Shipped = true;

        // On the roadmap. Locked off in the UI until the implementation exists.
        private const bool Unbuilt = false;

        public SettingsTab CurrentTab = SettingsTab.Combat;

        // ---- Melee -------------------------------------------------------------
        public bool enableParry = Shipped;
        // Exponent divisors, not linear multipliers: higher means easier to parry.
        // 1.5 for both matches Vanilla Combat Reloaded, where only a rear attack
        // denied a parry outright.
        public float parryFrontFactor = 1.5f;
        public float parrySideFactor = 1.5f;
        // Measured at 2: negligible in a duel (0 rejections), light at 3v1 (2.4% of
        // attacks), and material at 6v1 (9.9%). See the balance notes in README.md.
        public int parryBudgetPerWindow = 2;
        public int parryWindowTicks = 60;
        public bool enableCounterAttack = Shipped;

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

        // ---- Armor -------------------------------------------------------------
        // Leftover stretch and AP compensation. Defaults match Vanilla Combat Reloaded:
        // always-block at 100% leftover (armorScale 2), weapons show 2x AP.
        public bool enableAdvancedArmor = Shipped;
        public float armorScale = 2f;
        public float penetrationScale = 2f;

        // ---- Suppression -------------------------------------------------------
        public bool enableSuppression = Unbuilt;
        public float suppressionBuildRate = 1f;

        // ---- Ammo --------------------------------------------------------------
        public bool enableAmmo = Unbuilt;
        public float ammoYieldFactor = 1f;

        // ---- Loadout / sidearms ------------------------------------------------
        public bool enableSidearms = Unbuilt;
        // Ticks per unit of weapon mass, divided by the pawn's VCO_WeaponSwapSpeed. A revolver
        // (mass 1.4) is about a second and a half at 60; a minigun (mass 20) is most of a fight.
        public float sidearmSwapTicksPerMass = 60f;
        public bool enableLoadouts = Unbuilt;

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

        // ---- Diagnostics -------------------------------------------------------
        public bool verboseLogging = true;
        // Ticks between automatic diagnostic dumps to the log. 2500 ticks is about one
        // in-game hour. Zero disables the periodic dump without losing the counters.
        public int diagnosticDumpIntervalTicks = 2500;

        public override void ExposeData()
        {
            base.ExposeData();

            Scribe_Values.Look(ref enableParry, nameof(enableParry), Shipped);
            Scribe_Values.Look(ref parryFrontFactor, nameof(parryFrontFactor), 1.5f);
            Scribe_Values.Look(ref parrySideFactor, nameof(parrySideFactor), 1.5f);
            Scribe_Values.Look(ref parryBudgetPerWindow, nameof(parryBudgetPerWindow), 2);
            Scribe_Values.Look(ref parryWindowTicks, nameof(parryWindowTicks), 60);
            Scribe_Values.Look(ref enableCounterAttack, nameof(enableCounterAttack), Shipped);

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

            Scribe_Values.Look(ref enableAdvancedArmor, nameof(enableAdvancedArmor), Shipped);
            Scribe_Values.Look(ref armorScale, nameof(armorScale), 2f);
            Scribe_Values.Look(ref penetrationScale, nameof(penetrationScale), 2f);

            Scribe_Values.Look(ref enableSuppression, nameof(enableSuppression), Unbuilt);
            Scribe_Values.Look(ref suppressionBuildRate, nameof(suppressionBuildRate), 1f);

            Scribe_Values.Look(ref enableAmmo, nameof(enableAmmo), Unbuilt);
            Scribe_Values.Look(ref ammoYieldFactor, nameof(ammoYieldFactor), 1f);

            Scribe_Values.Look(ref enableSidearms, nameof(enableSidearms), Unbuilt);
            Scribe_Values.Look(ref sidearmSwapTicksPerMass, nameof(sidearmSwapTicksPerMass), 60f);
            Scribe_Values.Look(ref enableLoadouts, nameof(enableLoadouts), Unbuilt);

            Scribe_Values.Look(ref enableHandFeetPatch, nameof(enableHandFeetPatch), Shipped);
            Scribe_Values.Look(ref enableAcidHeatPatch, nameof(enableAcidHeatPatch), Shipped);
            Scribe_Values.Look(ref enableThumpBluntPatch, nameof(enableThumpBluntPatch), Shipped);
            Scribe_Values.Look(ref enableGlassesHelmetPatch, nameof(enableGlassesHelmetPatch), Shipped);
            Scribe_Values.Look(ref enableNoseMouthPatch, nameof(enableNoseMouthPatch), Shipped);
            Scribe_Values.Look(ref enableMaskPatch, nameof(enableMaskPatch), Shipped);
            Scribe_Values.Look(ref enableHeadsetPatch, nameof(enableHeadsetPatch), Shipped);
            Scribe_Values.Look(ref enableArrayHeadsetPatch, nameof(enableArrayHeadsetPatch), Shipped);
            Scribe_Values.Look(ref enableApparelTweaks, nameof(enableApparelTweaks), Shipped);

            Scribe_Values.Look(ref verboseLogging, nameof(verboseLogging), true);
            Scribe_Values.Look(ref diagnosticDumpIntervalTicks, nameof(diagnosticDumpIntervalTicks), 2500);

            // A config written by an earlier build could have any of the unbuilt features
            // switched on, and those flags reach XML patching before the UI ever draws.
            // Forced off on load so an old settings file cannot enable something that is
            // not there.
            if (Scribe.mode == LoadSaveMode.LoadingVars)
            {
                ForceUnbuiltOff();
            }
        }

        /// <summary>
        /// Holds every roadmap feature at its unbuilt default. Called on load and by the
        /// settings window, so neither a stale config nor the UI can raise one of these.
        /// </summary>
        public void ForceUnbuiltOff()
        {
            enableSuppression = Unbuilt;
            enableAmmo = Unbuilt;
            enableSidearms = Unbuilt;
            enableLoadouts = Unbuilt;
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
            if (enableAmmo)
            {
                yield return "Ammo";
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
