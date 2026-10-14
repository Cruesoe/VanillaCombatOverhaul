using RimWorld;
using Verse;

namespace VanillaCombatOverhaul
{
    /// <summary>Sets parry aptitude to the pawn's MeleeHitChance, so everything that modifies that stat applies.</summary>
    public class StatPart_ParryAptitudeFromMelee : StatPart
    {
        public override void TransformValue(StatRequest req, ref float val)
        {
            if (req.Thing is Pawn pawn)
            {
                val = pawn.GetStatValue(StatDefOf.MeleeHitChance);
            }
        }

        public override string ExplanationPart(StatRequest req)
        {
            if (!(req.Thing is Pawn pawn))
            {
                return null;
            }
            return "VCO_StatPart_MeleeAbility".Translate()
                   + ": " + pawn.GetStatValue(StatDefOf.MeleeHitChance).ToStringPercent();
        }
    }
}
