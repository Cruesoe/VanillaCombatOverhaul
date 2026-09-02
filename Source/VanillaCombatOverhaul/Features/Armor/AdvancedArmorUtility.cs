using UnityEngine;
using Verse;

namespace VanillaCombatOverhaul
{
    /// <summary>
    /// Vanilla Combat Reloaded's leftover stretch: the armor roll is unchanged, both of its
    /// inputs are scaled so leftover armor becomes a hard stop at 100% instead of 200%.
    ///
    /// Vanilla always-blocks when leftover * 0.5 &gt;= 1, i.e. leftover &gt;= 200%. Stretching
    /// leftover by <c>armorScale</c> (default 2) moves that wall to 200 / scale percent.
    /// Armor that merely matches AP still does nothing: leftover stays zero.
    ///
    /// A StatPart on armor rating cannot do this. Leftover is rating minus AP, and scaling
    /// only one side would create protection from a match. Both numbers have to move together
    /// at the point vanilla subtracts them, which is <see cref="ArmorUtility"/>.ApplyArmor.
    /// </summary>
    public static class AdvancedArmorUtility
    {
        /// <summary>
        /// Vanilla <see cref="ArmorUtility.MaxArmorRating"/>: leftover at or above this always
        /// deflects, because leftover * <see cref="VanillaDeflectThresholdFactor"/> reaches 1.
        /// </summary>
        public const float VanillaAlwaysBlockLeftover = 2f;

        /// <summary>Vanilla <see cref="ArmorUtility.DeflectThresholdFactor"/>.</summary>
        public const float VanillaDeflectThresholdFactor = 0.5f;

        public static bool Active
        {
            get
            {
                var settings = VCOMod.Settings;
                return settings != null && settings.enableAdvancedArmor;
            }
        }

        /// <summary>
        /// Displayed leftover at which a hit always bounces, for the given stretch.
        /// Default stretch 2 → 100%. Stretch 1 is vanilla's 200%.
        /// </summary>
        public static float AlwaysBlockDisplayedLeftover(float stretch) =>
            stretch > 0f ? VanillaAlwaysBlockLeftover / stretch : VanillaAlwaysBlockLeftover;

        /// <summary>
        /// Leftover vanilla will roll against after stretching both inputs.
        /// <paramref name="displayedPenetration"/> is AP after penetrationScale, which is what
        /// the weapon inspect shows and what DamageInfo carries into ApplyArmor.
        /// </summary>
        public static float StretchedLeftover(float rating, float displayedPenetration, float stretch) =>
            Mathf.Max(stretch * (rating - displayedPenetration), 0f);

        /// <summary>Vanilla chance the hit is fully blocked, given leftover after any stretch.</summary>
        public static float BlockChance(float leftover) =>
            Mathf.Min(Mathf.Max(leftover, 0f) * VanillaDeflectThresholdFactor, 1f);

        /// <summary>Vanilla chance the hit is halved (and Sharp becomes Blunt).</summary>
        public static float HalfChance(float leftover)
        {
            var atLeastHalf = Mathf.Min(Mathf.Max(leftover, 0f), 1f);
            return Mathf.Max(0f, atLeastHalf - BlockChance(leftover));
        }

        /// <summary>
        /// Multiplies both ApplyArmor inputs by the leftover stretch. Harmony's prefix can
        /// take <c>ref</c> on by-value arguments, so each layer is scaled from the caller's
        /// numbers once; AP does not compound across apparel.
        /// </summary>
        public static void ScaleLeftoverInputs(ref float armorPenetration, ref float armorRating)
        {
            var settings = VCOMod.Settings;
            if (settings == null || !settings.enableAdvancedArmor)
            {
                return;
            }

            var stretch = settings.armorScale;
            if (stretch <= 0f || Mathf.Approximately(stretch, 1f))
            {
                return;
            }

            armorPenetration *= stretch;
            armorRating *= stretch;

            VCODiagnostics.Count("armor.applied");
            var leftover = Mathf.Max(armorRating - armorPenetration, 0f);
            VCODiagnostics.Sample("armor.leftover", leftover);
            if (leftover >= VanillaAlwaysBlockLeftover - 0.0001f)
            {
                VCODiagnostics.Count("armor.alwaysBlock");
            }
        }

        /// <summary>
        /// Inspect and DamageInfo AP. ApplyArmor then stretches this together with rating, so
        /// the number on the weapon is the number subtracted from armor before the leftover
        /// wall. Reloaded 1.6 targeted a GetArmorPenetration overload that 1.6 no longer has;
        /// this is the live (Thing, StringBuilder) getter plus the melee equivalents.
        /// </summary>
        public static float ScaleDisplayedPenetration(float armorPenetration)
        {
            var settings = VCOMod.Settings;
            if (settings == null || !settings.enableAdvancedArmor)
            {
                return armorPenetration;
            }

            var scale = settings.penetrationScale;
            if (scale <= 0f || Mathf.Approximately(scale, 1f))
            {
                return armorPenetration;
            }

            return armorPenetration * scale;
        }
    }
}
