using System.Collections.Generic;
using System.Linq;
using RimWorld;
using Verse;

namespace VanillaCombatOverhaul
{
    /// <summary>
    /// Checks on the one operation that can lose a weapon.
    ///
    /// A swap takes a thing out of one container and puts it in another, and every failure mode
    /// worth fearing is a weapon that ends up in neither. So the live checks all count: the same
    /// weapons that existed before an operation must exist after it, in a place the player can
    /// still get at.
    /// </summary>
    public static class SidearmAssertions
    {
        /// <summary>Formula checks. No map needed.</summary>
        public static List<AssertionResult> SelfTests()
        {
            var results = new List<AssertionResult>();
            var settings = VCOMod.Settings;
            var perMass = settings?.sidearmSwapTicksPerMass ?? 60f;

            var heavy = MakeWeapon(HeavyWeaponName);
            var light = MakeWeapon(LightWeaponName);

            if (heavy == null || light == null)
            {
                results.Add(Check("weapon defs resolved", false, "could not resolve test weapons"));
                return results;
            }

            var heavyTicks = WeaponSwapUtility.SwapTicks(null, heavy);
            var lightTicks = WeaponSwapUtility.SwapTicks(null, light);

            results.Add(Check("a heavier weapon takes longer to draw",
                              heavyTicks > lightTicks,
                              $"{heavy.LabelShort} {heavyTicks}t vs {light.LabelShort} {lightTicks}t"));
            results.Add(Check("no weapon draws instantly",
                              lightTicks >= WeaponSwapUtility.MinimumSwapTicks,
                              lightTicks + "t floor " + WeaponSwapUtility.MinimumSwapTicks));
            results.Add(Check("delay tracks the setting",
                              heavyTicks == UnityEngine.Mathf.Max(
                                  WeaponSwapUtility.MinimumSwapTicks,
                                  UnityEngine.Mathf.RoundToInt(
                                      heavy.GetStatValue(StatDefOf.Mass) * perMass)),
                              heavyTicks + "t at " + perMass + " per mass"));

            heavy.Destroy();
            light.Destroy();
            return results;
        }

        /// <summary>
        /// Live checks on a real map: designate, swap, replace, drop, and a few hundred swaps in
        /// a row to prove nothing leaks. Restores the settings it changes.
        /// </summary>
        public static List<AssertionResult> LiveTests(Map map)
        {
            var results = new List<AssertionResult>();
            if (map == null)
            {
                results.Add(Check("map available", false, "no map"));
                return results;
            }
            if (SidearmUtility.ConflictingMod != null)
            {
                results.Add(Check("sidearms not stood down", false,
                                  "conflicting mod: " + SidearmUtility.ConflictingMod));
                return results;
            }

            var settings = VCOMod.Settings;
            var wasEnabled = settings.enableSidearms;
            settings.enableSidearms = true;

            Pawn pawn = null;
            try
            {
                // Generated colonists vary, and one incapable of violence cannot be handed a
                // weapon at all -- a real rule, but not the one under test. Discard those rather
                // than let the suite pass or fail on the roll.
                for (var attempt = 0; attempt < 40 && pawn == null; attempt++)
                {
                    var candidate = PawnGenerator.GeneratePawn(PawnKindDefOf.Colonist, Faction.OfPlayer);
                    if (candidate == null)
                    {
                        continue;
                    }
                    if (candidate.WorkTagIsDisabled(WorkTags.Violent))
                    {
                        candidate.Destroy();
                        continue;
                    }
                    pawn = candidate;
                }
                if (pawn == null)
                {
                    results.Add(Check("test pawn generated", false,
                                      "no violence-capable colonist in 40 attempts"));
                    return results;
                }
                GenSpawn.Spawn(pawn, CellFinder.RandomNotEdgeCell(10, map), map);

                var comp = SidearmUtility.CompFor(pawn);
                if (comp == null)
                {
                    results.Add(Check("sidearm comp attached to colonists", false, "no comp"));
                    return results;
                }
                results.Add(Check("sidearm comp attached to colonists", true, "present"));

                var primary = MakeWeapon(HeavyWeaponName);
                var sidearm = MakeWeapon(LightWeaponName);
                var spare = MakeWeapon(LightWeaponName);
                if (primary == null || sidearm == null || spare == null)
                {
                    results.Add(Check("weapon defs resolved", false, "could not resolve test weapons"));
                    return results;
                }

                pawn.equipment.DestroyAllEquipment();
                pawn.equipment.AddEquipment(primary);
                GenPlace.TryPlaceThing(sidearm, pawn.Position, map, ThingPlaceMode.Near);

                // --- take from the map -------------------------------------------------
                // The eligibility check is asserted separately from the move, because the two
                // fail for completely different reasons and a single failing line would not say
                // which. The refusal reason is carried into the detail either way.
                var eligible = SidearmUtility.CanBeSidearm(pawn, sidearm, out var refusal);
                results.Add(Check("an ordinary gun is an eligible sidearm",
                                  eligible, refusal.NullOrEmpty() ? "no reason given" : refusal));

                var took = WeaponSwapUtility.TakeAsSidearm(pawn, sidearm);
                results.Add(Check("a weapon on the map can be taken as a sidearm",
                                  took && comp.Reserve == sidearm && !sidearm.Spawned,
                                  "returned " + took + ", " + State(pawn, sidearm) + ", "
                                  + Describe(pawn, comp)));
                results.Add(Check("taking a sidearm does not disturb the equipped weapon",
                                  pawn.equipment.Primary == primary,
                                  Describe(pawn, comp)));

                // --- swap --------------------------------------------------------------
                var swapped = WeaponSwapUtility.Swap(pawn);
                results.Add(Check("a swap trades the equipped and stowed weapons",
                                  swapped && pawn.equipment.Primary == sidearm && comp.Reserve == primary,
                                  Describe(pawn, comp)));

                WeaponSwapUtility.Swap(pawn);
                results.Add(Check("swapping back restores the original pair",
                                  pawn.equipment.Primary == primary && comp.Reserve == sidearm,
                                  Describe(pawn, comp)));

                // --- many swaps in a row ------------------------------------------------
                var leaked = false;
                for (var i = 0; i < 500; i++)
                {
                    WeaponSwapUtility.Swap(pawn);
                    if (CarriedWeapons(pawn).Count != 2 || primary.Destroyed || sidearm.Destroyed)
                    {
                        leaked = true;
                        break;
                    }
                }
                results.Add(Check("500 swaps neither duplicate nor destroy a weapon",
                                  !leaked, Describe(pawn, comp)));

                // --- replacing ----------------------------------------------------------
                var displaced = comp.Reserve;
                GenPlace.TryPlaceThing(spare, pawn.Position, map, ThingPlaceMode.Near);
                WeaponSwapUtility.TakeAsSidearm(pawn, spare);
                results.Add(Check("a second sidearm replaces rather than accumulates",
                                  comp.Reserve == spare && CarriedWeapons(pawn).Count == 2,
                                  Describe(pawn, comp)));
                results.Add(Check("the displaced sidearm is dropped, not destroyed",
                                  displaced != null && !displaced.Destroyed && displaced.Spawned,
                                  displaced == null
                                      ? "none"
                                      : displaced.LabelShort + (displaced.Spawned ? " on map" : " missing")));

                // --- dropping -----------------------------------------------------------
                var dropped = comp.Reserve;
                WeaponSwapUtility.DropSidearm(pawn);
                results.Add(Check("dropping the sidearm clears the designation and leaves it on the map",
                                  comp.Reserve == null && dropped != null && dropped.Spawned,
                                  Describe(pawn, comp)));
            }
            finally
            {
                settings.enableSidearms = wasEnabled;
                if (pawn != null && !pawn.Destroyed)
                {
                    pawn.Destroy();
                }
            }

            return results;
        }

        private const string HeavyWeaponName = "Gun_BoltActionRifle";
        private const string LightWeaponName = "Gun_Revolver";

        private static List<Thing> CarriedWeapons(Pawn pawn)
        {
            var carried = new List<Thing>();
            if (pawn.equipment?.Primary != null)
            {
                carried.Add(pawn.equipment.Primary);
            }
            carried.AddRange(pawn.inventory.innerContainer.Where(t => t.def.IsWeapon));
            return carried;
        }

        /// <summary>Where a weapon actually is, for a failure that needs to name its own cause.</summary>
        private static string State(Pawn pawn, Thing weapon) =>
            "weapon spawned=" + weapon.Spawned
            + " destroyed=" + weapon.Destroyed
            + " holder=" + (weapon.holdingOwner == null ? "none" : weapon.holdingOwner.Owner.GetType().Name)
            + " inventory=" + pawn.inventory.innerContainer.Count;

        private static string Describe(Pawn pawn, CompSidearm comp) =>
            "held " + (pawn.equipment?.Primary?.LabelShort ?? "nothing")
            + ", stowed " + (comp.Reserve?.LabelShort ?? "nothing")
            + ", carrying " + CarriedWeapons(pawn).Count;

        /// <summary>
        /// A weapon by defName, falling back to any weapon of the right kind. Vanilla could rename
        /// a gun; the checks are about the swap, not about that gun in particular.
        /// </summary>
        private static ThingWithComps MakeWeapon(string defName)
        {
            var def = DefDatabase<ThingDef>.GetNamedSilentFail(defName)
                      ?? DefDatabase<ThingDef>.AllDefs.FirstOrDefault(
                          d => d.IsRangedWeapon && !d.MadeFromStuff && d.destroyOnDrop == false);
            if (def == null)
            {
                return null;
            }
            var stuff = def.MadeFromStuff ? GenStuff.DefaultStuffFor(def) : null;
            return ThingMaker.MakeThing(def, stuff) as ThingWithComps;
        }

        private static AssertionResult Check(string name, bool passed, string detail) =>
            new AssertionResult { Name = name, Passed = passed, Detail = detail };
    }
}
