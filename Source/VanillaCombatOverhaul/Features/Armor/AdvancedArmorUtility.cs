using UnityEngine;
using Verse;

namespace VanillaCombatOverhaul
{
    /// <summary>
    /// Vanilla Combat Reloaded's leftover stretch: both ApplyArmor inputs are multiplied by
    /// <c>armorScale</c>, so vanilla's always-block at 200% leftover moves to 200 / scale percent.
    /// </summary>
    public static class AdvancedArmorUtility
    {
        /// <summary>Vanilla <see cref="ArmorUtility.MaxArmorRating"/>: leftover at or above this always deflects.</summary>
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

        /// <summary>Displayed leftover at which a hit always bounces, for the given stretch.</summary>
        public static float AlwaysBlockDisplayedLeftover(float stretch) =>
            stretch > 0f ? VanillaAlwaysBlockLeftover / stretch : VanillaAlwaysBlockLeftover;

        /// <summary>Leftover vanilla rolls against after stretching; <paramref name="displayedPenetration"/> is AP after penetrationScale.</summary>
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

        /// <summary>Multiplies both ApplyArmor inputs by the stretch, once per apparel layer.</summary>
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

        /// <summary>AP shown on weapons and carried by DamageInfo, multiplied by penetrationScale.</summary>
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
