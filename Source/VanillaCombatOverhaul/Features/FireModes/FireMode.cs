using Verse;

namespace VanillaCombatOverhaul
{
    public enum FireMode : byte
    {
        Default,
        Precision,
        ShortBurst,
        Suppression
    }

    /// <summary>
    /// The numbers behind one fire mode.
    ///
    /// Accuracy is not a plain multiplier on hit chance. The shooter factor vanilla computes is
    /// accuracy-per-cell raised to the distance, so the mode divides that exponent instead:
    /// accuracy 1.5 aims as if the target were at two thirds of the range. That can never push
    /// a shot past certainty, and it bites hardest at long range where aiming matters.
    ///
    /// Burst size is scaled and then capped, so a minigun's 25-round burst does not balloon
    /// into 50 the way an assault rifle's 3 becomes 6.
    /// </summary>
    public class FireModeTuning : IExposable
    {
        public float accuracy = 1f;
        public float aimTime = 1f;
        public float cooldown = 1f;
        public float burstFactor = 1f;
        public int burstMaxChange;

        public FireModeTuning()
        {
        }

        public FireModeTuning(float accuracy, float aimTime, float cooldown, float burstFactor, int burstMaxChange)
        {
            this.accuracy = accuracy;
            this.aimTime = aimTime;
            this.cooldown = cooldown;
            this.burstFactor = burstFactor;
            this.burstMaxChange = burstMaxChange;
        }

        // Precision costs aim time and gains no cooldown, so it is a trade even for a
        // single-shot rifle that has no burst to give up.
        public static FireModeTuning PrecisionDefaults() => new FireModeTuning(1.5f, 1.3f, 1f, 0.67f, 10);

        public static FireModeTuning ShortBurstDefaults() => new FireModeTuning(0.85f, 0.8f, 0.9f, 1.5f, 3);

        public static FireModeTuning SuppressionDefaults() => new FireModeTuning(0.5f, 0.5f, 1.2f, 2f, 10);

        public void ExposeData()
        {
            Scribe_Values.Look(ref accuracy, nameof(accuracy), 1f);
            Scribe_Values.Look(ref aimTime, nameof(aimTime), 1f);
            Scribe_Values.Look(ref cooldown, nameof(cooldown), 1f);
            Scribe_Values.Look(ref burstFactor, nameof(burstFactor), 1f);
            Scribe_Values.Look(ref burstMaxChange, nameof(burstMaxChange), 0);
        }
    }
}
