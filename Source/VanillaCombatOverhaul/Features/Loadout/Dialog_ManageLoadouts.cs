using System.Collections.Generic;
using RimWorld;
using UnityEngine;
using Verse;

namespace VanillaCombatOverhaul
{
    public class Dialog_ManageLoadouts : Dialog_ManagePolicies<LoadoutPolicy>
    {
        private enum Tab
        {
            Weapons,
            Sidearm,
            Items
        }

        private const float RowHeight = 30f;
        private const float CheckboxHeight = 30f;

        private readonly ThingFilterUI.UIState weaponFilterState = new ThingFilterUI.UIState();
        private readonly ThingFilterUI.UIState sidearmFilterState = new ThingFilterUI.UIState();
        private readonly Dictionary<LoadoutItem, string> countBuffers = new Dictionary<LoadoutItem, string>();
        private Tab tab = Tab.Weapons;
        private Vector2 itemScroll;

        protected override string TitleKey => "VCO_Loadout_Title";

        protected override string TipKey => "VCO_Loadout_TitleTip";

        public override Vector2 InitialSize => new Vector2(700f, 700f);

        public Dialog_ManageLoadouts(LoadoutPolicy policy) : base(policy)
        {
        }

        private static AutoEquipPolicyComponent Database => AutoEquipPolicyComponent.Current;

        protected override LoadoutPolicy CreateNewPolicy() => Database.MakeNewLoadout();

        protected override LoadoutPolicy GetDefaultPolicy() => Database.DefaultLoadout();

        protected override void SetDefaultPolicy(LoadoutPolicy policy) => Database.SetDefault(policy);

        protected override AcceptanceReport TryDeletePolicy(LoadoutPolicy policy) => Database.TryDelete(policy);

        protected override List<LoadoutPolicy> GetPolicies() => Database.AllLoadouts;

        protected override void DoContentsRect(Rect rect)
        {
            var policy = SelectedPolicy;
            var tabs = new Rect(rect.x, rect.y, rect.width, RowHeight);
            var width = tabs.width / 3f;
            DoTabButton(new Rect(tabs.x, tabs.y, width, tabs.height), "VCO_Loadout_TabWeapons", Tab.Weapons);
            DoTabButton(new Rect(tabs.x + width, tabs.y, width, tabs.height), "VCO_Loadout_TabSidearm", Tab.Sidearm);
            DoTabButton(new Rect(tabs.x + width * 2f, tabs.y, width, tabs.height), "VCO_Loadout_TabItems", Tab.Items);
            rect.yMin += RowHeight + 6f;

            switch (tab)
            {
                case Tab.Sidearm:
                    DoSidearm(rect, policy);
                    break;
                case Tab.Items:
                    DoItems(rect, policy);
                    break;
                default:
                    DoWeapons(rect, policy);
                    break;
            }
        }

        private void DoTabButton(Rect rect, string key, Tab value)
        {
            var label = key.Translate().ToString();
            if (tab == value)
            {
                Widgets.DrawHighlightSelected(rect);
            }
            if (Widgets.ButtonText(rect.ContractedBy(2f), label))
            {
                tab = value;
            }
        }

        private void DoWeapons(Rect rect, LoadoutPolicy policy)
        {
            var check = new Rect(rect.x, rect.y, rect.width, CheckboxHeight);
            Widgets.CheckboxLabeled(check, "VCO_Loadout_AutoPrimary".Translate(), ref policy.autoPrimary);
            TooltipHandler.TipRegion(check, "VCO_Loadout_AutoPrimary_Tip".Translate());
            rect.yMin += CheckboxHeight + 4f;
            ThingFilterUI.DoThingFilterConfigWindow(rect, weaponFilterState, policy.weaponFilter,
                LoadoutUtility.WeaponParentFilter, 16);
        }

        private void DoSidearm(Rect rect, LoadoutPolicy policy)
        {
            var check = new Rect(rect.x, rect.y, rect.width, CheckboxHeight);
            var byOtherMod = LoadoutUtility.SidearmsByOtherMod;
            Widgets.CheckboxLabeled(check, "VCO_Loadout_CarrySidearm".Translate(), ref policy.carrySidearm, byOtherMod);
            TooltipHandler.TipRegion(check, (byOtherMod ? "VCO_Loadout_SidearmsByOtherMod" : "VCO_Loadout_CarrySidearm_Tip").Translate());
            rect.yMin += CheckboxHeight + 4f;
            if (byOtherMod)
            {
                GUI.color = Color.gray;
                Widgets.Label(rect, "VCO_Loadout_SidearmsByOtherMod".Translate());
                GUI.color = Color.white;
                return;
            }
            if (!policy.carrySidearm)
            {
                GUI.color = Color.gray;
                Widgets.Label(rect, "VCO_Loadout_SidearmOff".Translate());
                GUI.color = Color.white;
                return;
            }
            ThingFilterUI.DoThingFilterConfigWindow(rect, sidearmFilterState, policy.sidearmFilter,
                LoadoutUtility.MeleeParentFilter, 16);
        }

        private void DoItems(Rect rect, LoadoutPolicy policy)
        {
            var intro = new Rect(rect.x, rect.y, rect.width, Text.CalcHeight("VCO_Loadout_ItemsIntro".Translate(), rect.width));
            Widgets.Label(intro, "VCO_Loadout_ItemsIntro".Translate());
            rect.yMin = intro.yMax + 6f;

            var add = new Rect(rect.x, rect.y, Mathf.Min(rect.width, 200f), RowHeight);
            if (Widgets.ButtonText(add, "VCO_Loadout_AddItem".Translate()))
            {
                Find.WindowStack.Add(new Dialog_AddLoadoutItem(policy));
            }
            rect.yMin = add.yMax + 6f;

            Widgets.DrawMenuSection(rect);
            var outRect = rect.ContractedBy(4f);
            var view = new Rect(0f, 0f, outRect.width - 16f, policy.items.Count * RowHeight);
            Widgets.BeginScrollView(outRect, ref itemScroll, view);
            LoadoutItem remove = null;
            for (var i = 0; i < policy.items.Count; i++)
            {
                var item = policy.items[i];
                var row = new Rect(0f, i * RowHeight, view.width, RowHeight);
                if (i % 2 == 1)
                {
                    Widgets.DrawLightHighlight(row);
                }
                if (DoItemRow(row, item))
                {
                    remove = item;
                }
            }
            Widgets.EndScrollView();
            if (remove != null)
            {
                policy.items.Remove(remove);
                countBuffers.Remove(remove);
            }
        }

        /// <summary>Draws one carried item; returns true when its remove button was clicked.</summary>
        private bool DoItemRow(Rect row, LoadoutItem item)
        {
            var icon = new Rect(row.x + 2f, row.y + 3f, 24f, 24f);
            Widgets.ThingIcon(icon, item.thingDef);
            var removeRect = new Rect(row.xMax - 24f, row.y + 3f, 24f, 24f);
            var countRect = new Rect(removeRect.x - 130f, row.y + 3f, 120f, 24f);
            var labelRect = new Rect(icon.xMax + 6f, row.y, countRect.x - icon.xMax - 12f, row.height);
            using (new TextBlock(TextAnchor.MiddleLeft))
            {
                Widgets.Label(labelRect, item.thingDef.LabelCap.Truncate(labelRect.width));
            }
            if (!countBuffers.TryGetValue(item, out var buffer))
            {
                buffer = item.count.ToString();
            }
            Widgets.TextFieldNumeric(countRect, ref item.count, ref buffer, 1, LoadoutPolicy.MaxItemCount);
            countBuffers[item] = buffer;
            TooltipHandler.TipRegion(countRect, "VCO_Loadout_ItemCount_Tip".Translate());
            TooltipHandler.TipRegion(removeRect, "VCO_Loadout_RemoveItem".Translate());
            return Widgets.ButtonImage(removeRect, TexButton.Delete);
        }
    }
}
