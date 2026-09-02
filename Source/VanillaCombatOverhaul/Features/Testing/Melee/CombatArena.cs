using System.Collections.Generic;
using System.Linq;
using RimWorld;
using UnityEngine;
using Verse;
using Verse.AI;

namespace VanillaCombatOverhaul
{
    /// <summary>
    /// Spawns a controlled melee fight, runs it at full speed, and reports what the
    /// diagnostic counters saw.
    ///
    /// Hand-testing cannot answer the questions worth asking here. Confirming a predicted 48%
    /// parry rate to within two points needs roughly 600 samples, and doing it by hand means
    /// skills, weapons and facing all drift. This pins those and generates the volume in
    /// seconds by driving the tick loop directly rather than waiting on wall-clock time.
    /// </summary>
    public static class CombatArena
    {
        /// <summary>
        /// Where attackers are placed around a defender, orthogonals first so a small group
        /// surrounds cleanly before falling back to the diagonals.
        /// </summary>
        private static readonly IntVec3[] RingOffsets =
        {
            IntVec3.East, IntVec3.West, IntVec3.North, IntVec3.South,
            new IntVec3(1, 0, 1), new IntVec3(-1, 0, 1),
            new IntVec3(1, 0, -1), new IntVec3(-1, 0, -1)
        };

        public static ArenaResult Run(ArenaSpec spec, Map map)
        {
            var result = new ArenaResult { Spec = spec };

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

            // Swapped in for the duration of the run and restored below, so a scenario can ask
            // what the crowd looks like with the cap lifted without leaving the player's
            // configured value changed afterwards.
            var settings = VCOMod.Settings;
            var restoreBudget = settings?.parryBudgetPerWindow ?? 0;
            var budgetOverridden = settings != null && spec.parryBudgetOverride > 0;
            if (budgetOverridden)
            {
                settings.parryBudgetPerWindow = spec.parryBudgetOverride;
            }

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

                // Counters are reset here so the result describes this run alone, and scoped to
                // the defenders so only the direction under test is measured. A parry triggers
                // a counter-attack, and that counter is a melee attack the original attacker
                // can parry in turn; without this scoping an asymmetric matchup records both
                // directions and reports their mean.
                VCODiagnostics.Reset();
                var underTest = new HashSet<Pawn>(defenders);
                VCODiagnostics.SubjectFilter = underTest.Contains;
                VCODiagnostics.ParryChanceProbe = SampleExpectedChance;

                RunTicks(spec, attackers, attackerTarget, defenders, attackerWeapon, defenderWeapon);

                result.Counters = VCODiagnostics.SnapshotCounters();
                result.Readings = VCODiagnostics.SnapshotReadings();
                result.PawnsDied = attackers.Concat(defenders).Count(p => p.Dead);
                ArenaAssertions.Evaluate(result);
            }
            finally
            {
                // Must be cleared, or normal play would keep counting only these pawns.
                VCODiagnostics.SubjectFilter = null;
                VCODiagnostics.ParryChanceProbe = null;
                if (budgetOverridden)
                {
                    settings.parryBudgetPerWindow = restoreBudget;
                }
                Cleanup(attackers);
                Cleanup(defenders);
                if (seeded)
                {
                    Rand.PopState();
                }
            }

            return result;
        }

        /// <summary>
        /// A humanlike faction to draw attackers from, made hostile if it is not already.
        ///
        /// This used to be RandomEnemyFaction, which is how a run could end up fielding
        /// mechanoids or insects: in a fresh quick-test world the humanlike factions start
        /// neutral, so the only permanently hostile ones are exactly the races with no skills
        /// tracker. Pinning melee skill on those silently did nothing, and a skill-20 attacker
        /// scenario quietly ran at roughly skill 7 -- parry came out at 17% where 1% was
        /// expected, with every other assertion still green.
        ///
        /// So hostility is imposed rather than searched for, and the choice is ordered by
        /// defName to stay stable between runs, which a seeded harness needs.
        /// </summary>
        private static Faction HostileHumanlikeFaction()
        {
            var player = Faction.OfPlayer;
            var faction = Find.FactionManager.AllFactionsListForReading
                .Where(f => f != player && !f.IsPlayer && !f.Hidden)
                .OrderBy(f => f.def.defName)
                .FirstOrDefault();

            if (faction != null && !faction.HostileTo(player))
            {
                // Set on both sides: SetRelationDirect changes only the faction it is called
                // on, and the melee AI checks the relation from whichever side is asking.
                faction.SetRelationDirect(player, FactionRelationKind.Hostile, false);
                player.SetRelationDirect(faction, FactionRelationKind.Hostile, false);
            }

            if (faction != null && VCODiagnostics.Enabled)
            {
                Log.Message($"[VCO] Arena drawing attackers from {faction.Name} ({faction.def.defName}).");
            }
            return faction;
        }

        /// <summary>
        /// Ends a run that could not start, leaving a failed assertion behind.
        ///
        /// Bailing out silently was worse than any bug it hid: a suite where every scenario
        /// spawned nothing still reported "12 of 12 checks passed", because a scenario with no
        /// assertions contributes nothing to fail. A run that could not set itself up has to
        /// be a failure, not an absence.
        /// </summary>
        private static ArenaResult Abort(ArenaResult result, string reason)
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

        /// <summary>
        /// Places each defender with its attackers ringed around it. Cells already taken are
        /// tracked so two groups never share ground; overlapping rings would let an attacker
        /// reach a defender it was not assigned to and quietly contaminate the matchup.
        /// </summary>
        private static void SpawnCombatants(ArenaSpec spec, Map map, Faction enemyFaction,
                                            ThingDef attackerWeapon, ThingDef defenderWeapon,
                                            List<Pawn> attackers, List<int> attackerTarget,
                                            List<Pawn> defenders)
        {
            var centre = map.Center;
            var perDefender = Mathf.Clamp(spec.attackersPerDefender, 1, RingOffsets.Length);
            var used = new HashSet<IntVec3>();

            // Groups need more elbow room once they are bigger than a pair, or the rings
            // collide and most candidate cells get rejected.
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
                                             spec.defenderMeleeSkill, defenderWeapon);
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

                    // Deliberately the same kind as the defender. Drawing attackers from the
                    // faction's own basicMemberKind is what let mechanoids into a melee-skill
                    // test; more generally, any difference in pawn kind is a variable the
                    // scenario did not ask for. Only faction and skill separate the two sides.
                    var attacker = MakeCombatant(PawnKindDefOf.Colonist, enemyFaction,
                                                 spec.attackerMeleeSkill, attackerWeapon);
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

        /// <summary>
        /// Sets melee skill and confirms it took.
        ///
        /// Assigning SkillRecord.Level is not enough on its own. The getter adds an aptitude
        /// offset from genes on top of the stored value, so a pawn set to 10 can read back as
        /// 18; and on a pawn whose backstory disables Melee the setter does nothing at all and
        /// the level stays 0. Both quietly change the matchup a scenario claims to measure, so
        /// the value is read back and the pawn rejected if it did not land.
        /// </summary>
        private static bool TryPinMeleeSkill(Pawn pawn, int meleeSkill)
        {
            var melee = pawn.skills?.GetSkill(SkillDefOf.Melee);
            if (melee == null || melee.TotallyDisabled)
            {
                return false;
            }

            melee.Level = meleeSkill;
            melee.passion = Passion.None;
            melee.xpSinceLastLevel = 0;
            return melee.Level == meleeSkill;
        }

        private static Pawn MakeCombatant(PawnKindDef kind, Faction faction, int meleeSkill, ThingDef weapon)
        {
            Pawn pawn = null;

            // Generated pawns vary in ways that cannot be stripped after the fact -- a disabled
            // Melee skill, an aptitude gene -- so unsuitable ones are discarded rather than
            // patched up. Cheaper than fighting the generator, and it guarantees the cohort is
            // actually at the level the scenario names.
            for (var attempt = 0; attempt < GenerationAttempts; attempt++)
            {
                var candidate = PawnGenerator.GeneratePawn(kind, faction);
                if (candidate == null)
                {
                    continue;
                }

                // Strip everything that would otherwise move MeleeHitChance around between
                // pawns, so the only variables left are the ones the spec sets.
                candidate.health.RemoveAllHediffs();
                candidate.story?.traits?.allTraits?.Clear();
                candidate.apparel?.DestroyAll();

                if (TryPinMeleeSkill(candidate, meleeSkill))
                {
                    pawn = candidate;
                    break;
                }

                candidate.Destroy();
            }

            if (pawn == null)
            {
                Log.Error($"[VCO] Arena could not generate a pawn pinned to melee {meleeSkill} " +
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

        private static void RunTicks(ArenaSpec spec, List<Pawn> attackers, List<int> attackerTarget,
                                     List<Pawn> defenders, ThingDef attackerWeapon, ThingDef defenderWeapon)
        {
            var tickManager = Find.TickManager;
            Refresh(attackers, attackerTarget, defenders, attackerWeapon, defenderWeapon,
                    spec.attackerMeleeSkill, spec.defenderMeleeSkill);

            for (var i = 0; i < spec.ticks; i++)
            {
                tickManager.DoSingleTick();

                if (spec.refreshEveryTicks > 0 && i % spec.refreshEveryTicks == 0)
                {
                    Refresh(attackers, attackerTarget, defenders, attackerWeapon, defenderWeapon,
                            spec.attackerMeleeSkill, spec.defenderMeleeSkill);
                }
            }
        }

        /// <summary>
        /// Heals combatants, replaces lost weapons, and re-issues orders. Without the healing
        /// the fight ends in seconds and the sample is too small to conclude anything from.
        ///
        /// Only the attackers attack. Letting both sides swing means the sampled parry chance
        /// averages two opposite matchups, so an asymmetric scenario reports the mean of the
        /// two rather than the value under test -- which is precisely what the first run of
        /// this harness did, reporting 44% where 87% and 1% were expected.
        /// </summary>
        private static void Refresh(List<Pawn> attackers, List<int> attackerTarget, List<Pawn> defenders,
                                    ThingDef attackerWeapon, ThingDef defenderWeapon,
                                    int attackerSkill, int defenderSkill)
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

                Revive(attacker, attackerWeapon, attackerSkill);

                // Record the inputs the formula actually saw. The idealised skill curve is not
                // the whole story: Ideology light-level offsets and manipulation lost to
                // injuries both move MeleeHitChance, so a prediction built from skill alone
                // drifts below the live value. Sampling the real stats lets the assertion test
                // the formula rather than the weather.
                SampleFormulaInputs(attacker, defender);

                Engage(attacker, defender);
            }
        }

        /// <summary>
        /// Captures the two live values the parry formula consumes, so the assertion can check
        /// the formula's shape against what it was actually fed.
        /// </summary>
        private static void SampleFormulaInputs(Pawn attacker, Pawn defender)
        {
            if (attacker == null || defender == null || attacker.Dead || defender.Dead
                || !attacker.Spawned || !defender.Spawned)
            {
                return;
            }

            // Plain Sample, not SampleFor: these describe the pair, not a filtered subject.
            var aptitude = defender.GetStatValue(VCO_StatDefOf.VCO_ParryChance);
            var attackerMelee = attacker.GetStatValue(StatDefOf.MeleeHitChance)
                                + StatPart_ParryDarkness.Offset(attacker);

            VCODiagnostics.Sample("measured.defenderAptitude", aptitude);
            VCODiagnostics.Sample("measured.attackerMelee", attackerMelee);

            // The skill levels actually in play, so a scenario cannot silently run a matchup
            // other than the one it names. A pawn with no skills tracker records -1 rather
            // than being skipped, because "this combatant has no skills" is exactly the
            // condition worth failing on.
            VCODiagnostics.Sample("measured.attackerSkill", SkillLevel(attacker));
            VCODiagnostics.Sample("measured.defenderSkill", SkillLevel(defender));

        }

        /// <summary>
        /// Independent prediction for one real parry attempt, taken from the probe so the
        /// inputs are the ones the mod actually had in hand.
        ///
        /// Evaluated per attempt and then averaged, never the other way round. The formula is
        /// strongly non-linear in the attacker's ability, so averaging inputs first and
        /// evaluating once gives a materially different answer.
        /// </summary>
        private static void SampleExpectedChance(Pawn defender, Pawn attacker, float directionFactor)
        {
            var aptitude = defender.GetStatValue(VCO_StatDefOf.VCO_ParryChance);
            var attackerMelee = attacker.GetStatValue(StatDefOf.MeleeHitChance)
                                + StatPart_ParryDarkness.Offset(attacker);

            // SampleFor, not Sample: the probe fires for every parry attempt in the game,
            // including the attacker parrying the defender's counter-strike. Recorded
            // unfiltered, an asymmetric matchup averages both directions and reports the
            // symmetric value -- 43% where 87% was expected, and 49% where 1% was.
            VCODiagnostics.SampleFor(defender, "measured.expectedChance",
                (float)ArenaAssertions.FormulaFor(aptitude, attackerMelee, directionFactor));
        }

        private static float SkillLevel(Pawn pawn)
        {
            var skill = pawn.skills?.GetSkill(SkillDefOf.Melee);
            return skill == null ? -1f : skill.Level;
        }

        private static void Revive(Pawn pawn, ThingDef weapon, int meleeSkill)
        {
            if (pawn == null || pawn.Dead || !pawn.Spawned)
            {
                return;
            }
            pawn.health.RemoveAllHediffs();

            // Fighting earns melee XP, so a combatant does not stay at the level it was given.
            // Left alone, a skill-0 pawn climbs quickly and the run measures a matchup that
            // drifted away from the one being predicted -- which is why, before this was
            // pinned, only the skill-20 scenarios (already at the cap) matched.
            var melee = pawn.skills?.GetSkill(SkillDefOf.Melee);
            if (melee != null && melee.Level != meleeSkill)
            {
                VCODiagnostics.Count("arena.skillRepinned");
                TryPinMeleeSkill(pawn, meleeSkill);
            }

            // Weapons get dropped when a pawn goes down, which silently turns an armed
            // combatant into an unarmed one part-way through a run.
            if (weapon != null && pawn.equipment?.Primary == null)
            {
                var stuff = weapon.MadeFromStuff ? GenStuff.DefaultStuffFor(weapon) : null;
                if (ThingMaker.MakeThing(weapon, stuff) is ThingWithComps made)
                {
                    pawn.equipment.AddEquipment(made);
                }
            }
        }

        /// <summary>
        /// Keeps the defender in a combat stance without attacking, so it stays put and keeps
        /// facing its opponent instead of wandering or fleeing.
        /// </summary>
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

            // Re-issuing the job a pawn is already running cancels the swing it is winding up.
            // Healing has to happen often to keep a surrounded defender on its feet, and when
            // the two were driven off the same interval the re-order starved combat outright:
            // six attackers refreshed every 30 ticks landed fewer attacks than three refreshed
            // every 120. Orders are now replaced only when they are actually wrong.
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

                // A dead pawn sits inside a Corpse, which is a separate spawned Thing that
                // destroying the pawn does not remove. Left behind, corpses pile up across
                // scenarios and change the ground the next one spawns on -- which is why two
                // runs of the same seed stopped agreeing once fights got lethal enough to
                // leave bodies.
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
