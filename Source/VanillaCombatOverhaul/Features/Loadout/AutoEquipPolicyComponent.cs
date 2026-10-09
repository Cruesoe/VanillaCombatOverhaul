using System.Collections.Generic;
using System.Linq;
using RimWorld;
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

    /// <summary>The colony's loadouts and manually locked weapons. Class name kept for save compatibility.</summary>
    public sealed class AutoEquipPolicyComponent : GameComponent
    {
        private List<LoadoutPolicy> loadouts = new List<LoadoutPolicy>();
        private List<ForcedWeaponRecord> forcedWeapons = new List<ForcedWeaponRecord>();
        private List<AutoEquipPolicyRecord> legacyPolicies;

        public AutoEquipPolicyComponent(Game game)
        {
        }

        public static AutoEquipPolicyComponent Current => Verse.Current.Game?.GetComponent<AutoEquipPolicyComponent>();

        public List<LoadoutPolicy> AllLoadouts => loadouts;

        public override void ExposeData()
        {
            Scribe_Collections.Look(ref loadouts, "loadouts", LookMode.Deep);
            Scribe_Collections.Look(ref forcedWeapons, "forcedWeapons", LookMode.Deep);
            if (Scribe.mode == LoadSaveMode.LoadingVars)
            {
                Scribe_Collections.Look(ref legacyPolicies, "autoEquipPolicies", LookMode.Deep);
            }
            if (Scribe.mode == LoadSaveMode.PostLoadInit)
            {
                loadouts = loadouts ?? new List<LoadoutPolicy>();
                loadouts.RemoveAll(l => l == null);
                forcedWeapons = forcedWeapons ?? new List<ForcedWeaponRecord>();
            }
        }

        public override void FinalizeInit()
        {
            if (loadouts.Count > 0)
            {
                return;
            }
            if (legacyPolicies != null && legacyPolicies.Count > 0)
            {
                MigrateLegacyPolicies(legacyPolicies, PawnsFinder.AllMapsWorldAndTemporary_Alive);
            }
            else
            {
                GenerateStartingLoadouts();
            }
            legacyPolicies = null;
        }

        // ------------------------------------------------------------ database

        public LoadoutPolicy DefaultLoadout()
        {
            if (loadouts.Count == 0)
            {
                MakeNewLoadout();
            }
            return loadouts[0];
        }

        public void SetDefault(LoadoutPolicy policy)
        {
            var index = loadouts.IndexOf(policy);
            if (index <= 0)
            {
                return;
            }
            loadouts[index] = loadouts[0];
            loadouts[0] = policy;
        }

        public LoadoutPolicy MakeNewLoadout()
        {
            var id = loadouts.Count == 0 ? 1 : loadouts.Max(l => l.id) + 1;
            var policy = new LoadoutPolicy(id, "VCO_Loadout_New".Translate(id));
            loadouts.Add(policy);
            return policy;
        }

        public AcceptanceReport TryDelete(LoadoutPolicy policy)
        {
            foreach (var pawn in PawnsFinder.AllMapsCaravansAndTravellingTransporters_Alive)
            {
                if (LoadoutUtility.CompFor(pawn)?.HasAssignedLoadout(policy) == true && pawn.IsColonist)
                {
                    return new AcceptanceReport("VCO_Loadout_InUse".Translate(pawn));
                }
            }
            foreach (var pawn in PawnsFinder.AllMapsWorldAndTemporary_AliveOrDead)
            {
                var comp = LoadoutUtility.CompFor(pawn);
                if (comp != null && comp.HasAssignedLoadout(policy))
                {
                    comp.Loadout = null;
                }
            }
            loadouts.Remove(policy);
            return AcceptanceReport.WasAccepted;
        }

        private void GenerateStartingLoadouts()
        {
            MakeNewLoadout().label = "VCO_Loadout_Anything".Translate();

            var ranged = MakeNewLoadout();
            ranged.label = "VCO_Loadout_Ranged".Translate();
            ranged.weaponFilter = LoadoutUtility.NewWeaponFilter(ranged: true, melee: false);
            ranged.carrySidearm = true;

            var melee = MakeNewLoadout();
            melee.label = "VCO_Loadout_Melee".Translate();
            melee.weaponFilter = LoadoutUtility.NewWeaponFilter(ranged: false, melee: true);

            var manual = MakeNewLoadout();
            manual.label = "VCO_Loadout_Manual".Translate();
            manual.autoPrimary = false;
        }

        /// <summary>Turns each apparel policy's old weapon filter into a loadout and assigns it.</summary>
        internal void MigrateLegacyPolicies(List<AutoEquipPolicyRecord> legacy, IEnumerable<Pawn> pawns)
        {
            var byApparelPolicy = new Dictionary<int, LoadoutPolicy>();
            foreach (var apparel in Verse.Current.Game.outfitDatabase.AllOutfits)
            {
                var policy = MakeNewLoadout();
                policy.label = apparel.label;
                var record = legacy.Find(p => p.apparelPolicyId == apparel.id);
                if (record?.filter != null)
                {
                    policy.weaponFilter.CopyAllowancesFrom(record.filter);
                }
                byApparelPolicy[apparel.id] = policy;
            }
            foreach (var pawn in pawns)
            {
                var apparelId = pawn.outfits?.CurrentApparelPolicy?.id;
                var comp = LoadoutUtility.CompFor(pawn);
                if (comp != null && apparelId.HasValue && byApparelPolicy.TryGetValue(apparelId.Value, out var policy))
                {
                    comp.Loadout = policy;
                }
            }
            Log.Message($"[VCO] Moved {byApparelPolicy.Count} weapon policies from apparel policies to loadouts.");
        }

        // ------------------------------------------------------------ forced weapons

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
            var swapped = LoadoutUtility.CompFor(pawn)?.SwappedPrimary;
            if (swapped != null && swapped.thingIDNumber == record.weaponId)
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
