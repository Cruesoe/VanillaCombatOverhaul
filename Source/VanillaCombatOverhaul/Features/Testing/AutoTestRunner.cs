using System;
using System.IO;
using HarmonyLib;
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
            Log.Message("[VCO] Autotest trigger found; starting a quick-test map.");

            // Both calls are required and in this order. SetupForQuickTestPlay only prepares
            // Current.Game; InitGameStart is what generates the map and enters play. Calling
            // the first alone leaves the game with nothing to do and it exits immediately.
            // This mirrors what RimWorld's own dev-mode Quick Test button does.
            LongEventHandler.QueueLongEvent(delegate
            {
                Root_Play.SetupForQuickTestPlay();
                Log.Message("[VCO] Quick-test play set up; generating map.");
                PageUtility.InitGameStart();
            },
            "GeneratingMap", true, GameAndMapInitExceptionHandlers.ErrorWhileGeneratingMap);
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
                var facing = ArenaAssertions.FacingSelfTest();
                var results = TestSuite.RunMatrix(map, Seed);
                var rangedSelf = RangedAssertions.SelfTests();
                var rangedResults = TestSuite.RunRangedMatrix(map, Seed);
                report = TestSuite.FormatReport(results, facing)
                         + Environment.NewLine
                         + TestSuite.FormatRangedReport(rangedResults, rangedSelf);

                allPassed = !facing.Exists(a => !a.Passed)
                            && results.TrueForAll(r => r.AllAssertionsPassed)
                            && !rangedSelf.Exists(a => !a.Passed)
                            && rangedResults.TrueForAll(r => r.AllAssertionsPassed);
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

    /// <summary>Watches the main menu for the trigger and starts a map.</summary>
    [HarmonyPatch(typeof(Root_Entry), nameof(Root_Entry.Update))]
    public static class Patch_Root_Entry_Update
    {
        public static void Postfix() => AutoTest.BeginIfRequested();
    }

    /// <summary>
    /// Fires the suite once the map is live.
    ///
    /// Hooked to FinalizeInit rather than GameComponentTick deliberately: a map that starts
    /// paused never ticks, so a tick-driven trigger would wait forever. The arena drives
    /// DoSingleTick itself, so it does not need the game's own tick loop running at all.
    /// </summary>
    public class AutoTestGameComponent : GameComponent
    {
        public AutoTestGameComponent(Game game)
        {
        }

        public override void FinalizeInit()
        {
            if (!AutoTest.Requested)
            {
                return;
            }

            Log.Message("[VCO] Map ready; queueing autotest run.");

            // Deferred until loading finishes so the arena is not spawning into a map that is
            // still being set up behind a loading screen.
            LongEventHandler.ExecuteWhenFinished(delegate
            {
                var map = Find.CurrentMap;
                if (map == null)
                {
                    Log.Error("[VCO] Autotest reached FinalizeInit but no current map exists.");
                    Root.Shutdown();
                    return;
                }
                AutoTest.RunAndExit(map);
            });
        }
    }
}
