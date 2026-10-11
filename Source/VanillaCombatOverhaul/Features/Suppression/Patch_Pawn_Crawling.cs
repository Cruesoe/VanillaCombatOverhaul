using HarmonyLib;
using Verse;

namespace VanillaCombatOverhaul
{
    /// <summary>Pinned pawns that move use vanilla's crawling pose and gait.</summary>
    [HarmonyPatch(typeof(Pawn), nameof(Pawn.Crawling), MethodType.Getter)]
    public static class Patch_Pawn_Crawling
    {
        public static void Postfix(Pawn __instance, ref bool __result)
        {
            if (!__result && SuppressionUtility.IsCrawling(__instance))
            {
                __result = true;
            }
        }
    }
}
