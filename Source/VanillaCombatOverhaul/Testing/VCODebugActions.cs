using LudeonTK;
using Verse;

namespace VanillaCombatOverhaul
{
    /// <summary>
    /// Tier 1: manual entry points in the dev-mode debug menu, under "Vanilla Combat Overhaul".
    /// Results go to the log; the same code path is what the headless runner drives.
    /// </summary>
    public static class VCODebugActions
    {
        private const string Category = "Vanilla Combat Overhaul";

        [DebugAction(Category, "Run full combat test", allowedGameStates = AllowedGameStates.PlayingOnMap)]
        private static void RunFullSuite()
        {
            var facing = MeleeAssertions.FacingSelfTest();
            var armor = ArmorAssertions.SelfTests();
            var results = TestSuite.RunMatrix(Find.CurrentMap);
            Log.Message(TestSuite.FormatReport(results, facing, armor)
                        + "\n" + TestSuite.FormatChecks("wound formula checks", WoundAssertions.SelfTests())
                        + "\n" + TestSuite.FormatChecks("height formula checks", HeightAssertions.SelfTests())
                        + "\n" + TestSuite.FormatChecks("automatic weapon checks", AutoEquipAssertions.SelfTests()));
        }

        [DebugAction(Category, "Run one quick arena", allowedGameStates = AllowedGameStates.PlayingOnMap)]
        private static void RunQuickArena()
        {
            var spec = new MeleeArenaSpec { label = "quick", pairs = 12, ticks = 2000 };
            var result = MeleeCombatArena.Run(spec, Find.CurrentMap);
            Log.Message(TestSuite.FormatReport(
                new System.Collections.Generic.List<MeleeArenaResult> { result }, null));
        }

        [DebugAction(Category, "Facing checks only", allowedGameStates = AllowedGameStates.Entry
                                                                        | AllowedGameStates.PlayingOnMap)]
        private static void RunFacingChecks()
        {
            Log.Message(TestSuite.FormatReport(
                new System.Collections.Generic.List<MeleeArenaResult>(), MeleeAssertions.FacingSelfTest()));
        }

        [DebugAction(Category, "Armor formula checks", allowedGameStates = AllowedGameStates.Entry
                                                                          | AllowedGameStates.PlayingOnMap)]
        private static void RunArmorChecks()
        {
            Log.Message(TestSuite.FormatReport(
                new System.Collections.Generic.List<MeleeArenaResult>(), null, ArmorAssertions.SelfTests()));
        }

        [DebugAction(Category, "Run ranged accuracy test", allowedGameStates = AllowedGameStates.PlayingOnMap)]
        private static void RunRangedSuite()
        {
            var self = RangedAssertions.SelfTests();
            var results = TestSuite.RunRangedMatrix(Find.CurrentMap);
            Log.Message(TestSuite.FormatRangedReport(results, self));
        }

        [DebugAction(Category, "Wound and height checks", allowedGameStates = AllowedGameStates.Entry
                                                                            | AllowedGameStates.PlayingOnMap)]
        private static void RunWoundChecks()
        {
            Log.Message(TestSuite.FormatChecks("wound formula checks", WoundAssertions.SelfTests())
                        + "\n" + TestSuite.FormatChecks("height formula checks", HeightAssertions.SelfTests()));
        }

        [DebugAction(Category, "Sidearm checks", allowedGameStates = AllowedGameStates.PlayingOnMap)]
        private static void RunSidearmChecks()
        {
            Log.Message(TestSuite.FormatChecks("sidearm formula checks", SidearmAssertions.SelfTests())
                        + "\n" + TestSuite.FormatChecks("sidearm live checks",
                                                        SidearmAssertions.LiveTests(Find.CurrentMap)));
        }

        [DebugAction(Category, "Automatic weapon checks", allowedGameStates = AllowedGameStates.Entry
                                                                              | AllowedGameStates.PlayingOnMap)]
        private static void RunAutoEquipChecks()
        {
            Log.Message(TestSuite.FormatChecks("automatic weapon checks", AutoEquipAssertions.SelfTests()));
        }

        /// <summary>
        /// Sidearms are still an unbuilt roadmap toggle, so the settings window pins them off and
        /// there is no supported way to switch them on. This is the developer's way in until the
        /// gizmo lands and the toggle unlocks; opening mod settings turns it straight back off.
        /// </summary>
        [DebugAction(Category, "Sidearms: enable for this session",
                     allowedGameStates = AllowedGameStates.PlayingOnMap)]
        private static void EnableSidearmsForSession()
        {
            if (SidearmUtility.ConflictingMod != null)
            {
                Log.Warning("[VCO] Sidearms cannot be enabled: " + SidearmUtility.ConflictingMod
                            + " is installed and already manages carried weapons.");
                return;
            }
            VCOMod.Settings.enableSidearms = true;
            Log.Message("[VCO] Sidearms enabled for this session. Right-click a weapon with a "
                        + "colonist selected to carry it as a sidearm. Opening mod settings "
                        + "switches this back off.");
        }

        [DebugAction(Category, "Write diagnostic counters", allowedGameStates = AllowedGameStates.PlayingOnMap)]
        private static void WriteCounters() => VCODiagnostics.WriteReport();

        [DebugAction(Category, "Reset diagnostic counters", allowedGameStates = AllowedGameStates.PlayingOnMap)]
        private static void ResetCounters() => VCODiagnostics.Reset();
    }
}
