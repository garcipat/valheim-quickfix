# Map Routes

By [garcipat](https://github.com/garcipat) · [Source code on GitHub](https://github.com/garcipat/valheim-quickfix)

Plan where you want to go before you set off. Above the pin icons on the large map there is now a **quill**.
Pick it and draw your route on the map, freehand, as with a pen. It is shown as a dotted line in the same red the
game uses to cross out pins.

Routes are saved with your character, per world, so they are still there the next time you open the map.

It also gives your pins **icons**: name a circle pin `Cave`, `Portal` or `Swamp` and it shows a cave, a portal or
reeds instead of the circle. See [Pin icons](#pin-icons).

The large map can also show faint **dashed lines along the borders between biomes**, so you can see where the Black
Forest ends and the Swamp begins. They only show on land and only where you have explored. They are off until you
turn them on (see [Configuration](#configuration)).

Client-side only: it works on any server and nothing needs to be installed on the server. Other players don't see
your routes.

## Using it

- Open the large map (M) and click the **quill** above the pin icons. The mouse cursor turns into a quill.
- **Hold the left mouse button and drag** to draw. Letting go finishes the line; each line is its own route.
- **Hold the right mouse button and drag** to move the map while the quill is picked. The mouse wheel still zooms.
- **Right-click a route** (without dragging) to remove it. This works whether the quill is picked or not. A pin
  under the cursor comes first, so right-clicking a pin still removes the pin.
- **Click the quill again**, pick a pin icon or close the map to stop drawing. Left-drag moves the map again, as
  usual.
- While the quill is picked, clicking a pin doesn't cross it out and double-clicking doesn't add a pin.

Routes show on the large map and on the small map in the corner. You draw them on the large map. Sharing your map
through a cartography table doesn't pass them on.

## Pin icons

Place a **circle** pin as usual and give it one of the names below. The circle turns into the matching icon.

- The **whole name** has to match. Capital letters don't matter: `cave`, `Cave` and `CAVE` all work, but
  `Cave near home` stays a circle.
- With the **short name** (first column) you see only the icon, like the game's own pins.
- With a **longer name** (last column) you see the same icon with the name under it, so you still know which ore or
  which berries it is.
- Only circle pins change. Fire, house, hammer and rune pins stay as they are, whatever their name.

| Icon | Short name, icon only | Longer names, icon with name |
|---|---|---|
| Cave | `Cave` | `Troll cave`, `Frost cave` |
| Crypt | `Crypt` | `Burial chamber`, `Sunken crypt` |
| Dungeon | `Dungeon` | |
| Mine | `Mine` | `Infested mine` |
| Village | `Village` or `Camp` | `Fuling village`, `Fuling camp` |
| Portal | `Portal` | |
| Ship | `Ship` | `Boat`, `Karve`, `Longship` |
| Ore | `Ore` | `Copper`, `Tin`, `Silver`, `Iron`, `Obsidian` |
| Tar | `Tar` | `Tar pit` |
| Berries | `Berries` | `Raspberries`, `Blueberries`, `Cloudberries` |
| Mushroom | `Mushroom` | `Yellow mushroom`, `Blue mushroom`, `Jotun puffs`, `Magecap` |
| Egg | `Egg` | `Dragon egg` |
| Nest | `Nest` | `Greydwarf nest` |
| Surtling | `Surtling` | `Fire geyser` |
| Leviathan | `Leviathan` or `Turtle` | |
| Lox | `Lox` | |
| Forest | `Forest` | `Black forest` |
| Swamp | `Swamp` | |
| Mountain | `Mountain` | |
| Plains | `Plains` | |
| Mistlands | `Mistlands` or `Mist` | |
| Ashlands | `Ashlands` or `Ash` | |
| Deep North | `Deep North` or `North` | |

The pin itself doesn't change, only how it is drawn. Without the mod it is a circle with its name, so nothing is lost
if you remove it. Pins shared through a cartography table keep their name, so friends who have the mod see the
icons too.

## Configuration

- **General → BiomeBorders** (default off): the dashed biome borders on the large map. Turn it on to see them.

Routes have no setting. To hide your routes, disable the mod in your mod manager. They come back when you enable it
again.

## Installation

Install through a mod manager such as Gale or r2modman. It installs
[BepInExPack Valheim](https://thunderstore.io/c/valheim/p/denikson/BepInExPack_Valheim/) automatically.

For a manual install, put `MapRoutes.dll` into `Valheim/BepInEx/plugins/`.

## Feedback

Bug reports, ideas, and source code: <https://github.com/garcipat/valheim-quickfix>

## License

MIT. See the `LICENSE` file in the package, or the source repository.

Pin icons by Lorc and Delapouite from [game-icons.net](https://game-icons.net), licensed under
[CC BY 3.0](https://creativecommons.org/licenses/by/3.0/). The Cave and Surtling icons are our own.
