using RimWorld;
using Verse;

namespace VanillaCombatOverhaul
{
    /// <summary>
    /// Body part groups used to resolve which side of a target an attack can reach.
    ///
    /// Prefixed, unlike the bare Left/Right/Center names Vanilla Combat Reloaded used, because
    /// unprefixed group defNames that generic are liable to collide with another mod's.
    /// </summary>
    [DefOf]
    public static class VCO_BodyPartGroupDefOf
    {
        public static BodyPartGroupDef VCO_Left;
        public static BodyPartGroupDef VCO_Right;
        public static BodyPartGroupDef VCO_Center;

        static VCO_BodyPartGroupDefOf()
        {
            DefOfHelper.EnsureInitializedInCtor(typeof(VCO_BodyPartGroupDefOf));
        }
    }
}
