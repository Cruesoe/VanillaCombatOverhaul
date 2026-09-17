using HarmonyLib;
using RimWorld;
using Verse;

namespace VanillaCombatOverhaul
{
    /// <summary>
    /// Marks the span of a real melee attack, so a melee verb starting inside it is known to
    /// be that attack and not some other caller's.
    /// </summary>
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

    /// <summary>
    /// Replaces the melee cast with a point-blank shot when the roll succeeds. Returning false
    /// skips the melee verb entirely, so it grants no melee XP and cannot be parried; the
    /// caller ignores this method's result, so the attack still counts as made.
    /// </summary>
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

    /// <summary>
    /// A point-blank shot has no aiming period. Scoped to the one cast, so info cards, AI and
    /// other mods keep reading the weapon's real warmup. Verb_LaunchProjectile multiplies the
    /// base value, so zero here stays zero there.
    /// </summary>
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

    /// <summary>
    /// First half of vanilla's melee lock: no pawn may fire a projectile at an adjacent,
    /// standing hostile, enforced as a 1.421-cell minimum range. A point-blank shot is the
    /// exception to exactly that rule, so only the adjacency part is lifted; the weapon's own
    /// minimum range still applies.
    /// </summary>
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

    /// <summary>
    /// Second half of the melee lock: a non-player pawn with an adjacent melee threat cannot
    /// launch projectiles at all. Lifted for point-blank casts by re-running the rest of
    /// Available without that clause, so fuel, charges, roles and a missing projectile still
    /// make the weapon unusable.
    /// </summary>
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
