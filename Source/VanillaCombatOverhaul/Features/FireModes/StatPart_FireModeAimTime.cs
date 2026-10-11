namespace VanillaCombatOverhaul
{
    public class StatPart_FireModeAimTime : StatPart_FireMode
    {
        protected override float FactorFor(FireModeTuning tuning) => tuning.aimTime;
    }
}
