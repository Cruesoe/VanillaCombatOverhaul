using RimWorld;
using Verse;

namespace VanillaCombatOverhaul
{
    /// <summary>Zero parry aptitude with nothing in hand.</summary>
    public class StatPart_ParryRequiresWeapon : StatPart
    {
        public override void TransformValue(StatRequest req, ref float val)
        {
            if (!(req.Thing is Pawn pawn))
            {
                return;
            }
            if (!HasParryingWeapon(pawn))
            {
                val = 0f;
            }
        }

        public override string ExplanationPart(StatRequest req)
        {
            if (!(req.Thing is Pawn pawn) || HasParryingWeapon(pawn))
            {
                return null;
            }
            return "VCO_StatPart_NoWeapon".Translate() + ": 0%";
        }

        private static bool HasParryingWeapon(Pawn pawn) =>
            pawn.equipment?.Primary != null;
    }
}
