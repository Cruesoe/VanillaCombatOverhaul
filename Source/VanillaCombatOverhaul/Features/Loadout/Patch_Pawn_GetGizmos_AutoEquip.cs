using System.Collections.Generic;
using HarmonyLib;
using Verse;

namespace VanillaCombatOverhaul
{
    [HarmonyPatch(typeof(Pawn), nameof(Pawn.GetGizmos))]
    public static class Patch_Pawn_GetGizmos_AutoEquip
    {
        public static IEnumerable<Gizmo> Postfix(IEnumerable<Gizmo> values, Pawn __instance)
        {
            foreach (var value in values)
            {
                yield return value;
            }
            // Shown only once the forced weapon is in hand, not while the equip job is still walking to it
            var primary = __instance.equipment?.Primary;
            if (LoadoutUtility.Enabled
                && primary != null
                && __instance.IsColonistPlayerControlled
                && AutoEquipPolicyComponent.Current?.HasForcedCurrentWeapon(__instance) == true)
            {
                yield return new Command_Action
                {
                    defaultLabel = "VCO_AutoEquip_Unlock".Translate(),
                    defaultDesc = "VCO_AutoEquip_Unlock_Tip".Translate(),
                    icon = primary.def.uiIcon,
                    action = () => AutoEquipPolicyComponent.Current?.ClearForcedWeapon(__instance)
                };
            }
        }
    }
}
