using Verse;

namespace VanillaCombatOverhaul
{
    /// <summary>
    /// The caster and verb of the shot being worked out, for code that only receives a ShotReport
    /// or stat request. Pushed for a scope and popped on dispose; reads outside a scope return false.
    /// </summary>
    public readonly struct ShotContext
    {
        public readonly Thing Caster;
        public readonly Verb Verb;

        public ShotContext(Thing caster, Verb verb)
        {
            Caster = caster;
            Verb = verb;
        }

        public bool IsValid => Caster != null;
    }
}
