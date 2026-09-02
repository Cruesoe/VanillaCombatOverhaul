using RimWorld;
using Verse;

namespace VanillaCombatOverhaul
{
    /// <summary>
    /// A pawn's accuracy mitigation aptitude is their shooting ability.
    /// </summary>
    public class StatPart_AccuracyMitigationFromShooting : StatPart
    {
        public override void TransformValue(StatRequest req, ref float val)
        {
            if (req.Thing is Pawn pawn)
            {
                val = StatDefOf.ShootingAccuracyPawn.Worker.GetValue(StatRequest.For(pawn), false);
            }
        }

        public override string ExplanationPart(StatRequest req)
        {
            if (!(req.Thing is Pawn pawn))
            {
                return null;
            }

            var stat = StatDefOf.ShootingAccuracyPawn.Worker.GetValue(StatRequest.For(pawn), false);
            return "VCO_StatPart_ShootingAbility".Translate()
                   + ": " + stat.ToString("0.##");
        }
    }
}
