using System.Collections.Generic;
using RimWorld;
using Verse;

namespace VanillaCombatOverhaul
{
    /// <summary>An item a loadout keeps in the pawn's inventory.</summary>
    public class LoadoutItem : IExposable
    {
        public ThingDef thingDef;
        public int count = 1;

        public LoadoutItem()
        {
        }

        public LoadoutItem(ThingDef thingDef, int count)
        {
            this.thingDef = thingDef;
            this.count = count;
        }

        public void ExposeData()
        {
            Scribe_Defs.Look(ref thingDef, "thingDef");
            Scribe_Values.Look(ref count, "count", 1);
        }
    }

    /// <summary>Which weapons a pawn may use, whether it carries a melee sidearm, and what else it carries.</summary>
    public class LoadoutPolicy : Policy
    {
        public const int MaxItemCount = 75;

        public bool autoPrimary = true;
        public ThingFilter weaponFilter = LoadoutUtility.NewWeaponFilter(ranged: true, melee: true);
        public bool carrySidearm;
        public ThingFilter sidearmFilter = LoadoutUtility.NewWeaponFilter(ranged: false, melee: true);
        public List<LoadoutItem> items = new List<LoadoutItem>();
        // Vanilla carry groups (medicine, and mods' such as ammo); null after loading a save from before stock, until seeded.
        public List<LoadoutStock> stock = LoadoutStockUtility.DefaultStock();

        protected override string LoadKey => "VCO_Loadout";

        /// <summary>The entry for a carry group, added with defaults if the group is new.</summary>
        public LoadoutStock StockFor(InventoryStockGroupDef group)
        {
            stock ??= LoadoutStockUtility.DefaultStock();
            var entry = stock.Find(s => s.group == group);
            if (entry == null)
            {
                entry = LoadoutStockUtility.DefaultEntry(group);
                stock.Add(entry);
            }
            return entry;
        }

        public LoadoutPolicy()
        {
        }

        public LoadoutPolicy(int id, string label) : base(id, label)
        {
        }

        public override void CopyFrom(Policy other)
        {
            if (!(other is LoadoutPolicy source))
            {
                return;
            }
            autoPrimary = source.autoPrimary;
            weaponFilter.CopyAllowancesFrom(source.weaponFilter);
            carrySidearm = source.carrySidearm;
            sidearmFilter.CopyAllowancesFrom(source.sidearmFilter);
            items.Clear();
            foreach (var item in source.items)
            {
                items.Add(new LoadoutItem(item.thingDef, item.count));
            }
            stock = new List<LoadoutStock>();
            foreach (var entry in source.stock ?? LoadoutStockUtility.DefaultStock())
            {
                stock.Add(entry.Copy());
            }
        }

        public int CountFor(ThingDef def)
        {
            for (var i = 0; i < items.Count; i++)
            {
                if (items[i].thingDef == def)
                {
                    return items[i].count;
                }
            }
            return 0;
        }

        public override void ExposeData()
        {
            base.ExposeData();
            Scribe_Values.Look(ref autoPrimary, "autoPrimary", true);
            Scribe_Deep.Look(ref weaponFilter, "weaponFilter");
            Scribe_Values.Look(ref carrySidearm, "carrySidearm", false);
            Scribe_Deep.Look(ref sidearmFilter, "sidearmFilter");
            Scribe_Collections.Look(ref items, "items", LookMode.Deep);
            Scribe_Collections.Look(ref stock, "stock", LookMode.Deep);
            if (Scribe.mode == LoadSaveMode.PostLoadInit)
            {
                stock?.RemoveAll(s => s?.group == null);
                weaponFilter ??= LoadoutUtility.NewWeaponFilter(ranged: true, melee: true);
                sidearmFilter ??= LoadoutUtility.NewWeaponFilter(ranged: false, melee: true);
                items ??= new List<LoadoutItem>();
                items.RemoveAll(i => i?.thingDef == null || i.count <= 0);
            }
        }
    }
}
