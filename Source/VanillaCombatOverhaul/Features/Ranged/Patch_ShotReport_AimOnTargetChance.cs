using HarmonyLib;
using Verse;

namespace VanillaCombatOverhaul
{
    [HarmonyPatch(typeof(ShotReport), "AimOnTargetChance_IgnoringPosture", MethodType.Getter)]
    public static class Patch_ShotReport_AimOnTargetChance
    {
        public static void Postfix(ref ShotReport __instance, ref float __result)
        {
            var settings = VCOMod.Settings;
            if (settings == null || !settings.enableEvasion)
            {
                return;
            }

            var target = ShotReportAccess.GetTarget(ref __instance);
            if (!(target.Thing is Pawn targetPawn))
            {
                return;
            }

            Thing shooter = null;
            if (CombatContext.TryGetShot(out var ctx))
            {
                shooter = ctx.Caster;
            }

            __result *= EvasionUtility.HitChanceMultiplier(targetPawn, shooter);
        }
    }
}
