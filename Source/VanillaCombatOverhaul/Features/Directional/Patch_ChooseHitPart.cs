using System;
using System.Collections.Generic;
using System.Reflection;
using HarmonyLib;
using Verse;

namespace VanillaCombatOverhaul
{
    /// <summary>
    /// Redirects hit location to the side an attack arrives from.
    ///
    /// Vanilla Combat Reloaded achieved this by replacing the workerClass on the Bullet and
    /// Arrow DamageDefs and transpiling the five melee workers. Replacing a workerClass claims
    /// ownership of a vanilla def, so any other mod wanting the same def loses -- and both
    /// Bullet and Arrow turn out to use DamageWorker_AddInjury directly, which means a postfix
    /// on the base method covers them with no def edit at all.
    ///
    /// Six methods, six postfixes, zero defs rewritten and no IL rewritten.
    /// </summary>
    [HarmonyPatch]
    public static class Patch_ChooseHitPart
    {
        public static IEnumerable<MethodBase> TargetMethods()
        {
            // DamageWorker_AddInjury covers Bullet, Arrow, Bomb, Burn and everything else that
            // does not override; the five subclasses below each provide their own.
            yield return Target(typeof(DamageWorker_AddInjury));
            yield return Target(typeof(DamageWorker_Bite));
            yield return Target(typeof(DamageWorker_Blunt));
            yield return Target(typeof(DamageWorker_Cut));
            yield return Target(typeof(DamageWorker_Scratch));
            yield return Target(typeof(DamageWorker_Stab));
        }

        private static MethodBase Target(System.Type type) =>
            AccessTools.DeclaredMethod(type, "ChooseHitPart",
                new[] { typeof(DamageInfo), typeof(Pawn) })
            ?? throw new MissingMethodException(
                $"{type.Name}.ChooseHitPart(DamageInfo, Pawn) not found. " +
                "Vanilla Combat cannot apply directional damage against this RimWorld build.");

        public static void Postfix(ref BodyPartRecord __result, DamageInfo dinfo, Pawn pawn)
        {
            var directional = DirectionalHitUtility.TryPickDirectional(dinfo, pawn);
            if (directional != null)
            {
                __result = directional;
            }
            // Null means "no opinion" -- vanilla's choice stands, so this can never turn a
            // valid hit part into a null one.
        }
    }
}
