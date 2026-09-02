using UnityEngine;
using Verse;

namespace VanillaCombatOverhaul
{
    public static class FiringArcUtility
    {
        /// <summary>
        /// Scales wild-miss radius with distance. Vanilla Combat Reloaded arc type 0:
        /// spread widens linearly with range inside a cone defined by arcDegrees.
        /// </summary>
        public static float AdjustMissRadius(float vanillaRadius, IntVec3 dest, IntVec3 source, float arcDegrees)
        {
            var settings = VCOMod.Settings;
            if (settings == null || !settings.enableFiringArc || arcDegrees <= 0f)
            {
                return vanillaRadius;
            }

            var angleRadians = arcDegrees * Mathf.PI / 360f;
            var distance = Vector3.Magnitude((dest - source).ToVector3());
            var maxSpread = distance * Mathf.Tan(angleRadians);
            var scaled = vanillaRadius * maxSpread / 10f;

            VCODiagnostics.Count("firingArc.adjusted");
            VCODiagnostics.Sample("firingArc.radius", scaled);
            return scaled;
        }
    }
}
