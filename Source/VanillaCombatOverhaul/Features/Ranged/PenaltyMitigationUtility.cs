using RimWorld;
using UnityEngine;
using Verse;

namespace VanillaCombatOverhaul
{
    /// <summary>Skilled shooters partly overcome weapon, weather and evasion penalties (Vanilla Combat Reloaded's statBump curve).</summary>
    public static class PenaltyMitigationUtility
    {
        /// <summary>Raises a penalty factor toward 1 by shooter skill; skill factor 1 leaves it unchanged.</summary>
        // Shot reports are built many times a tick while AI picks targets; skill changes far more slowly.
        private const int SkillCacheTicks = 60;

        public static float MitigatePenalty(float factor, Thing shooter, float scale)
        {
            if (factor >= 1f || shooter == null || scale <= 0f)
            {
                return factor;
            }
            return MitigateBySkill(factor, ShooterSkillFactor(shooter, scale));
        }

        /// <summary>Raises a penalty factor toward 1 by an already worked-out skill factor.</summary>
        public static float MitigateBySkill(float factor, float skillFactor)
        {
            if (factor >= 1f || skillFactor <= 1f)
            {
                return factor;
            }
            return Mathf.Pow(Mathf.Clamp01(factor), 1f / skillFactor);
        }

        /// <summary>Pawns read VCO_AccuracyMitigation, so mods that change it apply; turrets read their unfinalized accuracy.</summary>
        public static float ShooterSkillFactor(Thing shooter, float scale)
        {
            var stat = shooter is Pawn pawn
                ? pawn.GetStatValue(VCO_StatDefOf.VCO_AccuracyMitigation, true, SkillCacheTicks)
                : StatDefOf.ShootingAccuracyTurret.Worker.GetValue(StatRequest.For(shooter), false);
            return Mathf.Max(1f, stat / scale);
        }

        public static void ApplyToShotReport(ref ShotReport report, Thing caster)
        {
            var settings = VCOMod.Settings;
            if (settings == null || !settings.enableAdvancedAccuracy || caster == null || settings.accuracyScale <= 0f)
            {
                return;
            }

            var equipment = ShotReportAccess.GetEquipmentFactor(ref report);
            var weather = ShotReportAccess.GetWeatherFactor(ref report);
            if (equipment >= 1f && weather >= 1f)
            {
                return;
            }

            var skillFactor = ShooterSkillFactor(caster, settings.accuracyScale);
            var mitigatedEquipment = MitigateBySkill(equipment, skillFactor);
            var mitigatedWeather = MitigateBySkill(weather, skillFactor);

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
