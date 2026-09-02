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

        // ---- Ranged ------------------------------------------------------------
        public bool enableEvasion = Unbuilt;
        public float evasionFactor = 1f;
        public bool enableFiringArc = Unbuilt;
        public float firingArcDegrees = 45f;

        // ---- Suppression -------------------------------------------------------
        public bool enableSuppression = Unbuilt;
        public float suppressionBuildRate = 1f;

        // ---- Ammo --------------------------------------------------------------
        public bool enableAmmo = Unbuilt;
        public float ammoYieldFactor = 1f;

        // ---- Loadout / sidearms ------------------------------------------------
        public bool enableSidearms = Unbuilt;
        public bool enableLoadouts = Unbuilt;

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

            Scribe_Values.Look(ref enableEvasion, nameof(enableEvasion), Unbuilt);
            Scribe_Values.Look(ref evasionFactor, nameof(evasionFactor), 1f);
            Scribe_Values.Look(ref enableFiringArc, nameof(enableFiringArc), Unbuilt);
            Scribe_Values.Look(ref firingArcDegrees, nameof(firingArcDegrees), 45f);

            Scribe_Values.Look(ref enableSuppression, nameof(enableSuppression), Unbuilt);
            Scribe_Values.Look(ref suppressionBuildRate, nameof(suppressionBuildRate), 1f);

            Scribe_Values.Look(ref enableAmmo, nameof(enableAmmo), Unbuilt);
            Scribe_Values.Look(ref ammoYieldFactor, nameof(ammoYieldFactor), 1f);

            Scribe_Values.Look(ref enableSidearms, nameof(enableSidearms), Unbuilt);
            Scribe_Values.Look(ref enableLoadouts, nameof(enableLoadouts), Unbuilt);

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
            enableEvasion = Unbuilt;
            enableFiringArc = Unbuilt;
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
        }
    }
}
