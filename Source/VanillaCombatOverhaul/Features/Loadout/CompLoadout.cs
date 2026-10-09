using System.Collections.Generic;
using RimWorld;
using Verse;
using Verse.AI;

namespace VanillaCombatOverhaul
{
    public class CompProperties_Loadout : CompProperties
    {
        public CompProperties_Loadout() => compClass = typeof(CompLoadout);
    }

    /// <summary>A pawn's assigned loadout, its carry settings, and the swap between its primary and a carried melee sidearm.</summary>
    public class CompLoadout : ThingComp
    {
        public const int CheckIntervalTicks = 30;
        public const int StockIntervalTicks = 250;
        public const int CalmChecksBeforeSwapBack = 4;

        private LoadoutPolicy loadout;
        private ThingWithComps swappedPrimary;
        private ThingWithComps lockedSidearm;
        private bool autoSwapped;
        private int calmChecks;

        public Pawn Pawn => parent as Pawn;

        /// <summary>The assigned loadout, falling back to the default one.</summary>
        public LoadoutPolicy Loadout
        {
            get => loadout ??= AutoEquipPolicyComponent.Current?.DefaultLoadout();
            set => loadout = value;
        }

        public bool HasAssignedLoadout(LoadoutPolicy policy) => loadout == policy;

        /// <summary>The primary weapon moved to inventory while the sidearm is in hand.</summary>
        public ThingWithComps SwappedPrimary => swappedPrimary;

        /// <summary>The sidearm the player chose for this pawn, kept over any better one.</summary>
        public ThingWithComps LockedSidearm
        {
            get
            {
                if (lockedSidearm != null && lockedSidearm.Destroyed)
                {
                    lockedSidearm = null;
                }
                return lockedSidearm;
            }
            set => lockedSidearm = value;
        }

        public override void CompTickInterval(int delta)
        {
            var pawn = Pawn;
            if (pawn == null)
            {
                return;
            }
            if (LoadoutUtility.Enabled && pawn.IsColonist && parent.IsHashIntervalTick(StockIntervalTicks, delta))
            {
                LoadoutStockUtility.Apply(pawn, Loadout);
            }
            if (!parent.IsHashIntervalTick(CheckIntervalTicks, delta))
            {
                return;
            }
            // Sidearms switched off with a primary still stowed: put the primary back in hand.
            if (!LoadoutUtility.SidearmsEnabled)
            {
                if (swappedPrimary != null && pawn.inventory != null
                    && (!pawn.inventory.innerContainer.Contains(swappedPrimary) || LoadoutUtility.Swap(pawn, swappedPrimary)))
                {
                    ClearSwap();
                }
                return;
            }
            if (!pawn.Spawned || pawn.Downed || pawn.InMentalState || !pawn.IsColonistPlayerControlled)
            {
                return;
            }
            if (swappedPrimary != null)
            {
                UpdateSwapBack(pawn);
            }
            else
            {
                UpdateSwapToSidearm(pawn);
            }
        }

        private void UpdateSwapToSidearm(Pawn pawn)
        {
            var policy = Loadout;
            var primary = pawn.equipment?.Primary;
            if (policy == null || !(policy.carrySidearm || LockedSidearm != null) || !LoadoutUtility.IsRanged(primary)
                || !InCombat(pawn) || !LoadoutUtility.AdjacentThreat(pawn) || LoadoutUtility.KeepsGunUpClose(pawn))
            {
                return;
            }
            var sidearm = LoadoutUtility.CarriedSidearm(pawn);
            if (sidearm != null && LoadoutUtility.Swap(pawn, sidearm))
            {
                swappedPrimary = primary;
                autoSwapped = true;
                calmChecks = 0;
            }
        }

        private void UpdateSwapBack(Pawn pawn)
        {
            if (!pawn.inventory.innerContainer.Contains(swappedPrimary))
            {
                ClearSwap();
                return;
            }
            // A sidearm the player chose stays in hand until they switch back or undraft.
            if (pawn.Drafted && !autoSwapped)
            {
                return;
            }
            if (LoadoutUtility.AdjacentThreat(pawn))
            {
                calmChecks = 0;
                return;
            }
            if (pawn.Drafted && ++calmChecks < CalmChecksBeforeSwapBack)
            {
                return;
            }
            if (LoadoutUtility.Swap(pawn, swappedPrimary))
            {
                ClearSwap();
            }
        }

        private void ClearSwap()
        {
            swappedPrimary = null;
            autoSwapped = false;
            calmChecks = 0;
        }

        private static bool InCombat(Pawn pawn)
        {
            if (pawn.Drafted)
            {
                return true;
            }
            var job = pawn.CurJobDef;
            return job == JobDefOf.AttackMelee || job == JobDefOf.AttackStatic || job == JobDefOf.Wait_Combat;
        }

        /// <summary>Manual switch between the primary and the carried sidearm.</summary>
        public void ToggleSidearm()
        {
            var pawn = Pawn;
            if (pawn == null)
            {
                return;
            }
            if (swappedPrimary != null)
            {
                if (LoadoutUtility.Swap(pawn, swappedPrimary))
                {
                    ClearSwap();
                }
                return;
            }
            var primary = pawn.equipment?.Primary;
            var sidearm = LoadoutUtility.CarriedSidearm(pawn);
            if (sidearm != null && LoadoutUtility.Swap(pawn, sidearm))
            {
                swappedPrimary = primary;
                autoSwapped = false;
                calmChecks = 0;
            }
        }

        public override IEnumerable<Gizmo> CompGetGizmosExtra()
        {
            var pawn = Pawn;
            if (!LoadoutUtility.SidearmsEnabled || pawn == null || !pawn.IsColonistPlayerControlled)
            {
                yield break;
            }
            var locked = LockedSidearm;
            if (locked != null)
            {
                yield return new Command_Action
                {
                    defaultLabel = "VCO_Loadout_ReleaseSidearm".Translate(),
                    defaultDesc = "VCO_Loadout_ReleaseSidearm_Tip".Translate(locked.LabelShortCap),
                    icon = locked.def.uiIcon,
                    iconAngle = locked.def.uiIconAngle,
                    action = () => LockedSidearm = null
                };
            }
            if (!pawn.Drafted)
            {
                yield break;
            }
            var target = swappedPrimary ?? LoadoutUtility.CarriedSidearm(pawn);
            if (target == null || (swappedPrimary == null && pawn.equipment?.Primary != null
                                   && !LoadoutUtility.IsRanged(pawn.equipment.Primary)))
            {
                yield break;
            }
            yield return new Command_Action
            {
                defaultLabel = "VCO_Loadout_Switch".Translate(target.LabelShortCap),
                defaultDesc = "VCO_Loadout_Switch_Tip".Translate(),
                icon = target.def.uiIcon,
                iconAngle = target.def.uiIconAngle,
                action = ToggleSidearm
            };
        }

        public override void PostExposeData()
        {
            base.PostExposeData();
            Scribe_References.Look(ref loadout, "vcoLoadout");
            Scribe_References.Look(ref swappedPrimary, "vcoSwappedPrimary");
            Scribe_References.Look(ref lockedSidearm, "vcoLockedSidearm");
            Scribe_Values.Look(ref autoSwapped, "vcoAutoSwapped", false);
        }
    }
}
