using HarmonyLib;
using Verse;

namespace VanillaCombatOverhaul
{
    [HarmonyPatch(typeof(ArmorUtility), "ApplyArmor")]
    public static class Patch_ArmorUtility_ApplyArmor
    {
        public static void Prefix(ref float armorPenetration, ref float armorRating) =>
            AdvancedArmorUtility.ScaleLeftoverInputs(ref armorPenetration, ref armorRating);
    }
}
