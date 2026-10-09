using HarmonyLib;
using RimWorld;
using Verse;

namespace VanillaCombatOverhaul
{
    /// <summary>Marks the span of a TryMeleeAttack call, so the melee cast inside it is recognised.</summary>
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

    /// <summary>Replaces the melee cast with a point-blank shot when the roll succeeds; the melee verb then does not run.</summary>
    [HarmonyPatch(typeof(Verb), nameof(Verb.TryStartCastOn), new[]
    {
        typeof(LocalTargetInfo), typeof(LocalTargetInfo), typeof(bool), typeof(bool), typeof(bool), typeof(bool)
    })]
    public static class Patch_Verb_TryStartCastOn_PointBlank
    {
        public static bool Prefix(Verb __instance, LocalTargetInfo castTarg, ref bool __result)
        {
            if (!(__instance is Verb_MeleeAttack) || !PointBlankUtility.Enabled)
            {
                return true;
            }
            if (!PointBlankUtility.TryReplace(__instance, castTarg))
            {
                return true;
            }
            __result = true;
            return false;
        }
    }

    /// <summary>Zero warmup for a point-blank cast only; everything else reads the weapon's real warmup.</summary>
    [HarmonyPatch(typeof(Verb), nameof(Verb.WarmupTime), MethodType.Getter)]
    public static class Patch_Verb_WarmupTime
    {
        public static void Postfix(Verb __instance, ref float __result)
        {
            if (__result > 0f && PointBlankUtility.IsPointBlankCast(__instance))
            {
                __result = 0f;
            }
        }
    }

    /// <summary>Lifts vanilla's adjacent-target minimum range for point-blank casts; the weapon's own minimum range still applies.</summary>
    [HarmonyPatch(typeof(Verb), nameof(Verb.OutOfRange))]
    public static class Patch_Verb_OutOfRange
    {
        public static void Postfix(Verb __instance, IntVec3 root, CellRect occupiedRect, ref bool __result)
        {
            if (!__result || !PointBlankUtility.IsPointBlankCast(__instance))
            {
                return;
            }
            var minRange = __instance.verbProps.EffectiveMinRange(allowAdjacentShot: true);
            var range = __instance.EffectiveRange;
            float distSquared = occupiedRect.ClosestDistSquaredTo(root);
            __result = distSquared > range * range || distSquared < minRange * minRange;
        }
    }

    /// <summary>Lifts the adjacent-threat block on launching projectiles for point-blank casts; fuel, charges and roles still apply.</summary>
    [HarmonyPatch(typeof(Verb_LaunchProjectile), nameof(Verb_LaunchProjectile.Available))]
    public static class Patch_Verb_LaunchProjectile_Available
    {
        public static void Postfix(Verb_LaunchProjectile __instance, ref bool __result)
        {
            if (__result || !PointBlankUtility.IsPointBlankCast(__instance))
            {
                return;
            }
            __result = VerbBaseAvailable.Available(__instance) && __instance.Projectile != null;
        }
    }

    /// <summary>Verb.Available as a non-virtual call, which C# cannot express directly.</summary>
    [HarmonyPatch(typeof(Verb), nameof(Verb.Available))]
    public static class VerbBaseAvailable
    {
        [HarmonyReversePatch]
        public static bool Available(Verb instance) =>
            throw new System.NotImplementedException("Replaced by Harmony at startup.");
    }

    /// <summary>Ends burst tracking once a point-blank burst has fired its last shot.</summary>
    [HarmonyPatch(typeof(Verb), "TryCastNextBurstShot")]
    public static class Patch_Verb_TryCastNextBurstShot_PointBlank
    {
        public static void Postfix(Verb __instance) => PointBlankUtility.NotifyBurstProgress(__instance);
    }

    /// <summary>Ends burst tracking when a burst is abandoned: stun, despawn, job change.</summary>
    [HarmonyPatch(typeof(Verb), nameof(Verb.Reset))]
    public static class Patch_Verb_Reset_PointBlank
    {
        public static void Postfix(Verb __instance) => PointBlankUtility.NotifyBurstProgress(__instance);
    }
}
