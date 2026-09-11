using System;
using System.Linq;
using RimWorld;
using UnityEngine;
using Verse;

namespace VanillaCombatOverhaul
{
    public static class WeaponScoreUtility
    {
        public static float Score(Thing weapon, Pawn pawn)
        {
            if (weapon?.def == null || pawn == null)
            {
                return 0f;
            }

            var rangedVerb = weapon.def.Verbs?.FirstOrDefault(v => v.isPrimary && v.LaunchesProjectile)
                             ?? weapon.def.Verbs?.FirstOrDefault(v => v.LaunchesProjectile);
            var ranged = rangedVerb != null;
            var raw = ranged ? RangedScore(weapon, rangedVerb) : MeleeScore(weapon);
            if (raw <= 0f)
            {
                return 0f;
            }

            var skill = pawn.skills?.GetSkill(ranged ? SkillDefOf.Shooting : SkillDefOf.Melee)?.Level ?? 0;
            var otherSkill = pawn.skills?.GetSkill(ranged ? SkillDefOf.Melee : SkillDefOf.Shooting)?.Level ?? 0;
            var skillMultiplier = SkillMultiplier(skill, otherSkill);
            var condition = weapon.def.useHitPoints
                ? 0.75f + 0.25f * Mathf.Clamp01(weapon.HitPoints / (float)Math.Max(weapon.MaxHitPoints, 1))
                : 1f;

            if (ranged && pawn.workSettings?.WorkIsActive(WorkTypeDefOf.Hunting) == true)
            {
                skillMultiplier *= 1.15f;
            }
            return raw * skillMultiplier * condition;
        }

        public static float RangedScore(Thing weapon, VerbProperties verb)
        {
            var projectile = verb?.defaultProjectile?.projectile;
            if (projectile == null)
            {
                return 0f;
            }
            var burst = Math.Max(verb.burstShotCount, 1);
            var damage = projectile.GetDamageAmount(weapon);
            var penetration = Mathf.Max(0f, projectile.GetArmorPenetration(weapon));
            var cycle = Mathf.Max(0.1f,
                verb.warmupTime + (burst - 1) * verb.ticksBetweenBurstShots / 60f
                + weapon.GetStatValue(StatDefOf.RangedWeapon_Cooldown));
            var accuracy = 0.2f * verb.accuracyShort
                         + 0.45f * verb.accuracyMedium
                         + 0.35f * verb.accuracyLong;
            return RangedScore(damage, burst, cycle, accuracy, penetration);
        }

        public static float MeleeScore(Thing weapon)
        {
            var dps = weapon.GetStatValue(StatDefOf.MeleeWeapon_AverageDPS);
            var penetrationDef = DefDatabase<StatDef>.GetNamedSilentFail("MeleeWeapon_AverageArmorPenetration");
            var penetration = penetrationDef == null ? 0f : weapon.GetStatValue(penetrationDef);
            return dps * (1f + Mathf.Max(0f, penetration));
        }

        public static float RangedScore(float damage, int burst, float cycleSeconds,
            float accuracy, float penetration)
        {
            return Mathf.Max(0f, damage) * Mathf.Max(1, burst) / Mathf.Max(0.1f, cycleSeconds)
                   * Mathf.Max(accuracy, 0.05f) * (1f + Mathf.Max(0f, penetration));
        }

        public static float SkillMultiplier(int relevantSkill, int otherSkill)
        {
            var aptitude = 0.75f + Mathf.Clamp(relevantSkill, 0, 20) / 40f;
            var preference = Mathf.Clamp(1f + (relevantSkill - otherSkill) * 0.02f, 0.8f, 1.2f);
            return aptitude * preference;
        }

        public static bool IsUpgrade(float currentScore, float candidateScore,
            float requiredRatio, bool currentAllowed)
        {
            var requiredScore = currentScore * Mathf.Max(requiredRatio, 1f);
            var comparisonTolerance = Mathf.Max(1f, Mathf.Abs(requiredScore)) * 0.000001f;
            return candidateScore > 0f
                   && (!currentAllowed || currentScore <= 0f
                       || candidateScore >= requiredScore - comparisonTolerance);
        }
    }
}
