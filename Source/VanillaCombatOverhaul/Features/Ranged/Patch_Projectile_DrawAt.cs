using HarmonyLib;
using UnityEngine;
using Verse;

namespace VanillaCombatOverhaul
{
    [HarmonyPatch(typeof(Projectile), "DrawAt")]
    public static class Patch_Projectile_DrawAt
    {
        public static void Prefix(Projectile __instance, ref Vector3 drawLoc, out float __state) =>
            __state = MuzzleUtility.ShiftToMuzzle(__instance, ref drawLoc);

        public static void Postfix(Projectile __instance, Vector3 drawLoc, float __state) =>
            TracerUtility.Draw(__instance, drawLoc, __state);
    }
}
