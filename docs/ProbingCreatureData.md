# Reading a creature's spawn and drop rates

A creature's spawn rates, drops and spawners are Unity data in the game's asset bundles, not code, so decompiling
`assembly_valheim.dll` does not show them. Reading the bundles directly (AssetRipper and similar) is slow. Instead,
[`tools/CreatureProbe`](../tools/CreatureProbe/Plugin.cs) is a throwaway BepInEx plugin that logs the loaded data from
inside the game. It was used for More bears and More Writhans.

## Running the probe

1. Set `Match` at the top of `tools/CreatureProbe/Plugin.cs` to part of the creature's prefab name (`Bjorn`, `Writhan`)
   or of its kill key. Global keys such as `defeated_writhan` in the probe's `globalKeys=` line, or in a world's
   keys, are a good hint when the in-game name is unknown. Display names (`$enemy_writhan`) are localized and don't
   always match the prefab.
2. Build it. It is not a mod and is not packaged or released:

   ```bash
   dotnet build tools/CreatureProbe -c Release
   ```

3. Copy `tools/CreatureProbe/bin/Release/net48/CreatureProbe.dll` into `BepInEx/plugins` and load into any world.
   It runs once, as soon as the local player exists; there is no need to be in the creature's biome or to wait for one.
4. Read the lines between `=== CreatureProbe start ===` and `=== CreatureProbe end ===` in `BepInEx/LogOutput.log`,
   then remove the DLL from `plugins`.

The probe reads every field by reflection, so fields that are newer than the compile-time libs show up too.

## What it logs

| Line | Source | What matters |
|---|---|---|
| `globalKeys=`, `resourceRate=` | world settings | World modifiers. `resourceRate` scales drop amounts, not chances or spawns. |
| `DEFEATKEY` | `Character.m_defeatSetGlobalKey` | Finds the prefab from a kill key; also health, faction, `m_boss`. |
| `PREFAB` / `DROP` | `CharacterDrop.m_drops` | `m_chance` (0–1) per drop; `m_levelMultiplier` scales it by star level. |
| `SPAWN` | `SpawnSystemList.m_spawners` (`_SpawnList_base` etc.) | The world spawn entry, see below. One line per entry; day and night are often separate entries. |
| `CREATURESPAWNER` | `CreatureSpawner` | Fixed spawn points placed by locations (`Spawner_<name>`), triggered within `m_triggerDistance`. |
| `SPAWNAREA` | `SpawnArea` | Spawn points such as dungeon or nest spawners. |

The `SPAWN` fields that set the rate:

- `m_spawnInterval`: seconds between rolls.
- `m_spawnChance`: percent (0–100) per roll.
- `m_maxSpawned`: the cap of that creature alive nearby; no roll spawns while it is reached.
- `m_spawnAtDay` / `m_spawnAtNight`: when the entry may roll.

Location limits: `m_biome`, `m_biomeArea` (`Edge`, `Median`, or `-1` for everywhere), `m_minAltitude` /
`m_maxAltitude`, `m_minDistanceFromCenter` / `m_maxDistanceFromCenter`, `m_inForest` / `m_outsideForest`.
Star levels: `m_minLevel` / `m_maxLevel`.

## Turning the numbers into a rate

The average wait for one spawn is the interval divided by the chance:
`m_spawnInterval / (m_spawnChance / 100)`, while the cap is not reached and the location limits hold.

| Creature | Interval | Chance | Max alive | Average wait |
|---|---|---|---|---|
| Bear (`Bjorn`), vanilla | 2000 s | 15% day / 20% night | 1 | 3.7 h / 2.8 h |
| Wraith, vanilla | 200 s | 30%, night only | 2 | 11 min |
| Writhan, vanilla | 4000 s | 10% | 1 | 11 h |

Treat the wait as a rough "per area" figure for comparing creatures: the spawn system rolls for the zones around the
player, not once per world.

Drops below a chance of 0.3 use the game's pseudo-random drop counter, which evens out streaks but keeps the average
at `m_chance`, so a 10% trophy averages one per 10 kills.

## Changing the rates

QuickFix edits spawn entries and drops once at load through
[`Patches/SpawnTuning.cs`](../src/ValheimQuickFix/Patches/SpawnTuning.cs). See `MoreBearsPatch` and `MoreWrithansPatch`
for the pattern, and [Fixes.md](Fixes.md) for what each changes. The helper logs the vanilla and new values, which is a
quick check that a change was applied.
