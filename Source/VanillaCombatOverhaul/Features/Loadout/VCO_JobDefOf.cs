using RimWorld;
using Verse;

namespace VanillaCombatOverhaul
{
    [DefOf]
    public static class VCO_JobDefOf
    {
        /// <summary>Draw a weapon out of combat. Interruptible like any other order.</summary>
        public static JobDef VCO_SwapWeapon;

        /// <summary>The same work under fire: not casually interrupted, weapon drawn throughout.</summary>
        public static JobDef VCO_SwapWeaponCombat;

        static VCO_JobDefOf()
        {
            DefOfHelper.EnsureInitializedInCtor(typeof(VCO_JobDefOf));
        }
    }
}
