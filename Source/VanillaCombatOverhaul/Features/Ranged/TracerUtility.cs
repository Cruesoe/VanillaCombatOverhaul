using HarmonyLib;
using RimWorld;
using UnityEngine;
using Verse;

namespace VanillaCombatOverhaul
{
    /// <summary>
    /// Short glowing streak behind in-flight projectiles. Vibrant Tracers for Combat Extended
    /// does this by swapping CE ammo graphics to <c>TransparentPostLight</c> textures keyed on
    /// calibre; vanilla has no ammo types, so a Harmony draw of a MoteGlow quad along the
    /// projectile's path is the equivalent that works for every gun and bow.
    /// </summary>
    public static class TracerUtility
    {
        private static readonly AccessTools.FieldRef<Projectile, Vector3> Origin =
            AccessTools.FieldRefAccess<Projectile, Vector3>("origin");

        private static readonly AccessTools.FieldRef<Projectile, Vector3> Destination =
            AccessTools.FieldRefAccess<Projectile, Vector3>("destination");

        public static void Draw(Projectile projectile, Vector3 drawLoc)
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

            var travel = Destination(projectile) - Origin(projectile);
            if (travel.sqrMagnitude < 0.0001f)
            {
                return;
            }

            var length = Mathf.Max(settings.tracerLength, 0.4f);
            var width = Mathf.Clamp(settings.tracerWidth, 0.04f, 0.6f);
            var dir = travel.normalized;
            var tail = drawLoc - dir * length;
            tail.y = drawLoc.y;
            var mid = (drawLoc + tail) * 0.5f;
            var scale = new Vector3(width, 1f, length);
            var rotation = Quaternion.LookRotation(dir);

            Graphics.DrawMesh(MeshPool.plane10, Matrix4x4.TRS(mid, rotation, scale),
                              MatFor(projectile), 0);

            var headScale = new Vector3(width * 1.6f, 1f, width * 1.6f);
            Graphics.DrawMesh(MeshPool.plane10, Matrix4x4.TRS(drawLoc, rotation, headScale),
                              MatFor(projectile), 0);
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

    [HarmonyPatch(typeof(Projectile), "DrawAt")]
    public static class Patch_Projectile_DrawAt
    {
        public static void Postfix(Projectile __instance, Vector3 drawLoc) =>
            TracerUtility.Draw(__instance, drawLoc);
    }
}
