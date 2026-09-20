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
        // Built once at startup; see VCODiagnostics.KeyTable. Concatenating at the call site
        // costs a reflective Enum.ToString on every eligible melee attack, even when
        // counting is switched off.
        private static readonly string[] FacingKeys =
            VCODiagnostics.KeyTable<AttackFacing>("parry.facing.");

        // Private members of Verb_MeleeAttack we need to respect. Resolved once; if RimWorld
        // renames either, Harmony's AccessTools throws at startup rather than silently
        // returning defaults, which is the failure mode we want.
        private static readonly AccessTools.FieldRef<Verb_MeleeAttack, bool> SurpriseAttackRef =
            AccessTools.FieldRefAccess<Verb_MeleeAttack, bool>("surpriseAttack");

        private delegate bool IsTargetImmobileDel(Verb_MeleeAttack verb, LocalTargetInfo target);

        private static readonly IsTargetImmobileDel IsTargetImmobile =
            AccessTools.MethodDelegate<IsTargetImmobileDel>(
                AccessTools.Method(typeof(Verb_MeleeAttack), "IsTargetImmobile"));

        // A counter-attack is itself a melee attack, so without this it can be parried,
        // triggering another counter, indefinitely. Counters never chain.
        [ThreadStatic] private static bool resolvingCounter;

        /// <summary>True while a counter-attack is being made, so other features can leave it alone.</summary>
        internal static bool ResolvingCounter => resolvingCounter;

        /// <summary>
        /// Decides whether the defender parries this attack, and applies the effects if so.
        /// Called before vanilla resolves hit/miss, so parry is a first line of defence and
        /// anything it does not stop still has to get past the normal miss and dodge rolls.
        /// </summary>
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

            // From here on the attack is eligible, so every exit is worth counting: knowing
            // which gate rejects most parries is the whole point of the instrumentation.
            VCODiagnostics.CountFor(defender, "parry.attempt");

            if (!defender.Spawned || defender.Dead || defender.Downed)
            {
                VCODiagnostics.CountFor(defender, "parry.reject.downedOrDead");
                return false;
            }

            // An attack the defender never saw cannot be answered.
            if (SurpriseAttackRef(verb) || IsTargetImmobile(verb, verb.CurrentTarget))
            {
                VCODiagnostics.CountFor(defender, "parry.reject.surpriseOrImmobile");
                return false;
            }

            // Something has to be in hand to parry with. The stat enforces this too, but
            // checking here avoids the stat lookup on the common unarmed case.
            if (defender.equipment?.Primary == null)
            {
                VCODiagnostics.CountFor(defender, "parry.reject.noWeapon");
                return false;
            }

            // A pawn mid-way through a ranged attack is not in a stance to deflect anything.
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
                // If this one climbs, the budget is binding more than intended.
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
                TryCounterAttack(attacker, defender);
            }

            return true;
        }

        /// <summary>
        /// Contested parry chance: <c>aptitude ^ ((1/d) / (1 - attackerMelee))</c>.
        ///
        /// This is Vanilla Combat Reloaded's curve, kept deliberately. It is the only
        /// field-tested shape available, and it captures something a defender-only stat
        /// cannot: a skilled attacker beats a parry. At stock values a skill-20 defender turns
        /// about 87% of attacks from an unskilled attacker and about 50% from another
        /// skill-20 fighter.
        ///
        /// The defender's side lives in the VCO_ParryChance stat so it stays inspectable and
        /// moddable; only the contest against the attacker happens here, because it is
        /// situational and has no place in a per-pawn stat.
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

        /// <summary>
        /// Front and side default to the same value, matching Vanilla Combat Reloaded, so out
        /// of the box only getting behind someone denies a parry outright. Lowering the side
        /// value is the knob for making a flank meaningful on its own; note that directional
        /// damage already makes flanking matter through hit location.
        /// </summary>
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

            // Without this the player gets no explanation for an attack that did nothing.
            verb.CreateCombatLog(m => m.combatLogRulesDeflect, alwaysShow: false);

            // Reading an attack well enough to turn it is how you get better at melee.
            defender.skills?.Learn(SkillDefOf.Melee, 35f);
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
