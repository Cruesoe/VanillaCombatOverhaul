using System;
using System.Collections.Generic;
using System.Reflection;
using HarmonyLib;
using Verse;

namespace VanillaCombatOverhaul
{
    /// <summary>Redirects hit location to the side an attack arrives from, on every vanilla ChooseHitPart.</summary>
    [HarmonyPatch]
    public static class Patch_ChooseHitPart
    {
        public static IEnumerable<MethodBase> TargetMethods()
        {
            // The base covers every worker that does not override; the subclasses below do.
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
            // Null keeps vanilla's choice.
        }
    }
}
