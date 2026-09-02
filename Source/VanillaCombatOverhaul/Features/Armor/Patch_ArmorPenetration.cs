using System;
using System.Collections.Generic;
using System.Reflection;
using System.Text;
using HarmonyLib;
using RimWorld;
using Verse;

namespace VanillaCombatOverhaul
{
    /// <summary>
    /// Scales AP at the getters that fill both the inspect card and DamageInfo.
    ///
    /// <see cref="VerbProperties.AdjustedArmorPenetration(Verb, Pawn)"/> is not patched: it
    /// only forwards to the (Tool, Pawn, Thing, HediffComp_VerbGiver) overload, and a postfix
    /// on both would apply penetrationScale twice.
    /// </summary>
    [HarmonyPatch]
    public static class Patch_ArmorPenetration
    {
        public static IEnumerable<MethodBase> TargetMethods()
        {
            foreach (var method in new[]
            {
                AccessTools.Method(
                    typeof(ProjectileProperties),
                    nameof(ProjectileProperties.GetArmorPenetration),
                    new[] { typeof(Thing), typeof(StringBuilder) }),
                AccessTools.Method(
                    typeof(VerbProperties),
                    nameof(VerbProperties.AdjustedArmorPenetration),
                    new[] { typeof(Tool), typeof(Pawn), typeof(Thing), typeof(HediffComp_VerbGiver) }),
                AccessTools.Method(
                    typeof(VerbProperties),
                    nameof(VerbProperties.AdjustedArmorPenetration),
                    new[] { typeof(Tool), typeof(Pawn), typeof(ThingDef), typeof(ThingDef), typeof(HediffComp_VerbGiver) }),
                AccessTools.Method(
                    typeof(ExtraDamage),
                    nameof(ExtraDamage.AdjustedArmorPenetration),
                    Type.EmptyTypes),
                AccessTools.Method(
                    typeof(ExtraDamage),
                    nameof(ExtraDamage.AdjustedArmorPenetration),
                    new[] { typeof(Verb), typeof(Pawn) })
            })
            {
                if (method == null)
                {
                    Log.Error("[VCO] Armor penetration patch could not find a target method. " +
                              "Weapon inspect AP will not match the armor roll.");
                    continue;
                }
                yield return method;
            }
        }

        public static void Postfix(ref float __result) =>
            __result = AdvancedArmorUtility.ScaleDisplayedPenetration(__result);
    }
}
