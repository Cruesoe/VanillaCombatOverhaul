using System.Collections.Generic;
using RimWorld;
using UnityEngine;
using Verse;

namespace VanillaCombatOverhaul
{
    /// <summary>Searchable list of items a loadout can carry.</summary>
    public class Dialog_AddLoadoutItem : Window
    {
        private const float RowHeight = 28f;

        private static List<ThingDef> carryable;

        private readonly LoadoutPolicy policy;
        private readonly QuickSearchWidget search = new QuickSearchWidget();
        private Vector2 scroll;

        public override Vector2 InitialSize => new Vector2(420f, 560f);

        public Dialog_AddLoadoutItem(LoadoutPolicy policy)
        {
            this.policy = policy;
            doCloseX = true;
            doCloseButton = true;
            closeOnClickedOutside = true;
            absorbInputAroundWindow = true;
        }

        /// <summary>Haulable items other than weapons, apparel and corpses.</summary>
        private static List<ThingDef> Carryable
        {
            get
            {
                if (carryable != null)
                {
                    return carryable;
                }
                carryable = new List<ThingDef>();
                foreach (var def in DefDatabase<ThingDef>.AllDefs)
                {
                    if (def.category == ThingCategory.Item && def.EverHaulable && !def.IsWeapon && !def.IsApparel
                        && !def.IsCorpse && def.thingCategories != null && !InStockGroup(def))
                    {
                        carryable.Add(def);
                    }
                }
                carryable.SortBy(d => d.label);
                return carryable;
            }
        }

        /// <summary>Items a carry group (medicine, ammunition) handles, set in the loadout's stock rows instead.</summary>
        private static bool InStockGroup(ThingDef def)
        {
            foreach (var group in DefDatabase<InventoryStockGroupDef>.AllDefs)
            {
                if (group.thingDefs.Contains(def))
                {
                    return true;
                }
            }
            return false;
        }

        public override void DoWindowContents(Rect inRect)
        {
            using (new TextBlock(GameFont.Medium))
            {
                Widgets.Label(new Rect(inRect.x, inRect.y, inRect.width, 32f), "VCO_Loadout_AddItem".Translate());
            }
            search.OnGUI(new Rect(inRect.x, inRect.y + 36f, inRect.width, 24f));

            var outRect = new Rect(inRect.x, inRect.y + 66f, inRect.width, inRect.height - 66f - CloseButSize.y - 10f);
            var shown = new List<ThingDef>();
            foreach (var def in Carryable)
            {
                if (search.filter.Matches(def.label) && policy.CountFor(def) == 0)
                {
                    shown.Add(def);
                }
            }
            var view = new Rect(0f, 0f, outRect.width - 16f, shown.Count * RowHeight);
            Widgets.BeginScrollView(outRect, ref scroll, view);
            for (var i = 0; i < shown.Count; i++)
            {
                var def = shown[i];
                var row = new Rect(0f, i * RowHeight, view.width, RowHeight);
                Widgets.DrawHighlightIfMouseover(row);
                Widgets.ThingIcon(new Rect(row.x + 2f, row.y + 2f, 24f, 24f), def);
                using (new TextBlock(TextAnchor.MiddleLeft))
                {
                    Widgets.Label(new Rect(row.x + 32f, row.y, row.width - 32f, row.height), def.LabelCap);
                }
                if (Widgets.ButtonInvisible(row))
                {
                    policy.items.Add(new LoadoutItem(def, 1));
                    Close();
                }
            }
            Widgets.EndScrollView();
        }
    }
}
