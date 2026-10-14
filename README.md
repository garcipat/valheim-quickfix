# Valheim QuickFix

A small BepInEx plugin collecting quality-of-life fixes for Valheim. Client-side only.

## Fixes

- **Restore shield** — when a bow or two-handed weapon replaces your shield, switching back to a
  one-handed weapon re-equips the shield automatically. Toggle: `RestoreShield` in
  `BepInEx/config/dev.pgarcia.valheim.quickfix.cfg`.

## Install

Requires [BepInExPack Valheim](https://thunderstore.io/c/valheim/p/denikson/BepInExPack_Valheim/).
Copy `ValheimQuickFix.dll` into `Valheim/BepInEx/plugins/`.

## Build

```bash
dotnet build src/ValheimQuickFix -c Release
```

Builds without a Valheim install (publicized game assemblies come from the BepInEx NuGet feed).
To copy the DLL into your game after each build:

```bash
dotnet build src/ValheimQuickFix -c Release -p:PluginsDir="/path/to/Valheim/BepInEx/plugins"
```

## Docs

- [Architecture](docs/Architecture.md) · [Fixes](docs/Fixes.md) · [Development](docs/Development.md) · [Adding a fix](docs/AddingAFix.md)
- [Changelog](CHANGELOG.md)

Note: the NuGet game libs are version 0.202.14, older than the current game. Patched method
signatures are stable, but verify in-game after game updates.
