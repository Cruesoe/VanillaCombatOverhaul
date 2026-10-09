using System.Collections.Generic;
using System.Linq;
using RimWorld;
using UnityEngine;
using Verse;
using Verse.AI;

namespace VanillaCombatOverhaul
{
    /// <summary>Spawns a melee fight with pinned skills and weapons, ticks it directly and reports the diagnostic counters.</summary>
    public static class MeleeCombatArena
    {
        /// <summary>Attacker positions around a defender, orthogonals first.</summary>
        private static readonly IntVec3[] RingOffsets =
        {
            IntVec3.East, IntVec3.West, IntVec3.North, IntVec3.South,
            new IntVec3(1, 0, 1), new IntVec3(-1, 0, 1),
            new IntVec3(1, 0, -1), new IntVec3(-1, 0, -1)
        };

        public static MeleeArenaResult Run(MeleeArenaSpec spec, Map map)
        {
            var result = new MeleeArenaResult { Spec = spec };

            if (map == null)
            {
                return Abort(result, "arena needs a loaded map");
            }

            var enemyFaction = HostileHumanlikeFaction();
            if (enemyFaction == null)
            {
                return Abort(result, "no humanlike faction to draw attackers from");
            }

            var attackerWeapon = ResolveWeapon(spec.attackerWeapon);
            var defenderWeapon = spec.defenderUnarmed ? null : ResolveWeapon(spec.defenderWeapon);

            var attackers = new List<Pawn>();
            var attackerTarget = new List<int>();
            var defenders = new List<Pawn>();

            var seeded = spec.seed != 0;
            if (seeded)
            {
                Rand.PushState(spec.seed);
            }

            // Overrides for this run, restored afterwards.
            var settings = VCOMod.Settings;
            var restoreBudget = settings?.parryBudgetPerWindow ?? 0;
            var budgetOverridden = settings != null && spec.parryBudgetOverride > 0;
            if (budgetOverridden)
            {
                settings.parryBudgetPerWindow = spec.parryBudgetOverride;
            }
            var restorePointBlank = settings?.enablePointBlank ?? false;
            if (settings != null && spec.IsPointBlank)
            {
                settings.enablePointBlank = spec.pointBlankOverride > 0;
            }
            PointBlankUtility.IgnorePlayerForcedForTesting = spec.IsPointBlank && !spec.pointBlankHonourOrders;

            try
            {
                SpawnCombatants(spec, map, enemyFaction, attackerWeapon, defenderWeapon,
                                attackers, attackerTarget, defenders);
                result.PawnsSpawned = attackers.Count + defenders.Count;
                result.AttackersSpawned = attackers.Count;
                result.DefendersSpawned = defenders.Count;

                if (attackers.Count == 0)
                {
                    return Abort(result, "could not place any combatants; is the map crowded?");
                }

                // Counters reset for this run and limited to the defenders, so counter-attacks are not measured.
                VCODiagnostics.Reset();
                var underTest = new HashSet<Pawn>(defenders);
                if (spec.IsPointBlank)
                {
                    // Point-blank events are recorded for the attackers.
                    underTest.UnionWith(attackers);
                }
                VCODiagnostics.SubjectFilter = underTest.Contains;
                VCODiagnostics.ParryChanceProbe = SampleExpectedChance;

                RunTicks(spec, attackers, attackerTarget, defenders, attackerWeapon, defenderWeapon);

                result.Counters = VCODiagnostics.SnapshotCounters();
                result.Readings = VCODiagnostics.SnapshotReadings();
                result.PawnsDied = attackers.Concat(defenders).Count(p => p.Dead);
                MeleeAssertions.Evaluate(result);
            }
            finally
            {
                // Cleared so later counting is not limited to these pawns.
                VCODiagnostics.SubjectFilter = null;
                VCODiagnostics.ParryChanceProbe = null;
                if (budgetOverridden)
                {
                    settings.parryBudgetPerWindow = restoreBudget;
                }
                if (settings != null)
                {
                    settings.enablePointBlank = restorePointBlank;
                }
                PointBlankUtility.IgnorePlayerForcedForTesting = false;
                Cleanup(attackers);
                Cleanup(defenders);
                if (seeded)
                {
                    Rand.PopState();
                }
            }

            return result;
        }

        /// <summary>A humanlike faction for the attackers, chosen by defName and made hostile if needed.</summary>
        private static Faction HostileHumanlikeFaction()
        {
            var player = Faction.OfPlayer;
            var faction = Find.FactionManager.AllFactionsListForReading
                .Where(f => f != player && !f.IsPlayer && !f.Hidden)
                .OrderBy(f => f.def.defName)
                .FirstOrDefault();

            if (faction != null && !faction.HostileTo(player))
            {
                // Set on both sides; SetRelationDirect changes only the faction it is called on.
                faction.SetRelationDirect(player, FactionRelationKind.Hostile, false);
                player.SetRelationDirect(faction, FactionRelationKind.Hostile, false);
            }

            if (faction != null && VCODiagnostics.Enabled)
            {
                Log.Message($"[VCO] Arena drawing attackers from {faction.Name} ({faction.def.defName}).");
            }
            return faction;
        }

        /// <summary>Ends a run that could not start with a failed assertion.</summary>
        private static MeleeArenaResult Abort(MeleeArenaResult result, string reason)
        {
            Log.Error("[VCO] Arena aborted: " + reason);
            result.Assertions.Add(new AssertionResult
            {
                Name = "arena ran",
                Passed = false,
                Detail = reason
            });
            return result;
        }

        // ------------------------------------------------------------------ setup

        /// <summary>Places each defender with its attackers around it, keeping groups apart.</summary>
        private static void SpawnCombatants(MeleeArenaSpec spec, Map map, Faction enemyFaction,
                                            ThingDef attackerWeapon, ThingDef defenderWeapon,
                                            List<Pawn> attackers, List<int> attackerTarget,
                                            List<Pawn> defenders)
        {
            var centre = map.Center;
            var perDefender = Mathf.Clamp(spec.attackersPerDefender, 1, RingOffsets.Length);
            var used = new HashSet<IntVec3>();

            // Wider spacing for groups larger than a pair.
            var radius = 25 + (perDefender * 10);

            for (var i = 0; i < spec.pairs; i++)
            {
                if (!CellFinder.TryFindRandomCellNear(centre, map, radius,
                        c => IsFree(c, map, used) && FreeAdjacentCount(c, map, used) >= perDefender,
                        out var cell))
                {
                    continue;
                }

                var defender = MakeCombatant(PawnKindDefOf.Colonist, Faction.OfPlayer,
                                             spec.defenderMeleeSkill, -1, defenderWeapon);
                if (defender == null)
                {
                    continue;
                }

                GenSpawn.Spawn(defender, cell, map);
                used.Add(cell);
                var defenderIndex = defenders.Count;
                defenders.Add(defender);

                var placed = 0;
                foreach (var offset in RingOffsets)
                {
                    if (placed >= perDefender)
                    {
                        break;
                    }

                    var spot = cell + offset;
                    if (!IsFree(spot, map, used))
                    {
                        continue;
                    }

                    // Same pawn kind as the defender; only faction and skill differ.
                    var attacker = MakeCombatant(PawnKindDefOf.Colonist, enemyFaction,
                                                 spec.attackerMeleeSkill, spec.attackerShootingSkill, attackerWeapon);
                    if (attacker == null)
                    {
                        continue;
                    }

                    GenSpawn.Spawn(attacker, spot, map);
                    used.Add(spot);
                    attackers.Add(attacker);
                    attackerTarget.Add(defenderIndex);
                    placed++;
                }
            }
        }

        private static bool IsFree(IntVec3 c, Map map, HashSet<IntVec3> used) =>
            c.InBounds(map) && c.Standable(map) && !used.Contains(c);

        private static int FreeAdjacentCount(IntVec3 cell, Map map, HashSet<IntVec3> used)
        {
            var n = 0;
            foreach (var offset in RingOffsets)
            {
                if (IsFree(cell + offset, map, used))
                {
                    n++;
                }
            }
            return n;
        }

        /// <summary>How many generated pawns to discard before giving up on a clean one.</summary>
        private const int GenerationAttempts = 25;

        /// <summary>Sets a skill and reads it back, since aptitudes and disabled skills change the result.</summary>
        private static bool TryPinSkill(Pawn pawn, SkillDef def, int level)
        {
            var skill = pawn.skills?.GetSkill(def);
            if (skill == null || skill.TotallyDisabled)
            {
                return false;
            }

            skill.Level = level;
            skill.passion = Passion.None;
            skill.xpSinceLastLevel = 0;
            return skill.Level == level;
        }

        private static Pawn MakeCombatant(PawnKindDef kind, Faction faction, int meleeSkill, int shootingSkill,
                                          ThingDef weapon)
        {
            Pawn pawn = null;

            // Pawns whose skill cannot be set exactly are discarded.
            for (var attempt = 0; attempt < GenerationAttempts; attempt++)
            {
                var candidate = PawnGenerator.GeneratePawn(kind, faction);
                if (candidate == null)
                {
                    continue;
                }

                // Strips what would move MeleeHitChance between pawns.
                candidate.health.RemoveAllHediffs();
                candidate.story?.traits?.allTraits?.Clear();
                candidate.apparel?.DestroyAll();

                if (TryPinSkill(candidate, SkillDefOf.Melee, meleeSkill)
                    && (shootingSkill < 0 || TryPinSkill(candidate, SkillDefOf.Shooting, shootingSkill)))
                {
                    pawn = candidate;
                    break;
                }

                candidate.Destroy();
            }

            if (pawn == null)
            {
                Log.Error($"[VCO] Arena could not generate a pawn pinned to melee {meleeSkill} " +
                          (shootingSkill >= 0 ? $"and shooting {shootingSkill} " : "") +
                          $"in {GenerationAttempts} attempts.");
                return null;
            }

            pawn.equipment?.DestroyAllEquipment();
            if (weapon != null)
            {
                var stuff = weapon.MadeFromStuff ? GenStuff.DefaultStuffFor(weapon) : null;
                if (ThingMaker.MakeThing(weapon, stuff) is ThingWithComps made)
                {
                    pawn.equipment.AddEquipment(made);
                }
            }
            return pawn;
        }

        private static ThingDef ResolveWeapon(string defName)
        {
            if (defName.NullOrEmpty())
            {
                return null;
            }
            var def = DefDatabase<ThingDef>.GetNamedSilentFail(defName);
            if (def == null)
            {
                Log.Warning($"[VCO] Arena could not find weapon '{defName}'; combatant will be unarmed.");
            }
            return def;
        }

        // ------------------------------------------------------------------ run

        private static void RunTicks(MeleeArenaSpec spec, List<Pawn> attackers, List<int> attackerTarget,
                                     List<Pawn> defenders, ThingDef attackerWeapon, ThingDef defenderWeapon)
        {
            var tickManager = Find.TickManager;
            Refresh(attackers, attackerTarget, defenders, attackerWeapon, defenderWeapon,
                    spec.attackerMeleeSkill, spec.defenderMeleeSkill, spec.attackerShootingSkill);

            for (var i = 0; i < spec.ticks; i++)
            {
                tickManager.DoSingleTick();

                if (spec.refreshEveryTicks > 0 && i % spec.refreshEveryTicks == 0)
                {
                    Refresh(attackers, attackerTarget, defenders, attackerWeapon, defenderWeapon,
                            spec.attackerMeleeSkill, spec.defenderMeleeSkill, spec.attackerShootingSkill);
                }
            }
        }

        /// <summary>Heals combatants, replaces lost weapons and re-issues orders; only the attackers attack.</summary>
        private static void Refresh(List<Pawn> attackers, List<int> attackerTarget, List<Pawn> defenders,
                                    ThingDef attackerWeapon, ThingDef defenderWeapon,
                                    int attackerSkill, int defenderSkill, int attackerShootingSkill)
        {
            foreach (var defender in defenders)
            {
                Revive(defender, defenderWeapon, defenderSkill);
                HoldPosition(defender);
            }

            for (var i = 0; i < attackers.Count; i++)
            {
                var attacker = attackers[i];
                var defender = defenders[attackerTarget[i]];

                Revive(attacker, attackerWeapon, attackerSkill, attackerShootingSkill);

                // Samples the live stat values the formula sees.
                SampleFormulaInputs(attacker, defender);

                Engage(attacker, defender);
            }
        }

        /// <summary>Samples the two live values the parry formula uses.</summary>
        private static void SampleFormulaInputs(Pawn attacker, Pawn defender)
        {
            if (attacker == null || defender == null || attacker.Dead || defender.Dead
                || !attacker.Spawned || !defender.Spawned)
            {
                return;
            }

            // Unfiltered: these describe the pair.
            var aptitude = defender.GetStatValue(VCO_StatDefOf.VCO_ParryChance);
            var attackerMelee = attacker.GetStatValue(StatDefOf.MeleeHitChance)
                                + StatPart_ParryDarkness.Offset(attacker);

            VCODiagnostics.Sample("measured.defenderAptitude", aptitude);
            VCODiagnostics.Sample("measured.attackerMelee", attackerMelee);

            // Skill levels in play; -1 for a pawn with no skills tracker.
            VCODiagnostics.Sample("measured.attackerSkill", SkillLevel(attacker, SkillDefOf.Melee));
            VCODiagnostics.Sample("measured.defenderSkill", SkillLevel(defender, SkillDefOf.Melee));
            VCODiagnostics.Sample("measured.attackerShootingSkill", SkillLevel(attacker, SkillDefOf.Shooting));

        }

        /// <summary>Independent prediction for one parry attempt, from the probe's inputs; averaged per attempt.</summary>
        private static void SampleExpectedChance(Pawn defender, Pawn attacker, float directionFactor)
        {
            var aptitude = defender.GetStatValue(VCO_StatDefOf.VCO_ParryChance);
            var attackerMelee = attacker.GetStatValue(StatDefOf.MeleeHitChance)
                                + StatPart_ParryDarkness.Offset(attacker);

            // Filtered to the defenders, so parries of counter-strikes are excluded.
            VCODiagnostics.SampleFor(defender, "measured.expectedChance",
                (float)MeleeAssertions.FormulaFor(aptitude, attackerMelee, directionFactor));
        }

        private static float SkillLevel(Pawn pawn, SkillDef def)
        {
            var skill = pawn.skills?.GetSkill(def);
            return skill == null ? -1f : skill.Level;
        }

        private static void Revive(Pawn pawn, ThingDef weapon, int meleeSkill, int shootingSkill = -1)
        {
            if (pawn == null || pawn.Dead || !pawn.Spawned)
            {
                return;
            }
            pawn.health.RemoveAllHediffs();

            // Re-pins melee skill, which fighting raises.
            var melee = pawn.skills?.GetSkill(SkillDefOf.Melee);
            if (melee != null && melee.Level != meleeSkill)
            {
                VCODiagnostics.Count("arena.skillRepinned");
                TryPinSkill(pawn, SkillDefOf.Melee, meleeSkill);
            }

            // Re-pins shooting skill, which point-blank shots raise.
            var shooting = pawn.skills?.GetSkill(SkillDefOf.Shooting);
            if (shootingSkill >= 0 && shooting != null && shooting.Level != shootingSkill)
            {
                VCODiagnostics.Count("arena.skillRepinned");
                TryPinSkill(pawn, SkillDefOf.Shooting, shootingSkill);
            }

            // Re-arms pawns that dropped their weapon when downed.
            if (weapon != null && pawn.equipment?.Primary == null)
            {
                var stuff = weapon.MadeFromStuff ? GenStuff.DefaultStuffFor(weapon) : null;
                if (ThingMaker.MakeThing(weapon, stuff) is ThingWithComps made)
                {
                    pawn.equipment.AddEquipment(made);
                }
            }
        }

        /// <summary>Keeps the defender standing in a combat stance without attacking.</summary>
        private static void HoldPosition(Pawn pawn)
        {
            if (pawn == null || pawn.Dead || !pawn.Spawned || pawn.Downed)
            {
                return;
            }
            if (pawn.CurJobDef == JobDefOf.Wait_Combat)
            {
                return;
            }
            var job = JobMaker.MakeJob(JobDefOf.Wait_Combat);
            job.playerForced = true;
            job.expiryInterval = 600;
            pawn.jobs?.TryTakeOrderedJob(job, JobTag.Misc);
        }

        private static void Engage(Pawn pawn, Pawn target)
        {
            if (pawn == null || target == null || pawn.Dead || !pawn.Spawned
                || target.Dead || !target.Spawned || pawn.Downed)
            {
                return;
            }

            // Only replaces wrong orders; re-issuing a running job cancels the swing in progress.
            if (pawn.CurJobDef == JobDefOf.AttackMelee && pawn.CurJob?.targetA.Thing == target)
            {
                return;
            }

            var job = JobMaker.MakeJob(JobDefOf.AttackMelee, target);
            job.playerForced = true;
            job.expiryInterval = 600;
            pawn.jobs?.TryTakeOrderedJob(job, JobTag.Misc);
        }

        private static void Cleanup(List<Pawn> pawns)
        {
            foreach (var pawn in pawns)
            {
                if (pawn == null)
                {
                    continue;
                }

                // Corpses are separate things; destroyed so they do not affect later scenarios.
                var corpse = pawn.Corpse;
                if (corpse != null && !corpse.Destroyed)
                {
                    if (corpse.Spawned)
                    {
                        corpse.DeSpawn();
                    }
                    corpse.Destroy();
                }

                if (pawn.Spawned)
                {
                    pawn.DeSpawn();
                }
                if (!pawn.Destroyed)
                {
                    pawn.Destroy();
                }
            }
            pawns.Clear();
        }
    }
}
