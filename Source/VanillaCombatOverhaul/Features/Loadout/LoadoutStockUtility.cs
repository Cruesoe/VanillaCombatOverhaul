using System;
using System.Collections.Generic;
using System.Reflection;
using HarmonyLib;
using RimWorld;
using UnityEngine;
using Verse;

namespace VanillaCombatOverhaul
{
    /// <summary>A loadout's setting for one vanilla carry group: which item and how many.</summary>
    public class LoadoutStock : IExposable
    {
        public InventoryStockGroupDef group;
        public ThingDef thingDef;
        public int count;
        // Use the refill the primary weapon needs (Progression: Ammunition) instead of thingDef.
        public bool matchWeapon;

        public LoadoutStock Copy() =>
            new LoadoutStock { group = group, thingDef = thingDef, count = count, matchWeapon = matchWeapon };

        public void ExposeData()
        {
            Scribe_Defs.Look(ref group, "group");
            Scribe_Defs.Look(ref thingDef, "thingDef");
            Scribe_Values.Look(ref count, "count", 0);
            Scribe_Values.Look(ref matchWeapon, "matchWeapon", false);
        }
    }

    /// <summary>
    /// Drives vanilla's per-pawn carry settings (Pawn_InventoryStockTracker) from the pawn's loadout, so
    /// vanilla's fetch job and unload rules handle medicine and any mod's carry groups unchanged.
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

        /// <summary>Carry groups shown in loadouts; the ammunition group only while that mod is enabled.</summary>
        public static IEnumerable<InventoryStockGroupDef> Groups
        {
            get
            {
                foreach (var group in DefDatabase<InventoryStockGroupDef>.AllDefs)
                {
                    if (!IsAmmunitionGroup(group) || AmmunitionEnabled)
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
                stock.Add(DefaultEntry(group));
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
            return entry.thingDef != null && entry.group.thingDefs.Contains(entry.thingDef)
                ? entry.thingDef
                : entry.group.DefaultThingDef;
        }

        /// <summary>Writes the loadout's stock into the pawn's vanilla carry settings where they differ.</summary>
        public static void Apply(Pawn pawn, LoadoutPolicy loadout)
        {
            var tracker = pawn?.inventoryStock;
            if (tracker == null || loadout == null)
            {
                return;
            }
            foreach (var group in Groups)
            {
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
        }

        /// <summary>Vanilla's Carry column and Progression: Ammunition's ammo column, replaced by loadouts.</summary>
        public static bool IsReplacedColumn(PawnColumnDef def) =>
            def != null && (def.defName == "Carry" || def.defName == "PA_AmmoCarry");
    }
}
