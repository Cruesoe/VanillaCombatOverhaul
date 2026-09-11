# Ammunition — design

Status: **design in progress, not started.** `enableAmmo` exists, is locked off by
`ForceUnbuiltOff()`, and already has its settings section, intro copy and yield slider written.

This document is about how ammunition *plays*. Implementation is deliberately confined to
Appendix A, and nothing in the design below depends on it.

Read alongside the six design rules in [README.md](../README.md). Rule 6 — *generic categories
over per-item patches* — was written for this feature, and the settings copy already commits to
it in the player-facing text: ammunition is "grouped into a few simple types by tech level and
damage, rather than a separate calibre for every gun."

---

## 1. What ammunition is for

Vanilla combat is free. A colonist with a rifle can fight every raid of the year without
consuming anything but medicine and time, so the entire cost of defence is paid up front, once,
when the gun is bought or made. That is why late-game raids stop being a resource problem and
become only a risk problem.

Ammunition makes shooting cost something per shot, and that produces three things vanilla lacks:

1. **A running cost of defence.** Raids draw down a stockpile, so the war has a budget.
2. **A reason to care about weapon choice beyond DPS.** A minigun is not just a strong gun, it is
   an expensive one — it eats four times as many rounds per second as a rifle.
3. **A rhythm inside a firefight.** Reloading is dead time. It creates a moment where a pawn is
   out of the fight, which is a tactical event vanilla has almost none of.

The feature fails if it delivers those and also delivers a hauling chore, a micromanagement tax,
or a colony that quietly cannot shoot back. Section 9 is about avoiding that.

## 2. What the other mods decided

Read from the installed copies, not from memory. Seven ammo-adjacent mods are subscribed on this
machine; **none is active**, and VCO is. So there is nothing to displace on day one.

| Mod | Workshop id | Active | The decision it made |
|---|---|---|---|
| Combat Extended | 3495749827 | no | Per-calibre ammo, where the projectile belongs to the round rather than the gun. Maximum depth, and the reason CE ships ~700 per-mod patch folders. |
| The Generic Ammo Experience for CE | 3284460811 | no | An entire mod devoted to making CE's *generic* mode coherent. Evidence for where the audience actually is. |
| Yayo's Combat 3 | 2854006492 | no | Nine items — three tech tiers × plain/incendiary/EMP — classified in code, so any modded gun works unpatched. The closest thing to what VCO wants. |
| [LTS] Simple Ammo Pack | 2803605709 | no | Tier buckets with per-weapon include/exclude overrides as **defs**, so third parties can correct their own guns. Its core mod is not installed here, so it is inert. |
| Ammo Readout | 3788225671 | no | A HUD overlay for CE ammo. Will not read VCO's. |
| Vibrant Tracers for CE | 3357296805 | no | Colours projectiles by ammo type. VCO already ships the vanilla equivalent. |
| Gunplay | 2034896549 | **yes** | Cosmetic only. No ammo. |

Three conclusions carry into the design:

- **CE proved per-calibre is too much for many players** — it built a generic mode itself, and a
  second mod exists to finish that mode. VCO starts generic and never grows calibres.
- **Yayo proved classification can be derived, not authored.** No per-weapon mapping exists in
  that mod at all, and modded guns work anyway. That is rule 6 in someone else's shipped code.
- **LTS proved the escape hatch should be a def.** Automatic classification is right for the
  vast majority and wrong for a handful; the correction belongs in XML anyone can write.

And one warning: everything else CE hangs off ammo — autoloaders, ammo containers, cook-off,
casings, mech ammo, give-ammo jobs, a caliber stat — is scope that looks adjacent and is not
required to make guns need bullets. Section 10 keeps it out.

---

## 3. The loop

Ammunition lives in three places, and the whole design is about the movement between them.

```
   stockpile  ──(pawn takes a combat load)──>  inventory  ──(reload)──>  magazine  ──(fire)──> gone
       ^                                                                                        │
       └──────────────── crafted, bought, or looted from the dead ──────────────────────────────┘
```

- **Stockpile → inventory** happens once, when a pawn picks up a weapon or tops up between
  fights. The player never hauls ammo to a pawn by hand.
- **Inventory → magazine** is the reload: a few seconds, done automatically when a pawn is idle
  or safely drafted, and manually on demand.
- **Magazine → gone** is one round per shot fired, never per burst.

If the player is ever moving ammunition by hand, the design has failed. The colony's job is to
*produce* ammunition; the pawns' job is to carry and load it.

---

## 4. Buckets

Five kinds of ammunition, for every gun in the game and every gun any mod adds:

| Bucket | Who uses it | Where it comes from |
|---|---|---|
| **Primitive** | bows, pilums, muskets | crafting spot, from wood and steel |
| **Industrial** | every conventional firearm | machining table, from steel and chemfuel |
| **Spacer** | charge weapons, needle guns | fabrication bench, from plasteel and components |
| **Incendiary** | flamethrowers, incendiary launchers | any tier, gated behind its vanilla research |
| **Pulse** | EMP launchers | any tier, gated behind its vanilla research |

A weapon's bucket is worked out from its tech level, with damage type taking priority — anything
firing flame wants incendiary, anything firing EMP wants pulse, whatever era it comes from. An
unrecognised modded gun therefore lands in a bucket rather than needing a compatibility patch,
and a mod author who disagrees with where it landed can override it in XML without touching our
code.

**Why five and not fifty.** Every additional bucket multiplies three things: items in the
stockpile menu, bills on the crafting bench, and ways for a colony to be holding the wrong
ammunition when a raid arrives. Five is enough that a tribal start, an industrial colony and a
spacer arsenal each have their own supply chain, and few enough that the resource readout does
not become a spreadsheet.

**Weapons that are their own ammunition** — grenades, single-use launchers, mortars with their
shells — are left alone entirely. They already cost something per use.

---

## 5. Magazines and the reload rhythm

The design target: **a reload roughly every 20 to 30 seconds of sustained fire.** Often enough
to be a real interruption, rare enough that a firefight is not spent reloading.

Vanilla's own numbers make this derivable rather than a per-weapon guess. Taking burst size ×8,
against each weapon's actual rate of fire:

| Weapon | Shots/sec | Magazine | Seconds of fire per magazine |
|---|---:|---:|---:|
| Bolt-action rifle | 0.31 | 8 | 26 |
| Sniper rifle | 0.20 | 8 | 40 |
| Pump shotgun | 0.47 | 8 | 17 |
| Revolver | 0.53 | 8 | 15 |
| Assault rifle | 0.99 | 24 | 24 |
| Heavy SMG | 1.03 | 24 | 23 |
| Charge rifle | 0.88 | 24 | 27 |
| LMG | 1.51 | 48 | 32 |
| Minigun | 4.17 | 200 | 48 |

That lands almost the whole arsenal in the target band from one rule, which is the argument for
deriving magazine size rather than authoring it: it holds for modded guns too. The outliers —
revolver and shotgun a little fast, minigun a little slow — are worth a per-tier floor rather
than per-weapon tuning.

**Reload time** scales with the magazine, a couple of seconds for a pistol up to a genuinely
punishing dozen for a minigun belt. A minigun should cost something between bursts as well as
during them.

**Bows have no magazine.** A quiver is not a magazine, and a one-round magazine makes the meter a
light that is either on or off and the auto-reload threshold meaningless. Bows and other
single-shot primitive weapons draw straight from the pawn's carried arrows: the meter shows what
is in the quiver, there is no reload interruption, and running out means going to get more. This
resolves the open question from the previous draft, and it happens to be the more honest model of
what a bow is.

---

## 6. Running dry

The moment the feature exists for. When a pawn's magazine empties and there is nothing in their
inventory to refill it:

1. **They stop shooting.** Not "shoot slower", not "shoot for free" — the gun is silent.
2. **They fall back to melee.** A dry ranged weapon no longer provides an attack.
3. **The player is told**, by name, once — not a repeating alert per pawn per second.

Running dry leaves a colonist holding a paperweight, so the player's counterplay is logistical:
produce enough ammunition, keep it accessible and make sure fighters carry enough before combat.

**A colony can never be permanently disarmed.** Primitive ammunition is craftable at a crafting
spot from wood, with no research and no power. However badly a colony mismanages its supply, it
can always fall back to bows within a day. That floor is deliberate: an ammunition system that
can brick a save is not a difficulty setting, it is a bug.

---

## 7. What a war costs

The numbers that make this a design rather than a vibe. A colonist fires for perhaps 60–120
seconds of a raid, which at the rates above is:

- **~90 rounds** per rifleman per raid
- **~450–550 rounds** for a six-shooter defence line
- **plus ~250 rounds a minute** for anyone holding a minigun

So a raid costs roughly **500 rounds**, and a quadrum of raids somewhere near **2,000**. Those
are the numbers crafting and trade have to serve.

**Crafting** should therefore produce ammunition in hundreds per bill, not dozens — closer to a
chemfuel batch than a weapon. A bill yielding ~100 rounds means a raid costs about five bills'
worth of work, which is a real but unremarkable line item next to meals and medicine. The
existing `ammoYieldFactor` slider is the taste dial on top.

**Materials** should be cheap and boring: steel and chemfuel for industrial, wood for primitive,
plasteel and components for spacer. At roughly 10 steel per 100 rounds, a 500-round raid costs
about 50 steel — less than a turret, more than nothing. Enough that a minigun colony notices;
not enough that anyone stops using guns.

**Trade and loot** matter as much as crafting. Combat suppliers and outlanders stock every
bucket, and dead raiders drop a fraction of what they were carrying — a repelled raid should
partly pay for itself. A colony under siege that cannot craft should still be able to buy or
loot its way back to a stockpile.

---

## 8. Hostiles

Raiders do not run the colonist's think tree, so their ammunition is a decision, not a
consequence. Yayo treats this as a first-class setting and that is correct.

**CE arms its raiders, and has no enemy exemption at all.** Worth knowing precisely, because it
is the closest thing to a proven answer:

- Pawns spawn with a loaded weapon plus spare magazines — typically **2–5**, some kinds 6–14, a
  few 10–30, with a floor of around 20 rounds for low-magazine weapons.
- They reload mid-fight from their own inventory, through a tactical AI comp that checks the
  magazine before every shot.
- When genuinely dry they switch to melee and, unlike colonists, may fall back to grenades.
- Leftover ammunition stays on the corpse and is recovered by stripping.

The part VCO cannot copy is the cost: CE carries **279 XML files of pawnkind loadout properties**
to do it, one authored ammunition budget per raider type across vanilla, the DLCs and hundreds of
mods. Rule 6 forbids that. But the *shape* is portable if the issue is derived rather than
authored — three to five magazines of whatever that weapon's magazine already is, which costs
nothing per pawnkind and covers modded raiders unpatched. That makes "issued" much cheaper than
it first appears, and weakens the case for "free" as the default.

Three modes:

- **Free** *(default)* — hostiles never run dry. Ammunition is purely a colony logistics
  feature, and raids play exactly as they do now.
- **Issued** — hostiles arrive with three to five magazines, reload from their own inventory, and
  fall back to melee when dry. CE's model, with the budget derived from the weapon
  instead of authored per pawnkind. Raids lose their late-fight shooting power, which favours
  defensive play and long fights.
- **Strict** — everyone obeys the same rules.

Free is the default because it is the only mode that cannot make the game *easier* by accident,
and because the feature's whole subject is the colony's supply chain, not the enemy's. It has a
real cost, though — most players will never see ammunition change how a raid goes — and that
tension is unresolved (§12).

Whatever the mode, the dead drop part of what they carried.

---

## 9. What the player actually does

The interaction budget for this feature is close to zero. Everything below exists to keep it
there.

**Per pawn, once:** nothing. Equipping a gun takes a combat load of ammunition with it.

**Per pawn, occasionally:** drag the ammo meter to say "reload when you get down to about here."
That single gesture is the whole customisation surface, and it is CE's, which is where the good
version of this interaction already exists.

**Per fight:** nothing, ideally. Pawns top up between contacts on their own, and refuse to do it
while something is shooting at them — reloading in the open at the wrong moment is the failure
this guard exists to prevent. A manual reload button covers the case where the player knows
better.

**Per colony:** a crafting bill, and an alert when the stockpile runs low.

The meter itself is a bar over the drafted pawn: rounds remaining out of magazine size, the
bucket's name, and a draggable threshold. It shows only for player-controlled pawns; an ammo bar
over a visitor or a mental-breaking colonist is noise.

**Deliberately absent**: choosing which ammunition to load (there is only one per gun), managing
per-weapon loadouts, and any hauling job the player has to think about.

---

## 10. Failure modes

Named, because each one has killed an ammunition mod somewhere:

| Failure | What it looks like | The answer |
|---|---|---|
| **Micromanagement tax** | Player babysits reloads and hauling every raid | Auto-take on equip, opportunistic top-up, one threshold gesture, no ammo selection |
| **Silent disarmament** | Colony discovers mid-raid that nobody can shoot | Low-stock alert, primitive ammo always craftable, raider drops |
| **Reloading under fire** | Pawns wander off to reload while being shot at | Distance and post-fight guards on automatic reloads |
| **Hunter waste** | A hunter empties a magazine into a squirrel | Ammunition applies to hunting — that is the point — but hunting weapon choice becomes a real decision |
| **Stockpile clutter** | Five new items spamming the resource readout and hauling queues | One category, five items, large stack sizes |
| **Wrong bucket** | A modded gun classified as spacer and starved | Classification logged on startup; override defs anyone can write |
| **Scope creep** | Autoloaders and cook-off before the base feature ships | §11 |

---

## 11. Not in this feature

Deferred so the boundary is explicit:

- **Ammo variants** (AP / HP / incendiary for the same gun). The largest multiplier on content,
  UI and player decisions, and what forces CE's per-weapon ammo sets. This is the one deferral
  that would be a genuine redesign rather than an addition.
- **Bulk and encumbrance.** Ammunition costs mass and nothing else. Real loadout pressure needs a
  bulk stat that every apparel item in the game would need a value for.
- **Turret and mech ammunition.** Cheap to bolt on, and it changes base defence economics
  substantially — turrets currently become the *only* defence with no running cost, which is
  arguably backwards. Its own toggle, later, if at all.
- **Cook-off, casings, autoloaders, ammo containers, ammo-giving jobs.** CE features that hang
  off ammunition rather than constituting it.
- **Arrow recovery.** A nice touch. Not ammunition.

---

## 12. Open questions

- **Free hostile ammo as the default.** Safe, and it means most players never see the feature
  affect a raid. Is "issued" the better default for a mod whose audience opted into a combat
  overhaul?
- **Magazine floors.** Derived sizes put the revolver and pump shotgun below the target band. Per
  tier floor, per weapon exception, or leave it — a shotgun reloading often is arguably correct.
- **Does hunting deserve an exemption?** Consistency says no. A colony that cannot afford to hunt
  is a different and less interesting problem than one that cannot afford to fight.
- **Turrets free of ammunition** while pawns are not, for as long as §11 holds. That is a real
  balance statement, not a neutral omission.
- **How loud is the low-stock alert?** Too quiet and the colony is disarmed by surprise; too loud
  and it is another thing to dismiss every quadrum.

---

## Appendix A — implementation notes

Verified against the installed 1.6 assembly and CE's source; recorded so the design work above
does not have to be redone when it is time to build.

- **Reloading is vanilla's.** RimWorld 1.6 ships the whole pipeline behind
  `RimWorld.Utility.IReloadableComp` — `ReloadableUtility.MakeReloadJob` / `FindEnoughAmmo`,
  `JobDriver_Reload`, `FloatMenuOptionProvider_Reload`, and `JobGiver_Reload`, the last already
  in the humanlike think tree at priority 5.9. A comp implementing that interface gets hauling,
  the right-click option, the reload job and idle top-up with no new job def, work giver or
  think-tree patch. Note the interface carries **one** `AmmoDef` per weapon: buckets fit it,
  variants (§11) would not.
- **The meter is ours.** Vanilla's `CompApparelReloadable` contributes only a `LabelRemaining`
  string, no gizmo. But `Gizmo_Slider` is a vanilla base class — CE's `GizmoAmmoStatus`
  subclasses it — so the bar, header and drag handling are engine code.
- **Attaching the comp** is done by appending comp properties to ranged weapon defs at startup,
  the same additive trick `HeightTargetingUtility` already uses. Adding a
  comp is not rewriting a def, so rule 2 holds.
- **Hostile reloading** cannot ride on `JobGiver_Reload`, which is colonist-only — hence §8.
- **CE's reload options worth copying**: `OpportunisticReloadMode` (Off / DraftedOnly / Any), a
  per-weapon `TryReloadOn` threshold set by dragging the meter, `OpportunisticReloadSafeDistance`
  and `SecondsAfterFightToOpportunisticReload` as the two timing guards. Not copied: right-click
  ammo select and auto-reload-on-ammo-change, which exist only to choose between variants.
- **Compatibility.** VCO stands down entirely — with the toggle greyed and a reason shown — when
  `CETeam.CombatExtended`, `Mlie.YayosCombat3` or `LimeTreeSnake.Ammunition` is active, using a
  central conflicting-mod check.
  Gunplay and Holsters are cosmetic and not conflicts. Ammo Readout will not display VCO
  ammunition; that is theirs to patch, and worth naming in the mod description.

## Appendix B — what the arena suite must prove

- Every vanilla ranged weapon lands in a bucket, and the expected one for a named sample.
- Grenades, mortars and single-use launchers get no ammunition at all.
- N shots consume exactly N rounds — per shot, not per burst.
- An empty weapon stops firing, and the pawn falls back rather than standing idle.
- A pawn with rounds in inventory reloads without a hand-issued job.
- The threshold is obeyed and survives save/load.
- Opportunistic modes gate correctly, and neither reloads inside the safe distance or before the
  post-fight delay.
- A bow consumes carried arrows, with no magazine and no reload interruption.
- With a conflicting mod active, or with `enableAmmo` off, nothing is attached and nothing is
  craftable.
