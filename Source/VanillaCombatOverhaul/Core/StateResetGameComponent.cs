using Verse;

namespace VanillaCombatOverhaul
{
    /// <summary>Clears static state left over from the previous game whenever a game is created or loaded.</summary>
    public class StateResetGameComponent : GameComponent
    {
        public StateResetGameComponent(Game game)
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
