using System.Collections.Generic;
using RimWorld;
using Verse;
using Verse.AI;

namespace VanillaCombatOverhaul
{
    public sealed class JobGiver_AutoEquipPrimary : ThinkNode_JobGiver
    {
        private static readonly Dictionary<int, int> RetryAfterTick = new Dictionary<int, int>();

        /// <summary>
        /// MainColonistBehaviorCore places this node under a ThinkNode_PrioritySorter. Every
        /// direct child of that sorter must report a priority or RimWorld discards it without
        /// calling TryGiveJob. Keep automatic equipment just above ordinary scheduled work
        /// (which reaches 9), but below urgent food (9.5).
        /// </summary>
        public override float GetPriority(Pawn pawn)
        {
            var settings = VCOMod.Settings;
            return settings != null && settings.enableAutoEquip && CanConsiderEquipment(pawn)
                ? 9.25f
                : 0f;
        }

        protected override Job TryGiveJob(Pawn pawn)
        {
            var settings = VCOMod.Settings;
            if (settings == null || !settings.enableAutoEquip || !CanConsiderEquipment(pawn))
            {
                return null;
            }

            var current = pawn.equipment.Primary;
            if (current != null && AutoEquipPolicyComponent.Current?.HasForcedCurrentWeapon(pawn) == true)
            {
                return null;
            }

            var now = Find.TickManager.TicksGame;
            if (RetryAfterTick.TryGetValue(pawn.thingIDNumber, out var retry) && now < retry)
            {
                return null;
            }

            var policy = pawn.outfits?.CurrentApparelPolicy;
            var filter = AutoEquipPolicyComponent.Current?.FilterFor(policy);
            if (filter == null)
            {
                return null;
            }

            var currentAllowed = current != null && filter.Allows(current);
            var currentScore = current == null ? 0f : WeaponScoreUtility.Score(current, pawn);
            var bestScore = 0f;
            Thing best = null;
            var weapons = pawn.Map.listerThings.ThingsInGroup(ThingRequestGroup.Weapon);
            for (var i = 0; i < weapons.Count; i++)
            {
                var candidate = weapons[i];
                if (!CanUseCandidate(candidate, pawn, filter))
                {
                    continue;
                }
                var score = WeaponScoreUtility.Score(candidate, pawn);
                if (score > bestScore)
                {
                    best = candidate;
                    bestScore = score;
                }
            }

            if (best == null || !WeaponScoreUtility.IsUpgrade(currentScore, bestScore,
                    settings.autoEquipUpgradeThreshold, currentAllowed))
            {
                return null;
            }
            if (!pawn.CanReserve(best) || !pawn.CanReach(best, PathEndMode.ClosestTouch, Danger.Deadly))
            {
                RetryAfterTick[pawn.thingIDNumber] = now + 300;
                return null;
            }

            RetryAfterTick[pawn.thingIDNumber] = now + 600;
            VCODiagnostics.CountFor(pawn, "autoequip.job");
            return JobMaker.MakeJob(JobDefOf.Equip, best);
        }

        private static bool CanConsiderEquipment(Pawn pawn)
        {
            return pawn?.Map != null
                   && pawn.Spawned
                   && pawn.IsColonistPlayerControlled
                   && !pawn.Downed
                   && !pawn.Drafted
                   && !pawn.InMentalState
                   && pawn.carryTracker?.CarriedThing == null
                   && pawn.equipment != null
                   && pawn.outfits != null
                   && !pawn.WorkTagIsDisabled(WorkTags.Violent);
        }

        private static bool CanUseCandidate(Thing candidate, Pawn pawn, ThingFilter filter)
        {
            if (candidate == null || !candidate.Spawned || candidate.IsForbidden(pawn)
                || candidate.IsBurning() || !filter.Allows(candidate))
            {
                return false;
            }
            if (!candidate.def.IsWeapon || candidate.def.destroyOnDrop)
            {
                return false;
            }
            if (!EquipmentUtility.CanEquip(candidate, pawn, out _, false)
                || !EquipmentUtility.GetPersonaWeaponConfirmationText(candidate, pawn).NullOrEmpty())
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
