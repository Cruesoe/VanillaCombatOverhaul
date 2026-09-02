using RimWorld;
using Verse;

namespace VanillaCombatOverhaul
{
    /// <summary>
    /// Applies Ideology's light-level melee offsets to parry aptitude: you cannot turn an
    /// attack you cannot see coming.
    ///
    /// Vanilla Combat Reloaded applied the same offsets inline inside its parry roll. Doing it
    /// as a StatPart instead means the penalty shows up in the pawn's Stats tab with a reason
    /// attached, rather than silently changing a number the player never sees.
    /// </summary>
    public class StatPart_ParryDarkness : StatPart
    {
        public override void TransformValue(StatRequest req, ref float val)
        {
            if (req.Thing is Pawn pawn)
            {
                val += Offset(pawn);
            }
        }

        public override string ExplanationPart(StatRequest req)
        {
            if (!(req.Thing is Pawn pawn))
            {
                return null;
            }
            var offset = Offset(pawn);
            if (offset == 0f)
            {
                return null;
            }
            return "VCO_StatPart_Lighting".Translate() + ": " + offset.ToStringPercent();
        }

        internal static float Offset(Pawn pawn)
        {
            if (!ModsConfig.IdeologyActive)
            {
                return 0f;
            }
            if (DarknessCombatUtility.IsOutdoorsAndLit(pawn))
            {
                return pawn.GetStatValue(StatDefOf.MeleeHitChanceOutdoorsLitOffset);
            }
            if (DarknessCombatUtility.IsOutdoorsAndDark(pawn))
            {
                return pawn.GetStatValue(StatDefOf.MeleeHitChanceOutdoorsDarkOffset);
            }
            if (DarknessCombatUtility.IsIndoorsAndDark(pawn))
            {
                return pawn.GetStatValue(StatDefOf.MeleeHitChanceIndoorsDarkOffset);
            }
            if (DarknessCombatUtility.IsIndoorsAndLit(pawn))
            {
                return pawn.GetStatValue(StatDefOf.MeleeHitChanceIndoorsLitOffset);
            }
            return 0f;
        }
    }
}
