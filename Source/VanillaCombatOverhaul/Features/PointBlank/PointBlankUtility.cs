using System.Collections.Generic;
using RimWorld;
using Verse;
using Verse.AI;
using Verse.AI.Group;

namespace VanillaCombatOverhaul
{
    /// <summary>
    /// Point-blank shooting: a skilled shooter caught in melee sometimes fires the gun in hand
    /// instead of swinging it.
    ///
    /// The mod decides only whether this melee attack becomes a shot. Everything after that --
    /// line of fire, projectile, burst, accuracy, XP, cooldown -- is the weapon's own verb
    /// running exactly as it would for any other shot, with the warmup removed.
    ///
    /// The ranged cast is started from inside the melee verb's own TryStartCastOn, and only
    /// when that call is the one Pawn_MeleeVerbs.TryMeleeAttack makes. By then vanilla has
    /// already rejected a busy pawn and picked a usable melee verb, so each roll corresponds
    /// to exactly one real attack and none of those gates are copied here.
    /// </summary>
    public static class PointBlankUtility
    {
        /// <summary>
        /// Chance per eligible attack, indexed by Shooting level. A table rather than a curve
        /// because the agreed balance names an exact value per level, and interpolation
        /// between control points would drift from it.
        /// </summary>
        private static readonly float[] ChanceBySkill =
        {
            0f, 0f, 0f, 0f, 0f, 0f, 0f, 0f, 0f, 0f,
            0.05f, 0.06f, 0.08f, 0.10f, 0.13f, 0.17f, 0.22f, 0.28f, 0.35f, 0.43f, 0.50f
        };

        public const int MinimumSkill = 10;

        /// <summary>
        /// Test-only: lets the arena drive attacks with ordered jobs, which are always
        /// player-forced, without the player-order exclusion rejecting every one of them.
        /// </summary>
        public static bool IgnorePlayerForcedForTesting;

        // The pawn and target of the TryMeleeAttack call in progress. Anything that starts a
        // melee verb outside that call is not a normal melee attack and is left alone.
        private static Pawn meleeAttacker;
        private static Thing meleeTarget;

        // The ranged verb being checked and started as a point-blank shot. Its warmup reads
        // as zero, and vanilla's melee lock does not apply to it.
        private static Verb startingVerb;

        // Point-blank verbs still partway through a burst. The later shots run on later ticks,
        // outside any call we can scope, and must not be cut off by the melee lock the first
        // shot was allowed past. Not saved: a burst interrupted by a reload finishes under
        // normal rules, which for a non-player pawn usually means it stops early.
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

        /// <summary>
        /// The level the table is read at. Aptitudes count, because they are part of the
        /// Shooting level the player sees on the pawn; a pawn that cannot shoot at all has none.
        /// </summary>
        public static int ShootingLevel(Pawn pawn)
        {
            var skill = pawn.skills?.GetSkill(SkillDefOf.Shooting);
            if (skill == null || skill.TotallyDisabled)
            {
                return -1;
            }
            return skill.Level;
        }

        /// <summary>
        /// Marks a TryMeleeAttack call for its lifetime. The previous values are restored on
        /// dispose rather than cleared, because a parry counter-attack is a melee attack made
        /// from inside another pawn's.
        /// </summary>
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

        /// <summary>
        /// Called as a melee verb starts its cast. Returns true when a point-blank shot was
        /// fired in its place, in which case the melee cast must not run.
        /// </summary>
        public static bool TryReplace(Verb meleeVerb, LocalTargetInfo castTarg)
        {
            var pawn = meleeVerb.CasterPawn;
            if (pawn == null || pawn != meleeAttacker || castTarg.Thing != meleeTarget)
            {
                return false;   // Not the attack TryMeleeAttack is making.
            }

            // Only pawns holding a ranged weapon are counted, so the rates below describe
            // shooters rather than being diluted by every sword swing in the game.
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
                // Buildings are not the design's target, and a finishing blow on a downed
                // pawn must not turn into a gunshot.
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
                // Checked as a point-blank cast, so the melee lock is lifted but everything
                // else that makes a weapon unusable -- fuel, charges, roles -- still applies.
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
                    // Only reachable while the arena waives the rule; the ordered-melee
                    // scenario asserts this stays at zero.
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

        /// <summary>
        /// Melee that has a reason to stay melee: a social fight or duel is not meant to be
        /// lethal gunplay, an explicit player melee order is a deliberate choice, and a parry
        /// counter-attack is VCO's own bonus swing rather than a normal attack.
        ///
        /// Returns the counter key for the reason, or null when none applies.
        /// </summary>
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

        /// <summary>
        /// An explicit "melee attack" order. A drafted pawn's automatic melee runs under
        /// Wait_Combat, not AttackMelee, so it is not caught here.
        /// </summary>
        private static bool IsPlayerMeleeOrder(Pawn pawn)
        {
            var job = pawn.CurJob;
            return job != null && job.playerForced && job.def == JobDefOf.AttackMelee;
        }

        /// <summary>
        /// True while this verb is firing a point-blank shot: during its start, and for the
        /// rest of the burst that start began. Vanilla's melee lock is lifted for exactly
        /// these casts and no others.
        /// </summary>
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

        /// <summary>
        /// Called when a verb finishes a shot or is reset. Once its burst is over, its next
        /// cast is an ordinary one and must be held to the ordinary rules.
        /// </summary>
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

            // Vanilla's own checks decide whether the weapon can fire at this target; a
            // refusal returns before the verb changes any state, so melee proceeds clean.
            if (!ranged.TryStartCastOn(target))
            {
                VCODiagnostics.CountFor(pawn, "pointblank.reject.cannotStart");
                return false;
            }

            // Notify_AttackedTarget runs only when a shot actually went off, for any verb
            // type, which makes it a better signal than anything projectile-specific.
            var fired = mind != null && attackedBefore != now && mind.lastAttackTargetTick == now;
            if (!fired)
            {
                // The verb accepted the target and then failed its first shot. The melee
                // attack still runs, and its cooldown replaces the ranged one just set.
                VCODiagnostics.CountFor(pawn, "pointblank.reject.notFired");
                return false;
            }

            if (ranged.Bursting && !burstingVerbs.Contains(ranged))
            {
                burstingVerbs.Add(ranged);
            }

            VCODiagnostics.CountFor(pawn, "pointblank.success");
            // Recorded for tuning the cooldown decision: what the shot costs against what the
            // replaced swing would have.
            VCODiagnostics.SampleFor(pawn, "pointblank.rangedCooldownTicks",
                ranged.verbProps.AdjustedCooldownTicks(ranged, pawn));
            VCODiagnostics.SampleFor(pawn, "pointblank.meleeCooldownTicks",
                meleeVerb.verbProps.AdjustedCooldownTicks(meleeVerb, pawn));

            if (pawn.Spawned)
            {
                // ThrowText already skips maps and cells the player cannot see.
                MoteMaker.ThrowText(pawn.DrawPos, pawn.Map, "VCO_PointBlankShot".Translate());
            }
            return true;
        }
    }
}
