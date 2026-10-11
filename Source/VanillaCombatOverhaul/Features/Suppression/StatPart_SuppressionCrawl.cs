using RimWorld;
using Verse;

namespace VanillaCombatOverhaul
{
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
