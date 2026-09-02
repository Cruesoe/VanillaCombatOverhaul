# Sidearms and loadouts — design plan

Status: **paused.** Phase 1 is built, green in the arena suite, and dormant behind a locked toggle.
Phase 2 (the switch gizmo) is not started and is not currently planned. See §9 for why, and for the
alternative that pause is holding the door open for.

**Scope of this plan: a main weapon, one sidearm, and a switch between them.** Nothing else.
Loadouts stay on the roadmap for future development and are recorded in §3 with everything else
deliberately left out. `enableSidearms` is the toggle this unlocks; `enableLoadouts` stays locked
off where it is.

Read alongside the six design rules in [README.md](../README.md). Every decision below is
downstream of them, particularly rule 2 (no def rewrites), rule 3 (mechanics are stats) and
rule 6 (generic categories over per-item patches).

---

## 1. What the reference mods actually do

Read from the installed copies in the Steam workshop folder, not from memory.

### Simple Sidearms (`PeteTimesSix.SimpleSidearms`, 927155256)

The genre definition. Weapons live in the pawn's normal inventory; the mod adds a remembered
*set* per pawn and the machinery to swap between them.

- **Two job defs, not one**: `EquipSecondary` and `EquipSecondaryCombat`. The combat variant sets
  `casualInterruptible=false` and `alwaysShowWeapon=true`. Swapping under fire is a different act
  from swapping in the base, and it has its own driver.
- **Limits, four ways**, independently for melee and ranged (`SeparateModes`): slot count, absolute
  mass, mass as a fraction of carry capacity, or an explicit weapon whitelist. Per-sidearm *and*
  total, as separate settings.
- **Triggers**: melee-contact autoswitch (with "only when attacked by my current target" as a
  narrowing option), unarmed autoswitch, ranged autoswitch to a range-appropriate weapon,
  single-use-weapon autoswitch, and work-tool autoswitch for stat boosts.
- **The anti-dithering setting**: `RangedCombatAutoSwitchMaxWarmup` — past this fraction of a
  warmup, the pawn refuses to interrupt and swap. Without something like it, auto-switch pawns
  spend the fight changing weapons and never firing.
- **Selection bias sliders** (`SpeedSelectionBiasMelee` / `Ranged`): how much the picker weights
  attack speed against raw DPS. The choice of "best weapon" is a tunable, not a constant.
- **Primary weapon mode** per pawn: Ranged / Melee / By skill / By generated.
- **NPC spawning**: sidearm spawn chance, chance dropoff per additional sidearm, budget multiplier
  relative to the primary, budget dropoff. Cheap to implement, and it is what makes raiders feel
  like they were equipped rather than issued one gun.
- **Drop mode**: never / in distress / in combat / always.
- **Presets**: Disabled, Lite, Loadout only, Basic, Advanced. "Loadout only" — carrying with no
  automation — is a genuinely popular way to play, which argues for keeping carry and automation
  as separate toggles rather than one feature.

### Simple Sidearms — Switch Weapon (`Syrus.SimpleSidearmsSwitchWeapon`, 2652962609)

The manual half that Simple Sidearms leaves out, and the closest installed analogue to
Easy Weapon Switch. One drafted gizmo, expanding to buttons that switch by *intent*:

- Ranged / Melee / Unarmed / Preferred
- Long / Medium / Short range, with the target distance configurable (40 / 25 / 12 by default) and
  a mode flag choosing between "highest DPS at that distance" and "longest/shortest range weapon"
- Dangerous (explosives) / EMP / Non-lethal
- Next / Previous through the carried set
- Every button individually hideable, and keybindings for all of them

All of that machinery answers one question: *which* of the carried weapons do I want now. Under
VCO's scope — one sidearm — the question does not arise, so none of it is taken. What survives is
the smaller lesson underneath it: switching is something players do constantly and by hand, so it
belongs on a gizmo with a keybinding, not buried in a tab.

### Pocket Sand (`usagirei.pocketsand`, 2226330302)

Small, and two of its ideas are directly worth taking:

- **A weapon gizmo as an icon row**: every carried weapon plus an "unarmed" entry, left click to
  equip, right click to drop. Immediate and readable in a way a float menu is not.
- **Equip delay proportional to weapon mass** (`Delay = Mass × multiplier` ticks), applied to
  unequipping too. This is the single mechanic that makes a sidearm a decision instead of a free
  action, and it costs almost nothing to build.
- Also: a mass check when equipping off the floor, and caravan/shuttle fixes to stop weapons being
  dropped or swallowed on arrival.

**What not to take**: Pocket Sand replaces vanilla's `Equip`, `DropEquipment` and
`UnloadYourInventory` job drivers by XML `PatchOperationReplace` on `driverClass`. That is exactly
the def ownership rule 2 forbids — the last mod to load wins, silently, and the loser's mechanic
just stops existing. VCO gets the same effect with Harmony on the drivers' own methods.

### Combat Extended (`CETeam.CombatExtended`, 3495749827)

The maximal version, and mostly a catalogue of what *not* to scope into VCO.

- **`CompInventory`** (687 lines): tracks weight and bulk against `CarryBulk` / `Bulk` / `WornBulk`
  stats, and derives move speed, dodge chance, melee hit chance, work speed and an encumbrance
  penalty from the ratios. Also the weapon-swap API worth copying in shape:
  `TryFindViableWeapon` / `SwitchToNextViableWeapon` / `TrySwitchToWeapon(newEq, stopJob)`.
- **`Loadout`** = a list of `LoadoutSlot`s. A slot is either a specific `ThingDef` or a
  `LoadoutGenericDef` — a def carrying a `Predicate<ThingDef>` and a `ThingRequestGroup`, generated
  at startup for meals, raw food, drugs, and one per gun's ammo. That predicate-def pattern is how
  CE avoids a per-item patch for every modded gun, and it is the same instinct as VCO's rule 6.
- Slot count types: `pickupDrop` vs `dropExcess`. Loadouts support inheritance (`parentID`),
  ad-hoc generation, `dropUndefined`, and auto-added "basic" slots.
- **`Utility_HoldTracker`** (525 lines): remembers items a pawn picked up that the loadout does not
  cover, so opportunistic pickups are not instantly dropped again. There is a `TicksBeforeDropRaw`
  of 40000 before the pawn gives up on them.
- **`JobGiver_UpdateLoadout`**: priority-driven, throttled to one evaluation per 1800 ticks per
  pawn, proximity search at radius 20 before widening to 80.
- UI: `Dialog_ManageLoadouts`, `MainTabWindow_OutfitsAndLoadouts`, `PawnColumnWorker_Loadout`,
  `PawnColumnWorker_MassBulkBars`, plus save/load dialogs writing loadouts to files.

CE can afford all of this because it owns the entire item economy — every weapon, every piece of
apparel and every ammo type is CE's def. VCO owns none of that, so bulk in particular is off the
table: it would mean a `Bulk` value on every item in every mod the player has, i.e. exactly the
per-item compatibility patching rule 6 exists to avoid.

---

## 2. The design

What §6 phase 1 built, and what phase 2 would have. Written in the present tense because it is the
design, not a history; §9 says why the last step of it is not there.

**Scope, stated first, because the reference mods are all much larger than this.** A pawn carries a
main weapon and one sidearm, and the player can switch between them. That is the feature. Weapon
roles, intent buttons ("give me something for short range"), DPS-at-range resolution, next/previous
cycling, per-pawn weapon-mode preferences and split melee/ranged budgets are all *out* — every one
of them exists in Simple Sidearms or Switch Weapon to solve the problem of choosing among four or
five carried weapons, and with one sidearm there is nothing to choose. Anything below that a
one-sidearm design does not need has been cut rather than kept "for later".

### 2.1 Carrying — one main weapon, one sidearm

The main weapon is vanilla's `Pawn.equipment.Primary`, untouched. The sidearm is a single weapon in
vanilla's `Pawn.inventory`, remembered by a per-pawn `CompSidearm` so it is not confused with a
weapon the pawn happens to be hauling.

- **One sidearm slot.** Not a slot count setting, not a mass budget, not bulk — one. Vanilla's
  existing mass-based carry limit still applies underneath, so a pawn cannot pocket a minigun and
  keep walking at speed.
- **No mass cap.** One sidearm is already a limit, and vanilla's carry mass does the rest. A cap is
  balance tuning rather than a mechanic; §3 records what it would cost later.
- **No weapon classification at all.** Any weapon can be the sidearm. The `WeaponRoleDef` classifier
  from the earlier draft is gone: it existed to answer "which of these is the pistol", which is not
  a question a one-sidearm design ever asks. That also removes an entire compatibility surface —
  nothing has to decide what an unrecognised modded gun *is*.

### 2.2 Switching

**Cost.** A swap is a job with a delay, not an instant action — Pocket Sand's model, made moddable:
a per-weapon term from mass, divided by a `VCO_WeaponSwapSpeed` StatDef on the pawn (default 1,
movable by manipulation, traits and other mods via StatParts). Without a cost, a sidearm is a
strictly free upgrade and the mechanic has no downside to trade against.

Two job defs, following the one structural idea worth taking from Simple Sidearms:

- `VCO_SwapWeapon` — undrafted, casually interruptible.
- `VCO_SwapWeaponCombat` — `casualInterruptible=false`, `alwaysShowWeapon=true`. Swapping under
  fire is a different act from swapping in the base.

**The gizmo is one button** — designed, not built (§9). `Command_SwitchWeapon`, showing the *other* weapon's icon: click it and
the two trade places. That is the entire interaction. Right-click drops the sidearm. It shows on
undrafted pawns too — swapping in the base is a normal thing to want, and making the gizmo the only
switch surface is what lets §2.3 drop its Gear-tab patch. It carries a keybinding, and multi-select
applies to the whole selection the way
`Command_SetHeightTarget` already does through `InheritInteractionsFrom`. With two weapons there is
no row to browse and no menu to open — a toggle is the correct shape, and it is one gizmo slot
rather than nine.

Undrafting does not force a weapon back. The pawn keeps whatever is in hand; the player swapped
deliberately and should not have it silently undone.

### 2.3 How the player equips

There is no "primary slot" and "secondary slot" to fill. The main weapon is equipped exactly as in
vanilla. The sidearm is designated when the weapon is picked up.

1. **Right-click a weapon on the ground.** Vanilla's `FloatMenuOptionProvider_Equip` already offers
   "Equip X"; VCO adds one line beneath it — "Carry X as sidearm" — from its own
   `FloatMenuOptionProvider_EquipSidearm`. In 1.6 these providers are found by subclassing; CE's
   `FloatMenuOptionProvider_PickUp` is the working example. No vanilla def, provider or method is
   modified (rule 2), and a player who never opens the settings still finds the feature, because it
   appears where they already right-click.
   When the pawn already has a sidearm, the option reads "Carry X as sidearm (replaces Y)". When the
   pawn cannot equip that weapon at all — biocoded, persona-bonded, or unreachable — the option is
   shown **disabled with the reason** rather than hidden: CE's convention, and the difference
   between looking strict and looking broken.
2. **From storage.** The same right-click on a stockpiled weapon queues a walk-and-collect. To a
   float menu the ground and a shelf are the same click, so this is the same provider, not a
   second one.

Both issue a `VCO_SwapWeapon` job, so the delay applies uniformly and exactly one code path moves
equipment (`WeaponSwapUtility`).

**There is no Gear tab button**, though an earlier draft of this plan had one. Its only
justification was that the switch gizmo was drafted-only, leaving undrafted pawns no way to swap.
Showing the gizmo on undrafted pawns as well removes that gap, and with it a postfix on a private
vanilla drawing method whose signature is nobody's contract — a bad trade to make for a button that
duplicates a gizmo. Dropping is already vanilla on every inventory row.

**Downing and death** leave the sidearm in inventory, where vanilla already drops it with the corpse.

---

## 3. Deferred to future development

Not built, not settings-gated, not stubbed. Recorded here so the reasoning survives, and so the
build above does not quietly foreclose any of it. Each entry names what it would cost if picked up
later, because that is the only part that is hard to reconstruct.

**A sidearm mass cap** (`VCO_SidearmCapacity`). One sidearm is already a limit; a cap on top is
balance tuning, not a mechanic. If added later it is a StatDef plus one check in `SidearmUtility`,
and the check point exists from day one — nothing has to be rearranged for it.

**Auto-switching.** A pawn in melee contact drawing a melee sidearm by itself. Cheap to *decide*
under a one-sidearm design — with nothing to choose, a trigger is a yes/no — but the governor is not
cheap: a per-pawn cooldown, a refusal to interrupt past a fraction of a warmup, and no re-firing for
the same cause without an intervening state change. Auto-switch without a governor produces pawns
that swap forever and never attack, so the governor is most of the work and would be built first.

**NPC sidearms.** A spawn chance and a budget as a multiple of the primary's value, plus a
drop-chance control — giving every raider a second weapon doubles the loot. Two settings and a
postfix on weapon generation. Worth noting that without auto-switching an NPC sidearm is loot and
flavour rather than behaviour, so the two are best considered together.

**Loadouts.** The full CE-shaped system: named lists of generic slots, an Assign-tab column, a
throttled jobgiver, a hold tracker. Sidearms are deliberately *not* a loadout concern under the
scope above — the sidearm is one weapon the player picks per pawn, not a rule to satisfy — so
nothing built now needs revisiting if loadouts arrive later. What made this worth deferring: CE can
afford loadouts because it owns the item economy; VCO owns none of it, and a thin copy of a system
players can already install whole is the weakest thing in the roadmap.

**Keeping sidearms through a caravan.** Vanilla's unload-on-arrival empties pawn inventories into
the settlement, and a sidearm lives in the inventory, so a caravan currently disarms itself walking
in. Pocket Sand ships a fix; VCO does not yet. Doing it right means `Pawn_InventoryTracker`'s
`FirstUnloadableThing` and `HasAnyUnloadableThing` staying consistent with each other — patch one
and not the other and the unload job loops forever, which is a worse bug than the one being fixed.
Worth doing, not worth doing carelessly.

`VCOSettings.enableLoadouts` stays as it is — present, locked off, `Unbuilt`. That is what the flag
is for, and the README roadmap already says so.

---

## 4. Compatibility

**Mutual exclusion, declared loudly.** If Simple Sidearms, Combat Extended or Pocket Sand is loaded,
VCO's sidearm system disables itself at startup and says so in the Diagnostics tab. Two mods both
moving weapons between equipment and inventory and both patching equip jobs do not merge; they
produce weapons that vanish. This follows the existing `PatchGuard` / `VCODiagnostics` pattern —
detect by `packageId`, report, stand down.

**Combat Extended is incompatible with this mod outright**, not merely with this feature.
`About.xml` already declares that in `incompatibleWith`, so RimWorld warns the player in the mod
list. The sidearm standdown list keeps CE in it anyway, for a player who loads both regardless of
the warning.

**Pick Up And Haul must keep working**, and does. PUAH stuffs haulage into the same inventory and
tracks what it put there in a comp of its own; its unload job walks that record and drops what is in
it. A weapon VCO stows never enters that record, so in the ordinary case the two never meet. The one
case that does is a weapon PUAH hauled first and the player designated as a sidearm second: PUAH
would carry it back to a stockpile and the sidearm would vanish off the pawn. `PickUpAndHaulUtility`
takes the weapon out of PUAH's record on designation, by type name rather than assembly reference,
and is inert when PUAH is not installed.

Worth knowing for anything built later: PUAH transpiles `ITab_Pawn_Gear.DrawThingRow`, which is a
second reason §2.3 was right to drop its Gear-tab patch.

---

## 5. Layout

Following the naming conventions in README.md — flat namespace, one public type per file,
`Patch_<DeclaringType>_<Method>`, static helpers end in `Utility`. This is the whole surface of the
build; there is no scaffolding here for anything in §3.

```
Features/Loadout/
  CompSidearm.cs                            The one remembered sidearm (with CompProperties_Sidearm)
  SidearmUtility.cs                         Eligibility, comp attachment, conflicting-mod standdown
  WeaponSwapUtility.cs                      Equip/stow — the one place equipment moves
  JobDriver_SwapWeapon.cs                   Delay, interruptibility, the combat variant
  VCO_JobDefOf.cs                           The two job defs
  FloatMenuOptionProvider_EquipSidearm.cs   "Carry X as sidearm" right-click option
  Command_SwitchWeapon.cs                   The toggle gizmo                        (phase 2)

1.6/Defs/JobDefs/Jobs_Sidearms.xml   VCO_SwapWeapon, VCO_SwapWeaponCombat
1.6/Defs/StatDefs/Stats_Combat.xml   VCO_WeaponSwapSpeed
Testing/Loadout/                     SidearmAssertions
```

Seven files and two def entries; the comp is attached in code, so no race def is patched. State
lives on the pawn comp; nothing static and mutable crosses a patch boundary (rule 4). **No Harmony
patch at all** — the float menu is an added provider, the jobs are added defs, the comp is added at
startup. There is nothing here that a RimWorld update can silently break, which is why the feature
declares no `PatchGuard`.

The folder keeps the name `Features/Loadout/` it already has, so §3's loadouts have somewhere to
land without a move.

---

## 6. Phases

| Phase | Contents | State | Unlocks |
|---|---|---|---|
| 1 | `CompSidearm`, the swap job with its mass-based delay, `VCO_WeaponSwapSpeed`, and the equip routes: right-click on the ground or in storage | **Built.** 13 checks green in the arena suite | — |
| 2 | The switch gizmo and its keybinding | **Paused, see §9** | `enableSidearms` |

Phase 1 carries the whole mechanic and no way to trigger it by hand: a pawn can be given a sidearm,
and the swap works, but with no gizmo there is nothing to click. `enableSidearms` therefore stays a
locked roadmap toggle — a switch must never imply an effect that is only half there, and with the
work paused at §9 that is exactly what it would be.

The two debug actions remain the only way in: **"Sidearms: enable for this session"** turns the
feature on until the next time mod settings are opened, and **"Sidearm checks"** runs the suite on
the current map.

## 7. Tests

`Testing/Loadout/SidearmAssertions.cs`, wired into the headless run and into the "Sidearm checks"
debug action. Thirteen checks, all green:

- The delay scales with mass, tracks the setting, and has a floor no weapon draws under.
- A weapon on the map can be taken as a sidearm, and doing so does not disturb the equipped weapon.
- A swap trades the pair; swapping back restores the original; 500 swaps in a row neither duplicate
  nor destroy a weapon.
- A second sidearm replaces rather than accumulates, and the displaced one is dropped, not destroyed.
- Dropping clears the designation and leaves the weapon on the map.

The live checks generate their own pawn, discarding any incapable of violence — that pawn cannot
hold a weapon at all, which is a real rule but not the one under test, and letting the roll decide
would make the suite flaky.

Still to cover, and both need phase 2 or a running job: **a swap ordered mid-fight is not casually
interrupted**, and **an interrupted swap never leaves the weapon in neither hand nor inventory**.
That second one is the failure mode this whole design is arranged to prevent, and it deserves a
check that drives the real job rather than the utility underneath it. Also outstanding: save,
reload, and confirm the designation survives.

## 8. Decisions still open

1. **Does the swap delay apply undrafted too?** Built as yes, everywhere, which is Pocket Sand's
   model and the simpler rule. Restricting it to drafted pawns would make base management
   frictionless and keep the cost where it matters; it is a one-line change in
   `WeaponSwapUtility.SwapTicks` if that reads better in play.

---

## 9. Why this is paused

The arithmetic that stopped it, recorded here because it is the kind of thing that looks like
cowardice a year later if the reasoning is not written down.

**The audience is the problem, not the code.** §4 has VCO standing down whenever Simple Sidearms is
loaded, which is the right call — two systems moving weapons between equipment and inventory lose
weapons. But it means this feature is only ever visible to players who do *not* run Simple Sidearms.
A poll of 476 players put Simple Sidearms first at 339 votes. Those two facts together say the
system was built for the slice of the sidearm audience least likely to ever see it.

The poll's limits are worth stating fairly: it asks which sidearm mod is best among people who
already run one, about a mod that is eight years old and deeply embedded. It does not prove an
integrated sidearm system could not be good. It does say that displacing Simple Sidearms is not a
realistic goal, and that coexisting with it is.

**What survives the argument.** One piece of this work has no equivalent in Simple Sidearms: the
swap *cost*. Simple Sidearms swaps are effectively free, which is the standing complaint about it as
a combat mechanic rather than an inventory one. Pocket Sand has a mass-based delay but no memory and
no automation. VCO already has `VCO_WeaponSwapSpeed` as a real StatDef with a mass term — moddable
by hediffs, traits and genes like everything else here — and that is orthogonal to which mod
*manages* the weapons.

That points at inverting the compatibility relationship: rather than standing down when Simple
Sidearms is present, apply a universal swap-time cost to any weapon change — vanilla's equip job and
Simple Sidearms' `EquipSecondary` / `EquipSecondaryCombat` alike. It is a small feature, it is
unmistakably this mod's kind of mechanic, and it grows more valuable the more players run Simple
Sidearms rather than less. Unverified, and the thing to check first: whether those job drivers take
an added delay cleanly.

**What "dormant" means concretely.** Phase 1 stays in the tree, builds, and passes its thirteen
checks in every headless run, so it cannot rot unnoticed. `enableSidearms` stays locked, so no
player can switch on a system with no way to trigger it. Nothing needs undoing to resume, and
nothing needs undoing to delete it either.
