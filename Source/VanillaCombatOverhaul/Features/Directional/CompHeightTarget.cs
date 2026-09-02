using System.Collections.Generic;
using RimWorld;
using Verse;

namespace VanillaCombatOverhaul
{
    public class CompProperties_HeightTarget : CompProperties
    {
        public CompProperties_HeightTarget() => compClass = typeof(CompHeightTarget);
    }

    public class CompHeightTarget : ThingComp
    {
        private BodyPartHeight targetingMode = BodyPartHeight.Undefined;

        public Pawn Pawn => parent as Pawn;

        public BodyPartHeight TargetingMode => targetingMode;

        public void SetTargetingMode(BodyPartHeight height) => targetingMode = height;

        public override IEnumerable<Gizmo> CompGetGizmosExtra()
        {
            var settings = VCOMod.Settings;
            if (settings == null || !settings.enableHeightTargeting)
            {
                yield break;
            }
            if (parent.Faction != Faction.OfPlayer)
            {
                yield break;
            }
            if (Pawn != null && !Pawn.Drafted)
            {
                yield break;
            }

            yield return HeightTargetingUtility.CommandFor(this);
        }

        public override void PostExposeData()
        {
            base.PostExposeData();
            Scribe_Values.Look(ref targetingMode, "vcoHeightTarget", BodyPartHeight.Undefined);
        }
    }
}
