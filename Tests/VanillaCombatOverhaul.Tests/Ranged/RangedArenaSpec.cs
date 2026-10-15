using System;
using System.Collections.Generic;
using RimWorld;
using UnityEngine;
using Verse;

namespace VanillaCombatOverhaul
{
    /// <summary>
    /// Parameters for a controlled ranged accuracy check.
    /// </summary>
    public class RangedArenaSpec
    {
        public string label = "default";
        public int shooterSkill = 10;
        public string weaponDef = "Gun_BoltActionRifle";
        public int distance = 25;
        public bool targetMoving = false;
        public int seed = 0;
    }

    public class RangedArenaResult
    {
        public RangedArenaSpec Spec;
        public List<AssertionResult> Assertions = new List<AssertionResult>();
        public float EquipmentFactor;
        public float WeatherFactor;
        public float AimOnTarget;
        public float MitigatedEquipmentFactor;
        public float ExpectedMitigatedEquipment;
        public float EvasionMultiplier;

        public bool AllAssertionsPassed => Assertions.TrueForAll(a => a.Passed);
    }
}
