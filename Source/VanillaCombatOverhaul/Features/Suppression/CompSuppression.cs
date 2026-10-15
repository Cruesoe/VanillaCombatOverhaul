using RimWorld;
using UnityEngine;
using Verse;
using Verse.AI;

namespace VanillaCombatOverhaul
{
    public class CompProperties_Suppression : CompProperties
    {
        public CompProperties_Suppression() => compClass = typeof(CompSuppression);
    }

    /// <summary>A pawn's suppression level, its decay, its overhead icon, and going prone when pinned.</summary>
    public class CompSuppression : ThingComp
    {
        public const float CoverSearchRadius = 5.9f;
        public const int PinCooldownTicks = 60;
        // After the longest prone stretch, the pawn gets this long on its feet before it can be pinned again.
        public const int UnpinnedAfterMaxProneTicks = 600;

        private float level;
        private int lastSuppressedTick = -99999;
        private int nextPinTick;
        private IntVec3 lastSource = IntVec3.Invalid;
        private float threshold;
        private int thresholdTick = -99999;

        public Pawn Pawn => parent as Pawn;

        /// <summary>Raw suppression, as added by each round.</summary>
        public float Level => level;

        /// <summary>Raw suppression at which this pawn is suppressed, from mood and nerves, cached briefly.</summary>
        public float Threshold
        {
            get
            {
                var now = Find.TickManager.TicksGame;
                if (now - thresholdTick >= SuppressionUtility.ThresholdCacheTicks && Pawn != null)
                {
                    threshold = SuppressionUtility.ThresholdFor(Pawn);
                    thresholdTick = now;
                }
                return Mathf.Max(threshold, SuppressionUtility.MinThreshold);
            }
        }

        /// <summary>Suppression relative to the threshold: 1 is suppressed, 4 is pinned.</summary>
        public float Pressure => level <= 0f ? 0f : level / Threshold;

        public bool Suppressed => SuppressionUtility.Enabled && Pressure >= SuppressionUtility.SuppressedLevel;

        public bool Pinned => SuppressionUtility.Enabled && Pressure >= SuppressionUtility.PinnedLevel;

        public void AddSuppression(float amount, IntVec3 source)
        {
            level = Mathf.Min(SuppressionUtility.MaxLevel * Threshold, level + amount);
            lastSuppressedTick = Find.TickManager.TicksGame;
            lastSource = source;
            VCODiagnostics.CountFor(Pawn, "suppression.added");
            CheckPinned();
        }

        public override void CompTickInterval(int delta)
        {
            if (level <= 0f)
            {
                return;
            }
            if (Find.TickManager.TicksGame - lastSuppressedTick >= SuppressionUtility.DecayDelayTicks)
            {
                level = SuppressionUtility.Decay(level, delta);
            }
            // Mood falls under fire, lowering the threshold, so a pawn can become pinned between hits.
            CheckPinned();
        }

        private void CheckPinned()
        {
            var pawn = Pawn;
            if (pawn?.Map != null && lastSource.IsValid && Pressure >= SuppressionUtility.PinnedLevel)
            {
                TryPinDown(pawn, lastSource);
            }
        }

        /// <summary>Pinned pawns drop prone and stop firing; non-player pawns first crawl to nearby cover.</summary>
        private void TryPinDown(Pawn pawn, IntVec3 source)
        {
            var settings = VCOMod.Settings;
            var now = Find.TickManager.TicksGame;
            if (settings == null || !settings.enableSuppressionPinning || now < nextPinTick)
            {
                return;
            }
            // A player order given while pinned is carried out, crawling, instead of being interrupted.
            if (pawn.IsPrisoner || pawn.InMentalState || pawn.Downed || pawn.jobs == null || pawn.CurJob?.playerForced == true
                || pawn.CurJobDef == VCO_JobDefOf.VCO_PinnedDown || pawn.CurJobDef == JobDefOf.AttackMelee
                || pawn.CurJobDef == JobDefOf.Flee || pawn.CurJobDef == JobDefOf.FleeAndCower
                || pawn.GetPosture() != PawnPosture.Standing || pawn.IsBurning()
                || pawn.mindState?.MeleeThreatStillThreat == true || IsBursting(pawn))
            {
                return;
            }
            nextPinTick = now + PinCooldownTicks;

            var job = JobMaker.MakeJob(VCO_JobDefOf.VCO_PinnedDown);
            job.targetB = source;
            if (pawn.Faction != Faction.OfPlayer)
            {
                var cover = SuppressionUtility.FindCover(pawn, source, CoverSearchRadius);
                if (cover != pawn.Position)
                {
                    job.targetA = cover;
                    job.locomotionUrgency = LocomotionUrgency.Sprint;
                }
            }
            pawn.jobs.StartJob(job, JobCondition.InterruptForced);
            VCODiagnostics.CountFor(pawn, "suppression.pinned");
        }

        /// <summary>Called when the pawn gets up after the longest prone stretch while still pinned.</summary>
        public void Notify_MaxProneReached()
        {
            nextPinTick = Find.TickManager.TicksGame + UnpinnedAfterMaxProneTicks;
        }

        // A burst in progress finishes before the pawn goes down.
        private static bool IsBursting(Pawn pawn) =>
            pawn.stances?.curStance is Stance_Busy busy && busy.verb != null && busy.verb.Bursting;

        public override void PostDraw()
        {
            var pawn = Pawn;
            if (Suppressed && pawn != null && pawn.Spawned)
            {
                SuppressionOverlay.Draw(pawn, Pinned);
            }
        }

        public override string CompInspectStringExtra()
        {
            if (!Suppressed)
            {
                return null;
            }
            var key = Pinned ? "VCO_Suppression_PinnedInspect" : "VCO_Suppression_SuppressedInspect";
            return key.Translate(Mathf.Min(1f, Pressure / SuppressionUtility.PinnedLevel).ToStringPercent());
        }

        public override void PostExposeData()
        {
            base.PostExposeData();
            Scribe_Values.Look(ref level, "vcoSuppression", 0f);
            Scribe_Values.Look(ref lastSuppressedTick, "vcoSuppressionTick", -99999);
            Scribe_Values.Look(ref nextPinTick, "vcoSuppressionPinTick", 0);
            Scribe_Values.Look(ref lastSource, "vcoSuppressionSource", IntVec3.Invalid);
        }
    }
}
