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

## Releasing

1. Bump the version in `Plugin.cs` and `ValheimQuickFix.csproj`.
2. Move `Unreleased` entries in `CHANGELOG.md` under the new version heading.
3. Tag `vX.Y.Z` and attach the built DLL to a GitHub release.
