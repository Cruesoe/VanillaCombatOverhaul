using HarmonyLib;
using UnityEngine;
using Verse;

namespace VanillaCombatOverhaul
{
    [HarmonyPatch(typeof(Projectile), "DrawAt")]
    public static class Patch_Projectile_DrawAt
    {
        public static void Postfix(Projectile __instance, Vector3 drawLoc) =>
            TracerUtility.Draw(__instance, drawLoc);
    }
}
