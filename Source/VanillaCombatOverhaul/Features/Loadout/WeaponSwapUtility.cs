using RimWorld;
using UnityEngine;
using Verse;

namespace VanillaCombatOverhaul
{
    /// <summary>
    /// The one place a weapon moves between a pawn's hands and its inventory.
    ///
    /// Every route in the feature -- the float menu, the job driver, the debug actions, the arena
    /// -- funnels through here, so the swap cost applies uniformly and there is exactly one piece
    /// of code that can lose a weapon. Each operation either completes or restores what it found;
    /// none of them ever destroys anything.
    /// </summary>
    public static class WeaponSwapUtility
    {
        /// <summary>
        /// Floor on the swap delay. A featherweight knife should still take a moment: without a
        /// floor, mass near zero makes the delay zero and the job completes on the tick it starts,
        /// which reads as the swap not having happened at all.
        /// </summary>
        public const int MinimumSwapTicks = 15;

        /// <summary>
        /// How long drawing this weapon takes: its mass against the pawn's swap speed.
        ///
        /// Mass is the term because every weapon in the game already has it, including every
        /// modded one, and it is the closest thing vanilla has to "how awkward is this to bring up"
        /// (design rule 6 -- no per-weapon data of our own). Speed is a stat rather than a
        /// constant so manipulation, traits, hediffs and other mods can move it (rule 3).
        /// </summary>
        public static int SwapTicks(Pawn pawn, Thing weapon)
        {
            var perMass = VCOMod.Settings?.sidearmSwapTicksPerMass ?? 60f;
            var mass = weapon?.GetStatValue(StatDefOf.Mass) ?? 1f;
            var speed = pawn?.GetStatValue(VCO_StatDefOf.VCO_WeaponSwapSpeed) ?? 1f;

            return Mathf.Max(MinimumSwapTicks,
                             Mathf.RoundToInt(mass * perMass / Mathf.Max(speed, 0.05f)));
        }

        /// <summary>The weapon a swap would draw, for costing the job before it runs.</summary>
        public static Thing WeaponToDraw(Pawn pawn) => SidearmUtility.CompFor(pawn)?.Reserve;

        /// <summary>
        /// Trade the equipped weapon and the stowed one.
        ///
        /// Ordered so that a failure at any step leaves the pawn exactly as it was found. The
        /// reserve leaves the inventory first, because that is the step most likely to fail; once
        /// the equipment tracker has been touched, the only remaining call cannot fail. An unarmed
        /// pawn simply draws the reserve and is left with none.
        /// </summary>
        public static bool Swap(Pawn pawn)
        {
            var comp = SidearmUtility.CompFor(pawn);
            var reserve = comp?.Reserve;
            if (reserve == null || pawn.equipment == null || pawn.inventory == null)
            {
                return false;
            }

            if (!pawn.inventory.innerContainer.Remove(reserve))
            {
                return false;
            }

            var primary = pawn.equipment.Primary;
            if (primary != null)
            {
                pawn.equipment.Remove(primary);
                if (!pawn.inventory.innerContainer.TryAdd(primary, false))
                {
                    // Nothing has been lost yet, so put both weapons back where they came from.
                    pawn.equipment.AddEquipment(primary);
                    pawn.inventory.innerContainer.TryAdd(reserve, false);
                    return false;
                }
            }

            pawn.equipment.AddEquipment(reserve);
            comp.Designate(primary);
            VCODiagnostics.CountFor(pawn, "sidearm.swap");
            return true;
        }

        /// <summary>
        /// Take a weapon as the sidearm, replacing whatever was stowed before.
        ///
        /// The weapon may be lying on the map, sitting in a stockpile, or already in the pawn's
        /// own inventory as haulage; <see cref="ThingOwner.TryAddOrTransfer"/> covers all three.
        /// The previous sidearm is dropped rather than destroyed, and only after the new one is
        /// safely stowed.
        /// </summary>
        public static bool TakeAsSidearm(Pawn pawn, ThingWithComps weapon)
        {
            var comp = SidearmUtility.CompFor(pawn);
            if (comp == null || weapon == null || !SidearmUtility.CanBeSidearm(pawn, weapon, out _))
            {
                return false;
            }

            var previous = comp.Reserve;
            if (previous == weapon)
            {
                return true;
            }

            if (!pawn.inventory.innerContainer.Contains(weapon) && !Stow(pawn, weapon))
            {
                return false;
            }

            comp.Designate(weapon);

            // A weapon Pick Up And Haul brought in is still on its haul list, and PUAH would take
            // it back out to a stockpile. Designating it a sidearm is the player saying otherwise.
            PickUpAndHaulUtility.ReleaseFromHauling(pawn, weapon);

            if (previous != null)
            {
                DropWeapon(pawn, previous);
            }
            VCODiagnostics.CountFor(pawn, "sidearm.taken");
            return true;
        }

        /// <summary>Drop the stowed weapon at the pawn's feet and forget it.</summary>
        public static bool DropSidearm(Pawn pawn)
        {
            var comp = SidearmUtility.CompFor(pawn);
            var reserve = comp?.Reserve;
            if (reserve == null)
            {
                return false;
            }

            var dropped = DropWeapon(pawn, reserve);
            if (dropped)
            {
                comp.Designate(null);
            }
            return dropped;
        }

        /// <summary>
        /// Move a weapon into the pawn's inventory from wherever it currently is.
        ///
        /// Two cases, and the order of the tests between them matters. A spawned weapon reports a
        /// holding owner like any other -- the map's own container -- but a transfer out of that
        /// container is refused, so being on the map has to be checked first and answered with a
        /// despawn. Only a weapon that is held without being spawned, in another pawn's inventory
        /// or a shelf, is a transfer.
        ///
        /// A despawned weapon that then fails to go into the inventory is put straight back on the
        /// ground: one that is neither spawned nor held has left the game.
        /// </summary>
        private static bool Stow(Pawn pawn, Thing weapon)
        {
            if (weapon.Spawned)
            {
                var map = weapon.Map;
                var position = weapon.Position;
                weapon.DeSpawn();
                if (pawn.inventory.innerContainer.TryAdd(weapon, false))
                {
                    return true;
                }
                if (map != null)
                {
                    GenPlace.TryPlaceThing(weapon, position, map, ThingPlaceMode.Near);
                }
                return false;
            }

            return weapon.holdingOwner != null
                ? pawn.inventory.innerContainer.TryAddOrTransfer(weapon, false)
                : pawn.inventory.innerContainer.TryAdd(weapon, false);
        }

        /// <summary>
        /// Put a carried weapon on the ground. An unspawned pawn -- in a caravan, in a pod, on the
        /// world map -- has nowhere to drop it, so the weapon stays carried rather than vanishing.
        /// </summary>
        private static bool DropWeapon(Pawn pawn, Thing weapon)
        {
            if (pawn?.Map == null || !pawn.Spawned)
            {
                return false;
            }
            return pawn.inventory.innerContainer.TryDrop(
                weapon, pawn.Position, pawn.Map, ThingPlaceMode.Near, out _);
        }
    }
}
