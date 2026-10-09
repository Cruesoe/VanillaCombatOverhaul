using System.Collections.Generic;
using RimWorld;
using Verse;
using Verse.AI;
using Verse.AI.Group;

namespace VanillaCombatOverhaul
{
    /// <summary>
    /// Point-blank shooting: a skilled shooter in melee sometimes fires the gun in hand instead of
    /// swinging it. The shot is the weapon's own verb with no warmup, started from the melee verb's
    /// TryStartCastOn when called by Pawn_MeleeVerbs.TryMeleeAttack.
    /// </summary>
    public static class PointBlankUtility
    {
        /// <summary>Chance per eligible attack, indexed by Shooting level.</summary>
        private static readonly float[] ChanceBySkill =
        {
            0f, 0f, 0f, 0f, 0f, 0f, 0f, 0f, 0f, 0f,
            0.05f, 0.06f, 0.08f, 0.10f, 0.13f, 0.17f, 0.22f, 0.28f, 0.35f, 0.43f, 0.50f
        };

        public const int MinimumSkill = 10;

        /// <summary>Test-only: lets the arena's ordered (player-forced) attacks roll.</summary>
        public static bool IgnorePlayerForcedForTesting;

        // Pawn and target of the TryMeleeAttack call in progress.
        private static Pawn meleeAttacker;
        private static Thing meleeTarget;

        // Ranged verb being started as a point-blank shot.
        private static Verb startingVerb;

        // Point-blank verbs partway through a burst, so later shots also skip the melee lock. Not saved.
        private static readonly List<Verb> burstingVerbs = new List<Verb>();

        public static bool Enabled => VCOMod.Settings?.enablePointBlank ?? false;

        public static float ChanceFor(int shootingLevel)
        {
            if (shootingLevel < 0)
            {
                return 0f;
            }
            return ChanceBySkill[shootingLevel < ChanceBySkill.Length ? shootingLevel : ChanceBySkill.Length - 1];
        }

        /// <summary>Shooting level including aptitudes, or -1 when Shooting is disabled.</summary>
        public static int ShootingLevel(Pawn pawn)
        {
            var skill = pawn.skills?.GetSkill(SkillDefOf.Shooting);
            if (skill == null || skill.TotallyDisabled)
            {
                return -1;
            }
            return skill.Level;
        }

        /// <summary>Marks a TryMeleeAttack call; dispose restores the previous call's values, as melee attacks can nest.</summary>
        public static MeleeAttackScope BeginMeleeAttack(Pawn pawn, Thing target)
        {
            var scope = new MeleeAttackScope(meleeAttacker, meleeTarget);
            meleeAttacker = pawn;
            meleeTarget = target;
            return scope;
        }

        public readonly struct MeleeAttackScope
        {
            private readonly Pawn previousPawn;
            private readonly Thing previousTarget;
            private readonly bool active;

            internal MeleeAttackScope(Pawn previousPawn, Thing previousTarget)
            {
                this.previousPawn = previousPawn;
                this.previousTarget = previousTarget;
                active = true;
            }

            public void Dispose()
            {
                if (active)
                {
                    meleeAttacker = previousPawn;
                    meleeTarget = previousTarget;
                }
            }
        }

        /// <summary>Called as a melee verb starts its cast; true when a point-blank shot was fired instead.</summary>
        public static bool TryReplace(Verb meleeVerb, LocalTargetInfo castTarg)
        {
            var pawn = meleeVerb.CasterPawn;
            if (pawn == null || pawn != meleeAttacker || castTarg.Thing != meleeTarget)
            {
                return false;   // Not the attack TryMeleeAttack is making.
            }

            // Only pawns holding a ranged weapon count as opportunities.
            var ranged = pawn.equipment?.PrimaryEq?.PrimaryVerb;
            if (ranged == null || ranged.verbProps.IsMeleeAttack)
            {
                return false;
            }

            VCODiagnostics.CountFor(pawn, "pointblank.opportunity");

            if (!pawn.RaceProps.Humanlike)
            {
                VCODiagnostics.CountFor(pawn, "pointblank.reject.notHumanlike");
                return false;
            }

            if (!(castTarg.Thing is Pawn target) || target.Downed)
            {
                // Pawns only, and never a downed one.
                VCODiagnostics.CountFor(pawn, "pointblank.reject.target");
                return false;
            }

            var excluded = ExcludedContext(pawn);
            if (excluded != null)
            {
                VCODiagnostics.CountFor(pawn, "pointblank.reject.context");
                VCODiagnostics.CountFor(pawn, excluded);
                return false;
            }

            var level = ShootingLevel(pawn);
            if (level < MinimumSkill)
            {
                VCODiagnostics.CountFor(pawn, "pointblank.reject.skill");
                return false;
            }

            var previousVerb = startingVerb;
            startingVerb = ranged;
            try
            {
                // Checked as a point-blank cast: the melee lock is lifted, fuel, charges and roles still apply.
                if (!ranged.IsStillUsableBy(pawn))
                {
                    VCODiagnostics.CountFor(pawn, "pointblank.reject.weapon");
                    return false;
                }

                var chance = ChanceFor(level);
                VCODiagnostics.CountFor(pawn, "pointblank.roll");
                VCODiagnostics.SampleFor(pawn, "pointblank.chanceRolled", chance);
                if (IsPlayerMeleeOrder(pawn))
                {
                    // Only reachable with IgnorePlayerForcedForTesting set.
                    VCODiagnostics.CountFor(pawn, "pointblank.roll.onPlayerOrder");
                }
                if (!Rand.Chance(chance))
                {
                    VCODiagnostics.CountFor(pawn, "pointblank.reject.rollFailed");
                    return false;
                }

                return TryFire(pawn, meleeVerb, ranged, castTarg);
            }
            finally
            {
                startingVerb = previousVerb;
            }
        }

        /// <summary>Counter key when the attack stays melee (parry counter, social fight, duel, player melee order), else null.</summary>
        private static string ExcludedContext(Pawn pawn)
        {
            if (ParryUtility.ResolvingCounter)
            {
                return "pointblank.reject.context.counter";
            }
            if (pawn.MentalStateDef == MentalStateDefOf.SocialFighting)
            {
                return "pointblank.reject.context.socialFight";
            }
            if (pawn.GetLord()?.LordJob is LordJob_Ritual_Duel)
            {
                return "pointblank.reject.context.duel";
            }
            if (IsPlayerMeleeOrder(pawn) && !IgnorePlayerForcedForTesting)
            {
                return "pointblank.reject.context.playerOrder";
            }
            return null;
        }

        /// <summary>An explicit melee attack order; a drafted pawn's automatic melee runs under Wait_Combat instead.</summary>
        private static bool IsPlayerMeleeOrder(Pawn pawn)
        {
            var job = pawn.CurJob;
            return job != null && job.playerForced && job.def == JobDefOf.AttackMelee;
        }

        /// <summary>True while this verb is starting or bursting a point-blank shot.</summary>
        public static bool IsPointBlankCast(Verb verb)
        {
            if (verb == startingVerb)
            {
                return verb != null;
            }
            if (burstingVerbs.Count == 0)
            {
                return false;
            }
            var index = burstingVerbs.IndexOf(verb);
            if (index < 0)
            {
                return false;
            }
            if (verb.Bursting)
            {
                return true;
            }
            burstingVerbs.RemoveAt(index);
            return false;
        }

        /// <summary>Stops tracking a verb once its burst is over.</summary>
        public static void NotifyBurstProgress(Verb verb)
        {
            if (burstingVerbs.Count > 0 && !verb.Bursting)
            {
                burstingVerbs.Remove(verb);
            }
        }

        private static bool TryFire(Pawn pawn, Verb meleeVerb, Verb ranged, LocalTargetInfo target)
        {
            var now = Find.TickManager.TicksGame;
            var mind = pawn.mindState;
            var attackedBefore = mind?.lastAttackTargetTick ?? now;

            // A refusal leaves the verb untouched, so the melee attack goes ahead.
            if (!ranged.TryStartCastOn(target))
            {
                VCODiagnostics.CountFor(pawn, "pointblank.reject.cannotStart");
                return false;
            }

            // lastAttackTargetTick is set only when a shot actually went off.
            var fired = mind != null && attackedBefore != now && mind.lastAttackTargetTick == now;
            if (!fired)
            {
                // The first shot failed: the melee attack runs and its cooldown replaces the ranged one.
                VCODiagnostics.CountFor(pawn, "pointblank.reject.notFired");
                return false;
            }

            if (ranged.Bursting && !burstingVerbs.Contains(ranged))
            {
                burstingVerbs.Add(ranged);
            }

            VCODiagnostics.CountFor(pawn, "pointblank.success");
            // Shot cooldown against the replaced swing's cooldown.
            VCODiagnostics.SampleFor(pawn, "pointblank.rangedCooldownTicks",
                ranged.verbProps.AdjustedCooldownTicks(ranged, pawn));
            VCODiagnostics.SampleFor(pawn, "pointblank.meleeCooldownTicks",
                meleeVerb.verbProps.AdjustedCooldownTicks(meleeVerb, pawn));

            if (pawn.Spawned)
            {
                MoteMaker.ThrowText(pawn.DrawPos, pawn.Map, "VCO_PointBlankShot".Translate());
            }
            return true;
        }
    }
}
