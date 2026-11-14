# Pin icons

Sources for the MapRoutes pin icon sheet (`src/MapRoutes/Assets/pin-icons.png`). Not a mod and not packaged.

To change an icon, replace its SVG in `svg/`, run `./make-sheet.sh` (needs ImageMagick) and commit the PNG. To add one,
also add it to the list in `make-sheet.sh` and to `PinIcons.Icons`, in the same position, and to the README's table.

## Sources

Icons from [game-icons.net](https://game-icons.net), white on transparent, licensed
[CC BY 3.0](https://creativecommons.org/licenses/by/3.0/); the MapRoutes README credits the artists.

| Icon | Source |
|---|---|
| Cave, Surtling | own drawings |
| Crypt | Delapouite, dungeon-gate |
| Dungeon | Delapouite, sarcophagus |
| Village | Delapouite, hut |
| Portal | Lorc, portal |
| Ship | Delapouite, sailboat |
| Ore | Lorc, rock |
| Tar | Lorc, bubbles |
| Berries | Delapouite, raspberry |
| Mushroom | Lorc, mushroom |
| Egg | Delapouite, nest-eggs |
| Nest | Lorc, evil-tree (white block removed by the script) |
| Leviathan | Lorc, turtle-shell |
| Forest | Lorc, pine-tree |
| Swamp | Delapouite, reed |
| Mountain | Lorc, peaks |
| Plains | Lorc, wheat |
| Mistlands | Delapouite, fog |
| Ashlands | Delapouite, smoking-volcano |
| Deep North | Lorc, snowflake-2 |
| Lox | Delapouite, bison |
| Mine | Delapouite, mine-wagon |

## Look

White shape at 104 px in a 128 px cell, with a backdrop behind it: the shape dilated by 6 px, blurred by 3 px, black at
75 % opacity, like the soft shadow of the game's own pins.

Left out on purpose: creatures that roam (Lox is the exception, for tamed herds), Trader and bosses (the game marks
them), Meadows and Ocean, Bees.
