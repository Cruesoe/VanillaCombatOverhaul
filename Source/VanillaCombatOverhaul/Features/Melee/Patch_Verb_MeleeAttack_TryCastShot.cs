using HarmonyLib;
using RimWorld;
using Verse;

namespace VanillaCombatOverhaul
{
    /// <summary>Rolls the parry before vanilla's hit roll; a parried attack ends as a miss.</summary>
    [HarmonyPatch(typeof(Verb_MeleeAttack), "TryCastShot")]
    public static class Patch_Verb_MeleeAttack_TryCastShot
    {
        public static bool Prefix(Verb_MeleeAttack __instance, ref bool __result)
        {
            // Vanilla makes no attack at all here, so there is nothing to parry.
            var attacker = __instance.CasterPawn;
            if (attacker == null || !attacker.Spawned || attacker.stances.FullBodyBusy)
            {
                return true;
            }
            if (!ParryUtility.TryParry(__instance))
            {
                return true;   // Nothing parried; let vanilla resolve the attack.
            }

            // Reported as a miss, so cooldown and stance handling stay vanilla's.
            ApplyAttackBookkeeping(__instance, attacker);
            __result = false;
            return false;
        }

        /// <summary>What vanilla's TryCastShot does for any attack it makes, hit or miss: XP, melee threat, facing, lunge and call.</summary>
        private static void ApplyAttackBookkeeping(Verb_MeleeAttack verb, Pawn attacker)
        {
            var target = verb.CurrentTarget.Thing;
            attacker.rotationTracker.Face(target.DrawPos);
            var defender = target as Pawn;
            if (attacker.skills != null && defender?.IsColonyMech != true)
            {
                attacker.skills.Learn(SkillDefOf.Melee, 200f * verb.verbProps.AdjustedFullCycleTime(verb, attacker));
            }
            if (defender != null && !defender.Dead
                && (attacker.MentalStateDef != MentalStateDefOf.SocialFighting
                    || defender.MentalStateDef != MentalStateDefOf.SocialFighting)
                && (attacker.story == null || !attacker.story.traits.DisableHostilityFrom(defender)))
            {
                defender.mindState.meleeThreat = attacker;
                defender.mindState.lastMeleeThreatHarmTick = Find.TickManager.TicksGame;
            }
            attacker.Drawer.Notify_MeleeAttackOn(target);
            attacker.rotationTracker.FaceCell(target.Position);
            attacker.caller?.Notify_DidMeleeAttack();
        }
    }
}
