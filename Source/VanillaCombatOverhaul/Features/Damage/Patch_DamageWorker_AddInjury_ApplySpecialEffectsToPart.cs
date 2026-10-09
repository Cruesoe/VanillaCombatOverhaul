using System.Collections.Generic;
using HarmonyLib;
using RimWorld;
using UnityEngine;
using Verse;

namespace VanillaCombatOverhaul
{
    [HarmonyPatch(typeof(DamageWorker_AddInjury), "ApplySpecialEffectsToPart")]
    public static class Patch_DamageWorker_AddInjury_ApplySpecialEffectsToPart
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

        // Reused part list; a nested damage call while it is in use gets its own list.
        private static readonly List<BodyPartRecord> PartsBuffer = new List<BodyPartRecord>(16);
        private static bool partsBufferInUse;

        private static List<BodyPartRecord> RentParts()
        {
            if (partsBufferInUse)
            {
                return new List<BodyPartRecord>(16);
            }
            partsBufferInUse = true;
            PartsBuffer.Clear();
            return PartsBuffer;
        }

        private static void ReturnParts(List<BodyPartRecord> parts)
        {
            if (parts == PartsBuffer)
            {
                PartsBuffer.Clear();
                partsBufferInUse = false;
            }
        }

        private static void ApplyBullet(DamageWorker_AddInjury worker, Pawn pawn, float totalDamage,
                                        DamageInfo dinfo, DamageWorker.DamageResult result, float cap)
        {
            totalDamage /= 2f;
            FinalizeAndAddInjury(worker, pawn, totalDamage, dinfo, result);

            var stoppingPower = ProjectileWoundUtility.StoppingPowerFor(dinfo);
            var kind = ProjectileWoundUtility.KindFor(stoppingPower);
            var parts = RentParts();
            try
            {
                if (kind == BulletWoundKind.Fragment)
                {
                    var extra = GenMath.RoundRandom(ProjectileWoundUtility.FragmentTargets.Evaluate(Rand.Value));
                    if (extra > 0)
                    {
                        ProjectileWoundUtility.AddNearbyParts(dinfo.HitPart, parts);
                        ProjectileWoundUtility.ShuffleFront(parts, extra);
                        if (parts.Count > extra)
                        {
                            parts.RemoveRange(extra, parts.Count - extra);
                        }
                    }
                    VCODiagnostics.Count("wound.bullet.fragment");
                }
                else
                {
                    totalDamage *= Mathf.Min(stoppingPower, Mathf.Max(cap, 1f));
                    if (kind == BulletWoundKind.PassThrough)
                    {
                        ProjectileWoundUtility.AddPassThroughChain(dinfo.HitPart, parts);
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
                    FinalizeAndAddInjury(worker, pawn, share, slice, result);
                }
            }
            finally
            {
                ReturnParts(parts);
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

            BodyPartRecord neighbor = null;
            var parts = RentParts();
            try
            {
                ProjectileWoundUtility.AddNearbyExternalParts(pawn, dinfo.HitPart, parts);
                if (parts.Count > 0)
                {
                    neighbor = parts[Rand.Range(0, parts.Count)];
                }
            }
            finally
            {
                ReturnParts(parts);
            }

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
