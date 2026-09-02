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
            var facing = ArenaAssertions.FacingSelfTest();
            var armor = ArmorAssertions.SelfTests();
            var results = TestSuite.RunMatrix(Find.CurrentMap);
            Log.Message(TestSuite.FormatReport(results, facing, armor)
                        + "\n" + TestSuite.FormatChecks("wound formula checks", WoundAssertions.SelfTests())
                        + "\n" + TestSuite.FormatChecks("height formula checks", HeightAssertions.SelfTests()));
        }

        [DebugAction(Category, "Run one quick arena", allowedGameStates = AllowedGameStates.PlayingOnMap)]
        private static void RunQuickArena()
        {
            var spec = new ArenaSpec { label = "quick", pairs = 12, ticks = 2000 };
            var result = CombatArena.Run(spec, Find.CurrentMap);
            Log.Message(TestSuite.FormatReport(
                new System.Collections.Generic.List<ArenaResult> { result }, null));
        }

        [DebugAction(Category, "Facing checks only", allowedGameStates = AllowedGameStates.Entry
                                                                        | AllowedGameStates.PlayingOnMap)]
        private static void RunFacingChecks()
        {
            Log.Message(TestSuite.FormatReport(
                new System.Collections.Generic.List<ArenaResult>(), ArenaAssertions.FacingSelfTest()));
        }

        [DebugAction(Category, "Armor formula checks", allowedGameStates = AllowedGameStates.Entry
                                                                          | AllowedGameStates.PlayingOnMap)]
        private static void RunArmorChecks()
        {
            Log.Message(TestSuite.FormatReport(
                new System.Collections.Generic.List<ArenaResult>(), null, ArmorAssertions.SelfTests()));
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

        [DebugAction(Category, "Write diagnostic counters", allowedGameStates = AllowedGameStates.PlayingOnMap)]
        private static void WriteCounters() => VCODiagnostics.WriteReport();

        [DebugAction(Category, "Reset diagnostic counters", allowedGameStates = AllowedGameStates.PlayingOnMap)]
        private static void ResetCounters() => VCODiagnostics.Reset();
    }
}
