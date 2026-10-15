using System.Reflection;
using HarmonyLib;
using Verse;

namespace VanillaCombatOverhaul
{
    /// <summary>Applies the test suite's own patches (main menu trigger, seeded map generation).</summary>
    [StaticConstructorOnStartup]
    public static class TestHarmonyBootstrap
    {
        static TestHarmonyBootstrap()
        {
            new Harmony("cruesoe.vanillacombatoverhaul.tests").PatchAll(Assembly.GetExecutingAssembly());
        }
    }
}
