using System.Collections.Generic;
using RimWorld;
using Verse;

namespace VanillaCombatOverhaul
{
    public class CompProperties_FireMode : CompProperties
    {
        public CompProperties_FireMode() => compClass = typeof(CompFireMode);
    }

    /// <summary>A pawn's chosen fire mode, kept across drafts; Auto stores its distance-picked mode separately.</summary>
    public class CompFireMode : ThingComp
    {
        private FireMode mode = FireMode.Default;
        private bool autoSelect;
        private FireMode autoMode = FireMode.Default;

        public Pawn Pawn => parent as Pawn;

        public FireMode Mode => mode;

        public bool AutoSelect => autoSelect;

        /// <summary>The mode auto selection picked for the current target.</summary>
        public FireMode AutoMode => autoMode;

        public void SetMode(FireMode value)
        {
            mode = value;
            autoSelect = false;
        }

        public void SetAuto() => autoSelect = true;

        public void NoteAutoMode(FireMode value) => autoMode = value;

        public override IEnumerable<Gizmo> CompGetGizmosExtra()
        {
            if (!FireModeUtility.Enabled || parent.Faction != Faction.OfPlayer)
            {
                yield break;
            }
            var pawn = Pawn;
            if (pawn == null || !pawn.Drafted
                || !FireModeUtility.IsModeVerb(pawn, FireModeUtility.PrimaryVerb(pawn)))
            {
                yield break;
            }

            yield return FireModeUtility.CommandFor(this);
        }

        public override void PostExposeData()
        {
            base.PostExposeData();
            Scribe_Values.Look(ref mode, "vcoFireMode", FireMode.Default);
            Scribe_Values.Look(ref autoSelect, "vcoFireModeAuto", false);
            Scribe_Values.Look(ref autoMode, "vcoFireModeAutoCurrent", FireMode.Default);
        }
    }
}
