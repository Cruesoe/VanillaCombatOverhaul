using RimWorld;
using Verse;

namespace VanillaCombatOverhaul
{
    /// <summary>Combat Extended's mood penalty for being shot at: under fire, suppressed, then pinned.</summary>
    public class ThoughtWorker_UnderFire : ThoughtWorker
    {
        protected override ThoughtState CurrentStateInternal(Pawn p)
        {
            if (!(VCOMod.Settings?.enableSuppressionMood ?? false))
            {
                return ThoughtState.Inactive;
            }
            var stage = StageFor(SuppressionUtility.LevelOf(p));
            return stage < 0 ? ThoughtState.Inactive : ThoughtState.ActiveAtStage(stage);
        }

        /// <summary>-1 with no suppression, then 0 under fire, 1 suppressed, 2 pinned.</summary>
        public static int StageFor(float level)
        {
            if (level >= SuppressionUtility.PinnedLevel)
            {
                return 2;
            }
            if (level >= SuppressionUtility.SuppressedLevel)
            {
                return 1;
            }
            return level > 0f ? 0 : -1;
        }
    }
}
