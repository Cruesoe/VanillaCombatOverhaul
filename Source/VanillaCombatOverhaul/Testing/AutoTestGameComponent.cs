using Verse;

namespace VanillaCombatOverhaul
{
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
