using RimWorld;
using Verse;

namespace VanillaCombatOverhaul
{
    /// <summary>
    /// A pawn with nothing in hand has nothing to parry with.
    ///
    /// This is the pattern the whole mod uses: mechanics live in StatParts on real StatDefs
    /// rather than in formulas buried in a Harmony patch. The payoff is that the number shows
    /// up on the pawn's Stats tab with a readable explanation, other mods can add their own
    /// StatParts or offsets without patching us, and XML alone can retune it.
    /// </summary>
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
