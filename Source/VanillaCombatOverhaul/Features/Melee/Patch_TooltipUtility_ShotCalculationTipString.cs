using System.Linq;
using System.Text;
using HarmonyLib;
using RimWorld;
using UnityEngine;
using Verse;

namespace VanillaCombatOverhaul
{
    [HarmonyPatch(typeof(TooltipUtility), nameof(TooltipUtility.ShotCalculationTipString))]
    public static class Patch_TooltipUtility_ShotCalculationTipString
    {
        private delegate float GetNonMissChance(Verb_MeleeAttack verb, LocalTargetInfo target);
        private delegate float GetDodgeChance(Verb_MeleeAttack verb, LocalTargetInfo target);

        private static readonly GetNonMissChance NonMiss =
            AccessTools.MethodDelegate<GetNonMissChance>(
                AccessTools.Method(typeof(Verb_MeleeAttack), "GetNonMissChance"));

        private static readonly GetDodgeChance Dodge =
            AccessTools.MethodDelegate<GetDodgeChance>(
                AccessTools.Method(typeof(Verb_MeleeAttack), "GetDodgeChance"));

        public static void Postfix(ref string __result, Thing target)
        {
            if (!(Find.Selector.SingleSelectedThing is Pawn pawn) || pawn.equipment == null || target == null)
            {
                return;
            }

            var meleeVerb = pawn.equipment.PrimaryEq?.AllVerbs.OfType<Verb_MeleeAttack>().FirstOrDefault();
            if (meleeVerb == null)
            {
                return;
            }

            var settings = VCOMod.Settings;
            var sb = new StringBuilder(__result);
            var nonMiss = NonMiss(meleeVerb, target);
            var dodge = Dodge(meleeVerb, target);

            var vector = (target.Position.ToVector3() - pawn.Position.ToVector3()).Yto0();
            var angle = vector != Vector3.zero ? Quaternion.LookRotation(vector).eulerAngles.y : 0f;
            var facing = FacingUtility.FromTravelAngle(angle, target);

            var parry = 0f;
            if (settings != null && settings.enableParry && target is Pawn defender)
            {
                var factor = facing == AttackFacing.Rear ? 0f
                    : facing == AttackFacing.Front ? settings.parryFrontFactor
                    : settings.parrySideFactor;
                if (defender.equipment?.Primary != null)
                {
                    parry = ParryUtility.ParryChanceAgainst(defender, pawn, factor);
                }
            }

            var hit = nonMiss * (1f - dodge) * (1f - parry);
            sb.AppendLine("VCO_MeleeHitTotal".Translate(hit.ToStringPercent()));
            sb.AppendLine("   " + "VCO_MeleeHitChance".Translate(nonMiss.ToStringPercent()));
            if (target is Pawn)
            {
                sb.AppendLine("   " + "VCO_TargetDodgeChance".Translate(dodge.ToStringPercent()));
            }
            if (parry > 0f)
            {
                sb.AppendLine("   " + "VCO_TargetParryChance".Translate(parry.ToStringPercent()));
            }

            if (settings != null && (settings.enableMeleeFlanking || settings.enableHeightTargeting)
                && target is Pawn hitPawn)
            {
                var side = DirectionalHitUtility.GroupFor(facing);
                if (side != null)
                {
                    sb.AppendLine("   " + "VCO_TargetSide".Translate() + ": " + side.labelShort);
                }
                var height = HeightTargetingUtility.GetTargetHeight(pawn);
                if (height != BodyPartHeight.Undefined)
                {
                    var chance = HeightTargetingUtility.ChanceToLand(
                        pawn, hitPawn, side, height, meleeVerb.GetDamageDef(), melee: true);
                    sb.AppendLine("   " + "VCO_HeightChance".Translate()
                                  + HeightTargetingUtility.LabelFor(height) + ": " + chance.ToStringPercent());
                }
            }

            __result = sb.ToString();
        }
    }
}
