using System;
using System.IO;
using System.Linq;
using RimWorld;
using Verse;

namespace VanillaCombatOverhaul
{
    /// <summary>
    /// Tier 2: runs the whole suite with no human present.
    ///
    /// Driven by a trigger file rather than a command-line flag, because RimWorld 1.6 has no
    /// -quicktest argument (checked: the literal is absent from the assembly, while
    /// "savedatafolder" and "autostart" are present). Root_Play.SetupForQuickTestPlay is
    /// public though, so the mod can take itself into a playable map unaided.
    ///
    /// Pairing this with -savedatafolder gives the run its own config directory, so automation
    /// never touches the player's real mod list or saves.
    /// </summary>
    public static class AutoTest
    {
        public const string TriggerFileName = "vco_autotest.trigger";
        public const string ResultFileName = "vco_autotest_results.txt";

        private static bool started;
        private static bool finished;

        public static string TriggerPath => Path.Combine(GenFilePaths.ConfigFolderPath, TriggerFileName);
        public static string ResultPath => Path.Combine(GenFilePaths.ConfigFolderPath, ResultFileName);

        public static bool Requested
        {
            get
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
        }

        /// <summary>Reads the seed from the trigger file, if it holds one.</summary>
        public static int Seed
        {
            get
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

            // Both calls are required and in this order. SetupForQuickTestPlay only prepares
            // Current.Game; InitGameStart is what generates the map and enters play. Calling
            // the first alone leaves the game with nothing to do and it exits immediately.
            // This mirrors what RimWorld's own dev-mode Quick Test button does.
            LongEventHandler.QueueLongEvent(delegate
            {
                // The world seed is drawn from Rand, so without this the map -- terrain,
                // weather, season, time of day, starting colonists -- is different every run.
                // The arenas seed themselves, but they then drive DoSingleTick over whatever
                // is on the map, and everything that ticks draws from the same stream: a
                // different map means a different stream, so -Seed pinned nothing that
                // mattered. Verse.Rand's state is a plain static, not thread-static, so this
                // holds across the long event. Seed 0 stays unpinned, which is what the
                // README means by leaving a run unseeded to measure variance.
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
            // doAsynchronously: false, and this is the whole reason -Seed used to pin nothing.
            //
            // Asynchronously, map generation runs on a worker thread while the main thread
            // keeps pumping Root_Entry.Update. Verse.Rand's seed and state stack are plain
            // statics, not [ThreadStatic], so both threads draw from one generator and the
            // interleaving is decided by thread scheduling -- wall-clock, not seed. Two runs
            // of the same seed built maps differing by hundreds of things, which shifted every
            // thingIDNumber, and RimWorld schedules rare ticks and seeds much of its own
            // randomness off those. That is what made a "reproducible" run reproduce nothing.
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

            try
            {
                // Taken before any arena runs. Taken afterwards it describes the wreckage the
                // scenarios left, which is downstream of whatever made them differ.
                var fingerprint = Fingerprint(map);

                var facing = MeleeAssertions.FacingSelfTest();
                var armor = ArmorAssertions.SelfTests();
                var wounds = WoundAssertions.SelfTests();
                var height = HeightAssertions.SelfTests();
                var results = TestSuite.RunMatrix(map, Seed);

                // Run after the matrix so there is certainly a body on the map to check.
                height.AddRange(HeightAssertions.CoverageFusionTests(
                    map.mapPawns.AllPawnsSpawned.FirstOrDefault(p => p.RaceProps.Humanlike)));
                var rangedSelf = RangedAssertions.SelfTests();
                var rangedResults = TestSuite.RunRangedMatrix(map, Seed);

                // Sidearms are unbuilt in the settings, so the live checks switch the feature on
                // for their own duration and put it back. Run last: they spawn and destroy a pawn
                // of their own, and no other suite should have to reason about that.
                var sidearms = SidearmAssertions.SelfTests();
                sidearms.AddRange(SidearmAssertions.LiveTests(map));

                report = Environment.NewLine + fingerprint + Environment.NewLine
                         + TestSuite.FormatReport(results, facing, armor)
                         + Environment.NewLine
                         + TestSuite.FormatChecks("wound formula checks", wounds)
                         + Environment.NewLine
                         + TestSuite.FormatChecks("height formula checks", height)
                         + Environment.NewLine
                         + TestSuite.FormatRangedReport(rangedResults, rangedSelf)
                         + Environment.NewLine
                         + TestSuite.FormatChecks("sidearm checks", sidearms);

                allPassed = !facing.Exists(a => !a.Passed)
                            && !armor.Exists(a => !a.Passed)
                            && !wounds.Exists(a => !a.Passed)
                            && !height.Exists(a => !a.Passed)
                            && results.TrueForAll(r => r.AllAssertionsPassed)
                            && !rangedSelf.Exists(a => !a.Passed)
                            && rangedResults.TrueForAll(r => r.AllAssertionsPassed)
                            && !sidearms.Exists(a => !a.Passed);
            }
            catch (Exception e)
            {
                report = "[VCO] autotest threw:\n" + e;
            }
            finally
            {
                // Written before the exit code line so a crash still leaves the detail behind.
                TryWrite(report + Environment.NewLine
                         + "RESULT: " + (allPassed ? "PASS" : "FAIL") + Environment.NewLine);
                Log.Message(report);

                TryDeleteTrigger();
                Log.Message("[VCO] Autotest complete; shutting down.");
                Root.Shutdown();
            }
        }

        /// <summary>
        /// What the run actually got, as opposed to what it asked for.
        ///
        /// A seeded run is only reproducible if the map is, and a map is not something an
        /// assertion can check. Printing the world seed, biome, weather and clock makes a
        /// determinism regression visible in a diff of two reports instead of showing up
        /// weeks later as an assertion that fails one seed in three.
        /// </summary>
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
                       // Thing IDs matter more than they look. RimWorld schedules rare ticks
                       // and seeds a good deal of its own randomness off thingIDNumber, so if
                       // the map is built from a different number of things the whole run
                       // diverges even with Rand pinned.
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
            // Removed so a crashed run cannot leave the game booting into the test forever.
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
