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

    /// <summary>A pawn's suppression level, its decay, and taking cover when pinned.</summary>
    public class CompSuppression : ThingComp
    {
        public const float CoverSearchRadius = 5.9f;
        public const int PinTicks = 240;
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
                TryTakeCover(pawn, source);
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

        /// <summary>Non-player pawns move to nearby cover and hold there while pinned.</summary>
        private void TryTakeCover(Pawn pawn, IntVec3 source)
        {
            var settings = VCOMod.Settings;
            var now = Find.TickManager.TicksGame;
            if (settings == null || !settings.enableSuppressionPinning || now < nextPinTick)
            {
                return;
            }
            if (pawn.Faction == Faction.OfPlayer || pawn.IsPrisoner || pawn.InMentalState || pawn.Downed
                || pawn.jobs == null || pawn.CurJobDef == JobDefOf.AttackMelee)
            {
                return;
            }
            nextPinTick = now + PinTicks + PinCooldownTicks;

            var wait = JobMaker.MakeJob(JobDefOf.Wait_Combat);
            wait.expiryInterval = PinTicks;
            wait.checkOverrideOnExpire = true;

            var cover = SuppressionUtility.FindCover(pawn, source, CoverSearchRadius);
            if (cover != pawn.Position)
            {
                var move = JobMaker.MakeJob(JobDefOf.Goto, cover);
                move.locomotionUrgency = LocomotionUrgency.Sprint;
                move.expiryInterval = PinTicks;
                pawn.jobs.StartJob(move, JobCondition.InterruptForced);
                pawn.jobs.jobQueue.EnqueueFirst(wait);
            }
            else
            {
                pawn.jobs.StartJob(wait, JobCondition.InterruptForced);
            }
            VCODiagnostics.CountFor(pawn, "suppression.pinned");
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
