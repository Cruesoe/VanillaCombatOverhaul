using Verse;

namespace VanillaCombatOverhaul
{
    /// <summary>Rules of Engagement has its own walk-firing (for NPCs too) and loadouts, so run and gun and loadouts stay off while it is active.</summary>
    public static class RulesOfEngagementCompat
    {
        public const string PackageId = "asunib.RulesOfEngagement";

        public static readonly bool Active = ModsConfig.IsActive(PackageId);
    }
}
