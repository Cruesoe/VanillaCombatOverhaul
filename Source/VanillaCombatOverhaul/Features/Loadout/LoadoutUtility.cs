using System.Collections.Generic;
using RimWorld;
using Verse;
using Verse.AI;
using Verse.Sound;

namespace VanillaCombatOverhaul
{
    [StaticConstructorOnStartup]
    public static class LoadoutUtility
    {
        private static ThingFilter weaponParentFilter;
        private static ThingFilter meleeParentFilter;

        public static bool Enabled => VCOMod.Settings?.enableAutoEquip ?? false;

        public const string SimpleSidearmsPackageId = "PeteTimesSix.SimpleSidearms";

        /// <summary>Simple Sidearms carries and swaps weapons itself, so loadout sidearms stay off.</summary>
        public static readonly bool SidearmsByOtherMod = ModsConfig.IsActive(SimpleSidearmsPackageId);

        /// <summary>Whether loadouts carry and swap a melee sidearm.</summary>
        public static bool SidearmsEnabled => Enabled && !SidearmsByOtherMod;

        public static ThingFilter WeaponParentFilter => weaponParentFilter ??= NewWeaponFilter(ranged: true, melee: true);

        public static ThingFilter MeleeParentFilter => meleeParentFilter ??= NewWeaponFilter(ranged: false, melee: true);

        public static ThingFilter NewWeaponFilter(bool ranged, bool melee)
        {
            var filter = new ThingFilter
            {
                allowedHitPointsConfigurable = true,
                allowedQualitiesConfigurable = true
            };
            var category = ranged && melee
                ? ThingCategoryDefOf.Weapons
                : DefDatabase<ThingCategoryDef>.GetNamedSilentFail(ranged ? "WeaponsRanged" : "WeaponsMelee");
            filter.SetAllow(category ?? ThingCategoryDefOf.Weapons, true);
            return filter;
        }

        public static CompLoadout CompFor(Pawn pawn) => pawn?.TryGetComp<CompLoadout>();

        public static LoadoutPolicy LoadoutFor(Pawn pawn) => CompFor(pawn)?.Loadout;

        public static bool IsRanged(Thing weapon) => weapon != null && weapon.def.IsRangedWeapon;

        public static bool IsMelee(Thing weapon) => weapon != null && weapon.def.IsMeleeWeapon;

        /// <summary>The best melee weapon in the pawn's inventory, other than a swapped-out primary.</summary>
        public static ThingWithComps CarriedSidearm(Pawn pawn)
        {
            var inventory = pawn.inventory?.innerContainer;
            if (inventory == null)
            {
                return null;
            }
            var comp = CompFor(pawn);
            var swapped = comp?.SwappedPrimary;
            var locked = comp?.LockedSidearm;
            if (locked != null && inventory.Contains(locked))
            {
                return locked;
            }
            ThingWithComps best = null;
            var bestScore = -1f;
            for (var i = 0; i < inventory.Count; i++)
            {
                if (!(inventory[i] is ThingWithComps weapon) || weapon == swapped || !IsMelee(weapon))
                {
                    continue;
                }
                var score = WeaponScoreUtility.Score(weapon, pawn);
                if (score > bestScore)
                {
                    best = weapon;
                    bestScore = score;
                }
            }
            return best;
        }

        /// <summary>A job picking up a weapon as the sidearm and putting the carried one it replaces down in its place.</summary>
        public static Job TakeSidearmJob(Pawn pawn, Thing weapon)
        {
            var replaced = CarriedSidearm(pawn);
            var job = JobMaker.MakeJob(VCO_JobDefOf.VCO_TakeSidearm, weapon, replaced != weapon ? replaced : null);
            job.count = 1;
            return job;
        }

        private static readonly List<Thing> KeptWeaponBuffer = new List<Thing>();

        /// <summary>
        /// The exact weapons the loadout keeps in the pawn's inventory: the sidearm and a swapped-out primary.
        /// Both unload patches read this, so they always agree. Fills a shared buffer; use it before the next call.
        /// </summary>
        private static List<Thing> KeptWeapons(Pawn pawn)
        {
            KeptWeaponBuffer.Clear();
            if (!Enabled || pawn == null || !pawn.IsColonist || pawn.inventory == null)
            {
                return KeptWeaponBuffer;
            }
            var comp = CompFor(pawn);
            var loadout = comp?.Loadout;
            if (loadout == null)
            {
                return KeptWeaponBuffer;
            }
            var sidearm = SidearmsEnabled && (loadout.carrySidearm || comp.LockedSidearm != null) ? CarriedSidearm(pawn) : null;
            if (sidearm != null)
            {
                KeptWeaponBuffer.Add(sidearm);
            }
            var swapped = comp.SwappedPrimary;
            if (swapped != null && pawn.inventory.innerContainer.Contains(swapped))
            {
                KeptWeaponBuffer.Add(swapped);
            }
            return KeptWeaponBuffer;
        }

        /// <summary>How many of this def the pawn's loadout keeps in inventory as an item.</summary>
        public static int KeptCount(Pawn pawn, ThingDef def) =>
            Enabled && pawn != null && pawn.IsColonist ? LoadoutFor(pawn)?.CountFor(def) ?? 0 : 0;

        /// <summary>Adds what the pawn's loadout keeps in inventory, so unloading leaves it there.</summary>
        public static void AddKeptItems(Pawn pawn, List<ThingDefCount> kept)
        {
            if (!Enabled || pawn == null || kept == null || !pawn.IsColonist)
            {
                return;
            }
            var loadout = LoadoutFor(pawn);
            if (loadout == null)
            {
                return;
            }
            foreach (var item in loadout.items)
            {
                if (item.thingDef != null && item.count > 0)
                {
                    kept.Add(new ThingDefCount(item.thingDef, item.count));
                }
            }
            // Vanilla matches kept entries by def; AvoidUnloadingKeptWeapon makes sure the exact weapons stay.
            foreach (var weapon in KeptWeapons(pawn))
            {
                kept.Add(new ThingDefCount(weapon.def, 1));
            }
        }

        /// <summary>When unloading picks a kept weapon itself, another weapon of the same def is unloaded instead.</summary>
        public static void AvoidUnloadingKeptWeapon(Pawn pawn, ref ThingCount result)
        {
            var thing = result.Thing;
            if (thing == null || !thing.def.IsWeapon)
            {
                return;
            }
            var kept = KeptWeapons(pawn);
            if (!kept.Contains(thing))
            {
                return;
            }
            var inventory = pawn.inventory.innerContainer;
            for (var i = 0; i < inventory.Count; i++)
            {
                var other = inventory[i];
                if (other.def == thing.def && !kept.Contains(other))
                {
                    result = new ThingCount(other, other.stackCount);
                    return;
                }
            }
        }

        /// <summary>Point-blank chance from which a shooter who is better at Shooting than Melee keeps the gun up close.</summary>
        public const float KeepGunPointBlankChance = 0.17f;

        /// <summary>
        /// True when firing point-blank beats drawing the sidearm: point-blank shooting is on, the pawn's
        /// chance is at least KeepGunPointBlankChance (Shooting 15), and its Melee is lower than its Shooting.
        /// </summary>
        public static bool KeepsGunUpClose(Pawn pawn)
        {
            if (!PointBlankUtility.Enabled)
            {
                return false;
            }
            var shooting = PointBlankUtility.ShootingLevel(pawn);
            var melee = pawn.skills?.GetSkill(SkillDefOf.Melee);
            var meleeLevel = melee == null || melee.TotallyDisabled ? -1 : melee.Level;
            return PointBlankUtility.ChanceFor(shooting) >= KeepGunPointBlankChance && meleeLevel < shooting;
        }

        /// <summary>An adjacent pawn the pawn would melee, as vanilla's drafted auto-attack checks.</summary>
        public static bool AdjacentThreat(Pawn pawn)
        {
            var map = pawn.Map;
            for (var i = 0; i < 9; i++)
            {
                var cell = pawn.Position + GenAdj.AdjacentCellsAndInside[i];
                if (!cell.InBounds(map))
                {
                    continue;
                }
                var things = cell.GetThingList(map);
                for (var j = 0; j < things.Count; j++)
                {
                    if (things[j] is Pawn other && other != pawn && !other.Downed && pawn.HostileTo(other)
                        && !other.ThreatDisabled(pawn) && GenHostility.IsActiveThreatTo(other, pawn.Faction))
                    {
                        return true;
                    }
                }
            }
            return false;
        }

        /// <summary>True while Swap moves weapons, so the equip hook leaves weapon-matched stock alone.</summary>
        public static bool Swapping { get; private set; }

        /// <summary>Moves the primary into inventory and equips a carried weapon in its place.</summary>
        public static bool Swap(Pawn pawn, ThingWithComps carried)
        {
            Swapping = true;
            try
            {
                return SwapInt(pawn, carried);
            }
            finally
            {
                Swapping = false;
            }
        }

        private static bool SwapInt(Pawn pawn, ThingWithComps carried)
        {
            var inventory = pawn.inventory?.innerContainer;
            var equipment = pawn.equipment;
            if (inventory == null || equipment == null || carried == null || !inventory.Contains(carried))
            {
                return false;
            }
            var current = equipment.Primary;
            if (current != null && !equipment.TryTransferEquipmentToContainer(current, inventory))
            {
                return false;
            }
            if (!inventory.TryTransferToContainer(carried, equipment.GetDirectlyHeldThings()))
            {
                if (current != null)
                {
                    inventory.TryTransferToContainer(current, equipment.GetDirectlyHeldThings());
                }
                return false;
            }
            pawn.stances?.CancelBusyStanceSoft();
            if (pawn.CurJobDef == JobDefOf.AttackStatic)
            {
                pawn.jobs.EndCurrentJob(JobCondition.InterruptForced);
            }
            if (pawn.Spawned)
            {
                carried.def.soundInteract?.PlayOneShot(new TargetInfo(pawn.Position, pawn.Map));
            }
            VCODiagnostics.CountFor(pawn, "loadout.swap");
            return true;
        }

        /// <summary>Whether the pawn may pick this weapon up for its loadout.</summary>
        public static bool CanTake(Thing weapon, Pawn pawn, ThingFilter filter)
        {
            if (weapon == null || !weapon.Spawned || weapon.IsForbidden(pawn) || weapon.IsBurning()
                || !filter.Allows(weapon) || !weapon.def.IsWeapon || weapon.def.destroyOnDrop)
            {
                return false;
            }
            return EquipmentUtility.CanEquip(weapon, pawn, out _, false)
                   && EquipmentUtility.GetPersonaWeaponConfirmationText(weapon, pawn).NullOrEmpty();
        }
    }
}
