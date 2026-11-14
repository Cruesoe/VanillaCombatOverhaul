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
        private readonly ThingFilter inertSidearmFilter = new ThingFilter();
        private readonly Dictionary<LoadoutItem, string> countBuffers = new Dictionary<LoadoutItem, string>();
        private Tab tab = Tab.Weapons;
        private Vector2 itemScroll;
        private LoadoutPolicy inertSidearmSource;

        // Built once per window open: the carry groups shown and the text drawn every frame.
        private readonly List<(InventoryStockGroupDef group, string label)> stockGroups = new List<(InventoryStockGroupDef, string)>();
        private readonly string[] tabLabels;
        private readonly string autoPrimaryLabel, autoPrimaryTip, autoPrimaryOffTip;
        private readonly string carrySidearmLabel, carrySidearmTip, sidearmsByOtherModTip, sidearmOffTip;
        private readonly string itemsIntro, addItemLabel, matchWeaponLabel, matchWeaponTip, itemCountTip, removeItemTip;

        protected override string TitleKey => "VCO_Loadout_Title";

        protected override string TipKey => "VCO_Loadout_TitleTip";

        public override Vector2 InitialSize => new Vector2(700f, 700f);

        public Dialog_ManageLoadouts(LoadoutPolicy policy) : base(policy)
        {
            foreach (var group in LoadoutStockUtility.Groups)
            {
                stockGroups.Add((group, GroupLabel(group)));
            }
            tabLabels = new[]
            {
                "VCO_Loadout_TabWeapons".Translate().ToString(),
                "VCO_Loadout_TabSidearm".Translate().ToString(),
                "VCO_Loadout_TabItems".Translate().ToString()
            };
            autoPrimaryLabel = "VCO_Loadout_AutoPrimary".Translate();
            autoPrimaryTip = "VCO_Loadout_AutoPrimary_Tip".Translate();
            autoPrimaryOffTip = "VCO_Loadout_AutoPrimaryOff".Translate();
            carrySidearmLabel = "VCO_Loadout_CarrySidearm".Translate();
            carrySidearmTip = "VCO_Loadout_CarrySidearm_Tip".Translate();
            sidearmsByOtherModTip = "VCO_Loadout_SidearmsByOtherMod".Translate();
            sidearmOffTip = "VCO_Loadout_SidearmOff".Translate();
            itemsIntro = "VCO_Loadout_ItemsIntro".Translate();
            addItemLabel = "VCO_Loadout_AddItem".Translate();
            matchWeaponLabel = "VCO_Loadout_MatchWeapon".Translate();
            matchWeaponTip = "VCO_Loadout_MatchWeapon_Tip".Translate();
            itemCountTip = "VCO_Loadout_ItemCount_Tip".Translate();
            removeItemTip = "VCO_Loadout_RemoveItem".Translate();
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
            for (var i = 0; i < tabLabels.Length; i++)
            {
                DoTabButton(new Rect(tabs.x + width * i, tabs.y, width, tabs.height), tabLabels[i], (Tab)i);
            }
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

        private void DoTabButton(Rect rect, string label, Tab value)
        {
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
            var offForAll = !VCOMod.Settings.enableAutoPrimary;
            Widgets.CheckboxLabeled(check, autoPrimaryLabel, ref policy.autoPrimary, offForAll);
            TooltipHandler.TipRegion(check, offForAll ? autoPrimaryOffTip : autoPrimaryTip);
            rect.yMin += CheckboxHeight + 4f;
            ThingFilterUI.DoThingFilterConfigWindow(rect, weaponFilterState, policy.weaponFilter,
                LoadoutUtility.WeaponParentFilter, 16);
        }

        private void DoSidearm(Rect rect, LoadoutPolicy policy)
        {
            var check = new Rect(rect.x, rect.y, rect.width, CheckboxHeight);
            var byOtherMod = LoadoutUtility.SidearmsByOtherMod;
            Widgets.CheckboxLabeled(check, carrySidearmLabel, ref policy.carrySidearm, byOtherMod);
            TooltipHandler.TipRegion(check, byOtherMod ? sidearmsByOtherModTip : carrySidearmTip);
            rect.yMin += CheckboxHeight + 4f;
            if (!byOtherMod && policy.carrySidearm)
            {
                inertSidearmSource = null;
                ThingFilterUI.DoThingFilterConfigWindow(rect, sidearmFilterState, policy.sidearmFilter,
                    LoadoutUtility.MeleeParentFilter, 16);
                return;
            }

            // Shown grayed out and inert: the filter drawn is a copy, so clicks change nothing.
            var reason = byOtherMod ? sidearmsByOtherModTip : sidearmOffTip;
            var note = new Rect(rect.x, rect.y, rect.width, Text.CalcHeight(reason, rect.width));
            GUI.color = Color.gray;
            Widgets.Label(note, reason);
            rect.yMin = note.yMax + 4f;
            if (inertSidearmSource != policy)
            {
                inertSidearmFilter.CopyAllowancesFrom(policy.sidearmFilter);
                inertSidearmSource = policy;
            }
            var wasEnabled = GUI.enabled;
            GUI.enabled = false;
            ThingFilterUI.DoThingFilterConfigWindow(rect, sidearmFilterState, inertSidearmFilter,
                LoadoutUtility.MeleeParentFilter, 16);
            GUI.enabled = wasEnabled;
            GUI.color = Color.white;
            TooltipHandler.TipRegion(rect, reason);
        }

        private void DoItems(Rect rect, LoadoutPolicy policy)
        {
            var intro = new Rect(rect.x, rect.y, rect.width, Text.CalcHeight(itemsIntro, rect.width));
            Widgets.Label(intro, itemsIntro);
            rect.yMin = intro.yMax + 6f;

            for (var i = 0; i < stockGroups.Count; i++)
            {
                DoStockRow(new Rect(rect.x, rect.y, rect.width, RowHeight), policy, stockGroups[i].group, stockGroups[i].label);
                rect.yMin += RowHeight + 2f;
            }
            rect.yMin += 4f;

            var add = new Rect(rect.x, rect.y, Mathf.Min(rect.width, 200f), RowHeight);
            if (Widgets.ButtonText(add, addItemLabel))
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

        /// <summary>One vanilla carry group: which item, or the primary weapon's ammunition, and how many.</summary>
        private void DoStockRow(Rect row, LoadoutPolicy policy, InventoryStockGroupDef group, string groupLabel)
        {
            var entry = policy.StockFor(group);
            var labelRect = new Rect(row.x, row.y, 140f, row.height);
            var countRect = new Rect(row.xMax - 70f, row.y + 2f, 70f, row.height - 4f);
            var thingRect = new Rect(labelRect.xMax + 4f, row.y + 2f, countRect.x - labelRect.xMax - 8f, row.height - 4f);
            using (new TextBlock(TextAnchor.MiddleLeft))
            {
                Widgets.Label(labelRect, groupLabel);
            }

            var thingLabel = entry.matchWeapon
                ? matchWeaponLabel
                : LoadoutStockUtility.ChosenThing(entry).LabelCap.ToString();
            if (Widgets.ButtonText(thingRect, thingLabel.Truncate(thingRect.width)))
            {
                var options = new List<FloatMenuOption>();
                if (LoadoutStockUtility.IsAmmunitionGroup(group))
                {
                    options.Add(new FloatMenuOption(matchWeaponLabel, () =>
                    {
                        entry.matchWeapon = true;
                        LoadoutStockUtility.ApplyToUsers(policy, group);
                    }));
                }
                foreach (var thing in group.thingDefs)
                {
                    var captured = thing;
                    options.Add(new FloatMenuOption(thing.LabelCap, () =>
                    {
                        entry.thingDef = captured;
                        entry.matchWeapon = false;
                        LoadoutStockUtility.ApplyToUsers(policy, group);
                    }, captured));
                }
                Find.WindowStack.Add(new FloatMenu(options));
            }
            if (entry.matchWeapon)
            {
                TooltipHandler.TipRegion(thingRect, matchWeaponTip);
            }

            if (Widgets.ButtonText(countRect, entry.count.ToString()))
            {
                var options = new List<FloatMenuOption>();
                for (var i = group.min; i <= group.max; i++)
                {
                    var captured = i;
                    options.Add(new FloatMenuOption(i.ToString(), () =>
                    {
                        entry.count = captured;
                        LoadoutStockUtility.ApplyToUsers(policy, group);
                    }));
                }
                Find.WindowStack.Add(new FloatMenu(options));
            }
        }

        private static string GroupLabel(InventoryStockGroupDef group)
        {
            if (LoadoutStockUtility.IsAmmunitionGroup(group))
            {
                return "VCO_Loadout_Ammunition".Translate();
            }
            if (!group.label.NullOrEmpty())
            {
                return group.LabelCap.ToString();
            }
            // Groups made in code without a label (Dubs Mint Menus' meal and drug stock) take their default item's category.
            return group.DefaultThingDef?.FirstThingCategory?.LabelCap.ToString() ?? group.defName;
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
            TooltipHandler.TipRegion(countRect, itemCountTip);
            TooltipHandler.TipRegion(removeRect, removeItemTip);
            return Widgets.ButtonImage(removeRect, TexButton.Delete);
        }
    }
}
