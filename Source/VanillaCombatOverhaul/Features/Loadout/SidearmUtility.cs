using System.Collections.Generic;
using RimWorld;
using Verse;

namespace VanillaCombatOverhaul
{
    /// <summary>
    /// Who may carry a sidearm, what may be one, and whether the system is running at all.
    ///
    /// The comp is attached at startup the same way <see cref="HeightTargetingUtility"/> attaches
    /// its own: to every humanlike or tool-using race def, in code rather than by patching those
    /// defs in XML, so no mod's race def is claimed as ours (design rule 2).
    /// </summary>
    [StaticConstructorOnStartup]
    public static class SidearmUtility
    {
        /// <summary>
        /// Mods that also move weapons between equipment and inventory. Two systems doing that at
        /// once do not merge -- they fight over the same weapon and lose it -- so VCO stands down
        /// rather than shipping a conflict the player has to diagnose. Steam appends ".steam" to a
        /// package id, hence the postfix-insensitive lookup.
        /// </summary>
        private static readonly string[] ConflictingPackageIds =
        {
            "PeteTimesSix.SimpleSidearms",
            "CETeam.CombatExtended",
            "usagirei.pocketsand"
        };

        /// <summary>Name of the conflicting mod that made this system stand down, or null.</summary>
        public static string ConflictingMod { get; private set; }

        static SidearmUtility()
        {
            ConflictingMod = FindConflict();
            if (ConflictingMod != null)
            {
                Log.Message($"[VCO] Sidearms stood down: {ConflictingMod} already manages carried " +
                            "weapons. Everything else in VCO is unaffected.");
                return;
            }

            foreach (var def in DefDatabase<ThingDef>.AllDefs)
            {
                if (def.race == null || (!def.race.Humanlike && !def.race.ToolUser))
                {
                    continue;
                }
                if (def.HasComp(typeof(CompSidearm)))
                {
                    continue;
                }
                if (def.comps == null)
                {
                    def.comps = new List<CompProperties>();
                }
                def.comps.Add(new CompProperties_Sidearm());
            }
        }

        /// <summary>Cheap gate. False switches every entry point in the feature off at once.</summary>
        public static bool Active =>
            ConflictingMod == null && (VCOMod.Settings?.enableSidearms ?? false);

        /// <summary>The sidearm comp for a pawn, or null if the feature is off or does not apply.</summary>
        public static CompSidearm CompFor(Pawn pawn) =>
            Active ? pawn?.TryGetComp<CompSidearm>() : null;

        /// <summary>
        /// Whether this pawn may take this weapon as its sidearm, and why not when it may not.
        ///
        /// The reason is returned rather than swallowed so the float menu can show a disabled
        /// option that says what is wrong. An option that silently fails to appear reads as a
        /// broken mod; one that appears greyed out with a reason reads as a rule.
        /// </summary>
        public static bool CanBeSidearm(Pawn pawn, Thing weapon, out string reason)
        {
            reason = null;

            if (!Active || pawn?.equipment == null || pawn.inventory == null)
            {
                return false;
            }
            if (CompFor(pawn) == null)
            {
                return false;
            }
            if (!(weapon is ThingWithComps) || weapon.def == null || !weapon.def.IsWeapon)
            {
                return false;
            }
            if (weapon == pawn.equipment.Primary)
            {
                return false;
            }
            if (!EquipmentUtility.CanEquip(weapon, pawn, out reason, true))
            {
                // CanEquip leaves an empty reason for some refusals; never show an empty bracket.
                if (reason.NullOrEmpty())
                {
                    reason = "VCO_Sidearm_CannotEquip".Translate();
                }
                return false;
            }
            return true;
        }

        private static string FindConflict()
        {
            foreach (var id in ConflictingPackageIds)
            {
                var mod = ModLister.GetActiveModWithIdentifier(id, true);
                if (mod != null)
                {
                    return mod.Name;
                }
            }
            return null;
        }
    }
}
