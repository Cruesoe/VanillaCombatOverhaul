using HarmonyLib;
using RimWorld;
using Verse;

namespace VanillaCombatOverhaul
{
    /// <summary>Pinned pawns that move use vanilla's crawling pose and gait.</summary>
    [HarmonyPatch(typeof(Pawn), nameof(Pawn.Crawling), MethodType.Getter)]
    public static class Patch_Pawn_Crawling
    {
        public static void Postfix(Pawn __instance, ref bool __result)
        {
            if (!__result && SuppressionUtility.IsCrawling(__instance))
            {
                __result = true;
            }
        }
    }

    /// <summary>Slower movement while crawling under fire.</summary>
    public class StatPart_SuppressionCrawl : StatPart
    {
        public override void TransformValue(StatRequest req, ref float val)
        {
            if (req.Thing is Pawn pawn && SuppressionUtility.CrawlsWhenMoving(pawn))
            {
                val *= SuppressionUtility.CrawlSpeedFactor;
            }
        }

        public override string ExplanationPart(StatRequest req)
        {
            if (!(req.Thing is Pawn pawn) || !SuppressionUtility.CrawlsWhenMoving(pawn))
            {
                return null;
            }
            return "VCO_StatPart_Crawling".Translate() + ": x" + SuppressionUtility.CrawlSpeedFactor.ToStringPercent();
        }
    }
}
