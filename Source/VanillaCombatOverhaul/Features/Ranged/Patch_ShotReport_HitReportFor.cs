using HarmonyLib;
using Verse;

namespace VanillaCombatOverhaul
{
    [HarmonyPatch(typeof(ShotReport), nameof(ShotReport.HitReportFor))]
    public static class Patch_ShotReport_HitReportFor
    {
        public static void Prefix(Thing caster, Verb verb, ref CombatContext.Scope __state) =>
            __state = CombatContext.PushShot(caster, verb);

        public static void Finalizer(ref CombatContext.Scope __state) =>
            __state.Dispose();

        public static void Postfix(ref ShotReport __result, Thing caster, Verb verb)
        {
            PenaltyMitigationUtility.ApplyToShotReport(ref __result, caster);
            FireModeUtility.ApplyToShotReport(ref __result, caster, verb);
            SuppressionUtility.ApplyToShotReport(ref __result, caster);
        }
    }
}
