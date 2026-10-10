using System;
using System.Collections.Generic;
using RimWorld;
using Verse;
using Verse.AI;

namespace VanillaCombatOverhaul
{
    public static class SuppressionAssertions
    {
        private const float Tolerance = 0.0005f;
        private const int MaxShots = 30;
        private const int MaxFlightTicks = 600;

        public static List<AssertionResult> SelfTests()
        {
            var results = new List<AssertionResult>();
            results.Add(Check("below the suppressed level has no effect",
                Approx(SuppressionUtility.AccuracyFor(SuppressionUtility.SuppressedLevel - 1f), 1f)
                && Approx(SuppressionUtility.AimTimeFor(SuppressionUtility.SuppressedLevel - 1f), 1f), "1.0"));
            results.Add(Check("suppressed level starts the light penalty",
                Approx(SuppressionUtility.AccuracyFor(SuppressionUtility.SuppressedLevel), SuppressionUtility.SuppressedAccuracy),
                SuppressionUtility.AccuracyFor(SuppressionUtility.SuppressedLevel).ToString("F3")));
            results.Add(Check("pinned level reaches the full penalty",
                Approx(SuppressionUtility.AccuracyFor(SuppressionUtility.PinnedLevel), SuppressionUtility.MinAccuracy)
                && Approx(SuppressionUtility.AimTimeFor(SuppressionUtility.MaxLevel), SuppressionUtility.MaxAimTime),
                SuppressionUtility.AccuracyFor(SuppressionUtility.PinnedLevel).ToString("F3")));
            results.Add(Check("falloff is full at the impact and half at the edge",
                Approx(SuppressionUtility.Falloff(0f, 3f), 1f) && Approx(SuppressionUtility.Falloff(3f, 3f), 0.5f)
                && Approx(SuppressionUtility.Falloff(3.1f, 3f), 0f), "1 / 0.5 / 0"));
            results.Add(Check("decay removes 240 a second and stops at zero",
                Approx(SuppressionUtility.Decay(150f, 15), 90f) && Approx(SuppressionUtility.Decay(5f, 600), 0f), "150 -> 90 in 15 ticks"));
            results.Add(Check("no armour leaves suppression whole",
                Approx(SuppressionUtility.ArmorFactor(0f, 0.16f), 1f), SuppressionUtility.ArmorFactor(0f, 0.16f).ToString("F2")));
            results.Add(Check("more armour resists more, more penetration resists less",
                SuppressionUtility.ArmorFactor(1f, 0.16f) < SuppressionUtility.ArmorFactor(0.5f, 0.16f)
                && SuppressionUtility.ArmorFactor(1f, 0.35f) > SuppressionUtility.ArmorFactor(1f, 0.16f),
                $"{SuppressionUtility.ArmorFactor(0.5f, 0.16f):F2} / {SuppressionUtility.ArmorFactor(1f, 0.16f):F2} / {SuppressionUtility.ArmorFactor(1f, 0.35f):F2}"));
            var along = SuppressionUtility.DistanceToSegment(new UnityEngine.Vector3(5f, 0f, 2f), UnityEngine.Vector3.zero,
                new UnityEngine.Vector3(10f, 0f, 0f), 100f);
            var past = SuppressionUtility.DistanceToSegment(new UnityEngine.Vector3(13f, 0f, 4f), UnityEngine.Vector3.zero,
                new UnityEngine.Vector3(10f, 0f, 0f), 100f);
            results.Add(Check("fly-by distance is measured to the bullet's path, not past its end",
                Approx(along, 2f) && Approx(past, 5f), $"{along:F2} / {past:F2}"));
            results.Add(Check("mood stages follow the suppression level",
                ThoughtWorker_UnderFire.StageFor(0f) == -1 && ThoughtWorker_UnderFire.StageFor(1f) == 0
                && ThoughtWorker_UnderFire.StageFor(SuppressionUtility.SuppressedLevel) == 1
                && ThoughtWorker_UnderFire.StageFor(SuppressionUtility.PinnedLevel) == 2, "-1 / 0 / 1 / 2"));
            results.Add(Check("armour can stop suppression entirely, and no penetration suppresses nothing",
                Approx(SuppressionUtility.ArmorFactor(2f, 0.1f), 0f) && Approx(SuppressionUtility.ArmorFactor(0f, 0f), 0f), "0 / 0"));

            var calm = StatPart_SuppressabilityFromMind.FactorFor(0.35f, 0.5f);
            var ironWilled = StatPart_SuppressabilityFromMind.FactorFor(0.17f, 0.5f);
            var volatileNerves = StatPart_SuppressabilityFromMind.FactorFor(0.5f, 0.5f);
            var happy = StatPart_SuppressabilityFromMind.FactorFor(0.35f, 0.9f);
            results.Add(Check("default nerves and mood leave suppressability at 1",
                Approx(calm, 1f), calm.ToString("F3")));
            results.Add(Check("steadier nerves resist suppression",
                ironWilled < calm && calm < volatileNerves, $"{ironWilled:F2} < {calm:F2} < {volatileNerves:F2}"));
            results.Add(Check("good mood resists suppression", happy < calm, $"{happy:F2} < {calm:F2}"));
            results.Add(Check("suppressability stays within its limits",
                StatPart_SuppressabilityFromMind.FactorFor(0.01f, 1f) >= StatPart_SuppressabilityFromMind.Min
                && StatPart_SuppressabilityFromMind.FactorFor(0.5f, 0f) <= StatPart_SuppressabilityFromMind.Max,
                "0.5 to 1.5"));
            return results;
        }

        /// <summary>Fires real bullets near a hostile pawn and checks the build-up, pinning, penalties and decay.</summary>
        public static List<AssertionResult> MapTests(Map map)
        {
            var results = new List<AssertionResult>();
            var settings = VCOMod.Settings;
            var rifle = DefDatabase<ThingDef>.GetNamedSilentFail("Gun_AssaultRifle");
            var enemyFaction = Find.FactionManager.RandomEnemyFaction(allowNonHumanlike: false);
            if (map == null || settings == null || rifle == null || enemyFaction == null)
            {
                results.Add(Check("suppression arena set up", false, "needs a map, the assault rifle and an enemy faction"));
                return results;
            }

            var wasEnabled = settings.enableSuppression;
            var wasPinning = settings.enableSuppressionPinning;
            var wasStrength = settings.suppressionStrength;
            settings.enableSuppression = true;
            settings.enableSuppressionPinning = true;
            settings.suppressionStrength = 1f;

            var spawned = new List<Pawn>();
            try
            {
                var origin = map.Center;
                var shooter = RangedCombatArena.SpawnShooter(map, origin + IntVec3.West * 8, 10, rifle);
                var target = SpawnHostile(map, origin + IntVec3.East * 4, enemyFaction);
                var ally = RangedCombatArena.SpawnTarget(map, origin + IntVec3.East * 4 + IntVec3.North, false);
                spawned.Add(shooter);
                spawned.Add(target);
                spawned.Add(ally);
                if (shooter == null || target == null || ally == null)
                {
                    results.Add(Check("suppression arena set up", false, "could not spawn the pawns"));
                    return results;
                }

                var targetComp = SuppressionUtility.CompFor(target);
                // Held in place so every shot lands beside it; pinning replaces this job.
                var hold = JobMaker.MakeJob(JobDefOf.Wait, 99999);
                target.jobs.StartJob(hold, JobCondition.InterruptForced);

                var missCell = target.Position + IntVec3.South;
                FireAt(shooter, rifle, missCell);
                var afterOne = targetComp?.Level ?? 0f;
                results.Add(Check("a near miss suppresses the hostile target", afterOne > 0f, afterOne.ToString("F1")));
                results.Add(Check("the shooter's allies are not suppressed",
                    (SuppressionUtility.CompFor(ally)?.Level ?? 0f) == 0f && (SuppressionUtility.CompFor(shooter)?.Level ?? 0f) == 0f,
                    (SuppressionUtility.CompFor(ally)?.Level ?? 0f).ToString("F1")));

                var shots = 1;
                while (targetComp != null && !targetComp.Pinned && shots < MaxShots)
                {
                    FireAt(shooter, rifle, missCell);
                    shots++;
                }
                results.Add(Check("sustained fire pins the target", targetComp?.Pinned == true,
                    $"{shots} shots, level {targetComp?.Level:F1}"));
                var job = target.CurJobDef;
                results.Add(Check("a pinned enemy is pinned down", job == VCO_JobDefOf.VCO_PinnedDown,
                    job?.defName ?? "no job"));
                var thought = DefDatabase<ThoughtDef>.GetNamedSilentFail("VCO_UnderFire");
                var state = thought?.Worker.CurrentState(target) ?? ThoughtState.Inactive;
                results.Add(Check("a pinned pawn feels pinned down", state.Active && state.StageIndex == 2,
                    thought == null ? "thought def missing" : $"stage {state.StageIndex}"));

                // Compared with suppression switched off at the same moment and position.
                var aimPinned = StatDefOf.AimingDelayFactor.Worker.GetValue(StatRequest.For(target), false);
                var hitPinned = ShooterFactor(target, rifle, shooter);
                settings.enableSuppression = false;
                var aimClear = StatDefOf.AimingDelayFactor.Worker.GetValue(StatRequest.For(target), false);
                var hitClear = ShooterFactor(target, rifle, shooter);
                settings.enableSuppression = true;
                results.Add(Check("suppression slows aiming", aimPinned > aimClear, $"{aimClear:F2} -> {aimPinned:F2}"));
                results.Add(Check("suppression lowers the shooter's hit factor", hitPinned < hitClear,
                    $"{hitClear:F3} -> {hitPinned:F3}"));

                // Reaching cover and dropping takes a few seconds; the job holds prone after the level fades.
                for (var i = 0; i < 300 && target.CurJobDef == VCO_JobDefOf.VCO_PinnedDown
                                && target.GetPosture() == PawnPosture.Standing; i++)
                {
                    Find.TickManager.DoSingleTick();
                }
                results.Add(Check("a pinned enemy drops prone",
                    !target.Downed && target.CurJobDef == VCO_JobDefOf.VCO_PinnedDown && target.GetPosture() != PawnPosture.Standing,
                    $"{target.CurJobDef?.defName ?? "no job"}, {target.GetPosture()}"));

                // Nothing left to fight while the level decays.
                shooter.Destroy(DestroyMode.Vanish);
                ally.Destroy(DestroyMode.Vanish);
                HealthUtility.DamageUntilDowned(target, allowBleedingWounds: false);
                for (var i = 0; i < 600; i++)
                {
                    Find.TickManager.DoSingleTick();
                }
                results.Add(Check("suppression fades once the shooting stops", (targetComp?.Level ?? 1f) == 0f,
                    (targetComp?.Level ?? -1f).ToString("F1")));

                results.AddRange(ArmorTests(map, origin + IntVec3.North * 6, spawned));
                results.AddRange(FlybyTests(map, origin + IntVec3.South * 8, rifle, enemyFaction, spawned));
            }
            catch (Exception e)
            {
                results.Add(Check("suppression arena ran", false, e.ToString()));
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
                settings.enableSuppression = wasEnabled;
                settings.enableSuppressionPinning = wasPinning;
                settings.suppressionStrength = wasStrength;
            }
            return results;
        }

        /// <summary>An unarmed hostile pawn, so it cannot fire back while the test ticks the game.</summary>
        /// <summary>Armour factor for real apparel sets against real bullets, reported for tuning against Combat Extended.</summary>
        private static List<AssertionResult> ArmorTests(Map map, IntVec3 cell, List<Pawn> spawned)
        {
            var results = new List<AssertionResult>();
            var rifle = RawPenetrationOf("Bullet_AssaultRifle");
            var charge = RawPenetrationOf("Bullet_ChargeRifle");
            var none = ArmorOf(map, cell, spawned);
            var flak = ArmorOf(map, cell + IntVec3.East * 2, spawned, "Apparel_FlakVest", "Apparel_FlakPants", "Apparel_AdvancedHelmet");
            var marine = ArmorOf(map, cell + IntVec3.East * 4, spawned, "Apparel_PowerArmor", "Apparel_PowerArmorHelmet");

            var rifleNone = SuppressionUtility.ArmorFactor(none, rifle);
            var rifleFlak = SuppressionUtility.ArmorFactor(flak, rifle);
            var rifleMarine = SuppressionUtility.ArmorFactor(marine, rifle);
            var chargeMarine = SuppressionUtility.ArmorFactor(marine, charge);
            var detail = $"armour {none:F2}/{flak:F2}/{marine:F2}; assault rifle (pen {rifle:F2}) x{rifleNone:F2} none, "
                         + $"x{rifleFlak:F2} flak, x{rifleMarine:F2} marine; charge rifle (pen {charge:F2}) x{chargeMarine:F2} marine";
            results.Add(Check("an unarmoured pawn takes full suppression", Math.Abs(rifleNone - 1f) <= Tolerance, detail));
            results.Add(Check("flak gear resists part of a rifle's suppression", rifleFlak > 0.4f && rifleFlak < 0.85f, detail));
            results.Add(Check("marine armour resists most of a rifle's suppression", rifleMarine < rifleFlak && rifleMarine < 0.5f, detail));
            results.Add(Check("higher penetration suppresses armour more", chargeMarine > rifleMarine, detail));
            return results;
        }

        /// <summary>A bullet flying past a hostile far from where it lands suppresses it; one beside the shooter is left alone.</summary>
        private static List<AssertionResult> FlybyTests(Map map, IntVec3 cell, ThingDef rifle, Faction enemy, List<Pawn> spawned)
        {
            var results = new List<AssertionResult>();
            var shooter = RangedCombatArena.SpawnShooter(map, cell + IntVec3.West * 8, 10, rifle);
            var passed = SpawnHostile(map, cell + IntVec3.North, enemy);
            var nearShooter = SpawnHostile(map, cell + IntVec3.West * 8 + IntVec3.North, enemy);
            spawned.Add(shooter);
            spawned.Add(passed);
            spawned.Add(nearShooter);
            if (shooter == null || passed == null || nearShooter == null)
            {
                results.Add(Check("fly-by arena set up", false, "could not spawn the pawns"));
                return results;
            }
            FireAt(shooter, rifle, cell + IntVec3.East * 8);
            var passedLevel = SuppressionUtility.CompFor(passed)?.Level ?? 0f;
            var nearLevel = SuppressionUtility.CompFor(nearShooter)?.Level ?? 0f;
            results.Add(Check("a bullet flying past suppresses a hostile it misses by a cell", passedLevel > 0f,
                passedLevel.ToString("F1")));
            results.Add(Check("pawns beside the shooter are not suppressed by its own shots", nearLevel == 0f,
                nearLevel.ToString("F1")));
            return results;
        }

        private static float RawPenetrationOf(string bulletDefName)
        {
            var def = DefDatabase<ThingDef>.GetNamedSilentFail(bulletDefName);
            return def?.projectile == null ? 0f : SuppressionUtility.RawPenetration(def.projectile.GetArmorPenetration((Thing)null));
        }

        private static float ArmorOf(Map map, IntVec3 cell, List<Pawn> spawned, params string[] apparel)
        {
            var pawn = RangedCombatArena.SpawnTarget(map, cell, false);
            spawned.Add(pawn);
            foreach (var defName in apparel)
            {
                var def = DefDatabase<ThingDef>.GetNamedSilentFail(defName);
                if (def == null)
                {
                    continue;
                }
                var stuff = def.MadeFromStuff ? GenStuff.DefaultStuffFor(def) : null;
                pawn.apparel.Wear((Apparel)ThingMaker.MakeThing(def, stuff), false);
            }
            return SuppressionUtility.OverallSharpArmor(pawn);
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

        /// <summary>The pawn's shooter factor against a target with the weapon briefly in hand.</summary>
        private static float ShooterFactor(Pawn pawn, ThingDef weapon, Thing target)
        {
            var gun = (ThingWithComps)ThingMaker.MakeThing(weapon);
            pawn.equipment.AddEquipment(gun);
            try
            {
                var report = ShotReport.HitReportFor(pawn, pawn.equipment.PrimaryEq.PrimaryVerb, target);
                return ShotReportAccess.GetShooterFactor(ref report);
            }
            finally
            {
                pawn.equipment.DestroyEquipment(gun);
            }
        }

        /// <summary>Launches one bullet from the shooter's weapon at a cell and ticks until it lands.</summary>
        private static void FireAt(Pawn shooter, ThingDef weaponDef, IntVec3 cell)
        {
            var bulletDef = weaponDef.Verbs[0].defaultProjectile;
            var bullet = (Projectile)GenSpawn.Spawn(bulletDef, shooter.Position, shooter.Map);
            bullet.Launch(shooter, shooter.DrawPos, cell, cell, ProjectileHitFlags.None, false, shooter.equipment.Primary);
            for (var i = 0; i < MaxFlightTicks && !bullet.Destroyed; i++)
            {
                Find.TickManager.DoSingleTick();
            }
        }

        private static bool Approx(float actual, float expected) => Math.Abs(actual - expected) <= Tolerance;

        private static AssertionResult Check(string name, bool passed, string detail) =>
            new AssertionResult { Name = name, Passed = passed, Detail = detail };
    }
}
