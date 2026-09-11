# Vanilla Combat Overhaul (VCO)

Depth for RimWorld 1.6 combat, without replacing it.

## Features

Shipped and verified in-game:

| Feature | What it does |
|---|---|
| **Parry** | An armed defender can turn an incoming melee attack aside. Chance is contested between the defender's skill and the attacker's, so a better fighter beats a parry. Attacks from behind are never parried. |
| **Counter-attack** | A successful parry lets the defender strike back immediately. Counters never chain. |
| **Parry budget** | Caps parries per short window, so being surrounded overwhelms you. Negligible one-on-one, material against six. |
| **Directional damage** | Where a hit lands depends on which side it arrives from. Flanking exposes different body parts; frontal attacks are unrestricted. Applies to ranged fire and, separately toggled, to melee. |
| **Height targeting** | Drafted colonists pick None / Legs / Torso / Head. NPCs roll a random height on spawn. Skill raises the chance of landing in that band; a miss still hits that side. |
| **Leftover armor** | Armor that exceeds a hit's penetration still stretches leftover rating into extra protection. Weapons show 2× AP on the inspect card. |
| **Advanced accuracy** | Skilled shooters partially overcome penalties from a poor weapon at range and from bad weather. Cover and smoke are unaffected. |
| **Evasion** | Moving pawns are harder to hit; standing still gives no benefit. Optional skill contest lets good shooters track runners. |
| **Firing arc** | Missed shots spread wider with distance. All six Reloaded miss-spread distributions ship; type 0 is the default. |
| **Bullet and arrow wounds** | Stopping-power fragmentation / pass-through / mushrooming for bullets, and arrow split/internal hits. Intercepts vanilla injury application instead of replacing DamageDef workers. |
| **Visible tracers** | A short glowing streak behind projectiles, coloured by damage type. |
| **Apparel coverage** | Reloaded's coverage pack: hands/feet, acid-as-heat, thump-as-blunt, glasses with helmets, masks, headsets. Settings-gated; most need a restart. |
| **Automatic primary weapons** | Colonists choose from the weapons allowed by their assigned policy. Damage, cycle time, accuracy, penetration, condition and pawn skills affect the choice; a configurable upgrade margin prevents churn. |

On the roadmap, locked off in settings until built: suppression, ammo as tech-tier buckets,
and full item loadouts. Automatic primary-weapon selection is shipped separately.

## Vanilla Combat Reloaded coverage

Every live VCR combat toggle and every apparel/XML patch now ships in VCO. Reloaded's
beam damage worker is commented out in 1.6 and was not ported.

| Reloaded surface | In VCO | How it differs |
|---|---|---|
| Parry | Yes | Same curve. VCO adds a StatDef, a prefix instead of a local-slot transpiler, a 2/sec budget, and a free counter. |
| Advanced armor | Yes | Same leftover stretch and 2× displayed AP. |
| Advanced accuracy | Yes | Same skill mitigation of weapon and weather only. |
| Evasion | Yes | Same `0.8^(speed − 2.5)` model. |
| Firing arc (types 0–5) | Yes | Type 0 remains the default. |
| Directional flanking + melee flanking | Yes | Same side restriction. VCO's seeder also lets a flank land on the torso. |
| Height targeting gizmo | Yes | Same None / Legs / Torso / Head command. A missed band still hits that side. |
| Bullet and arrow damage workers | Yes | Same wound shapes, without swapping `workerClass` on Bullet/Arrow. |
| Shot / melee inspect readout | Yes | Vanilla vs skill-adjusted factors, evasion, side, height, parry. |
| Apparel coverage pack (9 patches) | Yes | Settings-gated copies; most need a restart. |

VCO-only: parry budget, counter-attack, visible tracers, automatic primary weapons, combat
StatDefs, PatchGuard, and the arena suite. Still to build, and not in Reloaded either:
suppression, ammo, and full item loadouts.

## What Vanilla Combat Reloaded gets wrong

VCO keeps VCR's parry maths, which is field-tested and good. These are the faults it does not
inherit. Line references are to VCR 1.5.

- **A transpiler pinned to local-variable slots.** VCR injects parry into `Verb_MeleeAttack.TryCastShot`
  by finding the `GetDodgeChance` call, counting forward exactly two instructions, and emitting
  hardcoded `Ldloca_S 0` / `Ldloca_S 6`. Any recompile that reorders locals breaks it, and a
  Harmony transpiler that misses its target fails *silently*. VCO uses a plain prefix.
- **A null returned from a success path.** VCR's directional hit picker could pass its coverage
  roll and still return null when no part existed at the requested height, which the arrow
  damage worker then dereferenced. In VCO null only ever means "no opinion", and the caller
  keeps vanilla's result.
- **A NaN divide.** VCR computed a coverage ratio over body parts, dividing by a total that is
  zero when a side has no coverage left. VCO uses a single weighted pass with no division.
- **The torso could not be flanked.** VCR's body-part seeder gave `corePart` the Center group
  only, so a flanking hit could never land on it. VCO's seeder adds all three groups to the
  core part.
- **A tooltip that disagrees with the code.** VCR has two copies of the "has a weapon" gate.
  The live one reads `!(target.equipment?.HasAnything() ?? false)`; the tooltip one reads
  `!target.equipment?.HasAnything() ?? false`. `!` binds tighter than `??`, so on a pawn with no
  equipment tracker at all -- animals, most mechs -- the tooltip advertises a parry chance for
  something that can never parry. VCO puts the requirement in the stat itself, so the info card
  and the behaviour cannot disagree.
- **The mechanic is invisible and unmoddable.** VCR computes parry inline from `MeleeHitChance`.
  VCO exposes it as `VCO_ParryChance`, a real `StatDef` other mods can influence via `StatPart`.
- **No verification.** VCR ships no tests. VCO's arena suite runs melee, ranged, armor, wound,
  and height checks in a headless game, on one command.

## Design rules

These are the constraints the mod is built around. They exist because the dominant cost in
this space is not implementing mechanics, it is staying compatible with everything else the
player has installed.

1. **Everything is off by default.** Install it, change nothing, get vanilla. Each feature is
   toggled independently, so a bug in one is switched off rather than uninstalled mid-save.
2. **No def rewrites.** No weapon defs replaced, no core verbs swapped. We never claim
   ownership of a def another mod might also want.
3. **Mechanics are stats, not formulas.** Parry, evasion and suppression resistance are real
   `StatDef`s with `StatPart` workers. They show on the pawn's Stats tab, they are retunable
   in XML, and apparel, hediffs, genes, traits and other mods can influence them without
   patching our C#.
4. **No static mutable state between patches.** Context travels through explicit scopes
   (`CombatContext`). Reads outside a scope return "no data" rather than a stale value.
5. **Every transpiler is verified.** A Harmony transpiler that misses its target silently does
   nothing. Every one of ours declares a `PatchGuard` and asserts its splice count on startup;
   failures are logged loudly for troubleshooting.
6. **Generic categories over per-item patches.** Ammo uses tech-tier buckets, not calibers, so
   an unrecognised modded gun falls into a bucket instead of needing a compatibility patch.

## Layout

```
About/              Mod metadata
LoadFolders.xml     Version routing; add 1.7 here rather than restructuring
1.6/Defs/           StatDefs and content defs
1.6/Patches/        XML patches (settings-gated via PatchOperationSettingGated)
1.6/Assemblies/     Build output (gitignored)
Languages/          Keyed strings
Source/VanillaCombatOverhaul/
  Core/             Mod entry, settings, Harmony bootstrap, patch guards, context
  Stats/            StatDefs and their StatParts
  Features/
    Melee/          Parry, counter-attack, melee inspect readout
    Ranged/         Advanced accuracy, evasion, firing arc, tracers, shot readout
    Directional/    Flanking hit location, height targeting
    Damage/         Bullet and arrow wound intercept
    Armor/          Leftover armor stretch
    Apparel/        Headgear classification
  Testing/          Arena harness, a sibling of Features rather than one of them:
                    it ships in the assembly but is not a gameplay feature.
                    Melee/ Ranged/ Armor/ Damage/ Directional/ suites
Tools/
  run-combat-test.ps1   Headless autotest launcher
  package-steam.ps1     Build and stage for RimWorld Mods folder
```

## Naming conventions

The namespace is flat -- every type in the assembly is `VanillaCombatOverhaul.<Name>` --
so a type name has to carry its own context. Folders group; they do not disambiguate.

**One public type per file, named after that type.** The exception is a type and its own
payload record, which stay together because neither means anything alone: `ShotContext`
with `CombatContext`, `TranspilerGuard` with `PatchGuard`, `ReadoutScratch` with the
readout patch it feeds.

**Harmony patches are `Patch_<DeclaringType>_<Method>`.** `Patch_ArmorUtility_ApplyArmor`,
`Patch_Pawn_DraftController_Drafted`. The name states exactly what vanilla method is being
touched, so an audit of the mod's surface is a directory listing. A patch that supplies
`TargetMethods()` has no single declaring type, so it takes a descriptive
`Patch_<Concept>` instead -- `Patch_ChooseHitPart`, `Patch_ArmorPenetration` -- and those
two are the only ones allowed to.

**Static helpers end in `Utility`.** `ParryUtility`, `EvasionUtility`, `FiringArcUtility`,
`HeightTargetingUtility`.

**RimWorld's own prefixes win where the game expects them.** `StatPart_*`, `Comp*` /
`CompProperties_*`, `Command_*`, `PatchOperation*`. These are referenced by name from XML,
so they are not ours to rename freely.

**`VCO_` marks a def-facing identifier, not a C# one.** DefOf classes and defNames take it
(`VCO_StatDefOf`, `VCO_ParryChance`, `VCO_Left`) because the prefix has to match the def
it resolves. Ordinary C# types that merely belong to the mod do not need it; the four that
carry it unseparated -- `VCOMod`, `VCOSettings`, `VCODiagnostics`, `VCODebugActions` --
are mod-wide singletons where the prefix reads as part of the word.

**Test suites are prefixed by the system they exercise.** `MeleeArenaSpec` /
`RangedArenaSpec`, `MeleeCombatArena` / `RangedCombatArena`, `MeleeAssertions` /
`RangedAssertions`. Suite-neutral types stay unprefixed at `Testing/` root
(`AssertionResult`, `TestSuite`, `AutoTest`).

## Building

Requires the .NET SDK. RimWorld and Harmony come from NuGet, so no local paths are baked in.

```
dotnet build Source/VanillaCombatOverhaul/VanillaCombatOverhaul.csproj -c Release
```

Output lands in `1.6/Assemblies/`. Reference assemblies are pinned to `Krafs.Rimworld.Ref
1.6.4871`; bump that when RimWorld updates.

## Packaging and installing

`Tools\package-steam.ps1` builds a release assembly and stages a distributable copy in
`Dist\VanillaCombatOverhaul` — About, 1.6, Languages and LoadFolders.xml, and nothing else.
No source, no build intermediates, no git history, and no README: this file is developer
documentation and has no place in a Workshop item.

```
powershell -File Tools\package-steam.ps1 -InstallToMods
```

`-InstallToMods` also replaces `RimWorld\Mods\VanillaCombatOverhaul` with the packaged copy,
so what sits there is always a distributable mod. That folder is a build output, not a link to
the working tree, so **rerun this after any code change you want to see in game** — including
before running the test suite, which loads the mod by packageId from the Mods folder.

The script refuses to package if a stray assembly is sitting beside the mod DLL (RimWorld
loads every assembly in that folder, so two copies would each apply their patches), fails if
the preview image exceeds Steam's 1 MB limit, and carries `About\PublishedFileId.txt` across
repackaging — losing that file orphans the Workshop item and the next upload creates a
duplicate rather than updating the original.

## Status

| System | State |
|---|---|
| Settings framework, tabbed UI | Done |
| Patch verification (`PatchGuard`) | Done |
| Scoped combat context | Done |
| Settings-gated XML patching | Done |
| Stat definitions + reference StatParts | Done |
| Parry budget (`ParryTracker`) | Done |
| Shared facing model (`FacingUtility`) | Done |
| Melee: parry + counter-attack | Shipped. Calibrated to VCR, verified in-game |
| Directional damage (ranged + melee) | Shipped. Seeder verified against real bodies |
| Height-targeting gizmo | Shipped. Drafted command + NPC roll |
| Ranged: advanced accuracy, evasion, firing arc | Shipped. All six Reloaded arc types; type 0 default |
| Leftover armor | Shipped. Calibrated to VCR |
| Bullet and arrow wounds | Shipped. Harmony intercept, not workerClass swap |
| Visible tracers | Shipped. Vanilla projectile streak |
| Apparel coverage pack | Shipped. Settings-gated Reloaded XML |
| Automatic primary weapons | Shipped. Policy filter, skill-aware scoring, manual locks, and upgrade hysteresis |
| Suppression | Not started |
| Ammo (tech-tier buckets) | Not started |
| Full item loadouts | Not started. Deferred; primary-weapon automation is shipped separately |

Runs in-game. The automated suite loads the mod in a real RimWorld process, generates a map,
fights several thousand melee attacks, exercises ranged accuracy scenarios, and asserts on the
results.

## Credits

Automatic weapon management is inspired by [Auto Arm](https://github.com/Snusene/AutoArm)
by Snues. VCO uses an independent implementation with a separate weapon filter per apparel
policy, conservative scoring, and manual-choice locks.

Two ideas are adapted from [Vanilla Combat Reloaded](https://github.com/DonaldKar/Rimworld-Vanilla-Combat-Reloaded)
by Donald (DonaldKar): the settings-gated `PatchOperation` pattern, and the generic XPath
"flanking seeder" approach for assigning directional body part groups across arbitrary
BodyDefs including modded races. That mod carries no license file; these are reimplementations
of the approach rather than copied code, and it is worth seeking the author's blessing before
shipping anything derived more directly.

## Balance calibration

Most players never open a settings menu, so the defaults are the mod. They are not invented
here; where a mechanic has an equivalent in Vanilla Combat Reloaded, its stock values are
carried over, because those are the only numbers that have had real playtime behind them.

**Parry.** VCR's curve is kept: `aptitude ^ ((1/d) / (1 - attackerMelee))`. Front attacks
use VCR's `d = 1.5`; side attacks default to `d = 1.25`, making a flank meaningfully harder
to parry without denying the attempt. Aptitude comes from vanilla `MeleeHitChance`, whose
post-process curve puts skill 0 at 50%, skill 10 at 80% and skill 20 at 90%. Front attacks yield:

| defender | vs skill 0 | vs 5 | vs 10 | vs 15 | vs 20 |
|---|---|---|---|---|---|
| skill 0  | 40% | 27% | 10% |  5% |  1% |
| skill 10 | 74% | 65% | 48% | 37% | 23% |
| skill 20 | 87% | 82% | 70% | 63% | 50% |

Two things this preserves that a defender-only stat could not: the chance is *contested*, so a
skilled attacker punches through a parry, and direction matters continuously: side attacks
weaken the chance while rear attacks deny a parry outright.

**Why resolving before the hit roll changes nothing.** VCR parried only attacks that would
otherwise have landed; this mod parries first and lets the rest resolve normally. Writing `m`
for vanilla miss chance and `p` for parry chance, VCR gives `m + (1-m)p` and this gives
`p + (1-p)m`. Those are the same expression. The restructure bought robustness — a prefix
instead of an IL transpiler keyed on a local variable index — and cost nothing in balance.

**Parry budget.** The one deliberate departure. VCR had no cap, so a surrounded pawn parried
every incoming attack forever. Vanilla melee cooldown is 2 seconds in 216 of 294 weapon defs,
so N attackers produce roughly N/2 attacks per second, and a budget of 2 per second starts to
bind at around `4/p` attackers — about 5 opponents against a strong defender, about 8 in an
even matchup. It is a backstop against swarms, not a routine tax, and it leaves the
one-on-one numbers above untouched.

**Flanking.** A side flank lowers the parry exponent divisor from 1.5 to 1.25, weakening the
defender's chance without denying it. Flanking also matters through directional damage: the
side an attack arrives from determines which body parts it can reach.

**Advanced accuracy.** VCR's mitigation curve is kept: `factor ^ (1 / (skill / scale))` applied
only to weapon and weather factors inside `ShotReport`. Default scale is 5. At a raw weapon
factor of 50%, skill 10 raises it to roughly 71% and skill 20 to roughly 84%.

**Evasion.** VCR's movement model is kept: hit chance is multiplied by `evasionFactor ^
(speed - minSpeed)` with defaults 0.8 and 2.5. Stationary pawns read zero on the `VCO_Evasion`
stat. Optional skill contest reuses the mitigation curve against the evasion multiplier.

**Firing arc.** All six Reloaded miss-spread distributions ship; type 0 is the default.
Type 0 scales wild-miss radius with `distance * tan(arc/2) / 10`. Default arc is 45 degrees.
A guarded transpiler on `ChangeDestToMissWild` verifies the splice on startup.

## Defaults

Defaults are set per feature in `Source/VanillaCombatOverhaul/Core/VCOSettings.cs` against the
`Shipped` constant: built and verified features default on. Parry, counter-attack, directional
damage and melee flanking are examples.

Unbuilt roadmap features are not exposed in the player settings window.

## Diagnostic counters — temporary

`VCODiagnostics` counts how often each part of the mod fires, so behaviour can be measured
rather than guessed at. A clean startup log proves nothing crashed; it says nothing about
whether pawns are actually parrying.

Counters are gated behind the internal `verboseLogging` setting (default off) and can be written
to the log every `diagnosticDumpIntervalTicks` (default 2500, about one in-game hour). These
controls remain available to developers but are not exposed in the player settings window.

Rejection reasons are counted individually, which is the useful part — `parry.attempt` versus
`parry.success` only tells you the rate, while `parry.reject.*` tells you *which gate* is
eating attempts:

```
parry.attempt / success / counterAttack
parry.facing.{Front,Left,Right,Rear}
parry.chanceRolled                     avg / min / max
parry.reject.{downedOrDead, surpriseOrImmobile, noWeapon,
              busyRanged, fromBehind, budgetSpent, rollFailed}
directional.considered / replaced
directional.source.{melee,ranged}
directional.facing.{Front,Left,Right,Rear}
directional.keep.{frontal, noPartsOnSide}
directional.skip.meleeDisabled
accuracy.mitigation.{applied,factor.weather,factor.weapon}
evasion.{considered,applied,stationary}
firingArc.adjusted
```

Two are worth watching specifically:

- **`parry.reject.budgetSpent`** — if this climbs, the parry budget is binding more often than
  the ~5-attacker estimate in the calibration section predicts.
- **`directional.keep.noPartsOnSide`** — should be near zero. Anything else means the group
  seeder is under-covering some body shape, most likely a modded race.

**To remove:** delete `Core/VCODiagnostics.cs` and `Core/DiagnosticsGameComponent.cs`, then
the `VCODiagnostics.Count`/`Sample` calls they pair with. The compiler will find them all.

## Automated combat testing

Hand-testing cannot answer the questions worth asking. Confirming a predicted 48% parry rate
to within two points needs roughly 600 samples, and fighting by hand lets skill, weapon and
facing all drift. The harness pins those and drives the tick loop directly, so a run takes
seconds instead of an evening.

### Tier 1 — manual, from the dev menu

Dev mode on, then debug actions under **Vanilla Combat Overhaul**:

| Action | Does |
|---|---|
| Run full combat test | The whole scenario matrix plus facing checks |
| Run one quick arena | A single 2000-tick fight, for a fast look |
| Facing checks only | Pure maths, no combat; works at the main menu |
| Write / Reset diagnostic counters | Manual control of the counters |

### Tier 2 — headless, one command

```
pwsh Tools\run-combat-test.ps1
pwsh Tools\run-combat-test.ps1 -Seed 12345
```

Exit code 0 = all checks passed, 1 = a check failed, 2 = no report produced.

RimWorld is launched with `-savedatafolder` pointing at a throwaway directory, so the run gets
its own config and mod list. **The real ModsConfig.xml, prefs and saves are never touched.**
There is no `-quicktest` argument in 1.6 (checked: the literal is absent from the assembly,
while `savedatafolder` and `autostart` are present), so the mod takes itself into a map by
calling the public `Root_Play.SetupForQuickTestPlay` when it finds a trigger file.

### What is asserted

A harness that only prints numbers cannot regress, so each run makes claims that can fail:

| Check | Catches |
|---|---|
| arena ran | A scenario that could not set itself up passing by having no assertions |
| sample size >= 200 attempts | A run that proves nothing being reported as a pass |
| attacker / defender skill pinned to spec | A scenario silently measuring a different matchup |
| rear attacks never parried | The facing gate leaking |
| directional seeder covers every body | `noPartsOnSide` above zero — a body shape the XPath missed |
| unarmed defender never parries | The weapon gate leaking |
| parry chance matches formula | Drift between the implementation and the balance table |
| roll honours chance | The RNG not delivering the chance actually offered |
| parry budget cap holds | The gate and the ledger disagreeing, letting a pawn bank past the cap |
| budget negligible in a duel | The cap throttling ordinary one-on-one fights |
| facing unit checks (12 cases) | Bugs in `FacingUtility` itself, isolated from combat |

The expected parry chance is derived from vanilla's `MeleeHitChance` post-process curve read
out of the def at runtime, not from constants copied into the test, so the prediction stays
honest if Ludeon retunes the curve.

### Seeding, and why a run is not reproducible

`-Seed` pins everything the mod controls: the world seed, both arenas' own RNG state, and the
serial portion of map generation. It does **not** make a run reproducible, and it cannot.

RimWorld 1.6 generates parts of a map in parallel, and `Verse.Rand` holds its seed and state
stack in plain statics rather than `[ThreadStatic]`. Worker threads therefore draw from one
shared generator in whatever order the scheduler picks, so two runs of the same seed build
different maps — measured at 58, 73 and 82 pawns across three runs of seed 12345, with the
world seed, tile, size, weather and clock all identical. Every `thingIDNumber` shifts with
that, and RimWorld schedules rare ticks and seeds much of its own randomness off those IDs.

The practical consequences:

- **The suite is statistical, not reproducible.** Assertions on sampled rates must derive
  their tolerance from the sample size. `roll honours chance` sizes its band as four binomial
  standard errors, and prints the arithmetic, so a failure can be read rather than guessed at.
  A flat 6% band sat at about 2.5 sigma and failed roughly one run in fifty on luck alone.
- **A seed is still worth passing.** It removes the map-*layout* variance, which is the part
  that decides whether a scenario can place its combatants at all.
- **Every run prints a fingerprint** — world seed, tile, weather, clock, pawn count, pawn ID
  sum, next thing ID — taken before any arena runs. Two reports that disagree below the
  fingerprint but agree in it have diverged inside the arenas; two that disagree in the
  fingerprint itself never had the same map to begin with.

Leave the seed off when measuring balance: it hides none of the variance that matters, and
nothing depends on it.

### Baseline (all 146 checks passing)

Nine scenarios, RimWorld 1.6.4871, ~5,000 real melee attacks. Duels verify the formula across
the skill range; the outnumbered scenarios verify the facing gate and the parry budget.

| scenario | attacker / defender melee | vanilla curve | formula | rolled | parry rate |
|---|---|---|---|---|---|
| even-novice     | 0 / 0   | 39.7% | 40.4% | 40.4% | 220 / 519 (42.4%) |
| even-skilled    | 10 / 10 | 47.5% | 48.9% | 48.9% | 203 / 416 (48.8%) |
| even-master     | 20 / 20 | 49.5% | 50.2% | 50.2% | 242 / 472 (51.3%) |
| weak-attacker   | 0 / 20  | 86.9% | 87.4% | 87.4% | 436 / 512 (85.2%) |
| weak-defender   | 20 / 0  |  1.0% |  1.1% |  1.1% |   2 / 374 (0.5%) |
| unarmed-control | 10 / 10 |     — |     — |  0.0% |   0 / 460 (0.0%) |
| outnumbered-3v1 | 10 / 10 | 47.5% | 46.6% | 46.6% | 345 / 763 (45.2%) |
| outnumbered-6v1 | 10 / 10 | 47.5% | 45.1% | 45.1% | 209 / 680 (30.7%) |
| 6v1, cap lifted | 10 / 10 | 47.5% | 45.3% | 45.3% | 300 / 859 (34.9%) |

`formula` is an independent reimplementation of the parry maths in `MeleeAssertions`, fed the
same inputs at the same instant the mod computed its own chance; `rolled` is what the mod
used. They agree to a decimal place, which is the point. `parry rate` is lower than `rolled`
wherever the facing gate or the budget rejected attacks before any roll was taken.

`directional.keep.noPartsOnSide` was zero in every scenario, every rear-facing attack was
rejected by the facing gate, and `parry.budget.overrun` was zero — no defender ever banked
more parries in a window than the cap allows.

### What the crowd scenarios measure

The last three rows are the balance experiment for the parry budget, which is the one
mechanic here with no equivalent in Vanilla Combat Reloaded. The 6v1 pair is identical except
that the second lifts the cap.

| | duel | 3v1 | 6v1 | 6v1, cap lifted |
|---|---|---|---|---|
| rejected — rear | ~0% | 1.4% | 23.5% | 19.9% |
| rejected — budget | <1% | 4.2% | 8.4% | — |
| parry rate | 48.8% | 45.2% | 30.7% | 34.9% |

Geometry does most of the work: with the cap lifted entirely, six attackers still drag the
rate down by roughly 14 points, because a fifth of their attacks arrive from behind and are
refused outright. The budget is worth about 4 points on top of that, reproduced across two
well-sampled runs.

That is the intended shape — negligible one-on-one, light at three, material at six — so the
default of 2 per 60 ticks stays. Note the rear share at 3v1 is noisy between runs (1.4% here,
15.7% in another), because which way a defender faces depends on who it last traded with. The
6v1 figure is stable at 20-24%.

### Harness pitfalls

Every one of these produced confident, plausible, wrong numbers with all other checks green.
Worth reading before extending the suite.

- **Scope counters to the pawns under test.** A parry triggers a counter-attack, and that
  counter is a melee attack the original attacker can parry. Unscoped, an asymmetric matchup
  records both directions and reports their mean — 44% where 87% and 1% were expected. This
  one bites twice: it caught the counters first, then the chance probe added later.
- **Verify the combatants are who the scenario says.** `RandomEnemyFaction()` returns whatever
  is hostile, and in a fresh quick-test world the humanlike factions start *neutral* — so the
  only hostile ones are mechanoids and insects, which have no skills tracker. A skill-20
  scenario silently ran at about skill 7 and reported 17% parry where 1% was expected, with
  every other assertion passing. Both sides are now the same pawn kind and hostility is
  imposed, not searched for.
- **Setting a skill is not the same as pinning it.** `SkillRecord.Level`'s getter adds an
  aptitude offset from genes on top of the stored value, and on a pawn whose backstory
  disables Melee the setter does nothing at all. Read the value back and reject the pawn if it
  did not land.
- **Sample the formula's inputs when the formula runs.** Sampling at the refresh tick measures
  freshly healed pawns; under six attackers the roll almost never happens on a healthy
  defender. Worth 4.4 points of apparent error that belonged entirely to the harness.
- **Evaluate per attempt, then average — never the reverse.** The formula is strongly
  non-linear in attacker skill, so averaging inputs first gives a different answer.
- **Heal often, re-order rarely.** Re-issuing a job cancels the swing a pawn is winding up.
  Driven off one interval, six attackers refreshed every 30 ticks landed *fewer* attacks than
  three refreshed every 120.
- **A run that cannot start must fail, not vanish.** A scenario with no assertions contributes
  nothing to fail, so a suite where every scenario spawned zero pawns cheerfully reported
  "12 of 12 checks passed".
- **Clean up corpses.** A dead pawn sits inside a `Corpse`, a separate spawned Thing that
  destroying the pawn does not remove. Left behind they change the ground the next scenario
  spawns on, and two runs of the same seed stop agreeing.
- **Every arena needs the skill-pinning lesson, not just the first one.** The melee arena
  learned that assigning `SkillRecord.Level` can silently do nothing and started reading the
  value back. The ranged arena was written later and did not, so a scenario asking for a
  skill-20 shooter would quietly run at whatever the generator produced and then report that
  high skill had failed to improve the weapon factor — a mod-shaped failure with a harness
  cause. Both arenas now discard unsuitable pawns and assert the skill actually landed.
- **A scenario precondition must be asserted, not assumed.** The moving-target scenario sent
  its pawn to one hardcoded destination and never checked it set off. When that cell was
  unreachable the pawn stood still, evasion correctly read 1.0, and the report blamed evasion.
  Anything a scenario needs in order to mean what it says — a pinned skill, a moving target —
  is now its own named check that fails as a setup failure.
- **Fixed tolerances on sampled rates are a bug with a long fuse.** They pass for months and
  then fail on an unlucky seed, and the natural reading of that failure is "the mod broke".
  Size the band from the sample.
