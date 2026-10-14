using System.Collections.Generic;
using System.Reflection;
using HarmonyLib;
using RimWorld;

namespace VanillaCombatOverhaul
{
    /// <summary>Hides the Assign tab columns whose settings loadouts now drive, while loadouts are on.</summary>
    [HarmonyPatch]
    public static class Patch_PawnColumnWorker_VisibleCurrently
    {
        public static IEnumerable<MethodBase> TargetMethods()
        {
            yield return AccessTools.PropertyGetter(typeof(PawnColumnWorker), nameof(PawnColumnWorker.VisibleCurrently));
            // Progression: Ammunition's ammo column overrides the getter.
            var ammoColumn = Verse.ModsConfig.IsActive(LoadoutStockUtility.AmmunitionPackageId)
                ? AccessTools.TypeByName("ProgressionAmmunition.PawnColumnWorker_CarryAmmo")
                : null;
            var getter = ammoColumn == null ? null : AccessTools.DeclaredPropertyGetter(ammoColumn, "VisibleCurrently");
            if (getter != null)
            {
                yield return getter;
            }
        }

        public static void Postfix(PawnColumnWorker __instance, ref bool __result)
        {
            if (__result && LoadoutUtility.Enabled && LoadoutStockUtility.IsReplacedColumn(__instance.def))
            {
                __result = false;
            }
        }
    }
}
