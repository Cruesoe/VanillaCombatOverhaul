using RimWorld;
using Verse;

namespace VanillaCombatOverhaul
{
    [DefOf]
    public static class VCO_JobDefOf
    {
        public static JobDef VCO_PinnedDown;

        static VCO_JobDefOf() => DefOfHelper.EnsureInitializedInCtor(typeof(VCO_JobDefOf));
    }
}
