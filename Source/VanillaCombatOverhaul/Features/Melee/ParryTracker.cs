using System.Collections.Generic;
using Verse;

namespace VanillaCombatOverhaul
{
    /// <summary>
    /// Caps how often a pawn can parry.
    ///
    /// Without this, parry chance is rolled independently per incoming attack, so a skilled
    /// defender surrounded by six attackers parries all six at full rate forever and being
    /// flanked stops mattering. A budget per short window makes numbers matter again: one
    /// attacker is survivable, six overwhelm you regardless of skill.
    ///
    /// Not saved. Losing the budget across a save/load is harmless -- the window is about a
    /// second -- and it keeps this out of save data entirely.
    /// </summary>
    public class ParryTracker : GameComponent
    {
        private struct Record
        {
            public int WindowStart;
            public int Count;
        }

        private const int SweepIntervalTicks = 2000;

        private readonly Dictionary<int, Record> records = new Dictionary<int, Record>();
        private int lastSweepTick;

        public ParryTracker(Game game)
        {
        }

        /// <summary>True if the pawn has parry budget left in the current window.</summary>
        public bool CanParry(Pawn pawn)
        {
            var settings = VCOMod.Settings;
            if (settings == null)
            {
                return true;
            }

            var now = Find.TickManager.TicksGame;
            if (!records.TryGetValue(pawn.thingIDNumber, out var record))
            {
                return true;
            }
            if (now - record.WindowStart >= settings.parryWindowTicks)
            {
                return true;
            }
            return record.Count < settings.parryBudgetPerWindow;
        }

        /// <summary>Spends one parry from the pawn's budget.</summary>
        public void RecordParry(Pawn pawn)
        {
            var settings = VCOMod.Settings;
            var now = Find.TickManager.TicksGame;
            var id = pawn.thingIDNumber;

            if (records.TryGetValue(id, out var record)
                && settings != null
                && now - record.WindowStart < settings.parryWindowTicks)
            {
                record.Count++;
                records[id] = record;

                // RecordParry is only ever reached after CanParry allowed it, so exceeding the
                // cap means the gate and the ledger disagree. Counted rather than asserted in
                // place because the arena is the only thing that can drive enough attackers at
                // one defender to prove the cap actually holds.
                if (record.Count > settings.parryBudgetPerWindow)
                {
                    VCODiagnostics.CountFor(pawn, "parry.budget.overrun");
                }
                return;
            }

            records[id] = new Record { WindowStart = now, Count = 1 };
        }

        public override void GameComponentTick()
        {
            var now = Find.TickManager.TicksGame;
            if (now - lastSweepTick < SweepIntervalTicks)
            {
                return;
            }
            lastSweepTick = now;

            // Records expire after roughly a second, so anything older is dead weight.
            var window = VCOMod.Settings?.parryWindowTicks ?? 60;
            List<int> stale = null;
            foreach (var kv in records)
            {
                if (now - kv.Value.WindowStart >= window)
                {
                    (stale ?? (stale = new List<int>())).Add(kv.Key);
                }
            }
            if (stale != null)
            {
                foreach (var id in stale)
                {
                    records.Remove(id);
                }
            }
        }
    }
}
