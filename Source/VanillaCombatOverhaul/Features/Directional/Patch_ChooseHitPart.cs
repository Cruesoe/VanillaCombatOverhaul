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
            foreach (var type in new[]
                     {
                         typeof(DamageWorker_AddInjury), typeof(DamageWorker_Bite), typeof(DamageWorker_Blunt),
                         typeof(DamageWorker_Cut), typeof(DamageWorker_Scratch), typeof(DamageWorker_Stab)
                     })
            {
                var method = AccessTools.DeclaredMethod(type, "ChooseHitPart", new[] { typeof(DamageInfo), typeof(Pawn) });
                if (method == null)
                {
                    Log.Error($"[VCO] {type.Name}.ChooseHitPart(DamageInfo, Pawn) not found; directional damage skips that worker.");
                    continue;
                }
                yield return method;
            }
        }

        public static void Postfix(DamageWorker __instance, ref BodyPartRecord __result, DamageInfo dinfo, Pawn pawn)
        {
            var directional = DirectionalHitUtility.TryPickDirectional(dinfo, pawn);
            if (directional == null)
            {
                return;   // Keeps vanilla's choice.
            }
            // Stabs keep vanilla's chance of going straight through to an inner part of the chosen one.
            if (__instance is DamageWorker_Stab && directional.depth != BodyPartDepth.Inside
                && Rand.Chance(__instance.def.stabChanceOfForcedInternal))
            {
                var inner = pawn.health.hediffSet.GetRandomNotMissingPart(
                    dinfo.Def, BodyPartHeight.Undefined, BodyPartDepth.Inside, directional);
                if (inner != null)
                {
                    directional = inner;
                }
            }
            __result = directional;
        }
    }
}
