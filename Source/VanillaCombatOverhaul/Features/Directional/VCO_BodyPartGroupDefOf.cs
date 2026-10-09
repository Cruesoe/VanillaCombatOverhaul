using RimWorld;
using Verse;

namespace VanillaCombatOverhaul
{
    /// <summary>Body part groups marking which side of a target an attack can reach.</summary>
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
