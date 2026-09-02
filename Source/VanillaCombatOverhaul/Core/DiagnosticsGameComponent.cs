using Verse;

namespace VanillaCombatOverhaul
{
    /// <summary>
    /// Dumps the diagnostic counters to the log on an interval, so a session leaves a record
    /// behind without anyone having to open the settings window at the right moment.
    ///
    /// Scaffolding for tuning; goes away with VCODiagnostics.
    /// </summary>
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

            // Nothing happened since the last dump; stay quiet rather than logging an empty table.
            if (!VCODiagnostics.HasData)
            {
                return;
            }
            VCODiagnostics.WriteReport();
        }
    }
}
