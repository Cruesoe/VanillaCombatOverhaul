using Verse;

namespace VanillaCombatOverhaul
{
    /// <summary>Weapon filter attached to an apparel policy by earlier versions; read only to migrate saves.</summary>
    public sealed class AutoEquipPolicyRecord : IExposable
    {
        public int apparelPolicyId = -1;
        public ThingFilter filter;

        public void ExposeData()
        {
            Scribe_Values.Look(ref apparelPolicyId, "apparelPolicyId", -1);
            Scribe_Deep.Look(ref filter, "filter");
        }
    }
}
