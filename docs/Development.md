# Development

## Prerequisites

- .NET SDK 8+ (builds the `net48` target via reference assemblies; no Mono needed to compile)
- Valheim with [BepInExPack](https://thunderstore.io/c/valheim/p/denikson/BepInExPack_Valheim/)
  installed, for in-game testing

## Build and deploy

```bash
dotnet build src/ValheimQuickFix -c Release
dotnet build src/ValheimQuickFix -c Release -p:PluginsDir="/path/to/Valheim/BepInEx/plugins"
```

The second form copies the DLL into the game after building.

## Debugging

- Logs: `Valheim/BepInEx/LogOutput.log`
- Config: `Valheim/BepInEx/config/dev.pgarcia.valheim.quickfix.cfg`
- Enable the BepInEx console via `BepInEx/config/BepInEx.cfg` → `[Logging.Console] Enabled = true`

## CI

`.github/workflows/build.yml` builds on every push to `main` and every PR, and uploads the DLL
as a workflow artifact.

## Packaging for Gale / Thunderstore

```bash
./scripts/package.sh
```

Builds the plugin and writes `dist/ValheimQuickFix-<version>.zip` containing the DLL plus
`manifest.json`, `icon.png`, `README.md`, and `CHANGELOG.md` (sources in `package/`). In Gale, use
the profile's *Import local mod* option and pick the zip. The icon, README, and dependency on
BepInExPack then show up in Gale; a DLL copied into `plugins/` by hand shows none of them.

`README.md` is shipped inside the package and shown as the mod description, so keep it player-facing
(features, configuration, installation). Build and development notes belong in `docs/`.

Config options appear on Gale's config page only after the game has been started once with the
plugin loaded, because the `.cfg` file is generated on first run.

## Releasing

1. Bump the version in `Plugin.cs`, `ValheimQuickFix.csproj`, and `package/manifest.json`.
2. Move `Unreleased` entries in `CHANGELOG.md` under the new version heading.
3. Run `./scripts/package.sh`, tag `vX.Y.Z`, and attach the zip to a GitHub release.
