using RimWorld;
using Verse;

namespace VanillaCombatOverhaul
{
    /// <summary>Fire mode factor on a pawn stat, for the verb being cast or, outside a cast, the primary weapon.</summary>
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
