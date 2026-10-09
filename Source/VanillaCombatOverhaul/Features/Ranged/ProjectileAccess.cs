using HarmonyLib;
using UnityEngine;
using Verse;

namespace VanillaCombatOverhaul
{
    /// <summary>Field references to Projectile's protected flight path; a renamed field throws at startup.</summary>
    internal static class ProjectileAccess
    {
        public static readonly AccessTools.FieldRef<Projectile, Vector3> Origin =
            AccessTools.FieldRefAccess<Projectile, Vector3>("origin");

        public static readonly AccessTools.FieldRef<Projectile, Vector3> Destination =
            AccessTools.FieldRefAccess<Projectile, Vector3>("destination");
    }
}
