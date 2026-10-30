using HarmonyLib;
using RimWorld;
using UnityEngine;
using Verse;

namespace VanillaCombatOverhaul
{
    /// <summary>Tilts the body of pinned pawns that crawl while standing, matching the crawling head angle vanilla already applies.</summary>
    [HarmonyPatch(typeof(PawnRenderer), "GetDrawParms")]
    public static class Patch_PawnRenderer_GetDrawParms
    {
        public static void Postfix(Vector3 rootLoc, PawnRenderFlags flags, ref PawnDrawParms __result)
        {
            // Vanilla tilts the body only for non-standing postures, which pinned crawlers keep
            if (!__result.crawling || __result.posture != PawnPosture.Standing
                || flags.FlagSet(PawnRenderFlags.Portrait) || flags.FlagSet(PawnRenderFlags.Cache))
            {
                return;
            }
            var pawn = __result.pawn;
            if (!SuppressionUtility.IsCrawling(pawn))
            {
                return;
            }
            var angle = PawnRenderUtility.CrawlingBodyAngle(pawn.Rotation);
            __result.matrix = Matrix4x4.TRS(rootLoc + pawn.ageTracker.CurLifeStage.bodyDrawOffset, Quaternion.AngleAxis(angle, Vector3.up), Vector3.one);
        }
    }
}
