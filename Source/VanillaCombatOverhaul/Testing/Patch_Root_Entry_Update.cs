using HarmonyLib;
using Verse;

namespace VanillaCombatOverhaul
{
    /// <summary>Watches the main menu for the trigger and starts a map.</summary>
    [HarmonyPatch(typeof(Root_Entry), nameof(Root_Entry.Update))]
    public static class Patch_Root_Entry_Update
    {
        public static void Postfix() => AutoTest.BeginIfRequested();
    }
}
