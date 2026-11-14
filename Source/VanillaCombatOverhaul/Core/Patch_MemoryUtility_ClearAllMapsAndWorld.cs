using HarmonyLib;
using Verse.Profile;

namespace VanillaCombatOverhaul
{
    /// <summary>Clears static state when a game is torn down, including on return to the main menu.</summary>
    [HarmonyPatch(typeof(MemoryUtility), nameof(MemoryUtility.ClearAllMapsAndWorld))]
    public static class Patch_MemoryUtility_ClearAllMapsAndWorld
    {
        public static void Postfix() => StateResetGameComponent.ResetAll();
    }
}
