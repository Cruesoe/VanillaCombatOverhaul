using RimWorld;
using Verse;

namespace VanillaCombatOverhaul
{
    [DefOf]
    public static class VCO_ApparelGroupDefOf
    {
        public static BodyPartGroupDef Mouth;

        static VCO_ApparelGroupDefOf()
        {
            DefOfHelper.EnsureInitializedInCtor(typeof(VCO_ApparelGroupDefOf));
        }
    }
}
