using System.Collections.Generic;
using RimWorld;
using UnityEngine;
using Verse;

namespace VanillaCombatOverhaul
{
    public class VCOMod : Mod
    {
        public static VCOSettings Settings { get; private set; }
        public static VCOMod Instance { get; private set; }

        private Vector2 scrollPosition;
        private SettingsTab drawnTab = SettingsTab.Combat;

        /// <summary>
        /// Measured content height, per tab. One shared height was not enough: the scroll view is
        /// sized from the previous frame's measurement, so arriving on a long tab carrying a short
        /// tab's height drew the overflow outside the scrollable region, where it could neither be
        /// seen nor scrolled to, and no scrollbar appeared for it either.
        /// </summary>
        private readonly Dictionary<SettingsTab, float> contentHeights =
            new Dictionary<SettingsTab, float>();

        /// <summary>
        /// Height assumed for a tab that has not been measured yet. Deliberately taller than any
        /// tab can be: over-estimating costs one frame of empty space below the content, whereas
        /// under-estimating hides content outright, so the first draw errs long and the
        /// measurement taken from it corrects the next one.
        /// </summary>
        private const float UnmeasuredHeight = 4000f;

        // Empty means collapsed. The Combat tab is long enough that an always-open
        // list buries later options (tracers, suppression) below the fold.
        private static readonly HashSet<string> ExpandedSections = new HashSet<string>();

        public VCOMod(ModContentPack content) : base(content)
        {
            Instance = this;
            // Mod constructors run before def loading, so XML PatchOperations can read this.
            Settings = GetSettings<VCOSettings>();
        }

        public override string SettingsCategory() => "VCO_ModName".Translate();

        public override void WriteSettings()
        {
            base.WriteSettings();
            PatchOperationSettingGated.InvalidateSnapshot();
        }

        public override void DoSettingsWindowContents(Rect inRect)
        {
            var s = Settings;

            var tabAnchor = new Rect(inRect.x, inRect.y + TabDrawer.TabHeight, inRect.width, 0f);
            var body = new Rect(inRect.x, inRect.y + TabDrawer.TabHeight, inRect.width,
                                inRect.height - TabDrawer.TabHeight);
            Widgets.DrawMenuSection(body);

            TabDrawer.DrawTabs(tabAnchor, new List<TabRecord>
            {
                Tab("VCO_Tab_Combat", SettingsTab.Combat),
                Tab("VCO_Tab_Equipment", SettingsTab.Equipment)
            });

            if (s.CurrentTab != drawnTab)
            {
                drawnTab = s.CurrentTab;
                scrollPosition = Vector2.zero;
            }

            var outRect = body.ContractedBy(12f);

            if (!contentHeights.TryGetValue(s.CurrentTab, out var contentHeight))
            {
                contentHeight = UnmeasuredHeight;
            }

            // Clamped before the draw rather than after it. Collapsing a section shortens the
            // content under a scroll position that is still deep, and correcting that only on the
            // following frame shows a frame of blank space past the end of the list.
            scrollPosition.y = Mathf.Clamp(scrollPosition.y, 0f,
                                           Mathf.Max(0f, contentHeight - outRect.height));

            var viewRect = new Rect(0f, 0f, outRect.width - 20f, Mathf.Max(contentHeight, outRect.height));

            Widgets.BeginScrollView(outRect, ref scrollPosition, viewRect);
            var l = new Listing_Standard();
            l.Begin(viewRect);

            switch (s.CurrentTab)
            {
                case SettingsTab.Equipment:
                    DrawEquipment(l, s);
                    break;
                default:
                    DrawCombat(l, s);
                    break;
            }

            contentHeights[s.CurrentTab] = l.CurHeight + 8f;
            l.End();
            Widgets.EndScrollView();
        }

        private static TabRecord Tab(string key, SettingsTab tab) =>
            new TabRecord(key.Translate(), () => Settings.CurrentTab = tab, Settings.CurrentTab == tab);

        // ------------------------------------------------------------------ tabs

        private static void DrawCombat(Listing_Standard l, VCOSettings s)
        {
            if (Section(l, "VCO_Section_Melee"))
            {
                Toggle(l, "VCO_Parry", ref s.enableParry);
                if (s.enableParry)
                {
                    Toggle(l, "VCO_Counter", ref s.enableCounterAttack);
                }
            }

            if (Section(l, "VCO_Section_Armor"))
            {
                Toggle(l, "VCO_AdvancedArmor", ref s.enableAdvancedArmor);
            }

            if (Section(l, "VCO_Section_Damage"))
            {
                Toggle(l, "VCO_DirectionalDamage", ref s.enableDirectionalDamage, restartRequired: true);
                if (s.enableDirectionalDamage)
                {
                    Toggle(l, "VCO_MeleeFlanking", ref s.enableMeleeFlanking);
                }
                Toggle(l, "VCO_HeightTargeting", ref s.enableHeightTargeting);
            }

            if (Section(l, "VCO_Section_Wounds"))
            {
                var enabled = s.enableBulletWorker || s.enableArrowWorker;
                var before = enabled;
                Toggle(l, "VCO_ProjectileWounds", ref enabled);
                if (enabled != before)
                {
                    s.enableBulletWorker = enabled;
                    s.enableArrowWorker = enabled;
                }
            }

            if (Section(l, "VCO_Section_Ranged"))
            {
                Toggle(l, "VCO_AdvancedAccuracy", ref s.enableAdvancedAccuracy);
                Toggle(l, "VCO_Evasion", ref s.enableEvasion);
                Toggle(l, "VCO_FiringArc", ref s.enableFiringArc);
                Toggle(l, "VCO_VisibleTracers", ref s.enableVisibleTracers);
            }
        }

        private static void DrawEquipment(Listing_Standard l, VCOSettings s)
        {
            if (Section(l, "VCO_Section_AutoEquip"))
            {
                l.Label("VCO_AutoEquip_Intro".Translate());
                l.Gap(6f);
                Toggle(l, "VCO_AutoEquip", ref s.enableAutoEquip);
            }

            if (Section(l, "VCO_Section_Apparel"))
            {
                var enabled = s.enableHandFeetPatch || s.enableAcidHeatPatch || s.enableThumpBluntPatch
                              || s.enableGlassesHelmetPatch || s.enableNoseMouthPatch
                              || s.enableMaskPatch || s.enableHeadsetPatch
                              || s.enableArrayHeadsetPatch || s.enableApparelTweaks;
                var before = enabled;
                Toggle(l, "VCO_ApparelCoverage", ref enabled, restartRequired: true);
                if (enabled != before)
                {
                    s.enableHandFeetPatch = enabled;
                    s.enableAcidHeatPatch = enabled;
                    s.enableThumpBluntPatch = enabled;
                    s.enableGlassesHelmetPatch = enabled;
                    s.enableNoseMouthPatch = enabled;
                    s.enableMaskPatch = enabled;
                    s.enableHeadsetPatch = enabled;
                    s.enableArrayHeadsetPatch = enabled;
                    s.enableApparelTweaks = enabled;
                }
            }
        }

        // --------------------------------------------------------------- helpers

        /// <summary>
        /// Clickable foldout heading. Returns true while the body should be drawn.
        /// Starts collapsed so every heading on a tab is visible without scrolling.
        /// </summary>
        private static bool Section(Listing_Standard l, string key)
        {
            l.Gap(8f);
            var rect = l.GetRect(28f);
            var expanded = ExpandedSections.Contains(key);

            Widgets.DrawHighlightIfMouseover(rect);
            if (Widgets.ButtonInvisible(rect))
            {
                if (expanded)
                {
                    ExpandedSections.Remove(key);
                }
                else
                {
                    ExpandedSections.Add(key);
                }
                expanded = !expanded;
            }

            var icon = expanded ? TexButton.Collapse : TexButton.Reveal;
            GUI.DrawTexture(new Rect(rect.x, rect.y + 2f, 24f, 24f), icon);

            var previous = Text.Font;
            Text.Font = GameFont.Medium;
            Widgets.Label(new Rect(rect.x + 28f, rect.y, rect.width - 28f, rect.height),
                          key.Translate());
            Text.Font = previous;

            l.GapLine(4f);
            return expanded;
        }

        /// <summary>
        /// A labelled checkbox. Every option carries a plain-language tooltip.
        /// </summary>
        private static void Toggle(Listing_Standard l, string key, ref bool value,
                                   bool restartRequired = false)
        {
            var label = key.Translate().ToString();
            var tip = (key + "_Tip").Translate().ToString();

            if (restartRequired)
            {
                label += "  " + "VCO_RestartTag".Translate();
                tip += "\n\n" + "VCO_RestartTag_Tip".Translate();
            }

            var before = value;
            l.CheckboxLabeled(label, ref value, tip);
            if (restartRequired && before != value)
            {
                Messages.Message("VCO_RestartNeeded".Translate(), MessageTypeDefOf.CautionInput, false);
            }
        }

    }
}
