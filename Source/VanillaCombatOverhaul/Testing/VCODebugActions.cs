using LudeonTK;
using Verse;

namespace VanillaCombatOverhaul
{
    /// <summary>Dev-mode debug actions that run the checks on the current map and log the results.</summary>
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

        [DebugAction(Category, "Automatic weapon checks", allowedGameStates = AllowedGameStates.Entry
                                                                              | AllowedGameStates.PlayingOnMap)]
        private static void RunAutoEquipChecks()
        {
            Log.Message(TestSuite.FormatChecks("automatic weapon checks", AutoEquipAssertions.SelfTests()));
        }

        [DebugAction(Category, "Suppression and loadout checks", allowedGameStates = AllowedGameStates.PlayingOnMap)]
        private static void RunSuppressionAndLoadoutChecks()
        {
            var suppression = SuppressionAssertions.SelfTests();
            suppression.AddRange(SuppressionAssertions.MapTests(Find.CurrentMap));
            Log.Message(TestSuite.FormatChecks("suppression checks", suppression)
                        + "\n" + TestSuite.FormatChecks("loadout checks", LoadoutAssertions.MapTests(Find.CurrentMap)));
        }

        [DebugAction(Category, "Write diagnostic counters", allowedGameStates = AllowedGameStates.PlayingOnMap)]
        private static void WriteCounters() => VCODiagnostics.WriteReport();

        [DebugAction(Category, "Reset diagnostic counters", allowedGameStates = AllowedGameStates.PlayingOnMap)]
        private static void ResetCounters() => VCODiagnostics.Reset();
    }
}
