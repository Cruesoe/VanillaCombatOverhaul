using RimWorld;
using Verse;

namespace VanillaCombatOverhaul
{
    [DefOf]
    public static class VCO_JobDefOf
    {
        public static JobDef VCO_PinnedDown;
        public static JobDef VCO_TakeSidearm;
        public static JobDef VCO_StowWeapon;

        static VCO_JobDefOf() => DefOfHelper.EnsureInitializedInCtor(typeof(VCO_JobDefOf));
    }
}
