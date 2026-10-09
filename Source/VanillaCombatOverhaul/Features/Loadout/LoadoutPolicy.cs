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

        protected override string LoadKey => "VCO_Loadout";

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
            if (Scribe.mode == LoadSaveMode.PostLoadInit)
            {
                weaponFilter ??= LoadoutUtility.NewWeaponFilter(ranged: true, melee: true);
                sidearmFilter ??= LoadoutUtility.NewWeaponFilter(ranged: false, melee: true);
                items ??= new List<LoadoutItem>();
                items.RemoveAll(i => i?.thingDef == null || i.count <= 0);
            }
        }
    }
}
