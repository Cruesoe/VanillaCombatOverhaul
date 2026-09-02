using Verse;

namespace VanillaCombatOverhaul
{
    public class CompProperties_Sidearm : CompProperties
    {
        public CompProperties_Sidearm() => compClass = typeof(CompSidearm);
    }

    /// <summary>
    /// The one weapon a pawn keeps in reserve.
    ///
    /// The weapon itself lives in vanilla's <see cref="Pawn_InventoryTracker.innerContainer"/>,
    /// which already saves, drops on death and travels with caravans. This comp stores nothing
    /// but a reference, so it answers the one question the inventory cannot: which of the things
    /// a pawn is carrying is a weapon it means to fight with, rather than one it happens to be
    /// hauling.
    ///
    /// The reference always names the *stowed* weapon, never the equipped one. After a swap it
    /// points at whatever was just put away, which is what makes swapping back the same operation
    /// as swapping.
    /// </summary>
    public class CompSidearm : ThingComp
    {
        private ThingWithComps reserve;

        public Pawn Pawn => parent as Pawn;

        /// <summary>
        /// The stowed weapon, or null.
        ///
        /// A designation is only ever as good as the inventory it points into: the weapon can be
        /// dropped, stolen, burned or sold by code that knows nothing about this mod. Rather than
        /// patch every one of those paths, the read verifies and clears -- a stale designation is
        /// indistinguishable from none, and never resurrects a weapon the pawn no longer holds.
        /// </summary>
        public ThingWithComps Reserve
        {
            get
            {
                if (reserve == null)
                {
                    return null;
                }
                if (reserve.Destroyed || !Holds(reserve))
                {
                    reserve = null;
                }
                return reserve;
            }
        }

        public bool HasReserve => Reserve != null;

        /// <summary>Records the stowed weapon. Null clears the designation.</summary>
        public void Designate(ThingWithComps weapon) => reserve = weapon;

        private bool Holds(Thing weapon)
        {
            var pawn = Pawn;
            return pawn?.inventory != null && pawn.inventory.innerContainer.Contains(weapon);
        }

        public override void PostExposeData()
        {
            base.PostExposeData();
            Scribe_References.Look(ref reserve, "vcoSidearm");
        }
    }
}
