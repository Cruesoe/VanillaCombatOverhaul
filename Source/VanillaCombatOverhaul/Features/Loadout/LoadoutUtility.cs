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

        /// <summary>Adds what the pawn's loadout keeps in inventory, so unloading leaves it there.</summary>
        public static void AddKeptItems(Pawn pawn, List<ThingDefCount> kept)
        {
            if (!Enabled || pawn == null || kept == null || !pawn.IsColonist)
            {
                return;
            }
            var comp = CompFor(pawn);
            var loadout = comp?.Loadout;
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
            var sidearm = (loadout.carrySidearm || comp.LockedSidearm != null) && SidearmsEnabled ? CarriedSidearm(pawn) : null;
            if (sidearm != null)
            {
                kept.Add(new ThingDefCount(sidearm.def, 1));
            }
            var swapped = comp.SwappedPrimary;
            if (swapped != null && pawn.inventory.innerContainer.Contains(swapped))
            {
                kept.Add(new ThingDefCount(swapped.def, 1));
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

        /// <summary>Moves the primary into inventory and equips a carried weapon in its place.</summary>
        public static bool Swap(Pawn pawn, ThingWithComps carried)
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
