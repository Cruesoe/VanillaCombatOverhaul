using System.Collections.Generic;
using RimWorld;
using UnityEngine;
using Verse;
using Verse.AI;

namespace VanillaCombatOverhaul
{
    /// <summary>Keeps a colonist's gear in line with its loadout: primary weapon, melee sidearm, carried items.</summary>
    public sealed class JobGiver_Loadout : ThinkNode_JobGiver
    {
        public const int WeaponRetryTicks = 600;
        public const int UnreachableRetryTicks = 300;
        public const int ItemCheckTicks = 2500;

        private static readonly Dictionary<int, int> WeaponRetryAfterTick = new Dictionary<int, int>();
        private static readonly Dictionary<int, int> NextItemCheckTick = new Dictionary<int, int>();

        /// <summary>
        /// MainColonistBehaviorCore places this node under a ThinkNode_PrioritySorter. Every
        /// direct child of that sorter must report a priority or RimWorld discards it without
        /// calling TryGiveJob. Keep loadout jobs just above ordinary scheduled work
        /// (which reaches 9), but below urgent food (9.5).
        /// </summary>
        public override float GetPriority(Pawn pawn) =>
            LoadoutUtility.Enabled && CanConsider(pawn) ? 9.25f : 0f;

        protected override Job TryGiveJob(Pawn pawn)
        {
            if (!LoadoutUtility.Enabled || !CanConsider(pawn))
            {
                return null;
            }
            var comp = LoadoutUtility.CompFor(pawn);
            var policy = comp?.Loadout;
            if (policy == null || comp.SwappedPrimary != null)
            {
                return null;
            }
            var now = Find.TickManager.TicksGame;
            if (!pawn.WorkTagIsDisabled(WorkTags.Violent)
                && (!WeaponRetryAfterTick.TryGetValue(pawn.thingIDNumber, out var retry) || now >= retry))
            {
                var weaponJob = WeaponJob(pawn, policy, now);
                if (weaponJob != null)
                {
                    return weaponJob;
                }
                WeaponRetryAfterTick[pawn.thingIDNumber] = now + UnreachableRetryTicks;
            }
            if (!NextItemCheckTick.TryGetValue(pawn.thingIDNumber, out var next) || now >= next)
            {
                var itemJob = ItemJob(pawn, policy);
                if (itemJob != null)
                {
                    return itemJob;
                }
                NextItemCheckTick[pawn.thingIDNumber] = now + ItemCheckTicks;
            }
            return null;
        }

        private static Job WeaponJob(Pawn pawn, LoadoutPolicy policy, int now)
        {
            var settings = VCOMod.Settings;
            var current = pawn.equipment.Primary;
            var autoPrimary = policy.autoPrimary
                              && !(current != null && AutoEquipPolicyComponent.Current?.HasForcedCurrentWeapon(pawn) == true);
            var wantSidearm = policy.carrySidearm;
            if (wantSidearm)
            {
                DropSurplusSidearms(pawn);
            }

            Thing bestPrimary = null;
            var bestPrimaryScore = 0f;
            Thing bestSidearm = null;
            var bestSidearmScore = 0f;
            var weapons = pawn.Map.listerThings.ThingsInGroup(ThingRequestGroup.Weapon);
            for (var i = 0; i < weapons.Count; i++)
            {
                var candidate = weapons[i];
                if (autoPrimary && CanUseAsPrimary(candidate, pawn, policy.weaponFilter))
                {
                    var score = WeaponScoreUtility.Score(candidate, pawn);
                    if (score > bestPrimaryScore)
                    {
                        bestPrimary = candidate;
                        bestPrimaryScore = score;
                    }
                }
                if (wantSidearm && LoadoutUtility.IsMelee(candidate)
                    && LoadoutUtility.CanTake(candidate, pawn, policy.sidearmFilter))
                {
                    var score = WeaponScoreUtility.Score(candidate, pawn);
                    if (score > bestSidearmScore)
                    {
                        bestSidearm = candidate;
                        bestSidearmScore = score;
                    }
                }
            }

            if (bestPrimary != null)
            {
                var currentAllowed = current != null && policy.weaponFilter.Allows(current);
                var currentScore = current == null ? 0f : WeaponScoreUtility.Score(current, pawn);
                if (WeaponScoreUtility.IsUpgrade(currentScore, bestPrimaryScore, settings.autoEquipUpgradeThreshold,
                        currentAllowed))
                {
                    if (!Reachable(pawn, bestPrimary, now))
                    {
                        return null;
                    }
                    WeaponRetryAfterTick[pawn.thingIDNumber] = now + WeaponRetryTicks;
                    VCODiagnostics.CountFor(pawn, "autoequip.job");
                    return JobMaker.MakeJob(JobDefOf.Equip, bestPrimary);
                }
            }

            if (bestSidearm != null && LoadoutUtility.IsRanged(current))
            {
                var carried = LoadoutUtility.CarriedSidearm(pawn);
                var carriedScore = carried == null ? 0f : WeaponScoreUtility.Score(carried, pawn);
                var carriedAllowed = carried != null && policy.sidearmFilter.Allows(carried);
                if (WeaponScoreUtility.IsUpgrade(carriedScore, bestSidearmScore, settings.autoEquipUpgradeThreshold,
                        carriedAllowed)
                    && !MassUtility.WillBeOverEncumberedAfterPickingUp(pawn, bestSidearm, 1))
                {
                    if (!Reachable(pawn, bestSidearm, now))
                    {
                        return null;
                    }
                    WeaponRetryAfterTick[pawn.thingIDNumber] = now + WeaponRetryTicks;
                    VCODiagnostics.CountFor(pawn, "loadout.sidearm.job");
                    var job = JobMaker.MakeJob(JobDefOf.TakeCountToInventory, bestSidearm);
                    job.count = 1;
                    return job;
                }
            }
            return null;
        }

        private static Job ItemJob(Pawn pawn, LoadoutPolicy policy)
        {
            if (policy.items.Count == 0 || pawn.inventory.UnloadEverything)
            {
                return null;
            }
            foreach (var item in policy.items)
            {
                if (item.thingDef == null)
                {
                    continue;
                }
                var need = item.count - pawn.inventory.Count(item.thingDef);
                if (need <= 0)
                {
                    continue;
                }
                var thing = GenClosest.ClosestThingReachable(pawn.Position, pawn.Map, ThingRequest.ForDef(item.thingDef),
                    PathEndMode.ClosestTouch, TraverseParms.For(pawn), 9999f,
                    t => !t.IsForbidden(pawn) && pawn.CanReserve(t, 10, 1));
                if (thing == null)
                {
                    continue;
                }
                var count = Mathf.Min(need, thing.stackCount, MassUtility.CountToPickUpUntilOverEncumbered(pawn, thing));
                if (count <= 0)
                {
                    continue;
                }
                var job = JobMaker.MakeJob(JobDefOf.TakeCountToInventory, thing);
                job.count = count;
                VCODiagnostics.CountFor(pawn, "loadout.item.job");
                return job;
            }
            return null;
        }

        /// <summary>Keeps only the best melee weapon in inventory, besides a swapped-out primary.</summary>
        private static void DropSurplusSidearms(Pawn pawn)
        {
            var keep = LoadoutUtility.CarriedSidearm(pawn);
            if (keep == null)
            {
                return;
            }
            var inventory = pawn.inventory.innerContainer;
            for (var i = inventory.Count - 1; i >= 0; i--)
            {
                var thing = inventory[i];
                if (thing != keep && LoadoutUtility.IsMelee(thing) && thing != LoadoutUtility.CompFor(pawn).SwappedPrimary)
                {
                    inventory.TryDrop(thing, pawn.Position, pawn.Map, ThingPlaceMode.Near, out _);
                }
            }
        }

        private static bool Reachable(Pawn pawn, Thing thing, int now)
        {
            if (pawn.CanReserve(thing) && pawn.CanReach(thing, PathEndMode.ClosestTouch, Danger.Deadly))
            {
                return true;
            }
            WeaponRetryAfterTick[pawn.thingIDNumber] = now + UnreachableRetryTicks;
            return false;
        }

        private static bool CanConsider(Pawn pawn)
        {
            return pawn?.Map != null
                   && pawn.Spawned
                   && pawn.IsColonistPlayerControlled
                   && !pawn.Downed
                   && !pawn.Drafted
                   && !pawn.InMentalState
                   && pawn.carryTracker?.CarriedThing == null
                   && pawn.equipment != null
                   && pawn.inventory != null;
        }

        private static bool CanUseAsPrimary(Thing candidate, Pawn pawn, ThingFilter filter)
        {
            if (!LoadoutUtility.CanTake(candidate, pawn, filter))
            {
                return false;
            }
            var rangedVerb = candidate.def.Verbs?.Find(v => v.isPrimary && v.LaunchesProjectile)
                             ?? candidate.def.Verbs?.Find(v => v.LaunchesProjectile);
            if (rangedVerb == null)
            {
                return true;
            }
            if (pawn.story?.traits?.HasTrait(TraitDefOf.Brawler) == true)
            {
                return false;
            }
            var projectile = rangedVerb.defaultProjectile?.projectile;
            return projectile != null
                   && projectile.damageDef != null
                   && projectile.damageDef.harmsHealth
                   && projectile.explosionRadius <= 0f;
        }
    }
}
