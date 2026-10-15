using Verse;

namespace VanillaCombatOverhaul
{
    public sealed class ForcedWeaponRecord : IExposable
    {
        public int pawnId = -1;
        public int weaponId = -1;

        public ForcedWeaponRecord()
        {
        }

        public ForcedWeaponRecord(Pawn pawn, Thing weapon)
        {
            pawnId = pawn.thingIDNumber;
            weaponId = weapon.thingIDNumber;
        }

        public void ExposeData()
        {
            Scribe_Values.Look(ref pawnId, "pawnId", -1);
            Scribe_Values.Look(ref weaponId, "weaponId", -1);
        }
    }
}
