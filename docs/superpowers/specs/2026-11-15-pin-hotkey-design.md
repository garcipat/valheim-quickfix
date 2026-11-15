# MapRoutes: pin what you look at with a hotkey

## Why

Pinning something you found means opening the map, finding the spot, double-clicking and typing a name. With the
hotkey you look at it, press F8, and a circle pin with the right name lands on the map; the pin icons (already in
MapRoutes) then draw it as its icon.

## Goals

- Looking at a supported object adds a line `[F8] Pin on map` under its hover text.
- Pressing the key places a circle pin (`Minimap.PinType.Icon3`) at the object, saved, unchecked, with the English name
  from the table below, and shows a short message (`Pinned Copper`).
- Works in any game language: objects are recognised by internal (prefab/item) names, not by on-screen text.

## Non-goals and anti-goals

- No pin for objects that have no pin icon, and no generic "pin anything" fallback (junk pins).
- Not for things you spot from far away and pin from the map anyway: Leviathan, biomes, villages. (Tar stays: you may
  want to pin it while standing at the pit.)
- No obsidian, iron or later ores; no roaming creatures (Lox).
- Client-side only, no networking.

## What gets the hint

Updates after the first in-game tests, replacing the names in the table below:

- Pins use the icon's plain name so only the icon shows: troll and frost caves `Cave`, sunken crypts `Crypt`, burial
  chambers `Dungeon` (the sarcophagus icon; the name `Burial chamber` moved to that icon in the pin icons too),
  infested mines `Mine`, other entrances `Dungeon`, any ship `Ship`, berries `Berries`, mushrooms `Mushroom`, tar `Tar`,
  dragon eggs `Egg`, copper/tin/silver `Ore`.
- Portals get the game's rune pin (`Icon4`) named with the portal's tag, not a circle `Portal` pin.
- Untouched ore deposits are recognised by the broken variant they spawn on the first hit.

| Look at | Pin name |
|---|---|
| Troll cave entrance | `Troll cave` |
| Frost cave entrance | `Frost cave` |
| Burial chamber entrance | `Burial chamber` |
| Sunken crypt entrance | `Sunken crypt` |
| Infested mine entrance | `Infested mine` |
| Any other "E to enter" entrance | `Dungeon` |
| Portal | `Portal` |
| Karve / Longship | `Karve` / `Longship` |
| Raft or any other ship | `Ship` |
| Raspberry / blueberry / cloudberry bush | `Raspberries` / `Blueberries` / `Cloudberries` |
| Red mushroom | `Mushroom` |
| Yellow mushroom, blue mushroom, Jotun puffs, magecap | `Yellow mushroom`, `Blue mushroom`, `Jotun puffs`, `Magecap` |
| Tar | `Tar pit` |
| Dragon egg | `Dragon egg` |
| Copper / tin / silver deposit | `Copper` / `Tin` / `Silver` |

Every pin name must be a name the pin icons know (`PinIcons.Icons`).

## Behavior

- A pin with the same name within 10 m already exists → no new pin, message `Already pinned`.
- Inside a dungeon (`Player.InInterior()`) → no hint, key does nothing (interior positions are off the map).
- Key does nothing while chat, console, menu, text input or the map has focus (the game's own `TakeInput` rules).
- Ships: the pin marks where the ship was when the key was pressed.

## Config

`General → PinHotkey`, `KeyboardShortcut`, default `F8`. Setting it to `None` turns the feature off (no hint either).

## Approaches

1. **Recognise by internal names (picked).** Hoverable component type plus prefab/item/location name, language-neutral;
   entrances are told apart by the `Location` they belong to.
2. Recognise by the hover text on screen. Rejected: breaks in other languages, and entrances only say "[E] Enter".

## Calls made by Claude (correctable)

10 m duplicate radius; `Ship` for rafts; message wording.

## Open, to verify in-game

Tin and tar show hover text at all; each entrance's `Location` prefab name; ore detection. The dev build logs the
recognised (or unrecognised) prefab under the crosshair at debug level to settle this.
