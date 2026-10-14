using System.Collections.Generic;
using RimWorld;
using Verse;
using Verse.AI;

namespace VanillaCombatOverhaul
{
    /// <summary>Optionally sprints to cover (target A), then lies prone without firing until the pin wears off.</summary>
    public class JobDriver_PinnedDown : JobDriver
    {
        public const int DropTicks = 20;
        public const int MinProneTicks = 240;
        public const int MaxProneTicks = 2500;

        private int proneSinceTick = -1;

        public override bool TryMakePreToilReservations(bool errorOnFailed) => true;

        protected override IEnumerable<Toil> MakeNewToils()
        {
            this.FailOn(() => pawn.Downed || pawn.IsBurning() || pawn.mindState.MeleeThreatStillThreat);

            if (job.targetA.IsValid)
            {
                yield return Toils_Goto.GotoCell(TargetIndex.A, PathEndMode.OnCell);
            }

            var drop = ToilMaker.MakeToil("DropProne");
            drop.initAction = () =>
            {
                pawn.pather.StopDead();
                if (job.targetB.IsValid)
                {
                    pawn.rotationTracker.FaceCell(job.targetB.Cell);
                }
            };
            drop.defaultCompleteMode = ToilCompleteMode.Delay;
            drop.defaultDuration = DropTicks;
            yield return drop;

            var hold = ToilMaker.MakeToil("HoldProne");
            hold.initAction = () =>
            {
                pawn.jobs.posture = PawnPosture.LayingOnGroundNormal;
                proneSinceTick = Find.TickManager.TicksGame;
            };
            hold.tickIntervalAction = delta =>
            {
                if (ShouldGetUp())
                {
                    ReadyForNextToil();
                }
            };
            hold.defaultCompleteMode = ToilCompleteMode.Never;
            yield return hold;
        }

        private bool ShouldGetUp()
        {
            var settings = VCOMod.Settings;
            if (settings == null || !settings.enableSuppression || !settings.enableSuppressionPinning)
            {
                return true;
            }
            var prone = Find.TickManager.TicksGame - proneSinceTick;
            if (prone >= MaxProneTicks)
            {
                return true;
            }
            return prone >= MinProneTicks && SuppressionUtility.CompFor(pawn)?.Pinned != true;
        }

        public override void ExposeData()
        {
            base.ExposeData();
            Scribe_Values.Look(ref proneSinceTick, "vcoProneSinceTick", -1);
        }
    }
}
