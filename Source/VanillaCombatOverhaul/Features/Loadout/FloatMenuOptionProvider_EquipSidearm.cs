using System.Collections.Generic;
using RimWorld;
using Verse;
using Verse.AI;

namespace VanillaCombatOverhaul
{
    /// <summary>
    /// "Carry X as sidearm", one line under vanilla's own "Equip X".
    ///
    /// RimWorld 1.6 builds its right-click menu from every <see cref="FloatMenuOptionProvider"/>
    /// subclass it can find, so adding one is a matter of declaring it -- no vanilla def, provider
    /// or method is modified to make this appear (design rule 2). It covers both the ground and the
    /// stockpile, because to a float menu those are the same click.
    /// </summary>
    public class FloatMenuOptionProvider_EquipSidearm : FloatMenuOptionProvider
    {
        protected override bool Drafted => true;
        protected override bool Undrafted => true;
        protected override bool Multiselect => false;
        protected override bool MechanoidCanDo => false;

        public override IEnumerable<FloatMenuOption> GetOptionsFor(Thing clickedThing, FloatMenuContext context)
        {
            if (!SidearmUtility.Active)
            {
                yield break;
            }

            var pawn = context.FirstSelectedPawn;
            var comp = SidearmUtility.CompFor(pawn);
            if (comp == null || !(clickedThing is ThingWithComps weapon)
                || weapon.def == null || !weapon.def.IsWeapon)
            {
                yield break;
            }
            if (weapon == pawn.equipment?.Primary || weapon == comp.Reserve)
            {
                yield break;
            }

            var name = weapon.LabelShort;

            // Refusals are shown, not hidden. An option that quietly fails to appear reads as a
            // broken mod; a greyed one that says why reads as a rule.
            if (!SidearmUtility.CanBeSidearm(pawn, weapon, out var reason))
            {
                if (!reason.NullOrEmpty())
                {
                    yield return new FloatMenuOption(
                        "VCO_CannotCarryAsSidearm".Translate(name) + " (" + reason + ")", null);
                }
                yield break;
            }
            if (!pawn.CanReach(weapon, PathEndMode.ClosestTouch, Danger.Deadly))
            {
                yield return new FloatMenuOption(
                    "VCO_CannotCarryAsSidearm".Translate(name) + " (" + "NoPath".Translate() + ")", null);
                yield break;
            }

            var previous = comp.Reserve;
            var label = previous != null
                ? "VCO_CarryAsSidearmReplacing".Translate(name, previous.LabelShort)
                : "VCO_CarryAsSidearm".Translate(name);

            yield return FloatMenuUtility.DecoratePrioritizedTask(
                new FloatMenuOption(label, () =>
                {
                    var def = pawn.Drafted ? VCO_JobDefOf.VCO_SwapWeaponCombat : VCO_JobDefOf.VCO_SwapWeapon;
                    var job = JobMaker.MakeJob(def, weapon);
                    job.playerForced = true;
                    pawn.jobs.TryTakeOrderedJob(job, JobTag.Misc);
                }),
                pawn, weapon);
        }
    }
}
