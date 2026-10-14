# Development

## Prerequisites

- .NET SDK 8+ (builds the `net48` target via reference assemblies; no Mono needed to compile)
- `zip` and `python3` for `scripts/package.sh`
- Valheim with [BepInExPack](https://thunderstore.io/c/valheim/p/denikson/BepInExPack_Valheim/)
  installed, for in-game testing

## Build and deploy

```bash
dotnet build src/ValheimQuickFix -c Release
dotnet build src/ValheimQuickFix -c Release -p:PluginsDir="/path/to/Valheim/BepInEx/plugins"
```

The last form copies the DLL into the game after building. Shared settings live in `src/Directory.Build.props`.

## Debugging

- Logs: `Valheim/BepInEx/LogOutput.log`
- Config: `Valheim/BepInEx/config/<plugin guid>.cfg` (`dev.pgarcia.valheim.quickfix.cfg`)
- Enable the BepInEx console via `BepInEx/config/BepInEx.cfg` → `[Logging.Console] Enabled = true`

## CI

`.github/workflows/build.yml` runs `scripts/package.sh` on every push to `main` and every PR, so both mods are built
and zipped exactly as they would be released, and uploads the zips as a workflow artifact.

## Packaging for Gale / Thunderstore

```bash
./scripts/package.sh              # every mod
./scripts/package.sh ValheimQuickFix    # one mod
```

Writes `dist/<Mod>-<version>.zip` containing the DLL plus the files in `package/<Mod>/` (`manifest.json`,
`icon.png`, `README.md`, `CHANGELOG.md`) and the repo's `LICENSE`. In Gale, use the profile's *Import local mod* option and pick the zip.

`package/<Mod>/README.md` is shipped inside the package and shown as the mod description, so keep it player-facing
(features, configuration, installation). Build and development notes belong in `docs/`.

Config options appear on Gale's config page only after the game has been started once with the plugin loaded,
because the `.cfg` file is generated on first run.

## Publishing to Thunderstore

Each mod has a `package/<Mod>/thunderstore.toml` for the [Thunderstore CLI](https://github.com/thunderstore-io/thunderstore-cli)
(`tcli`). It holds what the manifest can't: the upload **categories** (tags), the community, and the build/copy rules.

| Mod             | Categories                       |
| --------------- | -------------------------------- |
| ValheimQuickFix | `client-side`, `tweaks`, `utility` |

The category slugs are Valheim's own; the full list is at
`https://thunderstore.io/api/experimental/community/valheim/category/`.

```bash
./scripts/package.sh <Mod>                                          # builds the DLL and checks manifest <-> toml
tcli build   --config-path package/<Mod>/thunderstore.toml          # writes dist/thunderstore/
tcli publish --config-path package/<Mod>/thunderstore.toml --token <token>
```

Notes:
- `namespace = "garcipat"` must be a Thunderstore team you belong to; the author shown in Gale and on the site is
  that team, not a manifest field. `tcli build` writes its own `manifest.json` from `[package]`, so the extra
  `author` field in `package/<Mod>/manifest.json` (used only by Gale's local import) isn't uploaded.
- `[package]` duplicates the manifest on purpose (tcli needs it). `package.sh` fails if the two disagree.
- This config follows the tcli project-file schema but hasn't been run against Thunderstore yet. Try `tcli build`
  first and look at the zip before publishing.

## Adding a mod

1. Create `src/<Mod>/<Mod>.csproj` containing only `AssemblyName`, `RootNamespace`, and `Version`
   (everything else comes from `Directory.Build.props`), plus `Plugin.cs` and a `Patches/` folder.
2. Create `package/<Mod>/` with `manifest.json`, `thunderstore.toml`, `icon.png` (256×256), `README.md`, and `CHANGELOG.md`.
3. Add a row to the table in the root `README.md`. CI and `scripts/package.sh` pick the new mod up automatically.

## Releasing

1. Bump the version in the mod's `Plugin.cs`, csproj, `package/<Mod>/manifest.json`, and `package/<Mod>/thunderstore.toml`.
2. Move `Unreleased` entries in `package/<Mod>/CHANGELOG.md` under the new version heading.
3. Run `./scripts/package.sh <Mod>`, tag `<Mod>-vX.Y.Z`, and attach the zip to a GitHub release.
