using Verse;

namespace VanillaCombatOverhaul
{
    /// <summary>Clears static state left over from the previous game whenever a game is created or loaded.</summary>
    public class StateResetGameComponent : GameComponent
    {
        public StateResetGameComponent(Game game)
        {
            ResetAll();
        }

        /// <summary>Clears every feature's static per-game state.</summary>
        public static void ResetAll()
        {
            CombatContext.Reset();
            JobGiver_Loadout.ResetState();
            ParryUtility.ResetState();
            PointBlankUtility.ResetState();
            RunAndGunUtility.ResetState();
            SuppressionUtility.ResetState();
        }
    }
}
