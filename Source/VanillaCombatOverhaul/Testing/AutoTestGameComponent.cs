using Verse;

namespace VanillaCombatOverhaul
{
    /// <summary>Starts the headless suite once the map has loaded; the arenas drive ticks themselves.</summary>
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

            // Deferred until loading has finished.
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
