using System;
using System.Collections.Generic;
using System.Linq;
using RimWorld;
using Verse;
using Verse.AI;

namespace VanillaCombatOverhaul
{
    /// <summary>Loadout checks on a live map: kept inventory, sidearm swaps, item stocking and save migration.</summary>
    public static class LoadoutAssertions
    {
        public static List<AssertionResult> MapTests(Map map)
        {
            var results = new List<AssertionResult>();
            var settings = VCOMod.Settings;
            var database = AutoEquipPolicyComponent.Current;
            var rifle = DefDatabase<ThingDef>.GetNamedSilentFail("Gun_AssaultRifle");
            var knife = DefDatabase<ThingDef>.GetNamedSilentFail("MeleeWeapon_Knife");
            var medicine = ThingDefOf.MedicineHerbal;
            var enemyFaction = Find.FactionManager.RandomEnemyFaction(allowNonHumanlike: false);
            if (map == null || settings == null || database == null || rifle == null || knife == null || enemyFaction == null)
            {
                results.Add(Check("loadout arena set up", false, "needs a map, the loadout database, weapons and an enemy faction"));
                return results;
            }

            var guard = PatchGuard.All.FirstOrDefault(g => g.Id == "Loadout.KeepInventory");
            results.Add(Check("inventory keep patch applied", guard?.Satisfied == true,
                guard == null ? "guard missing" : $"{guard.Actual} of {guard.Expected}"));

            var wasEnabled = settings.enableAutoEquip;
            settings.enableAutoEquip = true;
            var loadoutCount = database.AllLoadouts.Count;
            var spawned = new List<Thing>();
            LoadoutPolicy policy = null;
            try
            {
                var origin = map.Center + IntVec3.North * 10;
                var pawn = RangedCombatArena.SpawnShooter(map, origin, 10, rifle);
                spawned.Add(pawn);
                if (pawn == null)
                {
                    results.Add(Check("loadout pawn spawned", false, "could not spawn a colonist"));
                    return results;
                }
                var comp = LoadoutUtility.CompFor(pawn);
                policy = database.MakeNewLoadout();
                policy.autoPrimary = false;
                policy.carrySidearm = true;
                policy.items.Add(new LoadoutItem(medicine, 2));
                comp.Loadout = policy;

                var inventory = pawn.inventory.innerContainer;
                inventory.ClearAndDestroyContents();
                var sidearm = (ThingWithComps)ThingMaker.MakeThing(knife, GenStuff.DefaultStuffFor(knife));
                inventory.TryAdd(sidearm);
                var herbs = ThingMaker.MakeThing(medicine);
                herbs.stackCount = 3;
                inventory.TryAdd(herbs);
                var steel = ThingMaker.MakeThing(ThingDefOf.Steel);
                steel.stackCount = 10;
                inventory.TryAdd(steel);

                var first = pawn.inventory.FirstUnloadableThing;
                results.Add(Check("unloading keeps the loadout's count of an item",
                    first.Thing?.def == medicine && first.Count == 1, Describe(first)));
                inventory.Remove(herbs);
                herbs.stackCount = 2;
                inventory.TryAdd(herbs);
                first = pawn.inventory.FirstUnloadableThing;
                results.Add(Check("unloading keeps the sidearm and drops other things",
                    first.Thing == steel && first.Count == 10, Describe(first)));

                // Sidearm swap when an enemy reaches a drafted shooter.
                pawn.drafter.Drafted = true;
                var enemy = PawnGenerator.GeneratePawn(enemyFaction.def.basicMemberKind ?? PawnKindDefOf.Villager, enemyFaction);
                enemy.equipment?.DestroyAllEquipment();
                GenSpawn.Spawn(enemy, pawn.Position + IntVec3.East, map);
                spawned.Add(enemy);
                results.Add(Check("an adjacent enemy counts as a melee threat", LoadoutUtility.AdjacentThreat(pawn),
                    enemy.Position.ToString()));
                comp.CompTickInterval(CompLoadout.CheckIntervalTicks);
                results.Add(Check("a drafted shooter switches to its sidearm when reached",
                    pawn.equipment.Primary == sidearm && LoadoutUtility.IsRanged(comp.SwappedPrimary),
                    pawn.equipment.Primary?.def.defName ?? "empty hands"));

                enemy.Destroy(DestroyMode.Vanish);
                for (var i = 0; i < CompLoadout.CalmChecksBeforeSwapBack; i++)
                {
                    comp.CompTickInterval(CompLoadout.CheckIntervalTicks);
                }
                results.Add(Check("the shooter switches back once the enemy is gone",
                    LoadoutUtility.IsRanged(pawn.equipment.Primary) && comp.SwappedPrimary == null
                    && inventory.Contains(sidearm),
                    pawn.equipment.Primary?.def.defName ?? "empty hands"));

                comp.ToggleSidearm();
                var manual = pawn.equipment.Primary == sidearm;
                comp.CompTickInterval(CompLoadout.CheckIntervalTicks);
                results.Add(Check("a manual switch stays while drafted", manual && pawn.equipment.Primary == sidearm,
                    pawn.equipment.Primary?.def.defName ?? "empty hands"));
                comp.ToggleSidearm();
                pawn.drafter.Drafted = false;

                // Stocking: the job giver fetches missing items.
                inventory.Remove(herbs);
                policy.carrySidearm = false;
                var pile = ThingMaker.MakeThing(medicine);
                pile.stackCount = 5;
                GenPlace.TryPlaceThing(pile, pawn.Position + IntVec3.South * 2, map, ThingPlaceMode.Near);
                spawned.Add(pile);
                var package = new JobGiver_Loadout().TryIssueJobPackage(pawn, default(JobIssueParams));
                var job = package.Job;
                results.Add(Check("a colonist short of an item fetches it",
                    job?.def == JobDefOf.TakeCountToInventory && job.targetA.Thing?.def == medicine && job.count == 2,
                    job == null ? "no job" : $"{job.def.defName} {job.targetA.Thing?.def.defName} x{job.count}"));

                // Migration of an old apparel-policy weapon filter.
                var apparel = pawn.outfits.CurrentApparelPolicy;
                var legacyFilter = new ThingFilter();
                legacyFilter.SetAllow(knife, true);
                var legacy = new List<AutoEquipPolicyRecord>
                {
                    new AutoEquipPolicyRecord { apparelPolicyId = apparel.id, filter = legacyFilter }
                };
                var migrated = new AutoEquipPolicyComponent(Current.Game);
                migrated.MigrateLegacyPolicies(legacy, new[] { pawn });
                var assigned = comp.Loadout;
                results.Add(Check("old weapon policies become loadouts assigned by apparel policy",
                    assigned != null && assigned.label == apparel.label && assigned.weaponFilter.Allows(knife)
                    && !assigned.weaponFilter.Allows(rifle) && migrated.AllLoadouts.Count == Current.Game.outfitDatabase.AllOutfits.Count,
                    assigned?.label ?? "none"));
                comp.Loadout = null;
            }
            catch (Exception e)
            {
                results.Add(Check("loadout arena ran", false, e.ToString()));
            }
            finally
            {
                foreach (var thing in spawned)
                {
                    if (thing != null && !thing.Destroyed)
                    {
                        thing.Destroy(DestroyMode.Vanish);
                    }
                }
                if (policy != null)
                {
                    database.AllLoadouts.Remove(policy);
                }
                settings.enableAutoEquip = wasEnabled;
            }
            results.Add(Check("the test leaves the colony's loadouts as it found them",
                database.AllLoadouts.Count == loadoutCount, $"{loadoutCount} -> {database.AllLoadouts.Count}"));
            return results;
        }

        private static string Describe(ThingCount count) =>
            count.Thing == null ? "nothing" : $"{count.Thing.def.defName} x{count.Count}";

        private static AssertionResult Check(string name, bool passed, string detail) =>
            new AssertionResult { Name = name, Passed = passed, Detail = detail };
    }
}
