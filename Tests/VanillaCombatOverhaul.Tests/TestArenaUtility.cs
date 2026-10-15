using System.Collections.Generic;
using RimWorld;
using Verse;

namespace VanillaCombatOverhaul
{
    /// <summary>Clears the square around the map center the test arenas use to flat, open, unroofed soil.</summary>
    public static class TestArenaUtility
    {
        // Arenas reach 40 cells east of the center (long-range ranged tests) and about 24 north and south.
        public const int ArenaRadius = 45;

        private static readonly List<Thing> TmpThings = new List<Thing>();

        /// <summary>Removes everything but player pawns from the arena square and lays soil; returns how many things went.</summary>
        public static int PrepareArena(Map map)
        {
            var removed = 0;
            var rect = CellRect.CenteredOn(map.Center, ArenaRadius).ClipInsideMap(map);
            foreach (var cell in rect)
            {
                TmpThings.Clear();
                TmpThings.AddRange(cell.GetThingList(map));
                foreach (var thing in TmpThings)
                {
                    if (!thing.Spawned || (thing is Pawn pawn && pawn.Faction == Faction.OfPlayer))
                    {
                        continue;
                    }
                    if (thing.def.destroyable)
                    {
                        thing.Destroy(DestroyMode.Vanish);
                    }
                    else
                    {
                        thing.DeSpawn();
                    }
                    removed++;
                }
                map.roofGrid.SetRoof(cell, null);
                map.terrainGrid.SetTerrain(cell, TerrainDefOf.Soil);
                map.snowGrid.SetDepth(cell, 0f);
                map.fogGrid.Unfog(cell);
            }
            TmpThings.Clear();
            return removed;
        }
    }
}
