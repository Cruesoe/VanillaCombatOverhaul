using RimWorld;
using UnityEngine;
using Verse;

namespace VanillaCombatOverhaul
{
    /// <summary>Skilled shooters partly overcome weapon, weather and evasion penalties (Vanilla Combat Reloaded's statBump curve).</summary>
    public static class PenaltyMitigationUtility
    {
        /// <summary>Raises a penalty factor toward 1 by shooter skill; skill factor 1 leaves it unchanged.</summary>
        public static float MitigatePenalty(float factor, Thing shooter, float scale)
        {
            if (factor >= 1f || shooter == null || scale <= 0f)
            {
                return factor;
            }

            var skillFactor = ShooterSkillFactor(shooter, scale);
            if (skillFactor <= 1f)
            {
                return factor;
            }

            return Mathf.Pow(Mathf.Clamp01(factor), 1f / skillFactor);
        }

        public static float ShooterSkillFactor(Thing shooter, float scale)
        {
            float stat;
            if (shooter is Pawn pawn)
            {
                stat = StatDefOf.ShootingAccuracyPawn.Worker.GetValue(StatRequest.For(pawn), false);
            }
            else
            {
                stat = StatDefOf.ShootingAccuracyTurret.Worker.GetValue(StatRequest.For(shooter), false);
            }

            return Mathf.Max(1f, stat / scale);
        }

        public static void ApplyToShotReport(ref ShotReport report, Thing caster)
        {
            var settings = VCOMod.Settings;
            if (settings == null || !settings.enableAdvancedAccuracy || caster == null)
            {
                return;
            }

            var scale = settings.accuracyScale;
            var equipment = ShotReportAccess.GetEquipmentFactor(ref report);
            var weather = ShotReportAccess.GetWeatherFactor(ref report);

            var mitigatedEquipment = MitigatePenalty(equipment, caster, scale);
            var mitigatedWeather = MitigatePenalty(weather, caster, scale);

            if (!Mathf.Approximately(equipment, mitigatedEquipment))
            {
                VCODiagnostics.Count("accuracy.mitigation.factor.weapon");
                ShotReportAccess.SetEquipmentFactor(ref report, mitigatedEquipment);
            }

            if (!Mathf.Approximately(weather, mitigatedWeather))
            {
                VCODiagnostics.Count("accuracy.mitigation.factor.weather");
                ShotReportAccess.SetWeatherFactor(ref report, mitigatedWeather);
            }

            if (!Mathf.Approximately(equipment, mitigatedEquipment)
                || !Mathf.Approximately(weather, mitigatedWeather))
            {
                VCODiagnostics.Count("accuracy.mitigation.applied");
            }
        }
    }
}
