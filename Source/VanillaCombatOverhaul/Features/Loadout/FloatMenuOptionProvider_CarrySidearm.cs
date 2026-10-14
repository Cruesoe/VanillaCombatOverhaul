using RimWorld;
using Verse;
using Verse.AI;

namespace VanillaCombatOverhaul
{
    /// <summary>Right-click option to pick up a melee weapon and lock it as the colonist's sidearm.</summary>
    public class FloatMenuOptionProvider_CarrySidearm : FloatMenuOptionProvider
    {
        protected override bool Drafted => true;

        protected override bool Undrafted => true;

        protected override bool Multiselect => false;

        protected override bool RequiresManipulation => true;

        protected override bool AppliesInt(FloatMenuContext context)
        {
            var pawn = context.FirstSelectedPawn;
            return LoadoutUtility.SidearmsEnabled && pawn.IsColonistPlayerControlled && pawn.inventory != null
                   && LoadoutUtility.CompFor(pawn) != null;
        }

        protected override FloatMenuOption GetSingleOptionFor(Thing clickedThing, FloatMenuContext context)
        {
            if (!(clickedThing is ThingWithComps weapon) || !weapon.def.IsMeleeWeapon || !weapon.def.IsWeapon
                || weapon.def.destroyOnDrop)
            {
                return null;
            }
            var pawn = context.FirstSelectedPawn;
            var label = weapon.LabelShort;
            if (LoadoutUtility.CompFor(pawn).LockedSidearm == weapon)
            {
                return null;
            }
            if (pawn.WorkTagIsDisabled(WorkTags.Violent))
            {
                return Disabled(label, "IsIncapableOfViolenceLower".Translate(pawn.LabelShort, pawn));
            }
            if (!pawn.CanReach(weapon, PathEndMode.ClosestTouch, Danger.Deadly))
            {
                return Disabled(label, "NoPath".Translate().CapitalizeFirst());
            }
            if (!EquipmentUtility.CanEquip(weapon, pawn, out var reason, false))
            {
                return Disabled(label, reason.CapitalizeFirst());
            }
            if (MassUtility.WillBeOverEncumberedAfterPickingUp(pawn, weapon, 1))
            {
                return Disabled(label, "VCO_Loadout_TooHeavy".Translate());
            }
            return FloatMenuUtility.DecoratePrioritizedTask(new FloatMenuOption("VCO_Loadout_CarryAsSidearm".Translate(label), () =>
            {
                LoadoutUtility.CompFor(pawn).LockedSidearm = weapon;
                weapon.SetForbidden(false);
                var job = JobMaker.MakeJob(JobDefOf.TakeCountToInventory, weapon);
                job.count = 1;
                pawn.jobs.TryTakeOrderedJob(job, JobTag.Misc);
            }, MenuOptionPriority.Default), pawn, weapon);
        }

        private static FloatMenuOption Disabled(string label, string reason) =>
            new FloatMenuOption("VCO_Loadout_CannotCarryAsSidearm".Translate(label) + ": " + reason, null);
    }
}
