using UnityEngine;
using Verse;

namespace VanillaCombatOverhaul
{
    public static class FiringArcUtility
    {
        public const int TypeCount = 6;

        /// <summary>Scales the wild-miss radius with distance, using one of Vanilla Combat Reloaded's six distributions.</summary>
        public static float AdjustMissRadius(float vanillaRadius, IntVec3 dest, IntVec3 source,
                                             float arcDegrees, int arcType = 0)
        {
            var settings = VCOMod.Settings;
            if (settings == null || !settings.enableFiringArc || arcDegrees <= 0f)
            {
                return vanillaRadius;
            }

            var adjusted = RadiusFor(vanillaRadius, dest, source, arcDegrees, arcType);
            VCODiagnostics.Count("firingArc.adjusted");
            VCODiagnostics.Sample("firingArc.radius", adjusted);
            return adjusted;
        }

        /// <summary>Pure miss-radius maths, for tests. Does not consult settings.</summary>
        public static float RadiusFor(float vanillaRadius, IntVec3 dest, IntVec3 source,
                                      float arcDegrees, int arcType)
        {
            var angleRadians = arcDegrees * Mathf.PI / 360f;
            var distance = Vector3.Magnitude((dest - source).ToVector3());
            var maxSpread = distance * Mathf.Tan(angleRadians);

            switch (Mathf.Clamp(arcType, 0, TypeCount - 1))
            {
                case 0:
                    return vanillaRadius * maxSpread / 10f;
                case 1:
                    return Mathf.Min(vanillaRadius * maxSpread / 10f, 10f);
                case 2:
                    return vanillaRadius * Mathf.Min(maxSpread / 10f, 1f);
                case 3:
                    return Mathf.Max(Mathf.Min(vanillaRadius, maxSpread), vanillaRadius * maxSpread / 10f);
                case 4:
                    return Mathf.Max(Mathf.Min(vanillaRadius, maxSpread),
                                     Mathf.Min(vanillaRadius * maxSpread / 10f, 10f));
                default:
                    return Mathf.Min(vanillaRadius, maxSpread);
            }
        }
    }
}
