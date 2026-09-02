using System.Collections.Generic;

namespace VanillaCombatOverhaul
{
    /// <summary>
    /// Parameters for one controlled melee test.
    ///
    /// The point of fixing skills and weapons is that it lets a run verify a *specific cell*
    /// of the balance table rather than an aggregate that averages several matchups together.
    /// </summary>
    public class ArenaSpec
    {
        public string label = "default";

        public int pairs = 20;

        /// <summary>
        /// Attackers set on each defender. One is a duel; more than one is the case the parry
        /// budget exists for, and the only way to exercise it -- a lone attacker swings far too
        /// slowly to ever spend a budget of two per window.
        /// </summary>
        public int attackersPerDefender = 1;

        /// <summary>
        /// Overrides the parry budget for this run only; 0 leaves the configured value alone.
        /// Running the same crowd twice, once with the cap lifted, is the only way to separate
        /// what the budget contributes from what the facing gate was already doing.
        /// </summary>
        public int parryBudgetOverride = 0;
        public int attackerMeleeSkill = 10;
        public int defenderMeleeSkill = 10;

        public string attackerWeapon = "MeleeWeapon_LongSword";
        public string defenderWeapon = "MeleeWeapon_LongSword";

        /// <summary>Give the defender nothing to hold, to confirm the no-weapon gate.</summary>
        public bool defenderUnarmed = false;

        public int ticks = 4000;

        /// <summary>
        /// How often combatants are healed and re-ordered to attack. Without this they die and
        /// the sample stops growing; a short interval keeps volume up, which is the whole
        /// reason for automating this.
        /// </summary>
        public int refreshEveryTicks = 120;

        /// <summary>Non-zero pins the RNG, making a run reproducible for regression checks.</summary>
        public int seed = 0;

        public ArenaSpec Clone() => (ArenaSpec)MemberwiseClone();

        public IEnumerable<KeyValuePair<string, string>> Describe()
        {
            yield return new KeyValuePair<string, string>("label", label);
            yield return new KeyValuePair<string, string>("pairs", pairs.ToString());
            yield return new KeyValuePair<string, string>("attackersPerDefender", attackersPerDefender.ToString());
            yield return new KeyValuePair<string, string>("parryBudgetOverride", parryBudgetOverride.ToString());
            yield return new KeyValuePair<string, string>("attackerMeleeSkill", attackerMeleeSkill.ToString());
            yield return new KeyValuePair<string, string>("defenderMeleeSkill", defenderMeleeSkill.ToString());
            yield return new KeyValuePair<string, string>("attackerWeapon", attackerWeapon ?? "none");
            yield return new KeyValuePair<string, string>("defenderWeapon", defenderUnarmed ? "none" : (defenderWeapon ?? "none"));
            yield return new KeyValuePair<string, string>("ticks", ticks.ToString());
            yield return new KeyValuePair<string, string>("seed", seed.ToString());
        }
    }

    public class ArenaResult
    {
        public ArenaSpec Spec;
        public Dictionary<string, long> Counters = new Dictionary<string, long>();
        public Dictionary<string, VCODiagnostics.Reading> Readings = new Dictionary<string, VCODiagnostics.Reading>();
        public List<AssertionResult> Assertions = new List<AssertionResult>();
        public int PawnsSpawned;
        public int AttackersSpawned;
        public int DefendersSpawned;
        public int PawnsDied;

        public long Counter(string key)
        {
            Counters.TryGetValue(key, out var v);
            return v;
        }

        public double ReadingAverage(string key) =>
            Readings.TryGetValue(key, out var r) ? r.Average : 0d;

        public bool AllAssertionsPassed => !Assertions.Exists(a => !a.Passed);
    }

    public class AssertionResult
    {
        public string Name;
        public bool Passed;
        public string Detail;

        public override string ToString() => (Passed ? "PASS  " : "FAIL  ") + Name + "  " + Detail;
    }
}
