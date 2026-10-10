using Verse;

namespace VanillaCombatOverhaul
{
    /// <summary>Writes the diagnostic counters to the log on an interval while verbose logging is on.</summary>
    public class DiagnosticsGameComponent : GameComponent
    {
        private int lastDumpTick;

        public DiagnosticsGameComponent(Game game)
        {
        }

        public override void GameComponentTick()
        {
            var settings = VCOMod.Settings;
            if (settings == null || !settings.verboseLogging || settings.diagnosticDumpIntervalTicks <= 0)
            {
                return;
            }

            var now = Find.TickManager.TicksGame;
            if (now - lastDumpTick < settings.diagnosticDumpIntervalTicks)
            {
                return;
            }
            lastDumpTick = now;

            // Nothing recorded since the last dump.
            if (!VCODiagnostics.HasData)
            {
                return;
            }
            VCODiagnostics.WriteReport();
        }
    }
}
