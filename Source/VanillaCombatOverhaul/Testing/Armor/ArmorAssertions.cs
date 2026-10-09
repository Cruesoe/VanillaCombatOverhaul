using System;
using System.Collections.Generic;
using UnityEngine;
using Verse;

namespace VanillaCombatOverhaul
{
    /// <summary>Checks of the leftover-stretch maths, without pawns or a map.</summary>
    public static class ArmorAssertions
    {
        private const double Tolerance = 0.0005;

        public static List<AssertionResult> SelfTests()
        {
            var results = new List<AssertionResult>();
            foreach (var a in FormulaTests())
            {
                results.Add(a);
            }
            foreach (var a in SettingsHookTests())
            {
                results.Add(a);
            }
            return results;
        }

        private static IEnumerable<AssertionResult> FormulaTests()
        {
            yield return Check(
                "vanilla 100% leftover is 50% block, not a stop",
                Approx(AdvancedArmorUtility.BlockChance(1f), 0.5f)
                && Approx(AdvancedArmorUtility.HalfChance(1f), 0.5f),
                $"block {AdvancedArmorUtility.BlockChance(1f):P1}, half {AdvancedArmorUtility.HalfChance(1f):P1}");

            yield return Check(
                "vanilla 200% leftover always blocks",
                Approx(AdvancedArmorUtility.BlockChance(2f), 1f)
                && Approx(AdvancedArmorUtility.HalfChance(2f), 0f),
                $"block {AdvancedArmorUtility.BlockChance(2f):P1}");

            var stretch = 2f;
            yield return Check(
                "stretch 2 always-blocks at 100% displayed leftover",
                Approx(AdvancedArmorUtility.AlwaysBlockDisplayedLeftover(stretch), 1f),
                AdvancedArmorUtility.AlwaysBlockDisplayedLeftover(stretch).ToString("P0"));

            var hundred = AdvancedArmorUtility.StretchedLeftover(1f, 0f, stretch);
            yield return Check(
                "100% armor vs 0 AP stretches to a stop",
                Approx(hundred, 2f) && Approx(AdvancedArmorUtility.BlockChance(hundred), 1f),
                $"leftover {hundred:F2}, block {AdvancedArmorUtility.BlockChance(hundred):P1}");

            // Reloaded's own example: 50% AP, doubled on the inspect card, into 200% armor.
            var vcrExample = AdvancedArmorUtility.StretchedLeftover(2f, 1f, stretch);
            yield return Check(
                "200% armor vs 100% shown AP always blocks",
                Approx(vcrExample, 2f) && Approx(AdvancedArmorUtility.BlockChance(vcrExample), 1f),
                $"leftover {vcrExample:F2}");

            var matched = AdvancedArmorUtility.StretchedLeftover(0.4f, 0.4f, stretch);
            yield return Check(
                "armor that only matches AP still does nothing",
                Approx(matched, 0f) && Approx(AdvancedArmorUtility.BlockChance(matched), 0f),
                $"leftover {matched:F2}");

            var fifty = AdvancedArmorUtility.StretchedLeftover(0.5f, 0f, stretch);
            yield return Check(
                "50% leftover keeps a half-damage band",
                Approx(AdvancedArmorUtility.BlockChance(fifty), 0.5f)
                && Approx(AdvancedArmorUtility.HalfChance(fifty), 0.5f),
                $"block {AdvancedArmorUtility.BlockChance(fifty):P1}, half {AdvancedArmorUtility.HalfChance(fifty):P1}");

            var vest = AdvancedArmorUtility.StretchedLeftover(0.4f, 0.7f, stretch);
            yield return Check(
                "doubled AP dumps a vest it already matched",
                Approx(vest, 0f),
                $"leftover {vest:F2}");
        }

        private static IEnumerable<AssertionResult> SettingsHookTests()
        {
            var settings = VCOMod.Settings;
            if (settings == null)
            {
                yield return Check("armor settings hook", false, "settings not loaded");
                yield break;
            }

            var restoreEnable = settings.enableAdvancedArmor;
            var restoreStretch = settings.armorScale;
            var restorePen = settings.penetrationScale;
            settings.enableAdvancedArmor = true;
            settings.armorScale = 2f;
            settings.penetrationScale = 2f;

            try
            {
                float pen = 0.5f;
                float rating = 2f;
                AdvancedArmorUtility.ScaleLeftoverInputs(ref pen, ref rating);
                yield return Check(
                    "ApplyArmor prefix stretches both inputs",
                    Approx(pen, 1f) && Approx(rating, 4f),
                    $"pen {pen:F2}, rating {rating:F2}");

                var shown = AdvancedArmorUtility.ScaleDisplayedPenetration(0.5f);
                yield return Check(
                    "AP getter applies penetrationScale",
                    Approx(shown, 1f),
                    shown.ToString("P1"));

                settings.enableAdvancedArmor = false;
                float penOff = 0.5f;
                float ratingOff = 2f;
                AdvancedArmorUtility.ScaleLeftoverInputs(ref penOff, ref ratingOff);
                yield return Check(
                    "disabled armor leaves ApplyArmor inputs alone",
                    Approx(penOff, 0.5f) && Approx(ratingOff, 2f),
                    $"pen {penOff:F2}, rating {ratingOff:F2}");
            }
            finally
            {
                settings.enableAdvancedArmor = restoreEnable;
                settings.armorScale = restoreStretch;
                settings.penetrationScale = restorePen;
            }
        }

        private static bool Approx(float actual, float expected) =>
            Math.Abs(actual - expected) <= Tolerance;

        private static AssertionResult Check(string name, bool passed, string detail) =>
            new AssertionResult { Name = name, Passed = passed, Detail = detail };
    }
}
