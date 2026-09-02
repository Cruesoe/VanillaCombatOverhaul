using System.Collections.Generic;
using System.Linq;
using HarmonyLib;
using RimWorld;
using UnityEngine;
using Verse;

namespace VanillaCombatOverhaul
{
    public class CompProperties_HeightTarget : CompProperties
    {
        public CompProperties_HeightTarget() => compClass = typeof(CompHeightTarget);
    }

    public class CompHeightTarget : ThingComp
    {
        private BodyPartHeight targetingMode = BodyPartHeight.Undefined;

        public Pawn Pawn => parent as Pawn;

        public BodyPartHeight TargetingMode => targetingMode;

        public void SetTargetingMode(BodyPartHeight height) => targetingMode = height;

        public override IEnumerable<Gizmo> CompGetGizmosExtra()
        {
            var settings = VCOMod.Settings;
            if (settings == null || !settings.enableHeightTargeting)
            {
                yield break;
            }
            if (parent.Faction != Faction.OfPlayer)
            {
                yield break;
            }
            if (Pawn != null && !Pawn.Drafted)
            {
                yield break;
            }

            yield return HeightTargeting.CommandFor(this);
        }

        public override void PostExposeData()
        {
            base.PostExposeData();
            Scribe_Values.Look(ref targetingMode, "vcoHeightTarget", BodyPartHeight.Undefined);
        }
    }

    [StaticConstructorOnStartup]
    public static class HeightTargeting
    {
        public static readonly BodyPartHeight[] Modes =
        {
            BodyPartHeight.Undefined,
            BodyPartHeight.Bottom,
            BodyPartHeight.Middle,
            BodyPartHeight.Top
        };

        private static readonly Texture2D IconNone = MakeIcon(new Color(0.45f, 0.45f, 0.45f));
        private static readonly Texture2D IconBottom = MakeIcon(new Color(0.55f, 0.35f, 0.15f));
        private static readonly Texture2D IconMiddle = MakeIcon(new Color(0.85f, 0.75f, 0.2f));
        private static readonly Texture2D IconTop = MakeIcon(new Color(0.95f, 0.95f, 0.95f));

        static HeightTargeting()
        {
            foreach (var def in DefDatabase<ThingDef>.AllDefs)
            {
                if (def.race == null || (!def.race.Humanlike && !def.race.ToolUser))
                {
                    continue;
                }
                if (def.HasComp(typeof(CompHeightTarget)))
                {
                    continue;
                }
                if (def.comps == null)
                {
                    def.comps = new List<CompProperties>();
                }
                def.comps.Add(new CompProperties_HeightTarget());
            }
        }

        public static BodyPartHeight GetTargetHeight(Thing instigator)
        {
            if (!CanUse(instigator))
            {
                return BodyPartHeight.Undefined;
            }
            var comp = instigator.TryGetComp<CompHeightTarget>();
            return comp?.TargetingMode ?? BodyPartHeight.Undefined;
        }

        public static bool CanUse(Thing instigator)
        {
            if (instigator == null || !instigator.def.HasComp(typeof(CompHeightTarget)))
            {
                return false;
            }
            if (instigator is Pawn pawn && (pawn.CurrentEffectiveVerb?.verbProps.CausesExplosion ?? true))
            {
                return false;
            }
            if (instigator is Building_Turret turret
                && (turret.CurrentEffectiveVerb?.verbProps.CausesExplosion ?? true))
            {
                return false;
            }
            return true;
        }

        public static void AssignRandom(Pawn pawn)
        {
            var comp = pawn?.TryGetComp<CompHeightTarget>();
            if (comp == null)
            {
                return;
            }
            comp.SetTargetingMode((BodyPartHeight)Rand.RangeInclusive(0, 3));
        }

        public static void Reset(Pawn pawn) =>
            pawn?.TryGetComp<CompHeightTarget>()?.SetTargetingMode(BodyPartHeight.Undefined);

        public static string LabelFor(BodyPartHeight height)
        {
            switch (height)
            {
                case BodyPartHeight.Top:
                    return "VCO_Height_Top".Translate();
                case BodyPartHeight.Middle:
                    return "VCO_Height_Middle".Translate();
                case BodyPartHeight.Bottom:
                    return "VCO_Height_Bottom".Translate();
                default:
                    return "VCO_Height_None".Translate();
            }
        }

        public static Texture2D IconFor(BodyPartHeight height)
        {
            switch (height)
            {
                case BodyPartHeight.Top:
                    return IconTop;
                case BodyPartHeight.Middle:
                    return IconMiddle;
                case BodyPartHeight.Bottom:
                    return IconBottom;
                default:
                    return IconNone;
            }
        }

        public static Command CommandFor(CompHeightTarget comp) =>
            new Command_SetHeightTarget
            {
                icon = IconFor(comp.TargetingMode),
                defaultLabel = "VCO_CommandSetHeight".Translate(LabelFor(comp.TargetingMode)),
                defaultDesc = "VCO_CommandSetHeight_Tip".Translate(),
                comp = comp
            };

        /// <summary>
        /// Chance the targeted height band actually lands, given leftover coverage on that
        /// side. Skill pushes the chance up when advanced accuracy is on, matching Reloaded's
        /// <c>statpush</c>. A zero denominator returns 0 rather than NaN.
        /// </summary>
        public static float ChanceToLand(Thing caster, Pawn target, BodyPartGroupDef side,
                                         BodyPartHeight height, DamageDef damage, bool melee)
        {
            if (target?.health?.hediffSet == null || height == BodyPartHeight.Undefined)
            {
                return 1f;
            }

            var atHeight = Coverage(target, side, damage, height);
            var anyHeight = Coverage(target, side, damage, BodyPartHeight.Undefined);
            if (anyHeight <= 0f)
            {
                return 0f;
            }

            var relative = atHeight / anyHeight;
            var settings = VCOMod.Settings;
            if (settings == null || !settings.enableAdvancedAccuracy || caster == null)
            {
                return Mathf.Clamp01(relative);
            }

            float skill;
            if (melee)
            {
                skill = Mathf.Max(1f, StatDefOf.MeleeHitChance.Worker.GetValue(StatRequest.For(caster), false)
                                      / settings.accuracyScale);
            }
            else
            {
                skill = PenaltyMitigationUtility.ShooterSkillFactor(caster, settings.accuracyScale);
            }

            return Mathf.Clamp01(1f - Mathf.Pow(1f - relative, skill));
        }

        public static float Coverage(Pawn target, BodyPartGroupDef side, DamageDef damage,
                                     BodyPartHeight height)
        {
            var parts = target.health.hediffSet.GetNotMissingParts(height);
            if (side != null)
            {
                parts = parts.Where(p => p.groups != null && p.groups.Contains(side));
            }

            var total = 0f;
            foreach (var part in parts)
            {
                total += part.coverageAbs * part.def.GetHitChanceFactorFor(damage);
            }
            return total;
        }

        private static Texture2D MakeIcon(Color fill)
        {
            const int size = 32;
            var tex = new Texture2D(size, size, TextureFormat.ARGB32, false)
            {
                name = "VCO_HeightIcon",
                filterMode = FilterMode.Point
            };
            var pixels = new Color[size * size];
            for (var y = 0; y < size; y++)
            {
                for (var x = 0; x < size; x++)
                {
                    var edge = x < 2 || y < 2 || x >= size - 2 || y >= size - 2;
                    pixels[y * size + x] = edge ? new Color(0.1f, 0.1f, 0.1f, 1f) : fill;
                }
            }
            tex.SetPixels(pixels);
            tex.Apply();
            return tex;
        }
    }

    public class Command_SetHeightTarget : Command
    {
        public CompHeightTarget comp;
        public List<CompHeightTarget> comps;

        public override void ProcessInput(Event ev)
        {
            base.ProcessInput(ev);
            if (comps == null)
            {
                comps = new List<CompHeightTarget>();
            }
            if (comp != null && !comps.Contains(comp))
            {
                comps.Add(comp);
            }

            var options = new List<FloatMenuOption>();
            foreach (var mode in HeightTargeting.Modes)
            {
                var captured = mode;
                options.Add(new FloatMenuOption(HeightTargeting.LabelFor(captured), () =>
                {
                    foreach (var c in comps)
                    {
                        c.SetTargetingMode(captured);
                    }
                }));
            }
            Find.WindowStack.Add(new FloatMenu(options));
        }

        public override bool InheritInteractionsFrom(Gizmo other)
        {
            if (comps == null)
            {
                comps = new List<CompHeightTarget>();
            }
            if (other is Command_SetHeightTarget otherCommand && otherCommand.comp != null)
            {
                comps.Add(otherCommand.comp);
            }
            return false;
        }
    }

    [HarmonyPatch(typeof(Pawn), nameof(Pawn.SpawnSetup))]
    public static class Patch_Pawn_SpawnSetup_Height
    {
        public static void Postfix(Pawn __instance, bool respawningAfterLoad)
        {
            if (respawningAfterLoad || __instance.IsColonist)
            {
                return;
            }
            HeightTargeting.AssignRandom(__instance);
        }
    }

    [HarmonyPatch(typeof(Pawn_DraftController), nameof(Pawn_DraftController.Drafted), MethodType.Setter)]
    public static class Patch_Drafted_Height
    {
        public static void Postfix(Pawn_DraftController __instance, bool value)
        {
            if (!value)
            {
                HeightTargeting.Reset(__instance.pawn);
            }
        }
    }

    [HarmonyPatch(typeof(Pawn_HealthTracker), "MakeDowned")]
    public static class Patch_MakeDowned_Height
    {
        public static void Postfix(Pawn ___pawn)
        {
            if (___pawn != null && ___pawn.Downed)
            {
                HeightTargeting.Reset(___pawn);
            }
        }
    }
}
