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
            if (!ParryUtility.TryParry(__instance))
            {
                return true;   // Nothing parried; let vanilla resolve the attack.
            }

            // Reported as a miss, so cooldown and stance handling stay vanilla's.
            GrantAttackerExperience(__instance);
            __result = false;
            return false;
        }

        /// <summary>Grants the attacker the melee XP vanilla would have, since the original is skipped.</summary>
        private static void GrantAttackerExperience(Verb_MeleeAttack verb)
        {
            var attacker = verb.CasterPawn;
            if (attacker?.skills == null)
            {
                return;
            }
            attacker.skills.Learn(
                SkillDefOf.Melee,
                200f * verb.verbProps.AdjustedFullCycleTime(verb, attacker));
        }
    }
}
