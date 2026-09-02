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
        private float contentHeight = 600f;

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

            var outRect = body.ContractedBy(12f);
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

            contentHeight = l.CurHeight;
            l.End();
            Widgets.EndScrollView();
        }

        private static TabRecord Tab(string key, SettingsTab tab) =>
            new TabRecord(key.Translate(), () => Settings.CurrentTab = tab, Settings.CurrentTab == tab);

        // ------------------------------------------------------------------ tabs

        private static void DrawCombat(Listing_Standard l, VCOSettings s)
        {
            Section(l, "VCO_Section_Melee");
            Toggle(l, "VCO_Parry", ref s.enableParry);
            if (s.enableParry)
            {
                s.parryFrontFactor = Slider(l, "VCO_ParryFrontFactor", s.parryFrontFactor, 0.1f, 5f);
                s.parrySideFactor = Slider(l, "VCO_ParrySideFactor", s.parrySideFactor, 0f, 5f);
                s.parryBudgetPerWindow = Mathf.RoundToInt(
                    Slider(l, "VCO_ParryBudget", s.parryBudgetPerWindow, 1f, 6f, "0"));
                Toggle(l, "VCO_Counter", ref s.enableCounterAttack);
            }

            Section(l, "VCO_Section_Damage");
            Toggle(l, "VCO_DirectionalDamage", ref s.enableDirectionalDamage, restartRequired: true);
            if (s.enableDirectionalDamage)
            {
                Toggle(l, "VCO_MeleeFlanking", ref s.enableMeleeFlanking);
            }

            Section(l, "VCO_Section_Ranged");
            Toggle(l, "VCO_Evasion", ref s.enableEvasion, implemented: false);
            if (s.enableEvasion)
            {
                s.evasionFactor = Slider(l, "VCO_EvasionFactor", s.evasionFactor, 0f, 3f);
            }
            Toggle(l, "VCO_FiringArc", ref s.enableFiringArc, implemented: false);
            if (s.enableFiringArc)
            {
                s.firingArcDegrees = Slider(l, "VCO_FiringArcDegrees", s.firingArcDegrees, 1f, 179f, "0");
            }
            Toggle(l, "VCO_Suppression", ref s.enableSuppression, implemented: false);
            if (s.enableSuppression)
            {
                s.suppressionBuildRate = Slider(l, "VCO_SuppressionRate", s.suppressionBuildRate, 0.1f, 3f);
            }
        }

        private static void DrawEquipment(Listing_Standard l, VCOSettings s)
        {
            Section(l, "VCO_Section_Ammo");
            l.Label("VCO_Ammo_Intro".Translate());
            l.Gap(6f);
            Toggle(l, "VCO_Ammo", ref s.enableAmmo, restartRequired: true, implemented: false);
            if (s.enableAmmo)
            {
                s.ammoYieldFactor = Slider(l, "VCO_AmmoYield", s.ammoYieldFactor, 0.1f, 5f);
            }

            Section(l, "VCO_Section_Carrying");
            Toggle(l, "VCO_Sidearms", ref s.enableSidearms, implemented: false);
            Toggle(l, "VCO_Loadouts", ref s.enableLoadouts, implemented: false);
        }

        private static void DrawDiagnostics(Listing_Standard l, VCOSettings s)
        {
            Section(l, "VCO_Section_Logging");
            Toggle(l, "VCO_VerboseLogging", ref s.verboseLogging);
            if (s.verboseLogging)
            {
                s.diagnosticDumpIntervalTicks = Mathf.RoundToInt(
                    Slider(l, "VCO_DumpInterval", s.diagnosticDumpIntervalTicks, 0f, 15000f, "0"));
            }

            Section(l, "VCO_Section_Counters");
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

            Section(l, "VCO_Section_PatchStatus");
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

        // --------------------------------------------------------------- helpers

        /// <summary>Grey used for options that have no implementation behind them yet.</summary>
        private static readonly Color DisabledColour = new Color(1f, 1f, 1f, 0.45f);

        private static void Section(Listing_Standard l, string key)
        {
            l.Gap(10f);
            Text.Font = GameFont.Medium;
            l.Label(key.Translate());
            Text.Font = GameFont.Small;
            l.GapLine(4f);
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
