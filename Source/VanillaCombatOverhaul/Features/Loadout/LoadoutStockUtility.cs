using System;
using System.Collections.Generic;
using System.Reflection;
using HarmonyLib;
using RimWorld;
using UnityEngine;
using Verse;

namespace VanillaCombatOverhaul
{
    /// <summary>
    /// Drives vanilla's per-pawn carry settings (Pawn_InventoryStockTracker) from the pawn's loadout, so
    /// vanilla's fetch job and unload rules handle medicine and any mod's carry groups unchanged. Applied when
    /// a colonist's loadout changes, a group is edited, or the primary weapon changes; settings changed
    /// elsewhere (such as Dubs Mint Menus' gear menu) stay until then.
    /// </summary>
    [StaticConstructorOnStartup]
    public static class LoadoutStockUtility
    {
        public const string AmmunitionPackageId = "ferny.ProgressionAmmunition";
        public const string AmmunitionGroupDefName = "PA_AmmoStock";

        private static readonly bool AmmunitionActive = ModsConfig.IsActive(AmmunitionPackageId);
        private static readonly Type CompAmmoType = TypeIfActive("ProgressionAmmunition.CompAmmo");
        private static readonly PropertyInfo ConsumableDefProperty =
            CompAmmoType == null ? null : AccessTools.Property(CompAmmoType, "ConsumableDef");
        private static readonly Type AmmunitionModType = TypeIfActive("ProgressionAmmunition.ProgressionAmmunitionMod");
        private static readonly PropertyInfo AmmunitionEnabledProperty =
            AmmunitionModType == null ? null : AccessTools.Property(AmmunitionModType, "Enabled");

        private static Type TypeIfActive(string name) => AmmunitionActive ? AccessTools.TypeByName(name) : null;

        /// <summary>Progression: Ammunition is active and switched on in its own settings.</summary>
        public static bool AmmunitionEnabled =>
            AmmunitionActive && (AmmunitionEnabledProperty == null || (bool)AmmunitionEnabledProperty.GetValue(null));

        public static bool IsAmmunitionGroup(InventoryStockGroupDef group) =>
            AmmunitionActive && group.defName == AmmunitionGroupDefName;

        /// <summary>A group with items to choose from; one made in code with none has no default item.</summary>
        private static bool HasItems(InventoryStockGroupDef group) => !group.thingDefs.NullOrEmpty();

        /// <summary>Carry groups shown in loadouts; the ammunition group only while that mod is enabled.</summary>
        public static IEnumerable<InventoryStockGroupDef> Groups
        {
            get
            {
                foreach (var group in DefDatabase<InventoryStockGroupDef>.AllDefs)
                {
                    if (HasItems(group) && (!IsAmmunitionGroup(group) || AmmunitionEnabled))
                    {
                        yield return group;
                    }
                }
            }
        }

        public static List<LoadoutStock> DefaultStock()
        {
            var stock = new List<LoadoutStock>();
            foreach (var group in DefDatabase<InventoryStockGroupDef>.AllDefs)
            {
                if (HasItems(group))
                {
                    stock.Add(DefaultEntry(group));
                }
            }
            return stock;
        }

        public static LoadoutStock DefaultEntry(InventoryStockGroupDef group) =>
            new LoadoutStock
            {
                group = group,
                thingDef = group.DefaultThingDef,
                count = group.min,
                matchWeapon = IsAmmunitionGroup(group)
            };

        /// <summary>Copies a pawn's current carry settings into a loadout's stock.</summary>
        public static List<LoadoutStock> StockFromPawn(Pawn pawn)
        {
            var stock = DefaultStock();
            if (pawn?.inventoryStock == null)
            {
                return stock;
            }
            foreach (var entry in stock)
            {
                if (pawn.inventoryStock.stockEntries.TryGetValue(entry.group, out var current))
                {
                    entry.thingDef = current.thingDef ?? entry.thingDef;
                    entry.count = current.count;
                }
            }
            return stock;
        }

        /// <summary>The refill the pawn's primary weapon (or swapped-out primary) uses, or null.</summary>
        public static ThingDef AmmunitionFor(Pawn pawn)
        {
            if (ConsumableDefProperty == null)
            {
                return null;
            }
            var weapon = LoadoutUtility.CompFor(pawn)?.SwappedPrimary ?? pawn.equipment?.Primary;
            var comps = weapon?.AllComps;
            if (comps == null)
            {
                return null;
            }
            for (var i = 0; i < comps.Count; i++)
            {
                if (CompAmmoType.IsInstanceOfType(comps[i]))
                {
                    return ConsumableDefProperty.GetValue(comps[i]) as ThingDef;
                }
            }
            return null;
        }

        /// <summary>The item and count a loadout entry asks this pawn to carry.</summary>
        public static ThingDef ThingFor(LoadoutStock entry, Pawn pawn)
        {
            if (entry.matchWeapon)
            {
                var ammo = AmmunitionFor(pawn);
                if (ammo != null && entry.group.thingDefs.Contains(ammo))
                {
                    return ammo;
                }
            }
            return ChosenThing(entry);
        }

        /// <summary>The entry's chosen item, or the group's default once that item is no longer in the group.</summary>
        public static ThingDef ChosenThing(LoadoutStock entry) =>
            entry.thingDef != null && entry.group.thingDefs.Contains(entry.thingDef)
                ? entry.thingDef
                : entry.group.DefaultThingDef;

        /// <summary>Writes all of the loadout's stock into the pawn's vanilla carry settings where they differ.</summary>
        public static void Apply(Pawn pawn, LoadoutPolicy loadout)
        {
            if (pawn?.inventoryStock == null || loadout == null)
            {
                return;
            }
            foreach (var group in Groups)
            {
                ApplyGroup(pawn, loadout, group);
            }
        }

        /// <summary>Writes one group of the loadout's stock into the pawn's carry settings where they differ.</summary>
        public static void ApplyGroup(Pawn pawn, LoadoutPolicy loadout, InventoryStockGroupDef group)
        {
            var tracker = pawn?.inventoryStock;
            if (tracker == null || loadout == null)
            {
                return;
            }
            var entry = loadout.StockFor(group);
            var thing = ThingFor(entry, pawn);
            var count = Mathf.Clamp(entry.count, group.min, group.max);
            if (tracker.GetDesiredThingForGroup(group) != thing)
            {
                tracker.SetThingForGroup(group, thing);
            }
            if (tracker.GetDesiredCountForGroup(group) != count)
            {
                tracker.SetCountForGroup(group, count);
            }
        }

        /// <summary>Re-applies the groups set to match the primary weapon, after the weapon changes.</summary>
        public static void ApplyWeaponMatch(Pawn pawn, LoadoutPolicy loadout)
        {
            if (pawn?.inventoryStock == null || loadout?.stock == null)
            {
                return;
            }
            foreach (var group in Groups)
            {
                if (loadout.StockFor(group).matchWeapon)
                {
                    ApplyGroup(pawn, loadout, group);
                }
            }
        }

        /// <summary>Applies one group of a loadout to every colonist using it, after the group is edited.</summary>
        public static void ApplyToUsers(LoadoutPolicy loadout, InventoryStockGroupDef group)
        {
            if (!LoadoutUtility.Enabled)
            {
                return;
            }
            foreach (var pawn in PawnsFinder.AllMapsCaravansAndTravellingTransporters_Alive_Colonists)
            {
                if (LoadoutUtility.LoadoutFor(pawn) == loadout)
                {
                    ApplyGroup(pawn, loadout, group);
                }
            }
        }

        /// <summary>Vanilla's Carry column and Progression: Ammunition's ammo column, replaced by loadouts.</summary>
        public static bool IsReplacedColumn(PawnColumnDef def) =>
            def != null && (def.defName == "Carry" || def.defName == "PA_AmmoCarry");
    }
}
