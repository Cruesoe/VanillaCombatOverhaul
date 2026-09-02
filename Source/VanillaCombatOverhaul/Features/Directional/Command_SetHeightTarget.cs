using System.Collections.Generic;
using UnityEngine;
using Verse;

namespace VanillaCombatOverhaul
{
    public class Command_SetHeightTarget : Command
    {
        public CompHeightTarget comp;
        public List<CompHeightTarget> comps;

        public override void ProcessInput(Event ev)
        {
            base.ProcessInput(ev);
            if (comps == null)
            {
                comps = new List<CompHeightTarget>();
            }
            if (comp != null && !comps.Contains(comp))
            {
                comps.Add(comp);
            }

            var options = new List<FloatMenuOption>();
            foreach (var mode in HeightTargetingUtility.Modes)
            {
                var captured = mode;
                options.Add(new FloatMenuOption(HeightTargetingUtility.LabelFor(captured), () =>
                {
                    foreach (var c in comps)
                    {
                        c.SetTargetingMode(captured);
                    }
                }));
            }
            Find.WindowStack.Add(new FloatMenu(options));
        }

        public override bool InheritInteractionsFrom(Gizmo other)
        {
            if (comps == null)
            {
                comps = new List<CompHeightTarget>();
            }
            if (other is Command_SetHeightTarget otherCommand && otherCommand.comp != null)
            {
                comps.Add(otherCommand.comp);
            }
            return false;
        }
    }
}
