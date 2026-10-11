using HarmonyLib;
using Verse;

namespace VanillaCombatOverhaul
{
    /// <summary>Slower movement while shooting on the move.</summary>
    [HarmonyPatch(typeof(Pawn), "TicksPerMove")]
    public static class Patch_Pawn_TicksPerMove
    {
        public static void Postfix(Pawn __instance, ref float __result)
        {
            if (RunAndGunUtility.AnyActive)
            {
                __result *= RunAndGunUtility.MoveTicksFactor(__instance);
            }
        }
    }
}
