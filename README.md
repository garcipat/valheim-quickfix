# Valheim mods

By [garcipat](https://github.com/garcipat).

Small client-side BepInEx mods for Valheim, built from one repo. Each mod is its own plugin with its own
package, version, and changelog.

| Mod | What it does | Player docs |
| --- | ------------ | ----------- |
| **ValheimQuickFix** | Quality-of-life fixes: restore your shield after a bow, push boats from the shore. | [README](package/ValheimQuickFix/README.md) · [Changelog](package/ValheimQuickFix/CHANGELOG.md) |

Mods here are client-side only and need [BepInExPack Valheim](https://thunderstore.io/c/valheim/p/denikson/BepInExPack_Valheim/).
Install them through a mod manager such as Gale, or build them from source.

## Build

```bash
dotnet build src/ValheimQuickFix -c Release
./scripts/package.sh          # builds every mod and writes dist/<Mod>-<version>.zip for Gale / Thunderstore
```

Building needs no Valheim install. See [docs/Development.md](docs/Development.md) for deploying into a game,
debugging, and releasing.

## License

[MIT](LICENSE). Valheim and its game assemblies belong to Iron Gate and are not part of this repo or the
mod packages.

## Docs

- [Architecture](docs/Architecture.md) · [Development](docs/Development.md)
- [QuickFix fixes](docs/Fixes.md) · [Adding a fix](docs/AddingAFix.md)
