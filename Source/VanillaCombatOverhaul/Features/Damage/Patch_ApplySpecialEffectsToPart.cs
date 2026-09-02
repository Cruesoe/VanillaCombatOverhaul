using System.Collections.Generic;
using System.Linq;
using HarmonyLib;
using RimWorld;
using UnityEngine;
using Verse;

namespace VanillaCombatOverhaul
{
    [HarmonyPatch(typeof(DamageWorker_AddInjury), "ApplySpecialEffectsToPart")]
    public static class Patch_ApplySpecialEffectsToPart
    {
        // 1.6 FinalizeAndAddInjury returns the remaining damage (float), not void.
        private delegate float FinalizeInjury(DamageWorker_AddInjury worker, Pawn pawn,
            float totalDamage, DamageInfo dinfo, DamageWorker.DamageResult result);

        private delegate float ReduceOutside(DamageWorker_AddInjury worker, float totalDamage,
            DamageInfo dinfo, Pawn pawn);

        private static readonly FinalizeInjury FinalizeAndAddInjury =
            AccessTools.MethodDelegate<FinalizeInjury>(
                AccessTools.DeclaredMethod(typeof(DamageWorker_AddInjury), "FinalizeAndAddInjury",
                    new[]
                    {
                        typeof(Pawn), typeof(float), typeof(DamageInfo),
                        typeof(DamageWorker.DamageResult)
                    }));

        private static readonly ReduceOutside ReduceDamageToPreserveOutsideParts =
            AccessTools.MethodDelegate<ReduceOutside>(
                AccessTools.DeclaredMethod(typeof(DamageWorker_AddInjury),
                    "ReduceDamageToPreserveOutsideParts"));

        public static bool Prefix(DamageWorker_AddInjury __instance, Pawn pawn, float totalDamage,
                                  DamageInfo dinfo, DamageWorker.DamageResult result)
        {
            if (result == null || result.diminished || dinfo.HitPart == null || pawn == null)
            {
                return true;
            }

            var settings = VCOMod.Settings;
            if (settings == null)
            {
                return true;
            }

            if (settings.enableBulletWorker && ProjectileWoundUtility.IsBulletDef(dinfo.Def))
            {
                ApplyBullet(__instance, pawn, totalDamage, dinfo, result, settings.bulletStoppingPowerCap);
                return false;
            }

            if (settings.enableArrowWorker && ProjectileWoundUtility.IsArrowDef(dinfo.Def))
            {
                ApplyArrow(__instance, pawn, totalDamage, dinfo, result);
                return false;
            }

            return true;
        }

        private static void ApplyBullet(DamageWorker_AddInjury worker, Pawn pawn, float totalDamage,
                                        DamageInfo dinfo, DamageWorker.DamageResult result, float cap)
        {
            totalDamage /= 2f;
            FinalizeAndAddInjury(worker, pawn, totalDamage, dinfo, result);

            var stoppingPower = ProjectileWoundUtility.StoppingPowerFor(dinfo);
            var kind = ProjectileWoundUtility.KindFor(stoppingPower);
            var parts = new List<BodyPartRecord>();

            if (kind == BulletWoundKind.Fragment)
            {
                var extra = GenMath.RoundRandom(ProjectileWoundUtility.FragmentTargets.Evaluate(Rand.Value));
                if (extra > 0)
                {
                    IEnumerable<BodyPartRecord> nearby = dinfo.HitPart.GetDirectChildParts();
                    if (dinfo.HitPart.parent != null)
                    {
                        nearby = nearby.Concat(dinfo.HitPart.parent);
                        if (dinfo.HitPart.parent.parent != null)
                        {
                            nearby = nearby.Concat(dinfo.HitPart.parent.GetDirectChildParts());
                        }
                    }

                    parts.AddRange(nearby.Except(dinfo.HitPart)
                        .InRandomOrder()
                        .Where(p => !p.def.conceptual)
                        .Take(extra));
                }

                VCODiagnostics.Count("wound.bullet.fragment");
            }
            else
            {
                totalDamage *= Mathf.Min(stoppingPower, Mathf.Max(cap, 1f));
                if (kind == BulletWoundKind.PassThrough)
                {
                    parts.AddRange(ProjectileWoundUtility.PassThroughChain(dinfo.HitPart));
                    totalDamage *= parts.Count;
                    VCODiagnostics.Count("wound.bullet.passThrough");
                }
                else
                {
                    VCODiagnostics.Count("wound.bullet.mushroom");
                }
            }

            if (!parts.Contains(dinfo.HitPart))
            {
                parts.Add(dinfo.HitPart);
            }

            var share = totalDamage / parts.Count;
            for (var i = 0; i < parts.Count; i++)
            {
                var slice = dinfo;
                slice.SetHitPart(parts[i]);
                if (slice.HitPart.depth == BodyPartDepth.Outside)
                {
                    FinalizeAndAddInjury(worker, pawn, share, slice, result);
                }
                else
                {
                    FinalizeAndAddInjury(worker, pawn, share, slice, result);
                }
            }
        }

        private static void ApplyArrow(DamageWorker_AddInjury worker, Pawn pawn, float totalDamage,
                                       DamageInfo dinfo, DamageWorker.DamageResult result)
        {
            if (dinfo.HitPart.depth == BodyPartDepth.Inside)
            {
                FinalizeAndAddInjury(worker, pawn, totalDamage, dinfo, result);
                VCODiagnostics.Count("wound.arrow.internal");
                return;
            }

            var neighbor = ProjectileWoundUtility.NearbyExternalParts(pawn, dinfo.HitPart)
                .RandomElementWithFallback();
            if (neighbor == null)
            {
                FinalizeAndAddInjury(worker, pawn,
                    ReduceDamageToPreserveOutsideParts(worker, totalDamage, dinfo, pawn),
                    dinfo, result);
                VCODiagnostics.Count("wound.arrow.single");
                return;
            }

            var split = totalDamage * ProjectileWoundUtility.ArrowScratchSplit;
            FinalizeAndAddInjury(worker, pawn,
                ReduceDamageToPreserveOutsideParts(worker, split, dinfo, pawn),
                dinfo, result);

            var second = dinfo;
            second.SetHitPart(neighbor);
            FinalizeAndAddInjury(worker, pawn,
                ReduceDamageToPreserveOutsideParts(worker, split, second, pawn),
                second, result);
            VCODiagnostics.Count("wound.arrow.split");
        }
    }
}
