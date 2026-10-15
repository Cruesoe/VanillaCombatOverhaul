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
}
