using RimWorld;
using UnityEngine;
using Verse;

namespace VanillaCombatOverhaul
{
    /// <summary>Draws a short glowing streak behind in-flight projectiles, coloured by damage type.</summary>
    public static class TracerUtility
    {
        // Streak size at tracerScale 1, in cells.
        private const float BaseLength = 1.1f;
        private const float BaseWidth = 0.07f;

        public static void Draw(Projectile projectile, Vector3 drawLoc, float travelled)
        {
            var settings = VCOMod.Settings;
            if (settings == null || !settings.enableVisibleTracers || projectile?.def?.projectile == null)
            {
                return;
            }
            if (projectile.def.projectile.explosionRadius > 0.2f)
            {
                return;
            }

            var travel = ProjectileAccess.Destination(projectile) - ProjectileAccess.Origin(projectile);
            if (travel.sqrMagnitude < 0.0001f)
            {
                return;
            }

            var sizeScale = Mathf.Clamp(settings.tracerScale, 0.5f, 2f);
            // The tail never reaches back past the muzzle.
            var length = Mathf.Min(BaseLength * sizeScale, travelled);
            if (length < 0.05f)
            {
                return;
            }
            var width = BaseWidth * sizeScale;
            var dir = travel.normalized;
            var tail = drawLoc - dir * length;
            tail.y = drawLoc.y;
            var mid = (drawLoc + tail) * 0.5f;
            var scale = new Vector3(width, 1f, length);
            var rotation = Quaternion.LookRotation(dir);

            // One material lookup for both quads.
            var material = MatFor(projectile);

            Graphics.DrawMesh(MeshPool.plane10, Matrix4x4.TRS(mid, rotation, scale),
                              material, 0);

            var headScale = new Vector3(width * 1.6f, 1f, width * 1.6f);
            Graphics.DrawMesh(MeshPool.plane10, Matrix4x4.TRS(drawLoc, rotation, headScale),
                              material, 0);
        }

        public static Color ColorFor(Projectile projectile)
        {
            var def = projectile?.def?.projectile?.damageDef;
            if (def == null)
            {
                return new Color(1f, 0.9f, 0.35f, 0.9f);
            }
            if (ProjectileWoundUtility.IsBulletDef(def))
            {
                return new Color(1f, 0.85f, 0.2f, 0.95f);
            }
            if (ProjectileWoundUtility.IsArrowDef(def))
            {
                return new Color(1f, 0.5f, 0.15f, 0.95f);
            }
            if (def.isExplosive || def == DamageDefOf.Flame || def == DamageDefOf.Burn)
            {
                return new Color(1f, 0.35f, 0.1f, 0.9f);
            }
            if (def == DamageDefOf.Stun || def == DamageDefOf.EMP)
            {
                return new Color(0.35f, 0.85f, 1f, 0.9f);
            }
            return new Color(0.95f, 0.95f, 0.7f, 0.9f);
        }

        private static Material MatFor(Projectile projectile) =>
            MaterialPool.MatFrom(BaseContent.WhiteTex, ShaderDatabase.MoteGlow, ColorFor(projectile), 0);
    }
}
