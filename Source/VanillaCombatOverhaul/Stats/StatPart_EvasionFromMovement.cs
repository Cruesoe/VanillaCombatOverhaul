using RimWorld;
using UnityEngine;
using Verse;

namespace VanillaCombatOverhaul
{
    /// <summary>Evasion from current movement; zero while stationary.</summary>
    public class StatPart_EvasionFromMovement : StatPart
    {
        public override void TransformValue(StatRequest req, ref float val)
        {
            if (!(req.Thing is Pawn pawn))
            {
                return;
            }

            var settings = VCOMod.Settings;
            if (settings == null || !settings.enableEvasion)
            {
                val = 0f;
                return;
            }

            var mult = EvasionUtility.RawMovementMultiplier(
                pawn, settings.evasionFactor, settings.evasionMinSpeed);
            val = Mathf.Clamp01(1f - mult);
        }

        public override string ExplanationPart(StatRequest req)
        {
            if (!(req.Thing is Pawn pawn) || pawn.pather == null || !pawn.pather.MovingNow)
            {
                return null;
            }

            return "VCO_StatPart_Moving".Translate();
        }
    }
}
