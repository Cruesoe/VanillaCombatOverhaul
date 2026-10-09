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

        /// <summary>Content height measured on the previous frame, per tab, for sizing the scroll view.</summary>
        private readonly Dictionary<SettingsTab, float> contentHeights =
            new Dictionary<SettingsTab, float>();

        /// <summary>Height assumed for a tab not yet measured; taller than any tab so nothing is clipped.</summary>
        private const float UnmeasuredHeight = 4000f;

        // Expanded section keys; sections start collapsed.
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

            // Clamped before drawing so collapsing a section never shows blank space past the end.
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
                Toggle(l, "VCO_PointBlank", ref s.enablePointBlank);
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

            if (Section(l, "VCO_Section_Suppression"))
            {
                Toggle(l, "VCO_Suppression", ref s.enableSuppression);
                if (s.enableSuppression)
                {
                    Toggle(l, "VCO_SuppressionPinning", ref s.enableSuppressionPinning);
                    Toggle(l, "VCO_SuppressionMood", ref s.enableSuppressionMood);
                    s.suppressionStrength = Slider(l, "VCO_SuppressionStrength", s.suppressionStrength,
                                                   0.25f, 3f, 0.05f, "0.00");
                }
            }

            if (Section(l, "VCO_Section_FireModes"))
            {
                Toggle(l, "VCO_FireModes", ref s.enableFireModes);
                if (s.enableFireModes)
                {
                    DrawFireModes(l, s);
                }
            }
        }

        private static void DrawFireModes(Listing_Standard l, VCOSettings s)
        {
            Toggle(l, "VCO_FireModesNpc", ref s.fireModesForNpcs);

            l.Gap(6f);
            s.fireModeShortBurstRange = Slider(l, "VCO_FireModeShortBurstRange",
                                               s.fireModeShortBurstRange, 1f, 40f, 1f, "0");
            s.fireModePrecisionRange = Slider(l, "VCO_FireModePrecisionRange",
                                              s.fireModePrecisionRange, 1f, 60f, 1f, "0");
            // Precision starts where Short Burst does at the earliest, never before it.
            s.fireModePrecisionRange = Mathf.Max(s.fireModePrecisionRange, s.fireModeShortBurstRange);

            DrawTuning(l, FireMode.Precision, s.precisionTuning);
            DrawTuning(l, FireMode.ShortBurst, s.shortBurstTuning);
            DrawTuning(l, FireMode.Suppression, s.suppressionTuning);

            l.Gap(6f);
            if (l.ButtonText("VCO_FireModeReset".Translate()))
            {
                s.ResetFireModes();
            }
        }

        private static void DrawTuning(Listing_Standard l, FireMode mode, FireModeTuning t)
        {
            l.Gap(8f);
            l.Label(FireModeUtility.LabelFor(mode).Colorize(ColoredText.SubtleGrayColor));
            t.accuracy = Slider(l, "VCO_FireModeAccuracy", t.accuracy, 0.25f, 3f, 0.05f, "0.00");
            t.aimTime = Slider(l, "VCO_FireModeAimTime", t.aimTime, 0.25f, 3f, 0.05f, "0.00");
            t.cooldown = Slider(l, "VCO_FireModeCooldown", t.cooldown, 0.25f, 3f, 0.05f, "0.00");
            t.burstFactor = Slider(l, "VCO_FireModeBurst", t.burstFactor, 0.25f, 4f, 0.05f, "0.00");
            t.burstMaxChange = Mathf.RoundToInt(
                Slider(l, "VCO_FireModeBurstLimit", t.burstMaxChange, 0f, 30f, 1f, "0"));
        }

        private static void DrawEquipment(Listing_Standard l, VCOSettings s)
        {
            if (Section(l, "VCO_Section_AutoEquip"))
            {
                l.Label("VCO_AutoEquip_Intro".Translate());
                l.Gap(6f);
                Toggle(l, "VCO_AutoEquip", ref s.enableAutoEquip, restartRequired: true);
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

        /// <summary>Clickable foldout heading; returns true while the section is expanded.</summary>
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

        /// <summary>A labelled checkbox with the key plus "_Tip" as its tooltip.</summary>
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

        /// <summary>A labelled slider snapped to <paramref name="step"/>; the label takes the value as {0}.</summary>
        private static float Slider(Listing_Standard l, string key, float value, float min, float max,
                                    float step, string format)
        {
            var label = key.Translate(value.ToString(format)).ToString();
            var tip = (key + "_Tip").Translate().ToString();
            var result = l.SliderLabeled(label, value, min, max, 0.6f, tip);
            return Mathf.Clamp(GenMath.RoundTo(result, step), min, max);
        }

    }
}
