# Pin Hotkey Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** In MapRoutes, looking at a supported object shows `[F8] Pin on map`; pressing the key places a named circle
pin there, which the existing pin icons draw as its icon.

**Architecture:** `PinTargets` recognises the hovered object by component type and internal names and returns a pin
name. `PinHotkey` owns the config key, the hint text and placing the pin. Two Harmony postfixes wire it in:
`Hud.UpdateCrosshair` (append the hint) and `Player.Update` (read the key).

**Tech Stack:** C# / .NET Framework 4.8, BepInEx 5, HarmonyX, Valheim game libs (compile-time libs are older than the
live game: use reflection for anything newer, as `PinIcons` and `Ui` do).

**Spec:** `docs/superpowers/specs/2026-11-15-pin-hotkey-design.md`

## Global Constraints

- Config: section `General`, key `PinHotkey`, type `KeyboardShortcut`, default `new KeyboardShortcut(KeyCode.F8)`;
  `KeyboardShortcut.Empty` (shown as `None`) disables hint and key.
- Hint line, appended with `\n`: `[<color=yellow><b>{key}</b></color>] Pin on map`, `{key}` = `MainKey.ToString()`.
- Pin: `Minimap.instance.AddPin(position, Minimap.PinType.Icon3, name, save: true, isChecked: false)`.
- Messages (`Player.Message(MessageHud.MessageType.TopLeft, …)`): `Pinned {name}`, `Already pinned`.
- Duplicate: a pin with the same name (case-insensitive) within 10 m (`Utils.DistanceXZ`).
- Nothing happens when `Player.InInterior()` is true, or when the game's private `Player.TakeInput()` is false.
- Every pin name must be in `PinIcons.Icons`. English only. Client-side only. No new config section.
- Code style: 4-space indent, block-scoped namespace `ValheimMapRoutes`, `internal`, failures logged once, never thrown
  into the game loop (wrap postfix bodies in try/catch like `MinimapUpdatePatch`).

## Review Focus

- Hovering an unsupported object (tree, chest, enemy, player-built piece): no hint, key does nothing, no log spam.
- Pressing the key twice on the same bush: one pin, second press says `Already pinned`.
- Pressing the key while typing in chat or a sign: nothing (TakeInput false).
- Looking at a dungeon exit from inside: no hint (InInterior).
- Ore deposit after a few hits (fractured `_frac` variant): still recognised, because ore is matched by its drops, not
  its prefab name.

There is no automated test suite (patches run only in-game); each task's check is the build plus the in-game check in
Task 3.

---

### Task 1: Recognise targets

**Files:**
- Create: `src/MapRoutes/PinTargets.cs`

**Interfaces:**
- Produces: `internal static bool PinTargets.TryGet(GameObject hoverObject, out string name, out Vector3 position)`
  and `internal static string PinTargets.Describe(GameObject hoverObject)` (prefab name plus component type names of
  the hoverable, for the log).

- [ ] **Step 1: Implement `TryGet`.** Find the hoverable as the HUD does: `hoverObject.GetComponentInParent<Hoverable>()`.
  `position` is that component's `transform.position`. Rules, first match wins (prefab names via `Utils.GetPrefabName`):
  - `Teleport`: `GetComponentInParent<Location>()` prefab name, by prefix in this order: `SunkenCrypt` → `Sunken crypt`,
    `Crypt` → `Burial chamber`, `TrollCave` → `Troll cave`, `MountainCave` → `Frost cave`,
    `Mistlands_DvergrTownEntrance` → `Infested mine`; no location or no match → `Dungeon`.
  - `TeleportWorld` → `Portal`.
  - `ShipControlls`: `m_ship` prefab name `Karve` → `Karve`, `VikingShip` → `Longship`, else `Ship`.
  - `Pickable`: `m_itemPrefab` name `Raspberry` → `Raspberries`, `Blueberries` → `Blueberries`, `Cloudberry` →
    `Cloudberries`, `Mushroom` → `Mushroom`, `MushroomYellow` → `Yellow mushroom`, `MushroomBlue` → `Blue mushroom`,
    `MushroomJotunPuffs` → `Jotun puffs`, `MushroomMagecap` → `Magecap`, `Tar` → `Tar pit`, `DragonEgg` → `Dragon egg`.
  - `ItemDrop` whose `m_itemData.m_dropPrefab` name is `DragonEgg` → `Dragon egg`.
  - Ore: `MineRock`, `MineRock5` (`m_dropItems`), or a `DropOnDestroyed` on the hoverable's object or parent
    (`m_dropWhenDestroyed`, covers tin if it is a `Destructible` with `HoverText`): any `m_drops[i].m_item` named
    `CopperOre` → `Copper`, `TinOre` → `Tin`, `SilverOre` → `Silver`.
  Keep the name tables as static dictionaries at the top of the file, like `PinIcons.Icons`.
- [ ] **Step 2: Build.** Run `dotnet build src/MapRoutes -c Release`. Expected: `Build succeeded`, 0 warnings from the
  new file.

### Task 2: Hint, key, pin, config, docs

**Files:**
- Create: `src/MapRoutes/PinHotkey.cs`, `src/MapRoutes/Patches/PinHotkeyPatches.cs`
- Modify: `src/MapRoutes/Plugin.cs` (bind `PinHotkey`), `package/MapRoutes/README.md` (new section "Pin what you look
  at" after "Pin icons", plus the setting under Configuration), `package/MapRoutes/CHANGELOG.md` (`### Added` line
  under `[Unreleased]`), `docs/MapRoutes.md` (config, patches, behavior)

**Interfaces:**
- Consumes: `PinTargets.TryGet`, `PinTargets.Describe`.
- Produces: `internal static ConfigEntry<KeyboardShortcut> Plugin.PinHotkey`; `internal static string
  PinHotkey.Hint(GameObject hoverObject)` (hint line or null); `internal static void PinHotkey.Update(Player player)`.

- [ ] **Step 1: Config.** Bind in `Plugin.Awake` after `BiomeBorders`, description: "Key that pins what you look at
  (berries, mushrooms, ore, cave and dungeon entrances, portals, ships) on the map with its icon. None turns it off."
- [ ] **Step 2: `PinHotkey`.** `Hint` returns null when the key is empty, the local player is in an interior, or
  `TryGet` fails. `Update` runs only for `Player.m_localPlayer`, only when `PinHotkey.Value.IsDown()` and `TakeInput`
  (private, via `AccessTools.Method(typeof(Player), "TakeInput")`, invoked with `false`) is true. It then logs one info
  line `Pin hotkey on {Describe}: {name or "nothing to pin"}`, checks duplicates over `Minimap.m_pins` (by reflection,
  like `PinIcons.PinsField`), adds the pin and shows the message.
- [ ] **Step 3: Patches.** `Hud.UpdateCrosshair` postfix: when `Hint(player.GetHoverObject())` is not null and the hover
  label's text is not empty, append it. Reach `Hud.m_hoverName` with `AccessTools.Field` and its `text` property by
  reflection (the field is `Text` in the compile libs, `TextMeshProUGUI` live). `Player.Update` postfix calls
  `PinHotkey.Update(__instance)`. Both bodies in try/catch, logging once.
- [ ] **Step 4: Docs.** README section lists the table from the spec in player words, says the pin is a normal circle
  pin and that nothing is pinned inside dungeons or twice in the same spot. Changelog line: "Pin what you look at:
  press F8 while looking at berries, mushrooms, copper, tin or silver, a cave or dungeon entrance, a portal or a ship
  to put a pin with its icon on the map. Change the key with `PinHotkey` in `General`."
- [ ] **Step 5: Build and package.** Run `dotnet build src/MapRoutes -c Release` and `./scripts/package.sh MapRoutes`.
  Expected: `dist/MapRoutes-0.0.0-dev.zip` (or the script's dev name) exists.

### Task 3: In-game check

- [ ] **Step 1:** Copy the dev zip to `~/share/Valheim/MapRoutes-dev.zip` for the user.
- [ ] **Step 2:** User checks: hint and pin on a raspberry bush, a mushroom, copper, tin, silver, a troll cave and a
  burial chamber entrance, a portal, a karve; F8 twice → `Already pinned`; F8 in chat → nothing; no hint on a tree or
  chest; no hint inside a crypt. Any wrong or missing name is fixed from the `Pin hotkey on …` log lines in
  `LogOutput.log`.
- [ ] **Step 3:** Commit on `feat/pin-hotkey` when the user asks; record the in-game check in the PR description.
