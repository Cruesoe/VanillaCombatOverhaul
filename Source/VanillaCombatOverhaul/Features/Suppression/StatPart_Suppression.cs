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

    /// <summary>Slower movement while crawling under fire.</summary>
    public class StatPart_SuppressionCrawl : StatPart
    {
        public override void TransformValue(StatRequest req, ref float val)
        {
            if (req.Thing is Pawn pawn && SuppressionUtility.CrawlsWhenMoving(pawn))
            {
                val *= SuppressionUtility.CrawlSpeedFactor;
            }
        }

        public override string ExplanationPart(StatRequest req)
        {
            if (!(req.Thing is Pawn pawn) || !SuppressionUtility.CrawlsWhenMoving(pawn))
            {
                return null;
            }
            return "VCO_StatPart_Crawling".Translate() + ": x" + SuppressionUtility.CrawlSpeedFactor.ToStringPercent();
        }
    }
}
