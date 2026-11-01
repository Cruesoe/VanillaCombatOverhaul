using HarmonyLib;
using RimWorld;
using Verse;

namespace VanillaCombatOverhaul
{
    /// <summary>
    /// Shot readout: vanilla's lines with the raw factors restored, then the skill-adjusted factors,
    /// fire mode, suppression, evasion, incoming side and targeted-height chance appended.
    /// </summary>
    [HarmonyPatch(typeof(ShotReport), nameof(ShotReport.GetTextReadout))]
    public static class Patch_ShotReport_GetTextReadout
    {
        public static void Prefix(ref ShotReport __instance, ref CombatContext.Scope __state)
        {
            ReadoutScratch.Begin(ref __instance);
            // The readout has no shooter of its own, and evasion's skill contest needs one to match the real roll.
            var caster = Find.Selector.SingleSelectedThing;
            __state = caster == null ? default : CombatContext.PushShot(caster, (caster as Pawn)?.CurrentEffectiveVerb);
        }

        public static void Finalizer(ref CombatContext.Scope __state) => __state.Dispose();

        public static void Postfix(ref ShotReport __instance, ref string __result)
        {
            var extras = ReadoutScratch.End(ref __instance);
            if (extras.NullOrEmpty())
            {
                return;
            }
            __result += extras;
        }
    }
}
