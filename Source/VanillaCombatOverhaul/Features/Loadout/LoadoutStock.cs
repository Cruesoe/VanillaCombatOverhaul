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
}
