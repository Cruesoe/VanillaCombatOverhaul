using RimWorld;
using UnityEngine;
using Verse;

namespace VanillaCombatOverhaul
{
    /// <summary>Slower aiming while suppressed.</summary>
    public class StatPart_SuppressionAimTime : StatPart
    {
        public override void TransformValue(StatRequest req, ref float val)
        {
            var level = SuppressionUtility.LevelOf(req.Thing as Pawn);
            if (level >= SuppressionUtility.SuppressedLevel)
            {
                val *= SuppressionUtility.AimTimeFor(level);
            }
        }

        public override string ExplanationPart(StatRequest req)
        {
            var level = SuppressionUtility.LevelOf(req.Thing as Pawn);
            if (level < SuppressionUtility.SuppressedLevel)
            {
                return null;
            }
            return "VCO_StatPart_Suppressed".Translate() + ": x" + SuppressionUtility.AimTimeFor(level).ToStringPercent();
        }
    }

    /// <summary>How strongly incoming fire suppresses a pawn, from their nerve and mood.</summary>
    public class StatPart_SuppressabilityFromMind : StatPart
    {
        public const float DefaultBreakThreshold = 0.35f;
        public const float ThresholdWeight = 2f;
        public const float MoodWeight = 1f;
        public const float Min = 0.5f;
        public const float Max = 1.5f;

        public static float FactorFor(float breakThreshold, float mood) =>
            Mathf.Clamp(1f + (breakThreshold - DefaultBreakThreshold) * ThresholdWeight + (0.5f - mood) * MoodWeight,
                        Min, Max);

        public override void TransformValue(StatRequest req, ref float val)
        {
            if (req.Thing is Pawn pawn)
            {
                val *= FactorFor(pawn);
            }
        }

        public override string ExplanationPart(StatRequest req)
        {
            if (!(req.Thing is Pawn pawn))
            {
                return null;
            }
            return "VCO_StatPart_NerveAndMood".Translate() + ": x" + FactorFor(pawn).ToStringPercent();
        }

        private static float FactorFor(Pawn pawn)
        {
            var threshold = pawn.mindState?.mentalBreaker != null
                ? pawn.GetStatValue(StatDefOf.MentalBreakThreshold, true, 60)
                : DefaultBreakThreshold;
            var mood = pawn.needs?.mood?.CurLevel ?? 0.5f;
            return FactorFor(threshold, mood);
        }
    }
}
