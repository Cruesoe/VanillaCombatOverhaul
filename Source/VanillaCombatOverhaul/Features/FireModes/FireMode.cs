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
    /// Tuning for one fire mode. Accuracy divides the distance exponent of the shooter factor
    /// (1.5 aims as if the target were at two thirds of the range); burst size is scaled, then capped.
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
