using System.Text;
using HarmonyLib;
using RimWorld;
using UnityEngine;
using Verse;

namespace VanillaCombatOverhaul
{
    /// <summary>
    /// Vanilla Combat Reloaded's inspect readout: vanilla weapon/weather numbers plus the
    /// skill-adjusted ones, evasion, incoming side, and targeted-height chance.
    ///
    /// Reloaded replaced the whole string. This restores the raw factors for vanilla's own
    /// lines, then appends the extras, so a RimWorld update that adds a new factor still
    /// shows it.
    /// </summary>
    [HarmonyPatch(typeof(ShotReport), nameof(ShotReport.GetTextReadout))]
    public static class Patch_ShotReport_GetTextReadout
    {
        public static void Prefix(ref ShotReport __instance)
        {
            ReadoutScratch.Begin(ref __instance);
        }

        public static void Postfix(ref ShotReport __instance, ref string __result)
        {
            var extras = ReadoutScratch.End(ref __instance);
            if (extras.NullOrEmpty())
            {
                return;
            }
            __result += extras;
        }
    }

    internal static class ReadoutScratch
    {
        private static bool stashed;
        private static float mitigatedEquipment;
        private static float mitigatedWeather;
        private static float rawEquipment;
        private static float rawWeather;
        private static float rawEvasion;
        private static float finalEvasion;

        public static void Begin(ref ShotReport report)
        {
            stashed = false;
            var settings = VCOMod.Settings;
            if (settings == null)
            {
                return;
            }

            mitigatedEquipment = ShotReportAccess.GetEquipmentFactor(ref report);
            mitigatedWeather = ShotReportAccess.GetWeatherFactor(ref report);

            if (!TryRawFactors(ref report, out rawEquipment, out rawWeather))
            {
                return;
            }

            ShotReportAccess.SetEquipmentFactor(ref report, rawEquipment);
            ShotReportAccess.SetWeatherFactor(ref report, rawWeather);
            stashed = true;
        }

        public static string End(ref ShotReport report)
        {
            if (stashed)
            {
                ShotReportAccess.SetEquipmentFactor(ref report, mitigatedEquipment);
                ShotReportAccess.SetWeatherFactor(ref report, mitigatedWeather);
            }

            var settings = VCOMod.Settings;
            if (settings == null)
            {
                return null;
            }

            var sb = new StringBuilder();
            if (stashed && !Mathf.Approximately(rawEquipment, mitigatedEquipment))
            {
                sb.AppendLine("      " + "VCO_AdjWeapon".Translate() + ": " + mitigatedEquipment.ToStringPercent());
            }
            if (stashed && !Mathf.Approximately(rawWeather, mitigatedWeather) && rawWeather < 0.99f)
            {
                sb.AppendLine("      " + "VCO_AdjWeather".Translate() + ": " + mitigatedWeather.ToStringPercent());
            }

            var target = ShotReportAccess.GetTarget(ref report);
            Thing caster = Find.Selector.SingleSelectedThing;
            if (settings.enableEvasion && target.Thing is Pawn targetPawn && caster != null)
            {
                rawEvasion = EvasionUtility.RawMovementMultiplier(
                    targetPawn, settings.evasionFactor, settings.evasionMinSpeed);
                finalEvasion = EvasionUtility.HitChanceMultiplier(targetPawn, caster);
                if (rawEvasion < 0.99f)
                {
                    sb.AppendLine("   " + "VCO_EvasionRead".Translate() + ": " + rawEvasion.ToStringPercent());
                    if (!Mathf.Approximately(rawEvasion, finalEvasion))
                    {
                        sb.AppendLine("      " + "VCO_AdjEvasionRead".Translate() + ": "
                                      + finalEvasion.ToStringPercent());
                    }
                }
            }

            if ((settings.enableDirectionalDamage || settings.enableHeightTargeting)
                && target.Thing is Pawn hitPawn && caster != null)
            {
                var origin = caster.Position;
                var facing = FacingUtility.Relative(origin, hitPawn);
                var side = DirectionalHitUtility.GroupFor(facing);
                if (side != null)
                {
                    sb.AppendLine("   " + "VCO_TargetSide".Translate() + ": " + side.labelShort);
                }

                var height = HeightTargetingUtility.GetTargetHeight(caster);
                if (height != BodyPartHeight.Undefined)
                {
                    var chance = HeightTargetingUtility.ChanceToLand(
                        caster, hitPawn, side, height, DamageDefOf.Bullet, melee: false);
                    sb.AppendLine("   " + "VCO_HeightChance".Translate()
                                  + HeightTargetingUtility.LabelFor(height) + ": " + chance.ToStringPercent());
                }
            }

            stashed = false;
            return sb.Length == 0 ? null : sb.ToString();
        }

        private static bool TryRawFactors(ref ShotReport report, out float equipment, out float weather)
        {
            equipment = ShotReportAccess.GetEquipmentFactor(ref report);
            weather = ShotReportAccess.GetWeatherFactor(ref report);

            var pawn = Find.Selector.SingleSelectedThing as Pawn;
            var verb = pawn?.CurrentEffectiveVerb;
            if (verb == null || pawn?.Map == null)
            {
                return false;
            }

            var target = ShotReportAccess.GetTarget(ref report);
            if (!target.IsValid)
            {
                return false;
            }

            var distance = (target.Cell - pawn.Position).LengthHorizontal;
            equipment = verb.verbProps.GetHitChanceFactor(verb.EquipmentSource, distance);
            weather = pawn.Map.weatherManager.CurWeatherAccuracyMultiplier;
            return true;
        }
    }
}
