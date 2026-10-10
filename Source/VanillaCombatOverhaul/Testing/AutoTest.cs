using System;
using System.IO;
using System.Linq;
using RimWorld;
using Verse;

namespace VanillaCombatOverhaul
{
    /// <summary>
    /// Headless suite run, started by a trigger file in the config folder (Tools\Run-CombatTest.ps1
    /// writes it into a throwaway -savedatafolder): opens a quick-test map, runs every check, writes
    /// the report and quits.
    /// </summary>
    public static class AutoTest
    {
        public const string TriggerFileName = "vco_autotest.trigger";
        public const string ResultFileName = "vco_autotest_results.txt";

        private static bool started;
        private static bool finished;

        public static string TriggerPath => Path.Combine(GenFilePaths.ConfigFolderPath, TriggerFileName);
        public static string ResultPath => Path.Combine(GenFilePaths.ConfigFolderPath, ResultFileName);

        // Read once per session; the main menu asks every frame.
        private static readonly bool requested = ReadRequested();
        private static readonly int seed = requested ? ReadSeed() : 0;

        public static bool Requested => requested;

        /// <summary>The seed in the trigger file, or 0.</summary>
        public static int Seed => seed;

        private static bool ReadRequested()
        {
            try
            {
                return File.Exists(TriggerPath);
            }
            catch
            {
                return false;
            }
        }

        private static int ReadSeed()
        {
            try
            {
                var text = File.ReadAllText(TriggerPath).Trim();
                return int.TryParse(text, out var s) ? s : 0;
            }
            catch
            {
                return 0;
            }
        }

        /// <summary>Takes the game from the main menu into a throwaway map, once.</summary>
        public static void BeginIfRequested()
        {
            if (started || !Requested)
            {
                return;
            }
            started = true;
            var seed = Seed;
            Log.Message("[VCO] Autotest trigger found; starting a quick-test map.");

            // As vanilla's Quick Test button: SetupForQuickTestPlay prepares the game, InitGameStart generates the map.
            LongEventHandler.QueueLongEvent(delegate
            {
                // Pins the world to the seed; seed 0 leaves it unpinned.
                var pinned = seed != 0;
                if (pinned)
                {
                    Rand.PushState(seed);
                }
                try
                {
                    Root_Play.SetupForQuickTestPlay();
                    Log.Message("[VCO] Quick-test play set up; generating map.");
                    PageUtility.InitGameStart();
                }
                finally
                {
                    if (pinned)
                    {
                        Rand.PopState();
                    }
                }
            },
            // Synchronous, so the main thread does not draw from Rand while the map generates.
            "GeneratingMap", false, GameAndMapInitExceptionHandlers.ErrorWhileGeneratingMap);
        }

        /// <summary>Runs the suite, writes the report, and shuts the game down.</summary>
        public static void RunAndExit(Map map)
        {
            if (finished)
            {
                return;
            }
            finished = true;

            var report = "[VCO] autotest did not produce a report.";
            var allPassed = false;
            var settings = VCOMod.Settings;
            var restoreVerboseLogging = settings?.verboseLogging ?? false;
            if (settings != null)
            {
                // The assertions read the diagnostic counters, which only record with verbose logging on.
                settings.verboseLogging = true;
            }

            try
            {
                // Taken before any arena runs.
                var fingerprint = Fingerprint(map);

                var facing = MeleeAssertions.FacingSelfTest();
                var armor = ArmorAssertions.SelfTests();
                var wounds = WoundAssertions.SelfTests();
                var height = HeightAssertions.SelfTests();
                var results = TestSuite.RunMatrix(map, Seed);

                // After the matrix, so there is a humanlike body on the map.
                height.AddRange(HeightAssertions.CoverageFusionTests(
                    map.mapPawns.AllPawnsSpawned.FirstOrDefault(p => p.RaceProps.Humanlike)));
                var rangedSelf = RangedAssertions.SelfTests();
                var rangedResults = TestSuite.RunRangedMatrix(map, Seed);

                var autoEquip = AutoEquipAssertions.SelfTests();
                var suppression = SuppressionAssertions.SelfTests();
                suppression.AddRange(SuppressionAssertions.MapTests(map));
                var loadouts = LoadoutAssertions.MapTests(map);

                report = Environment.NewLine + fingerprint + Environment.NewLine
                         + TestSuite.FormatReport(results, facing, armor)
                         + Environment.NewLine
                         + TestSuite.FormatChecks("wound formula checks", wounds)
                         + Environment.NewLine
                         + TestSuite.FormatChecks("height formula checks", height)
                         + Environment.NewLine
                         + TestSuite.FormatRangedReport(rangedResults, rangedSelf)
                         + Environment.NewLine
                         + TestSuite.FormatChecks("automatic weapon checks", autoEquip)
                         + Environment.NewLine
                         + TestSuite.FormatChecks("suppression checks", suppression)
                         + Environment.NewLine
                         + TestSuite.FormatChecks("loadout checks", loadouts);

                allPassed = !facing.Exists(a => !a.Passed)
                            && !armor.Exists(a => !a.Passed)
                            && !wounds.Exists(a => !a.Passed)
                            && !height.Exists(a => !a.Passed)
                            && results.TrueForAll(r => r.AllAssertionsPassed)
                            && !rangedSelf.Exists(a => !a.Passed)
                            && rangedResults.TrueForAll(r => r.AllAssertionsPassed)
                            && !autoEquip.Exists(a => !a.Passed)
                            && !suppression.Exists(a => !a.Passed)
                            && !loadouts.Exists(a => !a.Passed);
            }
            catch (Exception e)
            {
                report = "[VCO] autotest threw:\n" + e;
            }
            finally
            {
                if (settings != null)
                {
                    settings.verboseLogging = restoreVerboseLogging;
                }
                // Written first so a later crash still leaves the report.
                TryWrite(report + Environment.NewLine
                         + "RESULT: " + (allPassed ? "PASS" : "FAIL") + Environment.NewLine);
                Log.Message(report);

                TryDeleteTrigger();
                Log.Message("[VCO] Autotest complete; shutting down.");
                Root.Shutdown();
            }
        }

        /// <summary>The world, map and clock the run actually got, for comparing two reports.</summary>
        private static string Fingerprint(Map map)
        {
            try
            {
                var world = Find.World?.info;
                var tile = map?.Tile ?? -1;
                return "=== run fingerprint ===" + Environment.NewLine
                       + "  requested seed : " + (Seed == 0 ? "unseeded" : Seed.ToString()) + Environment.NewLine
                       + "  world seed     : " + (world?.seedString ?? "?") + Environment.NewLine
                       + "  map tile       : " + tile + Environment.NewLine
                       + "  map size       : " + (map?.Size.ToString() ?? "?") + Environment.NewLine
                       + "  weather        : " + (map?.weatherManager?.curWeather?.defName ?? "?")
                       + "  accuracy x" + (map?.weatherManager?.CurWeatherAccuracyMultiplier ?? 0f).ToString("F3")
                       + Environment.NewLine
                       + "  ticks / hour   : " + Find.TickManager.TicksGame
                       + " / " + GenLocalDate.HourOfDay(map) + Environment.NewLine
                       + "  pawns on map   : " + (map?.mapPawns?.AllPawnsSpawned?.Count ?? -1) + Environment.NewLine
                       // Thing IDs drive tick scheduling and some randomness, so differing IDs mean differing runs.
                       + "  pawn id sum    : " + PawnIdSum(map) + Environment.NewLine
                       + "  next thing id  : " + NextThingId();
            }
            catch (Exception e)
            {
                return "=== run fingerprint unavailable: " + e.Message + " ===";
            }
        }

        private static long PawnIdSum(Map map)
        {
            if (map?.mapPawns?.AllPawnsSpawned == null)
            {
                return -1;
            }
            long sum = 0;
            foreach (var pawn in map.mapPawns.AllPawnsSpawned)
            {
                sum += pawn.thingIDNumber;
            }
            return sum;
        }

        private static string NextThingId()
        {
            try
            {
                var mgr = Find.UniqueIDsManager;
                var field = HarmonyLib.AccessTools.Field(mgr.GetType(), "nextThingID");
                return field?.GetValue(mgr)?.ToString() ?? "?";
            }
            catch (Exception e)
            {
                return "=== run fingerprint unavailable: " + e.Message + " ===";
            }
        }

        private static void TryWrite(string text)
        {
            try
            {
                File.WriteAllText(ResultPath, text);
            }
            catch (Exception e)
            {
                Log.Error("[VCO] Could not write autotest results: " + e.Message);
            }
        }

        private static void TryDeleteTrigger()
        {
            // Removed so the next launch does not start the test again.
            try
            {
                if (File.Exists(TriggerPath))
                {
                    File.Delete(TriggerPath);
                }
            }
            catch
            {
                // Nothing useful to do; the runner script clears it too.
            }
        }
    }
}
