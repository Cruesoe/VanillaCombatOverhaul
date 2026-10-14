using HarmonyLib;
using Verse.AI;

namespace VanillaCombatOverhaul
{
    [HarmonyPatch(typeof(JobDriver_Equip), nameof(JobDriver_Equip.Notify_Starting))]
    public static class Patch_JobDriver_Equip_Notify_Starting
    {
        public static void Postfix(JobDriver_Equip __instance)
        {
            if (__instance.job?.playerForced == true && __instance.job.targetA.Thing != null)
            {
                AutoEquipPolicyComponent.Current?.ForceWeapon(__instance.pawn, __instance.job.targetA.Thing);
            }
        }
    }
}
