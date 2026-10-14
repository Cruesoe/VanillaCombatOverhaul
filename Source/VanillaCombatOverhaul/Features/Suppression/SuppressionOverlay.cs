using UnityEngine;
using Verse;

namespace VanillaCombatOverhaul
{
    /// <summary>Icon drawn above a pawn's head for as long as it is suppressed or pinned.</summary>
    [StaticConstructorOnStartup]
    public static class SuppressionOverlay
    {
        private const float Size = 0.45f;
        private const float HeightOffset = 0.75f;

        private static readonly Material SuppressedMat =
            MaterialPool.MatFrom("UI/Overlays/VCO_Suppressed", ShaderDatabase.MetaOverlay);
        private static readonly Material PinnedMat =
            MaterialPool.MatFrom("UI/Overlays/VCO_Pinned", ShaderDatabase.MetaOverlay);

        public static void Draw(Pawn pawn, bool pinned)
        {
            var pos = pawn.DrawPos;
            pos.y = AltitudeLayer.MetaOverlays.AltitudeFor();
            pos.z += HeightOffset;
            Graphics.DrawMesh(MeshPool.plane10, Matrix4x4.TRS(pos, Quaternion.identity, new Vector3(Size, 1f, Size)),
                              pinned ? PinnedMat : SuppressedMat, 0);
        }
    }
}
