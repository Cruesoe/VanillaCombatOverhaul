using HarmonyLib;
using Verse;

namespace VanillaCombatOverhaul
{
    /// <summary>Field references to ShotReport's private fields; a renamed field throws at startup.</summary>
    internal static class ShotReportAccess
    {
        private static readonly AccessTools.StructFieldRef<ShotReport, float> EquipmentRef =
            AccessTools.StructFieldRefAccess<ShotReport, float>("factorFromEquipment");

        private static readonly AccessTools.StructFieldRef<ShotReport, float> WeatherRef =
            AccessTools.StructFieldRefAccess<ShotReport, float>("factorFromWeather");

        private static readonly AccessTools.StructFieldRef<ShotReport, float> ShooterRef =
            AccessTools.StructFieldRefAccess<ShotReport, float>("factorFromShooterAndDist");

        private static readonly AccessTools.StructFieldRef<ShotReport, float> DistanceRef =
            AccessTools.StructFieldRefAccess<ShotReport, float>("distance");

        private static readonly AccessTools.StructFieldRef<ShotReport, TargetInfo> TargetRef =
            AccessTools.StructFieldRefAccess<ShotReport, TargetInfo>("target");

        public static float GetEquipmentFactor(ref ShotReport report) => EquipmentRef(ref report);

        public static float GetWeatherFactor(ref ShotReport report) => WeatherRef(ref report);

        public static void SetEquipmentFactor(ref ShotReport report, float value) =>
            EquipmentRef(ref report) = value;

        public static void SetWeatherFactor(ref ShotReport report, float value) =>
            WeatherRef(ref report) = value;

        public static float GetShooterFactor(ref ShotReport report) => ShooterRef(ref report);

        public static void SetShooterFactor(ref ShotReport report, float value) =>
            ShooterRef(ref report) = value;

        public static float GetDistance(ref ShotReport report) => DistanceRef(ref report);

        public static TargetInfo GetTarget(ref ShotReport report) => TargetRef(ref report);
    }
}
