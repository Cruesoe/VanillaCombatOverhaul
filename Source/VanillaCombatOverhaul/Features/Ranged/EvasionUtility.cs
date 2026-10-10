using RimWorld;
using UnityEngine;
using Verse;

namespace VanillaCombatOverhaul
{
    public static class EvasionUtility
    {
        /// <summary>
        /// Multiplier applied to aim-on-target chance. 1.0 means no evasion; lower means harder to hit.
        /// </summary>
        public static float HitChanceMultiplier(Pawn target, Thing shooter)
        {
            var settings = VCOMod.Settings;
            if (settings == null || !settings.enableEvasion || target == null)
            {
                return 1f;
            }

            VCODiagnostics.Count("evasion.considered");

            var velocity = MovementVelocity(target);
            var excess = Mathf.Max(velocity - settings.evasionMinSpeed, 0f);
            if (excess <= 0f)
            {
                VCODiagnostics.Count("evasion.stationary");
                return 1f;
            }

            var baseFactor = Mathf.Clamp(settings.evasionFactor, 0.01f, 1f);
            var rawMult = Mathf.Pow(baseFactor, excess);

            // The VCO_Evasion stat, which other mods can modify, can raise evasion further.
            var statReduction = target.GetStatValue(VCO_StatDefOf.VCO_Evasion);
            if (statReduction > 0f)
            {
                rawMult = Mathf.Min(rawMult, 1f - statReduction);
            }

            if (settings.evasionSkillContest && shooter != null && settings.enableAdvancedAccuracy)
            {
                rawMult = PenaltyMitigationUtility.MitigatePenalty(rawMult, shooter, settings.accuracyScale);
            }

            VCODiagnostics.Count("evasion.applied");
            VCODiagnostics.Sample("evasion.multiplier", rawMult);
            return rawMult;
        }

        /// <summary>
        /// Movement speed used for evasion. Matches Vanilla Combat Reloaded's velocity model.
        /// </summary>
        public static float MovementVelocity(Pawn pawn)
        {
            if (pawn?.pather == null || !pawn.pather.MovingNow)
            {
                return 0f;
            }

            var path = pawn.pather.nextCellCostTotal;
            if (path <= 0f)
            {
                return 0f;
            }

            var speedFactor = 1f;
            if (pawn.stances?.stagger != null && pawn.stances.stagger.Staggered)
            {
                speedFactor *= pawn.stances.stagger.StaggerMoveSpeedFactor;
            }

            var minFactor = path / 450f;
            if (speedFactor < minFactor)
            {
                speedFactor = minFactor;
            }

            return 60f * speedFactor / path;
        }

        /// <summary>
        /// Raw movement multiplier before stat cap and shooter contest.
        /// </summary>
        public static float RawMovementMultiplier(Pawn pawn, float evasionFactor, float minSpeed)
        {
            var excess = Mathf.Max(MovementVelocity(pawn) - minSpeed, 0f);
            if (excess <= 0f)
            {
                return 1f;
            }

            return Mathf.Pow(Mathf.Clamp(evasionFactor, 0.01f, 1f), excess);
        }
    }
}
