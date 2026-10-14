using System;
using System.Collections.Generic;
using RimWorld;
using Verse;
using Verse.AI;

namespace VanillaCombatOverhaul
{
    public static class RunAndGunAssertions
    {
        private const int MaxWalkTicks = 1200;
        private const int WalkDistance = 14;
        private const int EnemyDistance = 12;

        /// <summary>Walks a drafted rifleman past a hostile and checks it shoots without stopping, and holds fire with Fire at Will off.</summary>
        public static List<AssertionResult> MapTests(Map map)
        {
            var results = new List<AssertionResult>();
            var settings = VCOMod.Settings;
            var rifle = DefDatabase<ThingDef>.GetNamedSilentFail("Gun_AssaultRifle");
            var enemyFaction = Find.FactionManager.RandomEnemyFaction(allowNonHumanlike: false);
            if (map == null || settings == null || rifle == null || enemyFaction == null)
            {
                results.Add(Check("run and gun arena set up", false, "needs a map, the assault rifle and an enemy faction"));
                return results;
            }

            var wasEnabled = settings.enableRunAndGun;
            var wasSuppression = settings.enableSuppression;
            settings.enableRunAndGun = true;
            // Return fire would otherwise pin the walker and change the job under test.
            settings.enableSuppression = false;

            var spawned = new List<Pawn>();
            try
            {
                if (!TryFindArena(map, out var origin))
                {
                    results.Add(Check("run and gun arena set up", false, "no clear strip of ground near the map center"));
                    return results;
                }
                var shooter = RangedCombatArena.SpawnShooter(map, origin, 10, rifle);
                var enemy = SpawnHostile(map, origin + IntVec3.East * EnemyDistance, enemyFaction);
                spawned.Add(shooter);
                spawned.Add(enemy);
                if (shooter?.drafter == null || enemy == null)
                {
                    results.Add(Check("run and gun arena set up", false, "could not spawn the pawns"));
                    return results;
                }
                enemy.jobs.StartJob(JobMaker.MakeJob(JobDefOf.Wait, 99999), JobCondition.InterruptForced);
                shooter.drafter.Drafted = true;

                // Held fire first, while the unarmed target is sure to be alive.
                shooter.drafter.FireAtWill = false;
                var quiet = Walk(shooter, origin + IntVec3.North * WalkDistance);
                results.Add(Check("with Fire at Will off it walks without shooting",
                    quiet.Arrived && quiet.ShotsWhileMoving == 0 && quiet.AimTicksWhileMoving == 0,
                    $"{quiet.ShotsWhileMoving} shots, {quiet.Ticks} ticks"));

                shooter.drafter.FireAtWill = true;
                var walk = Walk(shooter, origin);
                results.Add(Check("a drafted shooter fires while walking", walk.ShotsWhileMoving > 0,
                    $"{walk.ShotsWhileMoving} shots on the move, {walk.AimTicksWhileMoving} aiming ticks walked"));
                results.Add(Check("it keeps walking to where it was sent", walk.Arrived,
                    $"{walk.Ticks} ticks, at {shooter.Position}"));
            }
            catch (Exception e)
            {
                results.Add(Check("run and gun arena ran", false, e.ToString()));
            }
            finally
            {
                foreach (var pawn in spawned)
                {
                    if (pawn != null && !pawn.Destroyed)
                    {
                        pawn.Destroy(DestroyMode.Vanish);
                    }
                }
                settings.enableRunAndGun = wasEnabled;
                settings.enableSuppression = wasSuppression;
            }
            return results;
        }

        private struct WalkResult
        {
            public bool Arrived;
            public int Ticks;
            public int ShotsWhileMoving;
            public int AimTicksWhileMoving;
        }

        private static WalkResult Walk(Pawn pawn, IntVec3 dest)
        {
            var result = new WalkResult();
            pawn.jobs.TryTakeOrderedJob(JobMaker.MakeJob(JobDefOf.Goto, dest));
            var lastAttack = pawn.mindState.lastAttackTargetTick;
            for (; result.Ticks < MaxWalkTicks && pawn.Position != dest; result.Ticks++)
            {
                Find.TickManager.DoSingleTick();
                if (!pawn.pather.Moving)
                {
                    continue;
                }
                if (pawn.stances.curStance is Stance_Warmup)
                {
                    result.AimTicksWhileMoving++;
                }
                if (pawn.mindState.lastAttackTargetTick != lastAttack)
                {
                    lastAttack = pawn.mindState.lastAttackTargetTick;
                    result.ShotsWhileMoving++;
                }
            }
            result.Arrived = pawn.Position == dest;
            return result;
        }

        /// <summary>An origin whose walk north and the enemy's cell are open ground with sight lines along the walk.</summary>
        private static bool TryFindArena(Map map, out IntVec3 origin)
        {
            for (var i = 0; i < 400; i++)
            {
                origin = map.Center + GenRadial.RadialPattern[i * 7 % GenRadial.RadialPattern.Length] + IntVec3.South * 20;
                var enemy = origin + IntVec3.East * EnemyDistance;
                if (!enemy.InBounds(map) || !enemy.Standable(map) || enemy.Fogged(map))
                {
                    continue;
                }
                var clear = true;
                for (var step = 0; step <= WalkDistance && clear; step++)
                {
                    var cell = origin + IntVec3.North * step;
                    clear = cell.InBounds(map) && cell.Standable(map) && !cell.Fogged(map)
                            && cell.GetFirstPawn(map) == null && GenSight.LineOfSight(cell, enemy, map);
                }
                if (clear)
                {
                    return true;
                }
            }
            origin = IntVec3.Invalid;
            return false;
        }

        private static Pawn SpawnHostile(Map map, IntVec3 cell, Faction faction)
        {
            var kind = faction.def.basicMemberKind ?? PawnKindDefOf.Villager;
            var pawn = PawnGenerator.GeneratePawn(kind, faction);
            pawn.health.RemoveAllHediffs();
            pawn.equipment?.DestroyAllEquipment();
            pawn.apparel?.DestroyAll();
            GenSpawn.Spawn(pawn, cell, map);
            return pawn;
        }

        private static AssertionResult Check(string name, bool passed, string detail) =>
            new AssertionResult { Name = name, Passed = passed, Detail = detail };
    }
}
