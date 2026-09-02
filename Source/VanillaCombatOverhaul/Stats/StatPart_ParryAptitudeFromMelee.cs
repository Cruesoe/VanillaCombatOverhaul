using RimWorld;
using Verse;

namespace VanillaCombatOverhaul
{
    /// <summary>
    /// A pawn's parry aptitude is their melee ability.
    ///
    /// Deliberately read from the vanilla MeleeHitChance stat rather than the Melee skill
    /// record, so traits, hediffs, genes, bionics, age and anything a mod adds all feed in
    /// without knowing this mod exists.
    /// </summary>
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
