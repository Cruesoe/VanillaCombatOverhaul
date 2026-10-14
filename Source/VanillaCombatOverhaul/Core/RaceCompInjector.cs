using System.Collections.Generic;
using Verse;

namespace VanillaCombatOverhaul
{
    /// <summary>Adds the mod's pawn comps to every humanlike and tool-using race in one pass over the defs.</summary>
    [StaticConstructorOnStartup]
    public static class RaceCompInjector
    {
        static RaceCompInjector()
        {
            foreach (var def in DefDatabase<ThingDef>.AllDefs)
            {
                var race = def.race;
                if (race == null || (!race.Humanlike && !race.ToolUser))
                {
                    continue;
                }
                Add(def, typeof(CompFireMode), () => new CompProperties_FireMode());
                Add(def, typeof(CompHeightTarget), () => new CompProperties_HeightTarget());
                if (race.Humanlike)
                {
                    Add(def, typeof(CompSuppression), () => new CompProperties_Suppression());
                    Add(def, typeof(CompLoadout), () => new CompProperties_Loadout());
                }
            }
        }

        private static void Add(ThingDef def, System.Type compClass, System.Func<CompProperties> make)
        {
            if (def.HasComp(compClass))
            {
                return;
            }
            def.comps ??= new List<CompProperties>();
            def.comps.Add(make());
        }
    }
}
