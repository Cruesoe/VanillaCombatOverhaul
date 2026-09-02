using System.Collections.Generic;
using System.Linq;
using Verse;

namespace VanillaCombatOverhaul
{
    /// <summary>
    /// Keeps Pick Up And Haul and sidearms out of each other's way.
    ///
    /// PUAH stuffs haulage into the same inventory a sidearm lives in, and tracks what it put
    /// there in a comp of its own; its unload job then walks that record and drops everything in
    /// it. A weapon VCO stows never enters that record -- VCO adds it to the container directly --
    /// so in the ordinary case the two systems simply do not meet.
    ///
    /// The exception is a weapon PUAH hauled first and the player designated as a sidearm second.
    /// That weapon is in PUAH's record, so PUAH would carry it off to a stockpile and the sidearm
    /// would quietly disappear from the pawn. Taking it out of the record on designation is the
    /// whole fix.
    ///
    /// Done by name rather than by assembly reference: PUAH is a soft dependency, and every method
    /// here is inert when it is not installed.
    /// </summary>
    public static class PickUpAndHaulUtility
    {
        private const string CompTypeName = "PickUpAndHaul.CompHauledToInventory";
        private const string GetSetMethod = "GetHashSet";

        private static bool resolved;
        private static System.Type compType;

        /// <summary>Whether Pick Up And Haul is loaded and its tracking comp was found.</summary>
        public static bool Present
        {
            get
            {
                Resolve();
                return compType != null;
            }
        }

        /// <summary>
        /// Tell PUAH to stop considering this weapon haulage.
        ///
        /// A miss is not an error: the weapon is usually not in the record at all, and PUAH may
        /// have changed its internals. Either way the sidearm still works; the worst case is the
        /// pre-existing PUAH behaviour of hauling the weapon away, which is what happens today
        /// without this mod installed.
        /// </summary>
        public static void ReleaseFromHauling(Pawn pawn, Thing weapon)
        {
            Resolve();
            if (compType == null || pawn?.AllComps == null || weapon == null)
            {
                return;
            }

            var comp = pawn.AllComps.FirstOrDefault(c => compType.IsInstanceOfType(c));
            if (comp == null)
            {
                return;
            }

            var method = compType.GetMethod(GetSetMethod);
            if (method == null)
            {
                return;
            }

            if (method.Invoke(comp, null) is ICollection<Thing> tracked && tracked.Remove(weapon))
            {
                VCODiagnostics.CountFor(pawn, "sidearm.releasedFromHauling");
            }
        }

        private static void Resolve()
        {
            if (resolved)
            {
                return;
            }
            resolved = true;

            compType = GenTypes.GetTypeInAnyAssembly(CompTypeName);
            if (compType != null)
            {
                Log.Message("[VCO] Pick Up And Haul detected; sidearms will be excluded from its "
                            + "haul record so it cannot carry them off.");
            }
        }
    }
}
