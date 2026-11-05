using RimWorld;
using Verse;

namespace VanillaCombatOverhaul
{
    /// <summary>Lowers an apparel piece's armor rating once it drops below half its hit points, to half armor at 0.</summary>
    public class StatPart_ArmorDurability : StatPart
    {
        private const float FullArmorHitPoints = 0.5f;
        private const float ArmorAtZeroHitPoints = 0.5f;

        public override void TransformValue(StatRequest req, ref float val)
        {
            val *= Factor(req);
        }

        public override string ExplanationPart(StatRequest req)
        {
            var factor = Factor(req);
            if (factor >= 1f)
            {
                return null;
            }
            return "VCO_StatPart_ArmorWear".Translate(req.Thing.HitPoints, req.Thing.MaxHitPoints) + ": x" + factor.ToStringPercent();
        }

        internal static float Factor(StatRequest req)
        {
            var settings = VCOMod.Settings;
            if (settings == null || !settings.enableArmorDurability || !(req.Thing is Apparel apparel) || !apparel.def.useHitPoints || apparel.MaxHitPoints <= 0)
            {
                return 1f;
            }
            var health = (float)apparel.HitPoints / apparel.MaxHitPoints;
            if (health >= FullArmorHitPoints)
            {
                return 1f;
            }
            return ArmorAtZeroHitPoints + (1f - ArmorAtZeroHitPoints) * (health / FullArmorHitPoints);
        }
    }
}
