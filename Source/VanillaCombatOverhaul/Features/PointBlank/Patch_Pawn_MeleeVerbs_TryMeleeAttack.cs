using HarmonyLib;
using RimWorld;
using Verse;

namespace VanillaCombatOverhaul
{
    /// <summary>Marks the span of a TryMeleeAttack call, so the melee cast inside it is recognized.</summary>
    [HarmonyPatch(typeof(Pawn_MeleeVerbs), nameof(Pawn_MeleeVerbs.TryMeleeAttack))]
    public static class Patch_Pawn_MeleeVerbs_TryMeleeAttack
    {
        public static void Prefix(Pawn_MeleeVerbs __instance, Thing target,
                                  out PointBlankUtility.MeleeAttackScope __state)
        {
            __state = PointBlankUtility.Enabled
                ? PointBlankUtility.BeginMeleeAttack(__instance.Pawn, target)
                : default;
        }

        public static void Finalizer(PointBlankUtility.MeleeAttackScope __state) => __state.Dispose();
    }
}
