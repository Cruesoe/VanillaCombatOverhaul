using System.Reflection;
using HarmonyLib;
using Verse;

namespace VanillaCombatOverhaul
{
    [StaticConstructorOnStartup]
    public static class HarmonyBootstrap
    {
        public const string HarmonyId = "cruesoe.vanillacombatoverhaul";

        public static Harmony Instance { get; private set; }

        static HarmonyBootstrap()
        {
            Instance = new Harmony(HarmonyId);
            Instance.PatchAll(Assembly.GetExecutingAssembly());

            // Harmony applies transpilers during PatchAll, so guard counts are final here.
            PatchGuard.VerifyAll();
        }
    }
}
