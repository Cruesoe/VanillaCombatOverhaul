using System.Collections.Generic;
using RimWorld;
using Verse;
using Verse.AI;

namespace VanillaCombatOverhaul
{
    /// <summary>Carries one thing from the pawn's inventory (target A) to storage (target B), or drops it when there is none.</summary>
    public class JobDriver_StowInventoryThing : JobDriver
    {
        public override bool TryMakePreToilReservations(bool errorOnFailed) => true;

        protected override IEnumerable<Toil> MakeNewToils()
        {
            var findCell = ToilMaker.MakeToil("FindStoreCell");
            findCell.initAction = () =>
            {
                var thing = job.targetA.Thing;
                if (thing == null || !pawn.inventory.innerContainer.Contains(thing))
                {
                    EndJobWith(JobCondition.Incompletable);
                    return;
                }
                if (!StoreUtility.TryFindStoreCellNearColonyDesperate(thing, pawn, out var cell))
                {
                    pawn.inventory.innerContainer.TryDrop(thing, ThingPlaceMode.Near, thing.stackCount, out _);
                    EndJobWith(JobCondition.Succeeded);
                    return;
                }
                job.SetTarget(TargetIndex.B, cell);
            };
            yield return findCell;
            yield return Toils_Reserve.Reserve(TargetIndex.B);
            yield return Toils_Goto.GotoCell(TargetIndex.B, PathEndMode.Touch);

            var takeOut = ToilMaker.MakeToil("TakeOutOfInventory");
            takeOut.initAction = () =>
            {
                var thing = job.targetA.Thing;
                if (thing == null || !pawn.inventory.innerContainer.Contains(thing))
                {
                    EndJobWith(JobCondition.Incompletable);
                    return;
                }
                pawn.inventory.innerContainer.TryTransferToContainer(thing, pawn.carryTracker.innerContainer,
                    thing.stackCount, out var carried);
                if (carried == null)
                {
                    EndJobWith(JobCondition.Incompletable);
                    return;
                }
                job.count = carried.stackCount;
                job.SetTarget(TargetIndex.A, carried);
                carried.SetForbidden(false, false);
            };
            yield return takeOut;

            var carry = Toils_Haul.CarryHauledThingToCell(TargetIndex.B);
            yield return carry;
            yield return Toils_Haul.PlaceHauledThingInCell(TargetIndex.B, carry, true);
        }
    }
}
