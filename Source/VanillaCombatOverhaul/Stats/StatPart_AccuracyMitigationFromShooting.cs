using RimWorld;
using Verse;

namespace VanillaCombatOverhaul
{
    /// <summary>Sets accuracy mitigation to the pawn's shooting accuracy; left unchanged for pawns that cannot shoot.</summary>
    public class StatPart_AccuracyMitigationFromShooting : StatPart
    {
        public override void TransformValue(StatRequest req, ref float val)
        {
            if (ShootingAccuracyOf(req, out var accuracy))
            {
                val = accuracy;
            }
        }

        public override string ExplanationPart(StatRequest req)
        {
            if (!ShootingAccuracyOf(req, out var accuracy))
            {
                return null;
            }
            return "VCO_StatPart_ShootingAbility".Translate() + ": " + accuracy.ToString("0.##");
        }

        // Reading a disabled stat logs a consistency error, so pawns incapable of shooting are skipped.
        private static bool ShootingAccuracyOf(StatRequest req, out float accuracy)
        {
            accuracy = 0f;
            if (!(req.Thing is Pawn pawn))
            {
                return false;
            }
            var worker = StatDefOf.ShootingAccuracyPawn.Worker;
            if (worker.IsDisabledFor(pawn))
            {
                return false;
            }
            accuracy = worker.GetValue(StatRequest.For(pawn), false);
            return true;
        }
    }
}
