using RimWorld;
using Verse;

namespace VanillaCombatOverhaul
{
    /// <summary>Explains how much suppression this pawn takes to be suppressed and pinned; leaves the value alone.</summary>
    public class StatPart_SuppressionThreshold : StatPart
    {
        public override void TransformValue(StatRequest req, ref float val)
        {
        }

        public override string ExplanationPart(StatRequest req)
        {
            if (!(req.Thing is Pawn pawn) || SuppressionUtility.CompFor(pawn) == null)
            {
                return null;
            }
            var threshold = SuppressionUtility.ThresholdFor(pawn);
            return "VCO_StatPart_SuppressionThreshold".Translate(threshold.ToString("0"),
                (threshold * SuppressionUtility.PinnedLevel).ToString("0"));
        }
    }
}
