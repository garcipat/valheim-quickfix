# Map Routes

By [garcipat](https://github.com/garcipat) · [Source code on GitHub](https://github.com/garcipat/valheim-quickfix)

Plan where you want to go before you set off. Above the pin icons on the large map there is now a **quill**.
Pick it and draw your route on the map, freehand, as with a pen. It is shown as a dotted line in the same red the
game uses to cross out pins.

Routes are saved with your character, per world, so they are still there the next time you open the map.

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

## Configuration

There are no settings. To hide your routes, disable the mod in your mod manager. They come back when you enable it
again.

## Installation

Install through a mod manager such as Gale or r2modman. It installs
[BepInExPack Valheim](https://thunderstore.io/c/valheim/p/denikson/BepInExPack_Valheim/) automatically.

For a manual install, put `MapRoutes.dll` into `Valheim/BepInEx/plugins/`.

## Feedback

Bug reports, ideas, and source code: <https://github.com/garcipat/valheim-quickfix>

## License

MIT. See the `LICENSE` file in the package, or the source repository.
