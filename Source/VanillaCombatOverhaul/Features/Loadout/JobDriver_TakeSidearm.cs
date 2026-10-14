using System.Collections.Generic;
using RimWorld;
using Verse;
using Verse.AI;
using Verse.Sound;

namespace VanillaCombatOverhaul
{
    /// <summary>Picks up a better sidearm (target A) and puts the one it replaces (target B, in inventory) down where it lay.</summary>
    public class JobDriver_TakeSidearm : JobDriver
    {
        public override bool TryMakePreToilReservations(bool errorOnFailed) =>
            pawn.Reserve(job.targetA, job, 1, 1, null, errorOnFailed);

        protected override IEnumerable<Toil> MakeNewToils()
        {
            yield return Toils_Goto.GotoThing(TargetIndex.A, PathEndMode.ClosestTouch)
                .FailOnDespawnedNullOrForbidden(TargetIndex.A)
                .FailOnSomeonePhysicallyInteracting(TargetIndex.A);

            // Pick up and put down in one step: the weapon despawns on pickup, which would end a job failing on that.
            var swap = ToilMaker.MakeToil("TakeSidearm");
            swap.initAction = () =>
            {
                var weapon = job.targetA.Thing;
                if (weapon == null || !weapon.Spawned || weapon.IsForbidden(pawn))
                {
                    EndJobWith(JobCondition.Incompletable);
                    return;
                }
                var cell = weapon.Position;
                var map = weapon.Map;
                var inventory = pawn.inventory.innerContainer;
                if (!inventory.TryAdd(weapon.SplitOff(1)))
                {
                    EndJobWith(JobCondition.Incompletable);
                    return;
                }
                weapon.def.soundPickup?.PlayOneShot(new TargetInfo(cell, map));
                var replaced = job.targetB.Thing;
                if (replaced != null && inventory.Contains(replaced))
                {
                    inventory.TryDrop(replaced, cell, map, ThingPlaceMode.Near, out _);
                }
            };
            yield return swap;
        }
    }
}
