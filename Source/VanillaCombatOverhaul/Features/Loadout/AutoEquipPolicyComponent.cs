using System.Collections.Generic;
using RimWorld;
using Verse;

namespace VanillaCombatOverhaul
{
    /// <summary>
    /// Weapon permissions deliberately live beside apparel policies instead of inside their
    /// filter. This keeps weapons out of vanilla apparel code while still giving every pawn's
    /// existing policy a matching automatic-equipment policy.
    /// </summary>
    public sealed class AutoEquipPolicyRecord : IExposable
    {
        public int apparelPolicyId = -1;
        public ThingFilter filter;

        public AutoEquipPolicyRecord()
        {
            filter = CreateDefaultFilter();
        }

        public AutoEquipPolicyRecord(int policyId) : this()
        {
            apparelPolicyId = policyId;
        }

        public void ExposeData()
        {
            Scribe_Values.Look(ref apparelPolicyId, "apparelPolicyId", -1);
            Scribe_Deep.Look(ref filter, "filter");
            if (Scribe.mode == LoadSaveMode.PostLoadInit && filter == null)
            {
                filter = CreateDefaultFilter();
            }
        }

        private static ThingFilter CreateDefaultFilter()
        {
            var result = new ThingFilter(ThingCategoryDefOf.Weapons);
            result.allowedHitPointsConfigurable = true;
            result.allowedQualitiesConfigurable = true;
            result.SetAllow(ThingCategoryDefOf.Weapons, true);
            return result;
        }
    }

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

    public sealed class AutoEquipPolicyComponent : GameComponent
    {
        private List<AutoEquipPolicyRecord> policies = new List<AutoEquipPolicyRecord>();
        private List<ForcedWeaponRecord> forcedWeapons = new List<ForcedWeaponRecord>();

        public AutoEquipPolicyComponent(Game game)
        {
        }

        public static AutoEquipPolicyComponent Current =>
            CurrentGame?.GetComponent<AutoEquipPolicyComponent>();

        private static Game CurrentGame => Verse.Current.Game;

        public override void ExposeData()
        {
            Scribe_Collections.Look(ref policies, "autoEquipPolicies", LookMode.Deep);
            Scribe_Collections.Look(ref forcedWeapons, "forcedWeapons", LookMode.Deep);
            if (Scribe.mode == LoadSaveMode.PostLoadInit)
            {
                policies = policies ?? new List<AutoEquipPolicyRecord>();
                forcedWeapons = forcedWeapons ?? new List<ForcedWeaponRecord>();
            }
        }

        public ThingFilter FilterFor(ApparelPolicy policy)
        {
            var id = policy?.id ?? -1;
            var record = policies.Find(p => p.apparelPolicyId == id);
            if (record == null)
            {
                record = new AutoEquipPolicyRecord(id);
                policies.Add(record);
            }
            return record.filter;
        }

        public void CopyPolicy(ApparelPolicy destination, ApparelPolicy source)
        {
            if (destination == null || source == null)
            {
                return;
            }
            FilterFor(destination).CopyAllowancesFrom(FilterFor(source));
        }

        public void ForceWeapon(Pawn pawn, Thing weapon)
        {
            if (pawn == null || weapon == null)
            {
                return;
            }
            forcedWeapons.RemoveAll(r => r.pawnId == pawn.thingIDNumber);
            forcedWeapons.Add(new ForcedWeaponRecord(pawn, weapon));
        }

        public bool HasForcedCurrentWeapon(Pawn pawn)
        {
            if (pawn == null)
            {
                return false;
            }
            var record = forcedWeapons.Find(r => r.pawnId == pawn.thingIDNumber);
            if (record == null)
            {
                return false;
            }
            var current = pawn.equipment?.Primary;
            if (current != null && current.thingIDNumber == record.weaponId)
            {
                return true;
            }
            var pending = pawn.CurJob;
            if (pending?.playerForced == true && pending.def == JobDefOf.Equip
                && pending.targetA.Thing?.thingIDNumber == record.weaponId)
            {
                return true;
            }
            forcedWeapons.Remove(record);
            return false;
        }

        public void ClearForcedWeapon(Pawn pawn)
        {
            if (pawn != null)
            {
                forcedWeapons.RemoveAll(r => r.pawnId == pawn.thingIDNumber);
            }
        }
    }
}
