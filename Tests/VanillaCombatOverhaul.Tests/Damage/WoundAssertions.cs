using System.Collections.Generic;
using RimWorld;
using UnityEngine;
using Verse;

namespace VanillaCombatOverhaul
{
    public static class WoundAssertions
    {
        public static List<AssertionResult> SelfTests()
        {
            var results = new List<AssertionResult>();
            results.Add(Check(
                "stopping power below 1 fragments",
                ProjectileWoundUtility.KindFor(0.5f) == BulletWoundKind.Fragment,
                ProjectileWoundUtility.KindFor(0.5f).ToString()));
            results.Add(Check(
                "stopping power 1 passes through",
                ProjectileWoundUtility.KindFor(1f) == BulletWoundKind.PassThrough,
                ProjectileWoundUtility.KindFor(1f).ToString()));
            results.Add(Check(
                "stopping power 1.5 still passes through",
                ProjectileWoundUtility.KindFor(1.5f) == BulletWoundKind.PassThrough,
                ProjectileWoundUtility.KindFor(1.5f).ToString()));
            results.Add(Check(
                "stopping power above 1.5 mushrooms",
                ProjectileWoundUtility.KindFor(2f) == BulletWoundKind.Mushroom,
                ProjectileWoundUtility.KindFor(2f).ToString()));

            results.Add(Check(
                "fragment curve is zero at 0",
                Mathf.Approximately(ProjectileWoundUtility.FragmentTargets.Evaluate(0f), 0f),
                ProjectileWoundUtility.FragmentTargets.Evaluate(0f).ToString("F2")));
            results.Add(Check(
                "fragment curve is 3 at 1",
                Mathf.Approximately(ProjectileWoundUtility.FragmentTargets.Evaluate(1f), 3f),
                ProjectileWoundUtility.FragmentTargets.Evaluate(1f).ToString("F2")));
            results.Add(Check(
                "arrow scratch split is Reloaded's 0.67",
                Mathf.Approximately(ProjectileWoundUtility.ArrowScratchSplit, 0.67f),
                ProjectileWoundUtility.ArrowScratchSplit.ToString("F2")));
            return results;
        }

        /// <summary>
        /// Which damage types get bullet or arrow wounds: each one is dealt to a spawned pawn's torso and the
        /// wound counters are read, so a damage worker that bypasses the wound patch is caught.
        /// </summary>
        public static List<AssertionResult> MapTests(Map map)
        {
            var results = new List<AssertionResult>();
            var cases = new[]
            {
                // defName and the wound family it should get: "bullet", "arrow" or none
                ("Bullet", "bullet"),
                ("BulletToxic", "bullet"),          // Biotech's needle gun
                ("Bullet_TraitTox", "bullet"),      // Odyssey unique weapon trait
                ("Bullet_TraitIncendiary", "bullet"),
                ("Arrow", "arrow"),
                ("RangedStab", "arrow"),            // thrown pila, handled by DamageWorker_Stab
                ("Cut", (string)null)
            };
            var cell = map.Center + IntVec3.West * 30;
            foreach (var (defName, family) in cases)
            {
                var def = DefDatabase<DamageDef>.GetNamedSilentFail(defName);
                if (def == null)
                {
                    // DLC damage types are absent without their expansion.
                    continue;
                }
                var bulletKind = ProjectileWoundUtility.IsBulletDef(def);
                var arrowKind = ProjectileWoundUtility.IsArrowDef(def);
                results.Add(Check($"{defName} counts as {family ?? "neither bullet nor arrow"}",
                    bulletKind == (family == "bullet") && arrowKind == (family == "arrow"),
                    $"bullet {bulletKind}, arrow {arrowKind}"));

                var dealt = DealWound(map, cell, def);
                results.Add(Check($"{defName} on a pawn makes {(family == null ? "no special" : family == "arrow" ? "an arrow" : "a bullet")} wound",
                    dealt.bullet == (family == "bullet" ? 1 : 0) && dealt.arrow == (family == "arrow" ? 1 : 0),
                    $"bullet wounds {dealt.bullet}, arrow wounds {dealt.arrow}"));
            }
            return results;
        }

        /// <summary>Deals one hit of the damage type to a fresh pawn's torso; returns how many bullet and arrow wounds the counters recorded.</summary>
        private static (long bullet, long arrow) DealWound(Map map, IntVec3 cell, DamageDef def)
        {
            var pawn = PawnGenerator.GeneratePawn(PawnKindDefOf.Colonist);
            pawn.apparel?.DestroyAll();
            GenSpawn.Spawn(pawn, cell, map);
            try
            {
                var before = VCODiagnostics.SnapshotCounters();
                var torso = pawn.RaceProps.body.corePart;
                pawn.TakeDamage(new DamageInfo(def, 5f, 0f, -1f, null, torso));
                var after = VCODiagnostics.SnapshotCounters();
                return (Increase(before, after, "wound.bullet."), Increase(before, after, "wound.arrow."));
            }
            finally
            {
                if (!pawn.Destroyed)
                {
                    pawn.Destroy(DestroyMode.Vanish);
                }
            }
        }

        private static long Increase(Dictionary<string, long> before, Dictionary<string, long> after, string prefix)
        {
            long total = 0;
            foreach (var entry in after)
            {
                if (entry.Key.StartsWith(prefix))
                {
                    before.TryGetValue(entry.Key, out var earlier);
                    total += entry.Value - earlier;
                }
            }
            return total;
        }

        private static AssertionResult Check(string name, bool passed, string detail) =>
            new AssertionResult { Name = name, Passed = passed, Detail = detail };
    }
}
