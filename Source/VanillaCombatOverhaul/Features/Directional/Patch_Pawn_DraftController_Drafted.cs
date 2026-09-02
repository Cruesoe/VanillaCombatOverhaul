using HarmonyLib;
using RimWorld;
using Verse;

namespace VanillaCombatOverhaul
{
    [HarmonyPatch(typeof(Pawn_DraftController), nameof(Pawn_DraftController.Drafted), MethodType.Setter)]
    public static class Patch_Pawn_DraftController_Drafted
    {
        public static void Postfix(Pawn_DraftController __instance, bool value)
        {
            if (!value)
            {
                HeightTargetingUtility.Reset(__instance.pawn);
            }
        }
    }
}
