# Valheim mods

By [garcipat](https://github.com/garcipat).

Small client-side BepInEx mods for Valheim, built from one repo. Each mod is its own plugin with its own
package, version, and changelog.

| Mod | What it does | Player docs |
| --- | ------------ | ----------- |
| **ValheimQuickFix** | Quality-of-life fixes: restore your shield after a bow, push boats from the shore, merge partial stacks and restore your slots on a tombstone pickup. | [README](package/ValheimQuickFix/README.md) · [Changelog](package/ValheimQuickFix/CHANGELOG.md) |
| **SignIcons** | Show item icons on signs with `[ItemName]`. | [README](package/SignIcons/README.md) · [Changelog](package/SignIcons/CHANGELOG.md) |
| **ItemCompare** | Hover gear to compare it with what you wear: old -> new stats in green or red. | [README](package/ItemCompare/README.md) · [Changelog](package/ItemCompare/CHANGELOG.md) |
| **MoreSkills** | New skills you level by playing. Hauling: pull loaded carts more easily the more you haul. | [README](package/MoreSkills/README.md) · [Changelog](package/MoreSkills/CHANGELOG.md) |

All are client-side only and need [BepInExPack Valheim](https://thunderstore.io/c/valheim/p/denikson/BepInExPack_Valheim/).
Install them through a mod manager such as Gale, or build them from source.

## Build

```bash
dotnet build src/ValheimQuickFix -c Release
dotnet build src/SignIcons -c Release
dotnet build src/ItemCompare -c Release
dotnet build src/MoreSkills -c Release
./scripts/package.sh          # builds every mod and writes dist/<Mod>-<version>.zip for Gale / Thunderstore
```

Building needs no Valheim install. See [docs/Development.md](docs/Development.md) for deploying into a game,
debugging, and releasing.

## License

[MIT](LICENSE). Valheim and its game assemblies belong to Iron Gate and are not part of this repo or the
mod packages.

## Docs

- [Architecture](docs/Architecture.md) · [Development](docs/Development.md)
- [QuickFix fixes](docs/Fixes.md) · [Adding a fix](docs/AddingAFix.md) · [Sign icons](docs/SignIcons.md) · [Item compare](docs/ItemCompare.md) · [More skills](docs/MoreSkills.md)
