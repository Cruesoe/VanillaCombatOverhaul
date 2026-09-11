# Vanilla Combat Overhaul

Vanilla Combat Overhaul adds tactical depth to RimWorld combat while keeping the
game's familiar systems and broad mod compatibility.

## Features

- Skill-based parries and immediate counter-attacks in melee.
- Flanking, directional damage, and protection against unlimited parries when surrounded.
- Height-aware targeting for more believable hit locations.
- Stronger influence from shooting skill, weather, movement, and distance.
- More natural missed-shot spread and visible projectile trails.
- Distinct bullet and arrow wounds.
- Armour that can reduce damage even when it does not stop a hit completely.
- Improved coverage for gloves, boots, masks, helmets, glasses, and similar apparel.
- Automatic primary-weapon selection based on pawn skills and assigned apparel policy.

Combat and equipment features can be enabled individually from the mod settings.

## Compatibility

Designed for RimWorld 1.6 and vanilla-style weapons, apparel, and combat mods.

Not compatible with:

- Combat Extended
- Yayo's Combat
- Vanilla Combat Reloaded
- Auto Arm

Vanilla Combat Reloaded inspired several combat systems, while Auto Arm inspired automatic
weapon selection. Vanilla Combat Overhaul uses its own implementation of both.

## Installation

Install Harmony, then place this mod after Harmony in the RimWorld mod list. Existing saves
can use the mod; apparel coverage changes require restarting RimWorld after changing them.

## Building from source

```powershell
dotnet build Source\VanillaCombatOverhaul\VanillaCombatOverhaul.csproj -c Release
```

The compiled assembly is written to `1.6/Assemblies/`.
