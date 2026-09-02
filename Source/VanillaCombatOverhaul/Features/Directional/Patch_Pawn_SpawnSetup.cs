using HarmonyLib;
using Verse;

namespace VanillaCombatOverhaul
{
    [HarmonyPatch(typeof(Pawn), nameof(Pawn.SpawnSetup))]
    public static class Patch_Pawn_SpawnSetup
    {
        public static void Postfix(Pawn __instance, bool respawningAfterLoad)
        {
            if (respawningAfterLoad || __instance.IsColonist)
            {
                return;
            }
            HeightTargetingUtility.AssignRandom(__instance);
        }
    }
}
