using System.Collections.Generic;
using RimWorld;
using Verse;
using Verse.AI;

namespace VanillaCombatOverhaul
{
    /// <summary>
    /// Shooting on the move. A pawn walking a goto starts a shot without stopping; while it walks, that shot's
    /// aim and cooldown stances stop counting as busy, so the pather keeps going. Gates run cheapest first and
    /// the target search only happens every CheckInterval ticks, for a moving pawn, with a hostile on the map.
    /// </summary>
    public static class RunAndGunUtility
    {
        public const int CheckInterval = 15;
        // Run and Gun's defaults: accuracy x0.9; 10% slower with light weapons, 40% with heavy ones.
        public const float MovingAccuracy = 0.9f;
        public const float LightMoveTicksFactor = 1.1f;
        public const float HeavyMoveTicksFactor = 1.4f;
        // Vanilla rifles weigh 3.4 to 3.5 kg; the LMG 8.5 and minigun 10.
        public const float HeavyWeaponMass = 5f;
        private const int PruneAbove = 32;

        // Pawns whose current shot started on the move, with the verb firing it.
        private static readonly Dictionary<Pawn, Verb> Active = new Dictionary<Pawn, Verb>();
        private static readonly List<Pawn> PruneBuffer = new List<Pawn>();

        public static bool AnyActive => Active.Count > 0;

        public static void ResetState() => Active.Clear();

        /// <summary>Called from the pather for a moving pawn every CheckInterval ticks.</summary>
        public static void TryShoot(Pawn pawn)
        {
            var settings = VCOMod.Settings;
            if (settings == null || !settings.enableRunAndGun || pawn.Faction == null || pawn.CurJobDef != JobDefOf.Goto)
            {
                return;
            }
            if (pawn.Faction == Faction.OfPlayer)
            {
                if (!pawn.Drafted || !pawn.drafter.FireAtWill)
                {
                    return;
                }
            }
            else if (!settings.runAndGunForNpcs)
            {
                return;
            }
            if (pawn.stances.curStance is Stance_Busy || pawn.stances.stunner.Stunned || !pawn.RaceProps.Humanlike
                || pawn.Downed || pawn.InMentalState || pawn.IsCarryingPawn() || pawn.WorkTagIsDisabled(WorkTags.Violent))
            {
                return;
            }
            if (pawn.Map.attackTargetsCache.TargetsHostileToFaction(pawn.Faction).Count == 0
                || SuppressionUtility.LevelOf(pawn) >= SuppressionUtility.SuppressedLevel)
            {
                return;
            }
            var verb = FireModeUtility.PrimaryVerb(pawn);
            if (verb == null || verb.Bursting || !FireModeUtility.IsModeVerb(pawn, verb) || !verb.Available())
            {
                return;
            }

            var target = FindTarget(pawn, verb);
            if (target == null || !verb.TryStartCastOn(target))
            {
                return;
            }
            if (Active.Count > PruneAbove)
            {
                Prune();
            }
            Active[pawn] = verb;
            VCODiagnostics.CountFor(pawn, "runAndGun.started");
        }

        // AI pawns already track an enemy; colonists search the way vanilla's drafted auto-attack does.
        private static Thing FindTarget(Pawn pawn, Verb verb)
        {
            if (pawn.Faction != Faction.OfPlayer)
            {
                var enemy = pawn.mindState.enemyTarget;
                return enemy != null && enemy.Spawned && !(enemy is Pawn p && p.Downed) ? enemy : null;
            }
            var flags = TargetScanFlags.NeedLOSToAll | TargetScanFlags.NeedThreat | TargetScanFlags.NeedAutoTargetable;
            if (verb.IsIncendiary_Ranged())
            {
                flags |= TargetScanFlags.NeedNonBurning;
            }
            return (Thing)AttackTargetFinder.BestShootTargetFromCurrentPosition(pawn, flags);
        }

        /// <summary>The pawn is walking while its run-and-gun shot aims, fires or cools down.</summary>
        public static bool ShootingOnTheMove(Pawn pawn, Verb verb) =>
            Active.TryGetValue(pawn, out var active) && active == verb && pawn.pather != null && pawn.pather.Moving;

        /// <summary>Forgets the pawn once its shot has ended: a stance for another verb, or mobile with no burst left.</summary>
        public static void NotifyStanceChanged(Pawn pawn, Stance stance)
        {
            if (!Active.TryGetValue(pawn, out var verb))
            {
                return;
            }
            if (stance is Stance_Busy busy ? busy.verb == verb : verb.Bursting)
            {
                return;
            }
            Active.Remove(pawn);
        }

        /// <summary>Ticks-per-cell factor while a run-and-gun shot is under way.</summary>
        public static float MoveTicksFactor(Pawn pawn)
        {
            if (!Active.TryGetValue(pawn, out var verb))
            {
                return 1f;
            }
            if (!(pawn.stances.curStance is Stance_Busy busy && busy.verb == verb) && !verb.Bursting)
            {
                return 1f;
            }
            var weapon = pawn.equipment?.Primary;
            return weapon != null && weapon.def.BaseMass >= HeavyWeaponMass ? HeavyMoveTicksFactor : LightMoveTicksFactor;
        }

        /// <summary>Applies the moving-shooter penalty to a freshly built shot report.</summary>
        public static void ApplyToShotReport(ref ShotReport report, Thing caster, Verb verb)
        {
            if (Active.Count == 0 || !(caster is Pawn pawn) || !ShootingOnTheMove(pawn, verb))
            {
                return;
            }
            var factor = ShotReportAccess.GetShooterFactor(ref report);
            ShotReportAccess.SetShooterFactor(ref report, FireModeUtility.AdjustHitFactor(factor, MovingAccuracy));
            VCODiagnostics.Count("runAndGun.hit.adjusted");
        }

        private static void Prune()
        {
            PruneBuffer.Clear();
            foreach (var entry in Active)
            {
                if (!entry.Key.Spawned)
                {
                    PruneBuffer.Add(entry.Key);
                }
            }
            for (var i = 0; i < PruneBuffer.Count; i++)
            {
                Active.Remove(PruneBuffer[i]);
            }
            PruneBuffer.Clear();
        }
    }
}
