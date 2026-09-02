using HarmonyLib;
using Verse;

namespace VanillaCombatOverhaul
{
    /// <summary>
    /// Typed access to ShotReport's private fields via Traverse. ShotReport is a struct, so
    /// writes must be copied back after mutation.
    /// </summary>
    internal static class ShotReportAccess
    {
        public static float GetEquipmentFactor(ref ShotReport report) =>
            Traverse.Create(report).Field("factorFromEquipment").GetValue<float>();

        public static float GetWeatherFactor(ref ShotReport report) =>
            Traverse.Create(report).Field("factorFromWeather").GetValue<float>();

        public static void SetEquipmentFactor(ref ShotReport report, float value)
        {
            var traverse = Traverse.Create(report);
            traverse.Field("factorFromEquipment").SetValue(value);
            report = traverse.GetValue<ShotReport>();
        }

        public static void SetWeatherFactor(ref ShotReport report, float value)
        {
            var traverse = Traverse.Create(report);
            traverse.Field("factorFromWeather").SetValue(value);
            report = traverse.GetValue<ShotReport>();
        }

        public static TargetInfo GetTarget(ref ShotReport report) =>
            Traverse.Create(report).Field("target").GetValue<TargetInfo>();
    }
}
