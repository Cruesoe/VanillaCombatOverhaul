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
                comp.Loadout = policy;

                results.AddRange(StockTests(pawn, policy, migrated));
                results.AddRange(SidearmChoiceTests(map, pawn, comp, knife, spawned));
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

        /// <summary>Loadout stock drives vanilla's carry settings, seeds from colonists, and replaces the carry columns.</summary>
        private static List<AssertionResult> StockTests(Pawn pawn, LoadoutPolicy policy, AutoEquipPolicyComponent scratch)
        {
            var results = new List<AssertionResult>();
            var group = DefDatabase<InventoryStockGroupDef>.GetNamedSilentFail("Medicine");
            if (group == null || pawn.inventoryStock == null)
            {
                results.Add(Check("medicine carry group exists", false, "no Medicine group or stock tracker"));
                return results;
            }

            var entry = policy.StockFor(group);
            entry.thingDef = ThingDefOf.MedicineHerbal;
            entry.count = 2;
            LoadoutStockUtility.Apply(pawn, policy);
            results.Add(Check("the loadout sets the colonist's medicine to carry",
                pawn.inventoryStock.GetDesiredThingForGroup(group) == ThingDefOf.MedicineHerbal
                && pawn.inventoryStock.GetDesiredCountForGroup(group) == 2,
                $"{pawn.inventoryStock.GetDesiredThingForGroup(group)?.defName} x{pawn.inventoryStock.GetDesiredCountForGroup(group)}"));

            var seeded = scratch.MakeNewLoadout();
            seeded.stock = null;
            LoadoutUtility.CompFor(pawn).Loadout = seeded;
            scratch.SeedStock(new[] { pawn });
            var seededEntry = seeded.StockFor(group);
            results.Add(Check("a new loadout in an existing save takes its users' medicine setting",
                seededEntry.thingDef == ThingDefOf.MedicineHerbal && seededEntry.count == 2,
                $"{seededEntry.thingDef?.defName} x{seededEntry.count}"));
            LoadoutUtility.CompFor(pawn).Loadout = policy;

            var carry = DefDatabase<PawnColumnDef>.GetNamedSilentFail("Carry");
            results.Add(Check("vanilla's carry column is hidden while loadouts are on",
                carry != null && !carry.Worker.VisibleCurrently, carry == null ? "no Carry column" : "hidden"));

            if (LoadoutStockUtility.AmmunitionEnabled)
            {
                results.AddRange(AmmunitionTests(pawn, policy));
            }
            return results;
        }

        /// <summary>With Progression: Ammunition, the ammo stock follows the primary weapon.</summary>
        private static List<AssertionResult> AmmunitionTests(Pawn pawn, LoadoutPolicy policy)
        {
            var results = new List<AssertionResult>();
            var group = DefDatabase<InventoryStockGroupDef>.GetNamedSilentFail(LoadoutStockUtility.AmmunitionGroupDefName);
            var bow = DefDatabase<ThingDef>.GetNamedSilentFail("Bow_Short");
            var arrows = DefDatabase<ThingDef>.GetNamedSilentFail("PA_ArrowRefill");
            var ammo = DefDatabase<ThingDef>.GetNamedSilentFail("PA_AmmoRefill");
            if (group == null || bow == null || arrows == null || ammo == null)
            {
                results.Add(Check("Progression: Ammunition defs found", false, "missing ammo group, bow or refills"));
                return results;
            }
            var entry = policy.StockFor(group);
            entry.matchWeapon = true;
            entry.count = 2;

            LoadoutStockUtility.Apply(pawn, policy);
            var withRifle = pawn.inventoryStock.GetDesiredThingForGroup(group);
            var rifle = pawn.equipment.Primary;
            pawn.equipment.Remove(rifle);
            var bowThing = (ThingWithComps)ThingMaker.MakeThing(bow, GenStuff.DefaultStuffFor(bow));
            pawn.equipment.AddEquipment(bowThing);
            LoadoutStockUtility.Apply(pawn, policy);
            var withBow = pawn.inventoryStock.GetDesiredThingForGroup(group);
            pawn.equipment.DestroyEquipment(bowThing);
            pawn.equipment.AddEquipment(rifle);

            results.Add(Check("ammunition stock matches the primary weapon",
                withRifle == ammo && withBow == arrows, $"rifle {withRifle?.defName}, bow {withBow?.defName}"));
            results.Add(Check("Progression: Ammunition's ammo column is hidden while loadouts are on",
                DefDatabase<PawnColumnDef>.GetNamedSilentFail("PA_AmmoCarry")?.Worker.VisibleCurrently == false, "hidden"));
            return results;
        }

        /// <summary>A locked sidearm beats a better one, and strong shooters keep the gun up close.</summary>
        private static List<AssertionResult> SidearmChoiceTests(Map map, Pawn pawn, CompLoadout comp, ThingDef knife, List<Thing> spawned)
        {
            var results = new List<AssertionResult>();
            var inventory = pawn.inventory.innerContainer;
            var stuffs = GenStuff.AllowedStuffsFor(knife).OrderBy(s => s.GetStatValueAbstract(StatDefOf.SharpDamageMultiplier)).ToList();
            var poor = (ThingWithComps)ThingMaker.MakeThing(knife, stuffs.First());
            var good = (ThingWithComps)ThingMaker.MakeThing(knife, stuffs.Last());
            inventory.TryAdd(poor);
            inventory.TryAdd(good);
            var unlocked = LoadoutUtility.CarriedSidearm(pawn);
            comp.LockedSidearm = poor;
            var locked = LoadoutUtility.CarriedSidearm(pawn);
            comp.LockedSidearm = null;
            inventory.Remove(poor);
            inventory.Remove(good);
            results.Add(Check("the best melee weapon carried is the sidearm, unless one is locked",
                unlocked == good && locked == poor, $"{unlocked?.Stuff?.defName} then {locked?.Stuff?.defName}"));

            var shooting = pawn.skills.GetSkill(SkillDefOf.Shooting);
            var melee = pawn.skills.GetSkill(SkillDefOf.Melee);
            var keptShooting = shooting.Level;
            var keptMelee = melee.Level;
            shooting.Level = 20;
            melee.Level = 5;
            var specialist = LoadoutUtility.KeepsGunUpClose(pawn);
            shooting.Level = 10;
            melee.Level = 10;
            var allRounder = LoadoutUtility.KeepsGunUpClose(pawn);
            shooting.Level = keptShooting;
            melee.Level = keptMelee;
            results.Add(Check("a strong shooter keeps the gun up close, an all-rounder draws the sidearm",
                (!PointBlankUtility.Enabled || specialist) && !allRounder,
                $"shooting 20 melee 5: {specialist}, shooting 10 melee 10: {allRounder}"));
            return results;
        }

        private static string Describe(ThingCount count) =>
            count.Thing == null ? "nothing" : $"{count.Thing.def.defName} x{count.Count}";

        private static AssertionResult Check(string name, bool passed, string detail) =>
            new AssertionResult { Name = name, Passed = passed, Detail = detail };
    }
}
