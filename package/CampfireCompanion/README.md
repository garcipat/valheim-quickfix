# Campfire Companion

By [garcipat](https://github.com/garcipat) · [Source code on GitHub](https://github.com/garcipat/valheim-quickfix)

Your character no longer sits alone at the main menu campfire. Pick a creature and it stands on the other side of
the fire, looking into the flames and playing its idle animation: a Lox, a troll, a wolf, a Fuling shaman, Haldor and
more, or a random one each time the menu loads.

It is only scenery in the menu. It makes no sound, can't be touched, and nothing changes once you load a world.
Client-side only; other players are not affected.

## Configuration

The settings are in `Valheim/BepInEx/config/dev.pgarcia.valheim.campfirecompanion.cfg`, or in your mod manager's config
editor. Changes show right away while you are in the main menu.

| Setting | Default | What it does |
| ------- | ------- | ------------ |
| `Companion` | `None` | The creature at the fire. `None` leaves the menu as it is; `Random` picks a new one each time the menu loads. |
| `Stars` | `0` | Star level from 0 to 2: starred creatures get the colouring and extra parts they have in the world. |

The companions, by biome: Deer, Greydwarf, Troll, Bear, Draugr, Skeleton, Ghost, Wraith, Blob, Oozer, Kvastur,
Surtling, Wolf, Fenring, Ulv, Drake, Cultist, Lox, Unbjorn, Fuling, Fuling Berserker, Fuling Shaman, Seeker Soldier,
Dvergr, Asksvin, Charred Mage, Morgen, Fallen Valkyrie, Moose, and the traders Haldor, Hildir and the Bog Witch. The
biggest ones are shrunk a little so they fit next to the fire.

## Installation

Install through a mod manager such as Gale or r2modman. It installs
[BepInExPack Valheim](https://thunderstore.io/c/valheim/p/denikson/BepInExPack_Valheim/) automatically.

For a manual install, put `CampfireCompanion.dll` into `Valheim/BepInEx/plugins/`.

## Feedback

Bug reports, ideas, and source code: <https://github.com/garcipat/valheim-quickfix>

## License

MIT. See the `LICENSE` file in the package, or the source repository.

Icon Lox silhouette by Delapouite from [game-icons.net](https://game-icons.net), licensed under
[CC BY 3.0](https://creativecommons.org/licenses/by/3.0/).
