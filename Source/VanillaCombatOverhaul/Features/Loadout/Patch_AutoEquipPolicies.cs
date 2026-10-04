using System.Collections.Generic;
using System.Reflection;
using System.Runtime.CompilerServices;
using HarmonyLib;
using RimWorld;
using UnityEngine;
using Verse;
using Verse.AI;

namespace VanillaCombatOverhaul
{
    [HarmonyPatch(typeof(Dialog_ManageApparelPolicies), "DoContentsRect")]
    public static class Patch_DialogManageApparelPolicies_DoContentsRect
    {
        private sealed class DialogState
        {
            public bool weapons;
            public readonly ThingFilterUI.UIState filterState = new ThingFilterUI.UIState();
        }

        private static readonly ConditionalWeakTable<Dialog_ManageApparelPolicies, DialogState> States =
            new ConditionalWeakTable<Dialog_ManageApparelPolicies, DialogState>();
        private static readonly FieldInfo PolicyField =
            AccessTools.Field(typeof(Dialog_ManagePolicies<ApparelPolicy>), "policyInt");
        private static readonly ThingFilter WeaponParentFilter = CreateWeaponParentFilter();

        public static bool Prefix(Dialog_ManageApparelPolicies __instance, ref Rect rect)
        {
            var state = States.GetOrCreateValue(__instance);
            var tabs = new Rect(rect.x, rect.y, rect.width, 30f);
            if (Widgets.ButtonText(tabs.LeftHalf().ContractedBy(2f), "VCO_Policy_Apparel".Translate()))
            {
                state.weapons = false;
            }
            if (Widgets.ButtonText(tabs.RightHalf().ContractedBy(2f), "VCO_Policy_Weapons".Translate()))
            {
                state.weapons = true;
            }
            rect.yMin += 34f;

            if (!state.weapons)
            {
                return true;
            }
            var policy = PolicyField?.GetValue(__instance) as ApparelPolicy;
            var filter = AutoEquipPolicyComponent.Current?.FilterFor(policy);
            if (filter != null)
            {
                ThingFilterUI.DoThingFilterConfigWindow(rect, state.filterState, filter,
                    WeaponParentFilter, 16);
            }
            return false;
        }

        private static ThingFilter CreateWeaponParentFilter()
        {
            var filter = new ThingFilter(ThingCategoryDefOf.Weapons);
            filter.allowedHitPointsConfigurable = true;
            filter.allowedQualitiesConfigurable = true;
            filter.SetAllow(ThingCategoryDefOf.Weapons, true);
            return filter;
        }
    }

    [HarmonyPatch(typeof(ApparelPolicy), nameof(ApparelPolicy.CopyFrom))]
    public static class Patch_ApparelPolicy_CopyFrom
    {
        public static void Postfix(ApparelPolicy __instance, Policy other)
        {
            AutoEquipPolicyComponent.Current?.CopyPolicy(__instance, other as ApparelPolicy);
        }
    }

    [HarmonyPatch(typeof(JobDriver_Equip), nameof(JobDriver_Equip.Notify_Starting))]
    public static class Patch_JobDriverEquip_NotifyStarting
    {
        public static void Postfix(JobDriver_Equip __instance)
        {
            if (__instance.job?.playerForced == true && __instance.job.targetA.Thing != null)
            {
                AutoEquipPolicyComponent.Current?.ForceWeapon(__instance.pawn, __instance.job.targetA.Thing);
            }
        }
    }

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
            if (VCOMod.Settings?.enableAutoEquip == true
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
