using HarmonyLib;
using RimWorld;
using Verse;

namespace VanillaCombatOverhaul
{
    [HarmonyPatch(typeof(PawnApparelGenerator), "IsHeadgear")]
    public static class Patch_PawnApparelGenerator_IsHeadgear
    {
        public static void Postfix(ref bool __result, ThingDef td)
        {
            if (__result || VCOMod.Settings?.enableApparelTweaks != true || td?.apparel == null)
            {
                return;
            }

            if (td.apparel.bodyPartGroups.Contains(BodyPartGroupDefOf.Eyes)
                || td.apparel.bodyPartGroups.Contains(VCO_ApparelGroupDefOf.Mouth))
            {
                __result = true;
            }
        }
    }

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
