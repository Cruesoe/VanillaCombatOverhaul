using HarmonyLib;
using RimWorld;
using Verse;

namespace VanillaCombatOverhaul
{
    /// <summary>
    /// Resolves parrying ahead of vanilla's hit roll.
    ///
    /// Vanilla Combat Reloaded spliced this in with a transpiler keyed on a local variable
    /// index, because it wanted to parry only attacks that would otherwise have landed. That
    /// buys very little -- a parried attack and a missed attack both deal no damage -- and it
    /// costs a patch that silently stops working whenever Ludeon touches the method.
    ///
    /// Resolving first instead makes a plain prefix sufficient. The model a player sees is
    /// "you parry some attacks; the rest resolve normally", and total mitigation stacks
    /// multiplicatively with vanilla's existing miss and dodge chances.
    /// </summary>
    [HarmonyPatch(typeof(Verb_MeleeAttack), "TryCastShot")]
    public static class Patch_Verb_MeleeAttack_TryCastShot
    {
        public static bool Prefix(Verb_MeleeAttack __instance, ref bool __result)
        {
            if (!ParryUtility.TryParry(__instance))
            {
                return true;   // Nothing parried; let vanilla resolve the attack.
            }

            // The attack happened and did nothing, which is exactly what vanilla's own miss
            // path reports, so the caller's cooldown and stance handling stay correct.
            GrantAttackerExperience(__instance);
            __result = false;
            return false;
        }

        /// <summary>
        /// Skipping the original also skips the melee XP it would have granted the attacker.
        /// Swinging at someone is practice whether or not it connects, so it is granted here
        /// on the same terms vanilla uses.
        /// </summary>
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
