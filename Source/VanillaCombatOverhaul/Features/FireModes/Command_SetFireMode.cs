using System.Collections.Generic;
using UnityEngine;
using Verse;

namespace VanillaCombatOverhaul
{
    /// <summary>
    /// One button for the whole choice, auto included, so a drafted squad does not grow a
    /// second gizmo. Multi-select follows Command_SetHeightTarget: every selected pawn's
    /// command folds into the one that is clicked.
    /// </summary>
    public class Command_SetFireMode : Command
    {
        public CompFireMode comp;
        public List<CompFireMode> comps;

        public override void ProcessInput(Event ev)
        {
            base.ProcessInput(ev);
            if (comps == null)
            {
                comps = new List<CompFireMode>();
            }
            if (comp != null && !comps.Contains(comp))
            {
                comps.Add(comp);
            }

            var options = new List<FloatMenuOption>
            {
                new FloatMenuOption("VCO_FireMode_Auto".Translate(), () =>
                {
                    foreach (var c in comps)
                    {
                        c.SetAuto();
                    }
                }, FireModeUtility.IconAuto, Color.white)
            };

            foreach (var mode in FireModeUtility.Modes)
            {
                // Offered when any selected pawn's weapon can use it. A pawn whose weapon
                // cannot simply fires as Default until it holds one that can.
                if (!comps.Exists(c => FireModeUtility.Offers(c.Pawn, mode)))
                {
                    continue;
                }
                var captured = mode;
                options.Add(new FloatMenuOption(FireModeUtility.LabelFor(captured), () =>
                {
                    foreach (var c in comps)
                    {
                        c.SetMode(captured);
                    }
                }, FireModeUtility.IconFor(captured), Color.white));
            }
            Find.WindowStack.Add(new FloatMenu(options));
        }

        public override bool InheritInteractionsFrom(Gizmo other)
        {
            if (comps == null)
            {
                comps = new List<CompFireMode>();
            }
            if (other is Command_SetFireMode otherCommand && otherCommand.comp != null)
            {
                comps.Add(otherCommand.comp);
            }
            return false;
        }
    }
}
