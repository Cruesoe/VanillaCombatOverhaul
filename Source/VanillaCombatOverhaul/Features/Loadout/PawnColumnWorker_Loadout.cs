using System.Collections.Generic;
using RimWorld;
using UnityEngine;
using Verse;
using Verse.Sound;

namespace VanillaCombatOverhaul
{
    /// <summary>Assign tab column choosing each colonist's loadout, modelled on vanilla's apparel policy column.</summary>
    public class PawnColumnWorker_Loadout : PawnColumnWorker
    {
        private const int TopAreaHeight = 65;

        public override void DoHeader(Rect rect, PawnTable table)
        {
            base.DoHeader(rect, table);
            MouseoverSounds.DoRegion(rect);
            var button = new Rect(rect.x, rect.y + (rect.height - TopAreaHeight), Mathf.Min(rect.width, 360f), 32f);
            if (Widgets.ButtonText(button, "VCO_Loadout_Manage".Translate()))
            {
                Find.WindowStack.Add(new Dialog_ManageLoadouts(null));
            }
        }

        public override void DoCell(Rect rect, Pawn pawn, PawnTable table)
        {
            var comp = LoadoutUtility.CompFor(pawn);
            var current = comp?.Loadout;
            if (current == null)
            {
                return;
            }
            var cell = rect.ContractedBy(0f, 2f);
            if (!LoadoutUtility.Enabled)
            {
                GUI.color = Color.gray;
                using (new TextBlock(TextAnchor.MiddleCenter))
                {
                    Widgets.Label(cell, "VCO_Loadout_Disabled".Translate().Truncate(cell.width));
                }
                GUI.color = Color.white;
                TooltipHandler.TipRegion(cell, "VCO_Loadout_Disabled_Tip".Translate());
                return;
            }
            Widgets.Dropdown(cell, pawn, p => LoadoutUtility.LoadoutFor(p), Menu, current.label.Truncate(cell.width),
                null, current.label, null, null, true);
        }

        private static IEnumerable<Widgets.DropdownMenuElement<LoadoutPolicy>> Menu(Pawn pawn)
        {
            var database = AutoEquipPolicyComponent.Current;
            if (database == null)
            {
                yield break;
            }
            foreach (var loadout in database.AllLoadouts)
            {
                yield return new Widgets.DropdownMenuElement<LoadoutPolicy>
                {
                    option = new FloatMenuOption(loadout.label, () =>
                    {
                        var comp = LoadoutUtility.CompFor(pawn);
                        if (comp != null)
                        {
                            comp.Loadout = loadout;
                        }
                    }),
                    payload = loadout
                };
            }
            yield return new Widgets.DropdownMenuElement<LoadoutPolicy>
            {
                option = new FloatMenuOption($"{"AssignTabEdit".Translate()}...",
                    () => Find.WindowStack.Add(new Dialog_ManageLoadouts(LoadoutUtility.LoadoutFor(pawn))))
            };
        }

        public override int GetMinWidth(PawnTable table) => Mathf.Max(base.GetMinWidth(table), 194);

        public override int GetOptimalWidth(PawnTable table) =>
            Mathf.Clamp(251, GetMinWidth(table), GetMaxWidth(table));

        public override int GetMinHeaderHeight(PawnTable table) =>
            Mathf.Max(base.GetMinHeaderHeight(table), TopAreaHeight);

        public override int Compare(Pawn a, Pawn b) => IdOf(a).CompareTo(IdOf(b));

        private static int IdOf(Pawn pawn) => LoadoutUtility.LoadoutFor(pawn)?.id ?? int.MinValue;
    }
}
