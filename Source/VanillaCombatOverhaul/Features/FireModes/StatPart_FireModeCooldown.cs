namespace VanillaCombatOverhaul
{
    public class StatPart_FireModeCooldown : StatPart_FireMode
    {
        protected override float FactorFor(FireModeTuning tuning) => tuning.cooldown;
    }
}
