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

            // Belt and braces alongside the load-time reset: whatever a config file or an
            // earlier build left behind, a roadmap feature is never shown as enabled.
            s.ForceUnbuiltOff();

            var tabAnchor = new Rect(inRect.x, inRect.y + TabDrawer.TabHeight, inRect.width, 0f);
            var body = new Rect(inRect.x, inRect.y + TabDrawer.TabHeight, inRect.width,
                                inRect.height - TabDrawer.TabHeight);
            Widgets.DrawMenuSection(body);

            TabDrawer.DrawTabs(tabAnchor, new List<TabRecord>
            {
                Tab("VCO_Tab_Combat", SettingsTab.Combat),
                Tab("VCO_Tab_Equipment", SettingsTab.Equipment),
                Tab("VCO_Tab_Diagnostics", SettingsTab.Diagnostics)
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
                case SettingsTab.Diagnostics:
                    DrawDiagnostics(l, s);
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
                    s.parryFrontFactor = Slider(l, "VCO_ParryFrontFactor", s.parryFrontFactor, 0.1f, 5f);
                    s.parrySideFactor = Slider(l, "VCO_ParrySideFactor", s.parrySideFactor, 0f, 5f);
                    s.parryBudgetPerWindow = Mathf.RoundToInt(
                        Slider(l, "VCO_ParryBudget", s.parryBudgetPerWindow, 1f, 6f, "0"));
                    Toggle(l, "VCO_Counter", ref s.enableCounterAttack);
                }
            }

            if (Section(l, "VCO_Section_Armor"))
            {
                Toggle(l, "VCO_AdvancedArmor", ref s.enableAdvancedArmor);
                if (s.enableAdvancedArmor)
                {
                    var thresholdPct = 200f / Mathf.Max(s.armorScale, 0.001f);
                    thresholdPct = Slider(l, "VCO_ArmorThreshold", thresholdPct, 40f, 200f, "0");
                    s.armorScale = 200f / Mathf.Max(thresholdPct, 1f);
                    s.penetrationScale = Slider(l, "VCO_PenetrationScale", s.penetrationScale, 1f, 5f);
                }
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
                Toggle(l, "VCO_BulletWorker", ref s.enableBulletWorker);
                if (s.enableBulletWorker)
                {
                    s.bulletStoppingPowerCap = Slider(l, "VCO_BulletStoppingPowerCap",
                        s.bulletStoppingPowerCap, 1f, 20f, "0");
                }
                Toggle(l, "VCO_ArrowWorker", ref s.enableArrowWorker);
            }

            if (Section(l, "VCO_Section_Ranged"))
            {
                Toggle(l, "VCO_AdvancedAccuracy", ref s.enableAdvancedAccuracy);
                if (s.enableAdvancedAccuracy)
                {
                    s.accuracyScale = Slider(l, "VCO_AccuracyScale", s.accuracyScale, 1f, 60f, "0");
                }
                Toggle(l, "VCO_Evasion", ref s.enableEvasion);
                if (s.enableEvasion)
                {
                    s.evasionFactor = Slider(l, "VCO_EvasionFactor", s.evasionFactor, 0.01f, 1f);
                    s.evasionMinSpeed = Slider(l, "VCO_EvasionMinSpeed", s.evasionMinSpeed, 0f, 30f);
                    Toggle(l, "VCO_EvasionSkillContest", ref s.evasionSkillContest);
                }
                Toggle(l, "VCO_FiringArc", ref s.enableFiringArc);
                if (s.enableFiringArc)
                {
                    s.firingArcDegrees = Slider(l, "VCO_FiringArcDegrees", s.firingArcDegrees, 1f, 179f, "0");
                    s.firingArcType = Mathf.RoundToInt(
                        Slider(l, "VCO_FiringArcType", s.firingArcType, 0f, 5f, "0"));
                }
            }

            if (Section(l, "VCO_Section_Tracers"))
            {
                Toggle(l, "VCO_VisibleTracers", ref s.enableVisibleTracers);
                if (s.enableVisibleTracers)
                {
                    s.tracerScale = Slider(l, "VCO_TracerScale", s.tracerScale, 0.5f, 2f, "0.0");
                }
            }

            if (Section(l, "VCO_Section_Suppression"))
            {
                Toggle(l, "VCO_Suppression", ref s.enableSuppression, implemented: false);
                if (s.enableSuppression)
                {
                    s.suppressionBuildRate = Slider(l, "VCO_SuppressionRate", s.suppressionBuildRate, 0.1f, 3f);
                }
            }
        }

        private static void DrawEquipment(Listing_Standard l, VCOSettings s)
        {
            if (Section(l, "VCO_Section_Ammo"))
            {
                l.Label("VCO_Ammo_Intro".Translate());
                l.Gap(6f);
                Toggle(l, "VCO_Ammo", ref s.enableAmmo, restartRequired: true, implemented: false);
                if (s.enableAmmo)
                {
                    s.ammoYieldFactor = Slider(l, "VCO_AmmoYield", s.ammoYieldFactor, 0.1f, 5f);
                }
            }

            if (Section(l, "VCO_Section_Carrying"))
            {
                Toggle(l, "VCO_Sidearms", ref s.enableSidearms, implemented: false);
                if (s.enableSidearms)
                {
                    s.sidearmSwapTicksPerMass = Slider(l, "VCO_SidearmSwapTicks",
                                                       s.sidearmSwapTicksPerMass, 10f, 180f, "0");
                }
                if (SidearmUtility.ConflictingMod != null)
                {
                    l.Label("VCO_Sidearms_Conflict".Translate(SidearmUtility.ConflictingMod));
                }
                Toggle(l, "VCO_Loadouts", ref s.enableLoadouts, implemented: false);
            }

            if (Section(l, "VCO_Section_Apparel"))
            {
                l.Label("VCO_Apparel_Intro".Translate());
                l.Gap(6f);
                Toggle(l, "VCO_HandFeetPatch", ref s.enableHandFeetPatch, restartRequired: true);
                Toggle(l, "VCO_AcidHeatPatch", ref s.enableAcidHeatPatch, restartRequired: true);
                Toggle(l, "VCO_ThumpBluntPatch", ref s.enableThumpBluntPatch, restartRequired: true);
                Toggle(l, "VCO_GlassesHelmetPatch", ref s.enableGlassesHelmetPatch, restartRequired: true);
                Toggle(l, "VCO_NoseMouthPatch", ref s.enableNoseMouthPatch, restartRequired: true);
                Toggle(l, "VCO_MaskPatch", ref s.enableMaskPatch, restartRequired: true);
                Toggle(l, "VCO_HeadsetPatch", ref s.enableHeadsetPatch, restartRequired: true);
                Toggle(l, "VCO_ArrayHeadsetPatch", ref s.enableArrayHeadsetPatch, restartRequired: true);
                Toggle(l, "VCO_ApparelTweaks", ref s.enableApparelTweaks);
            }
        }

        private static void DrawDiagnostics(Listing_Standard l, VCOSettings s)
        {
            if (Section(l, "VCO_Section_Logging"))
            {
                Toggle(l, "VCO_VerboseLogging", ref s.verboseLogging);
                if (s.verboseLogging)
                {
                    s.diagnosticDumpIntervalTicks = Mathf.RoundToInt(
                        Slider(l, "VCO_DumpInterval", s.diagnosticDumpIntervalTicks, 0f, 15000f, "0"));
                }
            }

            if (Section(l, "VCO_Section_Counters"))
            {
                l.Label("VCO_Counters_Intro".Translate());
                l.Gap(6f);

                if (!s.verboseLogging)
                {
                    l.Label("VCO_Counters_Disabled".Translate());
                }
                else if (!VCODiagnostics.HasData)
                {
                    l.Label("VCO_Counters_Empty".Translate());
                }
                else
                {
                    foreach (var line in VCODiagnostics.Lines())
                    {
                        var row = l.GetRect(Text.LineHeight);
                        Widgets.Label(row.LeftPart(0.55f), line.Key);
                        Widgets.Label(row.RightPart(0.45f), line.Value);
                    }
                    l.Gap(8f);

                    var buttons = l.GetRect(30f);
                    if (Widgets.ButtonText(buttons.LeftHalf().ContractedBy(2f), "VCO_Counters_Write".Translate()))
                    {
                        VCODiagnostics.WriteReport();
                        Messages.Message("VCO_Counters_Written".Translate(), MessageTypeDefOf.TaskCompletion, false);
                    }
                    if (Widgets.ButtonText(buttons.RightHalf().ContractedBy(2f), "VCO_Counters_Reset".Translate()))
                    {
                        VCODiagnostics.Reset();
                    }
                }
            }

            if (Section(l, "VCO_Section_PatchStatus"))
            {
                l.Label("VCO_Diagnostics_Intro".Translate());
                l.Gap(6f);

                var any = false;
                foreach (var guard in PatchGuard.All)
                {
                    l.Label(guard.Id + ": " + (guard.Satisfied
                        ? "VCO_PatchOk".Translate()
                        : "VCO_PatchBroken".Translate(guard.Actual, guard.Expected)));
                    any = true;
                }
                if (!any)
                {
                    l.Label("VCO_NoGuardedPatches".Translate());
                }
            }
        }

        // --------------------------------------------------------------- helpers

        /// <summary>Grey used for options that have no implementation behind them yet.</summary>
        private static readonly Color DisabledColour = new Color(1f, 1f, 1f, 0.45f);

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
        /// A labelled checkbox. Every option carries a plain-language tooltip; options with no
        /// implementation behind them yet say so on the label, so a toggle can never imply an
        /// effect it does not have.
        /// </summary>
        private static void Toggle(Listing_Standard l, string key, ref bool value,
                                   bool restartRequired = false, bool implemented = true)
        {
            var label = key.Translate().ToString();
            var tip = (key + "_Tip").Translate().ToString();

            if (!implemented)
            {
                // Drawn greyed and inert rather than merely labelled. A switch that moves but
                // does nothing reads as a broken feature; one that cannot move reads as an
                // unfinished one, which is the truth.
                var unbuilt = false;
                var row = l.GetRect(Text.LineHeight);
                var previous = GUI.color;
                GUI.color = DisabledColour;
                Widgets.CheckboxLabeled(row, label + "  " + "VCO_NotImplemented".Translate(),
                                        ref unbuilt, disabled: true);
                GUI.color = previous;
                TooltipHandler.TipRegion(row, tip + "\n\n" + "VCO_NotImplemented_Tip".Translate());
                Widgets.DrawHighlightIfMouseover(row);

                // The caller still holds a ref to the real field, and the roadmap features
                // gate XML patching, so it is pinned off rather than merely left alone.
                value = false;
                return;
            }
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

        /// <summary>A labelled slider; the label carries the tooltip.</summary>
        private static float Slider(Listing_Standard l, string key, float value,
                                    float min, float max, string format = "0.00")
        {
            l.Label(key.Translate(value.ToString(format)), -1f, (key + "_Tip").Translate());
            return l.Slider(value, min, max);
        }
    }
}
