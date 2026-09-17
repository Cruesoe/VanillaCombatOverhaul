using RimWorld;
using Verse;

namespace VanillaCombatOverhaul
{
    /// <summary>
    /// Fire mode effect on a pawn stat, shown in the stat's explanation.
    ///
    /// During a cast the verb being fired decides, via the shot context the verb patches
    /// push, so a psycast from a pawn in Precision is not slowed down. Outside a cast (the
    /// Stats tab) the primary weapon stands in.
    /// </summary>
    public abstract class StatPart_FireMode : StatPart
    {
        protected abstract float FactorFor(FireModeTuning tuning);

        public override void TransformValue(StatRequest req, ref float val)
        {
            var tuning = TuningFor(req, out _);
            if (tuning != null)
            {
                val *= FactorFor(tuning);
            }
        }

        public override string ExplanationPart(StatRequest req)
        {
            var tuning = TuningFor(req, out var mode);
            if (tuning == null)
            {
                return null;
            }
            return "VCO_StatPart_FireMode".Translate(FireModeUtility.LabelFor(mode))
                   + ": x" + FactorFor(tuning).ToStringPercent();
        }

        private static FireModeTuning TuningFor(StatRequest req, out FireMode mode)
        {
            mode = FireMode.Default;
            // Gated before the verb is looked up: these stats are read uncached, for every
            // pawn, and most are undrafted colonists no mode applies to.
            if (!(req.Thing is Pawn pawn) || !FireModeUtility.ModesApplyTo(pawn))
            {
                return null;
            }

            var verb = CombatContext.TryGetShot(out var ctx) && ctx.Caster == pawn
                ? ctx.Verb
                : FireModeUtility.PrimaryVerb(pawn);
            mode = FireModeUtility.ActiveMode(pawn, verb);
            return mode == FireMode.Default ? null : FireModeUtility.TuningFor(mode);
        }
    }

    public class StatPart_FireModeAimTime : StatPart_FireMode
    {
        protected override float FactorFor(FireModeTuning tuning) => tuning.aimTime;
    }

    public class StatPart_FireModeCooldown : StatPart_FireMode
    {
        protected override float FactorFor(FireModeTuning tuning) => tuning.cooldown;
    }
}
