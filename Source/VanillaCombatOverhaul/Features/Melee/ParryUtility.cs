using System;
using HarmonyLib;
using RimWorld;
using UnityEngine;
using Verse;
using Verse.AI;
using Verse.Sound;

namespace VanillaCombatOverhaul
{
    public static class ParryUtility
    {
        // Built once; see VCODiagnostics.KeyTable.
        private static readonly string[] FacingKeys =
            VCODiagnostics.KeyTable<AttackFacing>("parry.facing.");

        // Private Verb_MeleeAttack members; a rename throws at startup.
        private static readonly AccessTools.FieldRef<Verb_MeleeAttack, bool> SurpriseAttackRef =
            AccessTools.FieldRefAccess<Verb_MeleeAttack, bool>("surpriseAttack");

        private delegate bool IsTargetImmobileDel(Verb_MeleeAttack verb, LocalTargetInfo target);

        private static readonly IsTargetImmobileDel IsTargetImmobile =
            AccessTools.MethodDelegate<IsTargetImmobileDel>(
                AccessTools.Method(typeof(Verb_MeleeAttack), "IsTargetImmobile"));

        // Set during a counter-attack so it cannot be parried into another counter.
        [ThreadStatic] private static bool resolvingCounter;

        /// <summary>True while a counter-attack is being made, so other features can leave it alone.</summary>
        internal static bool ResolvingCounter => resolvingCounter;

        /// <summary>Rolls a parry before vanilla's hit and dodge rolls, applying its effects if it succeeds.</summary>
        public static bool TryParry(Verb_MeleeAttack verb)
        {
            var settings = VCOMod.Settings;
            if (settings == null || !settings.enableParry || resolvingCounter)
            {
                return false;
            }

            var attacker = verb.CasterPawn;
            if (attacker == null || !(verb.CurrentTarget.Thing is Pawn defender))
            {
                return false;   // Not pawn-vs-pawn; not an eligible attack, so not counted.
            }

            // Eligible from here; each rejection is counted.
            VCODiagnostics.CountFor(defender, "parry.attempt");

            if (!defender.Spawned || defender.Dead || defender.Downed)
            {
                VCODiagnostics.CountFor(defender, "parry.reject.downedOrDead");
                return false;
            }

            // Surprise attacks and immobile targets cannot parry.
            if (SurpriseAttackRef(verb) || IsTargetImmobile(verb, verb.CurrentTarget))
            {
                VCODiagnostics.CountFor(defender, "parry.reject.surpriseOrImmobile");
                return false;
            }

            // Needs a weapon in hand (also enforced by the stat; checked first to skip the lookup).
            if (defender.equipment?.Primary == null)
            {
                VCODiagnostics.CountFor(defender, "parry.reject.noWeapon");
                return false;
            }

            // No parry while busy with a ranged attack.
            if (defender.stances?.curStance is Stance_Busy busy
                && busy.verb != null
                && !busy.verb.verbProps.IsMeleeAttack)
            {
                VCODiagnostics.CountFor(defender, "parry.reject.busyRanged");
                return false;
            }

            var facing = FacingUtility.Relative(attacker.Position, defender);
            VCODiagnostics.CountFor(defender, FacingKeys[(int)facing]);
            var facingFactor = FacingFactor(facing, settings);
            if (facingFactor <= 0f)
            {
                VCODiagnostics.CountFor(defender, "parry.reject.fromBehind");
                return false;
            }

            var tracker = Current.Game?.GetComponent<ParryTracker>();
            if (tracker != null && !tracker.CanParry(defender))
            {
                VCODiagnostics.CountFor(defender, "parry.reject.budgetSpent");
                return false;
            }

            var chance = ParryChanceAgainst(defender, attacker, facingFactor);
            VCODiagnostics.SampleFor(defender, "parry.chanceRolled", chance);
            VCODiagnostics.ProbeParryChance(defender, attacker, facingFactor);

            if (!Rand.Chance(chance))
            {
                VCODiagnostics.CountFor(defender, "parry.reject.rollFailed");
                return false;
            }

            VCODiagnostics.CountFor(defender, "parry.success");
            tracker?.RecordParry(defender);
            ApplyParryEffects(verb, attacker, defender);

            if (settings.enableCounterAttack)
            {
                pendingCounterVerb = verb;
                pendingCounterAttacker = attacker;
                pendingCounterDefender = defender;
            }

            return true;
        }

        /// <summary>
        /// Contested parry chance, Vanilla Combat Reloaded's curve: <c>aptitude ^ ((1/d) / (1 - attackerMelee))</c>,
        /// with aptitude from the VCO_ParryChance stat and d the facing factor.
        /// </summary>
        public static float ParryChanceAgainst(Pawn defender, Pawn attacker, float directionFactor)
        {
            if (directionFactor <= 0f)
            {
                return 0f;
            }

            var aptitude = Mathf.Min(defender.GetStatValue(VCO_StatDefOf.VCO_ParryChance), 0.999f);
            if (aptitude <= 0f)
            {
                return 0f;
            }

            var attackerMelee = attacker.GetStatValue(StatDefOf.MeleeHitChance)
                                + StatPart_ParryDarkness.Offset(attacker);
            attackerMelee = Mathf.Clamp(attackerMelee, 0f, 0.999f);

            var exponent = (1f / directionFactor) / (1f - attackerMelee);
            return Mathf.Clamp01(Mathf.Pow(aptitude, exponent));
        }

        /// <summary>Facing factor from the settings; attacks from behind cannot be parried.</summary>
        private static float FacingFactor(AttackFacing facing, VCOSettings settings)
        {
            switch (facing)
            {
                case AttackFacing.Front:
                    return settings.parryFrontFactor;
                case AttackFacing.Left:
                case AttackFacing.Right:
                    return settings.parrySideFactor;
                default:
                    return 0f;   // Nothing is parried from behind.
            }
        }

        private static void ApplyParryEffects(Verb_MeleeAttack verb, Pawn attacker, Pawn defender)
        {
            var weapon = defender.equipment?.Primary;

            var effecter = weapon?.Stuff?.stuffProps?.categories?.Contains(StuffCategoryDefOf.Metallic) == true
                ? EffecterDefOf.Deflect_Metal
                : EffecterDefOf.Deflect_General;

            var health = defender.health;
            if (health.deflectionEffecter == null || health.deflectionEffecter.def != effecter)
            {
                health.deflectionEffecter?.Cleanup();
                health.deflectionEffecter = effecter.Spawn();
            }
            health.deflectionEffecter.Trigger(defender, attacker);

            defender.Drawer.Notify_MeleeAttackOn(attacker);
            MoteMaker.ThrowText(defender.DrawPos, defender.Map, "VCO_Mote_Parried".Translate(), 1.9f);
            ParrySound(verb, defender)?.PlayOneShot(new TargetInfo(defender.Position, defender.Map));

            // Combat log entry for the deflected attack.
            verb.CreateCombatLog(m => m.combatLogRulesDeflect, alwaysShow: false);

            // Melee XP for the defender.
            defender.skills?.Learn(SkillDefOf.Melee, 35f);
        }

        // The counter runs once the parried swing has finished: a counter that kills the attacker
        // mid-swing leaves vanilla finishing a cast for a pawn with no stance tracker.
        private static Verb pendingCounterVerb;
        private static Pawn pendingCounterAttacker;
        private static Pawn pendingCounterDefender;

        /// <summary>Makes the counter-attack queued by a parry of this verb's swing.</summary>
        internal static void ResolvePendingCounter(Verb verb)
        {
            if (pendingCounterVerb == null || pendingCounterVerb != verb)
            {
                return;
            }
            var attacker = pendingCounterAttacker;
            var defender = pendingCounterDefender;
            pendingCounterVerb = null;
            pendingCounterAttacker = null;
            pendingCounterDefender = null;
            if (defender.Spawned && !defender.Dead && !defender.Downed)
            {
                TryCounterAttack(attacker, defender);
            }
        }

        private static void TryCounterAttack(Pawn attacker, Pawn defender)
        {
            if (!attacker.Spawned || attacker.Dead || defender.meleeVerbs == null)
            {
                return;
            }
            if (!defender.CanReachImmediate(attacker, PathEndMode.Touch))
            {
                return;
            }

            resolvingCounter = true;
            try
            {
                VCODiagnostics.CountFor(defender, "parry.counterAttack");
                defender.meleeVerbs.TryMeleeAttack(attacker);
            }
            finally
            {
                resolvingCounter = false;
            }
        }

        private static SoundDef ParrySound(Verb verb, Pawn defender)
        {
            var weapon = defender.equipment?.Primary;
            if (weapon != null && !weapon.def.meleeHitSound.NullOrUndefined())
            {
                return weapon.def.meleeHitSound;
            }

            var stuff = weapon?.Stuff?.stuffProps;
            if (stuff != null)
            {
                var sharp = verb.verbProps?.meleeDamageDef?.armorCategory == DamageArmorCategoryDefOf.Sharp;
                var sound = sharp ? stuff.soundMeleeHitSharp : stuff.soundMeleeHitBlunt;
                if (!sound.NullOrUndefined())
                {
                    return sound;
                }
            }

            return SoundDefOf.Pawn_Melee_Punch_HitBuilding_Generic;
        }
    }
}
