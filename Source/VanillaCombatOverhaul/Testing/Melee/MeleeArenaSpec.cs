using System.Collections.Generic;

namespace VanillaCombatOverhaul
{
    /// <summary>Parameters for one melee arena scenario, with fixed skills and weapons.</summary>
    public class MeleeArenaSpec
    {
        public string label = "default";

        public int pairs = 20;

        /// <summary>Attackers per defender; more than one exercises the parry budget.</summary>
        public int attackersPerDefender = 1;

        /// <summary>Parry budget for this run only; 0 keeps the configured value.</summary>
        public int parryBudgetOverride = 0;
        public int attackerMeleeSkill = 10;
        public int defenderMeleeSkill = 10;

        public string attackerWeapon = "MeleeWeapon_LongSword";
        public string defenderWeapon = "MeleeWeapon_LongSword";

        /// <summary>Give the defender nothing to hold, to confirm the no-weapon gate.</summary>
        public bool defenderUnarmed = false;

        /// <summary>Shooting skill pinned on attackers; -1 leaves whatever was generated.</summary>
        public int attackerShootingSkill = -1;

        /// <summary>Point-blank for this run: 1 on, -1 off, 0 configured value and not a point-blank scenario.</summary>
        public int pointBlankOverride = 0;

        /// <summary>On, the player-order exclusion applies to the arena's ordered attacks; off, it is waived so rolls can be measured.</summary>
        public bool pointBlankHonourOrders = false;

        public bool IsPointBlank => pointBlankOverride != 0;

        public int ticks = 4000;

        /// <summary>Ticks between healing and re-ordering the combatants.</summary>
        public int refreshEveryTicks = 120;

        /// <summary>Non-zero pins the RNG, making a run reproducible for regression checks.</summary>
        public int seed = 0;

        public MeleeArenaSpec Clone() => (MeleeArenaSpec)MemberwiseClone();

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
            if (IsPointBlank)
            {
                yield return new KeyValuePair<string, string>("attackerShootingSkill", attackerShootingSkill.ToString());
                yield return new KeyValuePair<string, string>("pointBlankOverride", pointBlankOverride.ToString());
                yield return new KeyValuePair<string, string>("pointBlankHonourOrders", pointBlankHonourOrders.ToString());
            }
            yield return new KeyValuePair<string, string>("ticks", ticks.ToString());
            yield return new KeyValuePair<string, string>("seed", seed.ToString());
        }
    }

    public class MeleeArenaResult
    {
        public MeleeArenaSpec Spec;
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
}
