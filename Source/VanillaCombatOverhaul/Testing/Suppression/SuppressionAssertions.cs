using System;
using System.Collections.Generic;
using System.Linq;
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
                Approx(SuppressionUtility.AccuracyFor(SuppressionUtility.SuppressedLevel * 0.5f), 1f)
                && Approx(SuppressionUtility.AimTimeFor(SuppressionUtility.SuppressedLevel * 0.5f), 1f), "1.0"));
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
            results.Add(Check("no armor leaves suppression whole",
                Approx(SuppressionUtility.ArmorFactor(0f, 0.16f), 1f), SuppressionUtility.ArmorFactor(0f, 0.16f).ToString("F2")));
            results.Add(Check("more armor resists more, more penetration resists less",
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
                ThoughtWorker_UnderFire.StageFor(0f) == -1 && ThoughtWorker_UnderFire.StageFor(0.5f) == 0
                && ThoughtWorker_UnderFire.StageFor(SuppressionUtility.SuppressedLevel) == 1
                && ThoughtWorker_UnderFire.StageFor(SuppressionUtility.PinnedLevel) == 2, "-1 / 0 / 1 / 2"));
            results.Add(Check("armor can stop suppression entirely, and no penetration suppresses nothing",
                Approx(SuppressionUtility.ArmorFactor(2f, 0.1f), 0f) && Approx(SuppressionUtility.ArmorFactor(0f, 0f), 0f), "0 / 0"));

            // Vanilla's default major break threshold is 0.35 * 4/7 = 0.2.
            var average = SuppressionUtility.ThresholdFor(0.5f, 0.2f);
            var happy = SuppressionUtility.ThresholdFor(0.8f, 0.2f);
            var ironWilled = SuppressionUtility.ThresholdFor(0.5f, 0.1f);
            var breaking = SuppressionUtility.ThresholdFor(0.15f, 0.2f);
            results.Add(Check("the threshold matches Combat Extended's at 50% mood",
                Math.Abs(average - 71.89f) < 0.05f, average.ToString("F2")));
            results.Add(Check("good mood and steady nerves raise the threshold",
                happy > average && ironWilled > average, $"{average:F1} < {happy:F1}, {ironWilled:F1}"));
            results.Add(Check("a pawn below its major break threshold is suppressed by any shot",
                Approx(breaking, SuppressionUtility.MinThreshold), breaking.ToString("F2")));
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

                // Rounds at the assault rifle's burst spacing, without pausing between bursts.
                var bulletDef = rifle.Verbs[0].defaultProjectile;
                var rifleSpacing = rifle.Verbs[0].ticksBetweenBurstShots;
                var shots = 1;
                var inFlight = new List<Projectile>();
                while (targetComp != null && !targetComp.Pinned && shots < MaxShots)
                {
                    inFlight.Add(Launch(shooter, bulletDef, missCell));
                    shots++;
                    TickWhile(rifleSpacing, () => !targetComp.Pinned);
                }
                TickWhile(MaxFlightTicks, () => !targetComp.Pinned && inFlight.Exists(b => !b.Destroyed));
                results.Add(Check("sustained fire pins the target", targetComp?.Pinned == true,
                    $"{shots} rounds; level {targetComp?.Level:F1}, threshold {targetComp?.Threshold:F1}"));
                // A pin reached between hits, as mood falls, is acted on at the pawn's next tick interval.
                TickWhile(15, () => target.CurJobDef != VCO_JobDefOf.VCO_PinnedDown);
                var job = target.CurJobDef;
                results.Add(Check("a pinned enemy is pinned down", job == VCO_JobDefOf.VCO_PinnedDown,
                    $"{job?.defName ?? "no job"}, forced {target.CurJob?.playerForced}, mental {target.InMentalState}, posture {target.GetPosture()}, melee threat {target.mindState?.MeleeThreatStillThreat}, burning {target.IsBurning()}, bursting {target.stances?.curStance}"));
                var moveCrawl = target.GetStatValue(StatDefOf.MoveSpeed, true, -1);
                results.Add(Check("a pinned pawn crawls when it moves",
                    SuppressionUtility.CrawlsWhenMoving(target) && (!target.pather.Moving || target.Crawling),
                    $"moving {target.pather.Moving}, crawling {target.Crawling}, speed {moveCrawl:F2}"));
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
                results.AddRange(BurstTests(map, origin + IntVec3.North * 14, enemyFaction, spawned));
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
        /// <summary>Armor factor for real apparel sets against real bullets, reported for tuning against Combat Extended.</summary>
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
            var detail = $"armor {none:F2}/{flak:F2}/{marine:F2}; assault rifle (pen {rifle:F2}) x{rifleNone:F2} none, "
                         + $"x{rifleFlak:F2} flak, x{rifleMarine:F2} marine; charge rifle (pen {charge:F2}) x{chargeMarine:F2} marine";
            results.Add(Check("an unarmored pawn takes full suppression", Math.Abs(rifleNone - 1f) <= Tolerance, detail));
            results.Add(Check("flak gear resists part of a rifle's suppression", rifleFlak > 0.4f && rifleFlak < 0.85f, detail));
            results.Add(Check("marine armor resists most of a rifle's suppression", rifleMarine < rifleFlak && rifleMarine < 0.5f, detail));
            results.Add(Check("higher penetration suppresses armor more", chargeMarine > rifleMarine, detail));
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
            var bullet = Launch(shooter, weaponDef.Verbs[0].defaultProjectile, cell);
            TickWhile(MaxFlightTicks, () => !bullet.Destroyed);
        }

        private static Projectile Launch(Pawn shooter, ThingDef bulletDef, IntVec3 cell)
        {
            var bullet = (Projectile)GenSpawn.Spawn(bulletDef, shooter.Position, shooter.Map);
            bullet.Launch(shooter, shooter.DrawPos, cell, cell, ProjectileHitFlags.None, false, shooter.equipment.Primary);
            return bullet;
        }

        private static void TickWhile(int maxTicks, Func<bool> condition)
        {
            for (var i = 0; i < maxTicks && condition(); i++)
            {
                Find.TickManager.DoSingleTick();
            }
        }

        /// <summary>
        /// One full burst of each weapon's own bullets and spacing, landing a cell from a fresh hostile at 50% mood,
        /// reporting the peak level reached (1 suppressed, 4 pinned).
        /// </summary>
        private static List<AssertionResult> BurstTests(Map map, IntVec3 cell, Faction enemyFaction, List<Pawn> spawned)
        {
            var results = new List<AssertionResult>();
            var settings = VCOMod.Settings;
            var wasPinning = settings.enableSuppressionPinning;
            // Peak level only; the target stays standing on its cell.
            settings.enableSuppressionPinning = false;
            try
            {
                var peaks = new Dictionary<string, float>();
                var thresholds = new List<string>();
                foreach (var weaponName in new[] { "Gun_AssaultRifle", "Gun_HeavySMG", "Gun_LMG", "Gun_Minigun" })
                {
                    var weapon = DefDatabase<ThingDef>.GetNamedSilentFail(weaponName);
                    if (weapon == null)
                    {
                        continue;
                    }
                    var shooter = RangedCombatArena.SpawnShooter(map, cell + IntVec3.West * 12, 10, weapon);
                    var target = SpawnHostile(map, cell, enemyFaction);
                    spawned.Add(shooter);
                    spawned.Add(target);
                    if (target.needs?.mood != null)
                    {
                        target.needs.mood.CurLevel = 0.5f;
                    }
                    target.jobs.StartJob(JobMaker.MakeJob(JobDefOf.Wait, 99999), JobCondition.InterruptForced);
                    var comp = SuppressionUtility.CompFor(target);
                    var verb = weapon.Verbs[0];
                    var peak = 0f;
                    var inFlight = new List<Projectile>();
                    for (var round = 0; round < verb.burstShotCount; round++)
                    {
                        inFlight.Add(Launch(shooter, verb.defaultProjectile, cell + IntVec3.South));
                        TickWhile(round < verb.burstShotCount - 1 ? verb.ticksBetweenBurstShots : 0, () =>
                        {
                            peak = Math.Max(peak, comp.Pressure);
                            return true;
                        });
                    }
                    TickWhile(MaxFlightTicks, () =>
                    {
                        peak = Math.Max(peak, comp.Pressure);
                        return inFlight.Exists(b => !b.Destroyed);
                    });
                    peaks[weaponName] = peak;
                    thresholds.Add($"{comp.Threshold:F0}");
                    shooter.Destroy(DestroyMode.Vanish);
                    target.Destroy(DestroyMode.Vanish);
                }
                var detail = string.Join(", ", peaks.Select(p => $"{p.Key.Replace("Gun_", "")} {p.Value:F2}"))
                             + "; thresholds " + string.Join("/", thresholds);
                results.Add(Check("one assault rifle burst does not suppress an average pawn",
                    peaks.TryGetValue("Gun_AssaultRifle", out var rifle) && rifle < SuppressionUtility.SuppressedLevel, detail));
                results.Add(Check("one LMG burst suppresses but does not pin",
                    peaks.TryGetValue("Gun_LMG", out var lmg) && lmg >= SuppressionUtility.SuppressedLevel
                    && lmg < SuppressionUtility.PinnedLevel, detail));
                results.Add(Check("one minigun burst pins",
                    peaks.TryGetValue("Gun_Minigun", out var minigun) && minigun >= SuppressionUtility.PinnedLevel, detail));
            }
            finally
            {
                settings.enableSuppressionPinning = wasPinning;
            }
            return results;
        }

        private static bool Approx(float actual, float expected) => Math.Abs(actual - expected) <= Tolerance;

        private static AssertionResult Check(string name, bool passed, string detail) =>
            new AssertionResult { Name = name, Passed = passed, Detail = detail };
    }
}
