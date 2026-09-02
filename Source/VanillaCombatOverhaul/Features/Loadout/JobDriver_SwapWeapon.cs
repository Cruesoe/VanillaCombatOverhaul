using System.Collections.Generic;
using RimWorld;
using Verse;
using Verse.AI;

namespace VanillaCombatOverhaul
{
    /// <summary>
    /// Drawing a weapon, as an action that takes time.
    ///
    /// Two jobs share this driver. <c>VCO_SwapWeapon</c> is an ordinary order; <c>VCO_SwapWeaponCombat</c>
    /// is the same work declared uninterruptible, because a swap abandoned halfway through a
    /// firefight is worse than never having started it. The split is Simple Sidearms'; it is the
    /// one structural idea in that mod that a single-sidearm design still needs.
    ///
    /// Target A is optional. With one, the pawn walks to a weapon and takes it as its sidearm;
    /// without one, it trades the weapon in its hands for the one already stowed.
    /// </summary>
    public class JobDriver_SwapWeapon : JobDriver
    {
        private ThingWithComps TargetWeapon => job.GetTarget(TargetIndex.A).Thing as ThingWithComps;

        public override bool TryMakePreToilReservations(bool errorOnFailed)
        {
            var target = TargetWeapon;
            if (target == null || !target.Spawned)
            {
                return true;
            }
            return pawn.Reserve(target, job, 1, -1, null, errorOnFailed);
        }

        public override string GetReport()
        {
            var target = TargetWeapon;
            return target != null
                ? "VCO_Job_TakeSidearm".Translate(target.LabelShort)
                : "VCO_Job_SwapWeapon".Translate();
        }

        protected override IEnumerable<Toil> MakeNewToils()
        {
            var target = TargetWeapon;
            var fromMap = target != null && target.Spawned;

            if (fromMap)
            {
                this.FailOnDespawnedNullOrForbidden(TargetIndex.A);
                yield return Toils_Goto.GotoThing(TargetIndex.A, PathEndMode.ClosestTouch);
            }

            // Costed against the weapon being drawn: the one coming out of storage when there is a
            // target, the stowed one otherwise. Read here rather than in the toil body because the
            // delay has to be known before the wait begins.
            var drawn = fromMap ? target : WeaponSwapUtility.WeaponToDraw(pawn);
            yield return Toils_General.Wait(WeaponSwapUtility.SwapTicks(pawn, drawn), TargetIndex.None);

            var perform = new Toil
            {
                initAction = () =>
                {
                    var weapon = TargetWeapon;
                    if (weapon != null)
                    {
                        WeaponSwapUtility.TakeAsSidearm(pawn, weapon);
                    }
                    else
                    {
                        WeaponSwapUtility.Swap(pawn);
                    }
                },
                defaultCompleteMode = ToilCompleteMode.Instant
            };
            yield return perform;
        }
    }
}
