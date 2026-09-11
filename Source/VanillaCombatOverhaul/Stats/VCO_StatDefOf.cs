using RimWorld;
using Verse;

namespace VanillaCombatOverhaul
{
    [DefOf]
    public static class VCO_StatDefOf
    {
        /// <summary>Chance to deflect an incoming melee attack with a held weapon.</summary>
        public static StatDef VCO_ParryChance;

        /// <summary>Ability to overcome weapon and weather accuracy penalties.</summary>
        public static StatDef VCO_AccuracyMitigation;

        /// <summary>Reduction to an attacker's chance to hit this pawn while it is moving.</summary>
        public static StatDef VCO_Evasion;

        /// <summary>How quickly a weapon is put away and another brought up.</summary>
        public static StatDef VCO_WeaponSwapSpeed;

        static VCO_StatDefOf()
        {
            DefOfHelper.EnsureInitializedInCtor(typeof(VCO_StatDefOf));
        }
    }
}
