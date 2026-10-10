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

        private float level;
        private int lastSuppressedTick = -99999;
        private int nextPinTick;

        public Pawn Pawn => parent as Pawn;

        public float Level => level;

        public bool Suppressed => SuppressionUtility.Enabled && level >= SuppressionUtility.SuppressedLevel;

        public bool Pinned => SuppressionUtility.Enabled && level >= SuppressionUtility.PinnedLevel;

        public void AddSuppression(float amount, IntVec3 source)
        {
            var wasSuppressed = level >= SuppressionUtility.SuppressedLevel;
            var wasPinned = level >= SuppressionUtility.PinnedLevel;
            level = Mathf.Min(SuppressionUtility.MaxLevel, level + amount);
            lastSuppressedTick = Find.TickManager.TicksGame;
            VCODiagnostics.CountFor(Pawn, "suppression.added");

            var pawn = Pawn;
            if (pawn?.Map == null)
            {
                return;
            }
            if (!wasPinned && level >= SuppressionUtility.PinnedLevel)
            {
                MoteMaker.ThrowText(pawn.DrawPos, pawn.Map, "VCO_Suppression_PinnedMote".Translate(), Color.red);
            }
            else if (!wasSuppressed && level >= SuppressionUtility.SuppressedLevel)
            {
                MoteMaker.ThrowText(pawn.DrawPos, pawn.Map, "VCO_Suppression_SuppressedMote".Translate(),
                                    new Color(1f, 0.75f, 0.3f));
            }
            if (level >= SuppressionUtility.PinnedLevel)
            {
                TryPinDown(pawn, source);
            }
        }

        public override void CompTickInterval(int delta)
        {
            if (level <= 0f || Find.TickManager.TicksGame - lastSuppressedTick < SuppressionUtility.DecayDelayTicks)
            {
                return;
            }
            level = SuppressionUtility.Decay(level, delta);
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
            return key.Translate(Mathf.Min(1f, level / SuppressionUtility.PinnedLevel).ToStringPercent());
        }

        public override void PostExposeData()
        {
            base.PostExposeData();
            Scribe_Values.Look(ref level, "vcoSuppression", 0f);
            Scribe_Values.Look(ref lastSuppressedTick, "vcoSuppressionTick", -99999);
            Scribe_Values.Look(ref nextPinTick, "vcoSuppressionPinTick", 0);
        }
    }
}
