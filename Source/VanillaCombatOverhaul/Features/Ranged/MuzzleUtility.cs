using UnityEngine;
using Verse;

namespace VanillaCombatOverhaul
{
    /// <summary>Draws projectiles along a path from the weapon's muzzle instead of the shooter's centre, where vanilla launches them.</summary>
    public static class MuzzleUtility
    {
        // Share of the weapon texture's length between its centre and the muzzle.
        private const float BarrelShare = 0.45f;

        /// <summary>Distance in cells from the launch origin to the muzzle, along the line of fire.</summary>
        public static float MuzzleDistance(Projectile projectile)
        {
            var equipment = projectile.EquipmentDef;
            if (equipment == null || projectile.def.projectile.arcHeightFactor > 0f)
            {
                return 0f;
            }
            if (projectile.Launcher is Pawn pawn && pawn.equipment?.Primary?.def == equipment)
            {
                var drawFactor = pawn.ageTracker?.CurLifeStage?.equipmentDrawDistanceFactor ?? 1f;
                var length = equipment.graphicData?.drawSize.x ?? 1f;
                return (0.4f + equipment.equippedDistanceOffset) * drawFactor + length * BarrelShare;
            }
            // Manned turrets record the turret building as equipment; unmanned ones are the launcher.
            var turret = equipment.building?.turretGunDef != null ? equipment : projectile.Launcher?.def;
            if (turret?.building?.turretGunDef != null)
            {
                return turret.building.turretTopDrawSize * BarrelShare;
            }
            return 0f;
        }

        /// <summary>Moves drawLoc onto the muzzle-to-destination path; returns how far the projectile has travelled from the muzzle.</summary>
        public static float ShiftToMuzzle(Projectile projectile, ref Vector3 drawLoc)
        {
            var origin = ProjectileAccess.Origin(projectile);
            var travel = (ProjectileAccess.Destination(projectile) - origin).Yto0();
            var total = travel.magnitude;
            var covered = (drawLoc - origin).Yto0().magnitude;
            if (total < 0.01f)
            {
                return covered;
            }
            var muzzle = Mathf.Min(MuzzleDistance(projectile), total * 0.5f);
            var fraction = Mathf.Clamp01(covered / total);
            drawLoc += travel / total * (muzzle * (1f - fraction));
            return fraction * (total - muzzle);
        }
    }
}
