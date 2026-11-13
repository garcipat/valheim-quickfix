# Development

## Prerequisites

- .NET SDK 8+ (builds the `net48` target via reference assemblies; no Mono needed to compile)
- `zip` and `python3` for `scripts/package.sh`
- Valheim with [BepInExPack](https://thunderstore.io/c/valheim/p/denikson/BepInExPack_Valheim/)
  installed, for in-game testing

## Build and deploy

```bash
dotnet build src/ValheimQuickFix -c Release
dotnet build src/SignIcons -c Release
dotnet build src/ItemCompare -c Release
dotnet build src/MoreSkills -c Release
dotnet build src/MapRoutes -c Release
dotnet build src/SignIcons -c Release -p:PluginsDir="/path/to/Valheim/BepInEx/plugins"
```

The last form copies the DLL into the game after building. Shared settings live in `src/Directory.Build.props`.

## Debugging

- Logs: `Valheim/BepInEx/LogOutput.log`
- Config: `Valheim/BepInEx/config/<plugin guid>.cfg` (`dev.pgarcia.valheim.quickfix.cfg`; SignIcons has none; ItemCompare uses `dev.pgarcia.valheim.itemcompare.cfg`; MoreSkills uses `dev.pgarcia.valheim.moreskills.cfg`; MapRoutes has none)
- Enable the BepInEx console via `BepInEx/config/BepInEx.cfg` → `[Logging.Console] Enabled = true`

## CI

`.github/workflows/build.yml` runs `scripts/package.sh` on every push to `main` and every PR, so both mods are built
and zipped exactly as they would be released, and uploads the zips as a workflow artifact.

## Packaging for Gale / Thunderstore

```bash
./scripts/package.sh                          # every mod, as a dev build (0.0.0-dev)
./scripts/package.sh SignIcons                # one mod
./scripts/package.sh --version 0.2.0 SignIcons    # what a release does
```

Writes `dist/<Mod>-<version>.zip` containing the DLL plus the files in `package/<Mod>/` (`manifest.json`,
`icon.png`, `README.md`, `CHANGELOG.md`) and the repo's `LICENSE`. The version is passed to the build, so the DLL
carries it, and is stamped into the zip's `manifest.json`; the repo's `manifest.json` only holds a `0.0.0` placeholder. In Gale, use the profile's *Import local mod* option and pick the zip.

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
| SignIcons       | `client-side`, `building`, `utility` |
| ItemCompare     | `client-side`, `tweaks`, `utility` |
| MoreSkills      | `client-side`, `tweaks`, `utility` |
| MapRoutes       | `client-side`, `tweaks`, `utility` |

The category slugs are Valheim's own; the full list is at
`https://thunderstore.io/api/experimental/community/valheim/category/`.

```bash
./scripts/package.sh <Mod>                                          # builds the DLL and checks manifest <-> toml
tcli build   --config-path package/<Mod>/thunderstore.toml --package-version X.Y.Z   # writes dist/thunderstore/
tcli publish --config-path package/<Mod>/thunderstore.toml --token <token>
```

Notes:
- `namespace = "garcipat"` must be a Thunderstore team you belong to; the author shown in Gale and on the site is
  that team, not a manifest field. `tcli build` writes its own `manifest.json` from `[package]`, so the extra
  `author` field in `package/<Mod>/manifest.json` (used only by Gale's local import) isn't uploaded.
- `[package]` duplicates the manifest on purpose (tcli needs it). `package.sh` fails if name, description, website or
  dependencies disagree. The version is a `0.0.0` placeholder in both; pass the real one with `--package-version`.
- This config follows the tcli project-file schema but hasn't been run against Thunderstore yet. Try `tcli build`
  first and look at the zip before publishing.

## Adding a mod

1. Create `src/<Mod>/<Mod>.csproj` containing only `AssemblyName`, `RootNamespace`, and `Version`
   (everything else comes from `Directory.Build.props`), plus `Plugin.cs` and a `Patches/` folder.
2. Create `package/<Mod>/` with `manifest.json`, `thunderstore.toml`, `icon.png` (256×256), `README.md`, and `CHANGELOG.md`.
3. Add a row to the table in the root `README.md`. CI and `scripts/package.sh` pick the new mod up automatically.
4. Add the mod to the `options` list and the tag patterns in `.github/workflows/release.yml`, or it can't be released.
   `scripts/package.sh` (and so CI) fails until you do.

## Releasing

A release is a **tag `<Mod>-vX.Y.Z` on `main`**, for example `SignIcons-v0.2.0` or `ValheimQuickFix-v1.0.0-rc.1`. The
prefix keeps the mods' versions independent, and the tag is the only place a version is written: no version in any file.
The tag is set by the Release workflow when a **release pull request** is merged, so the tagged commit already has the
mod's changelog closed for that version.

Use the **`release-mod` skill** (`/release-mod <Mod>`, in `.claude/skills/release-mod/`). It checks you are on an up-to-date
`main`, reads the mod's `Unreleased` changelog entries, proposes the next version with the reasons (new feature or breaking
change → minor while 0.x, fixes only → patch; first release → 0.1.0), and asks you to confirm. Then it opens the release
pull request `Release <Mod> X.Y.Z` from a branch `release/<Mod>-vX.Y.Z`, whose only change is
`scripts/update-changelog.sh` renaming `Unreleased` to `## [X.Y.Z] - date`. You merge it (`main` needs a code-owner review,
so typically `gh pr merge <n> --squash --admin --delete-branch`).

The merge starts the `Release` workflow (`.github/workflows/release.yml`), which:

1. works out the mod and version from the branch name, and refuses a merge commit that is not on `main`, a changelog
   without the `[X.Y.Z]` section, or a tag that already exists elsewhere (`scripts/resolve-release.sh`). Pull requests from
   other branches, and closed ones that were not merged, start no release;
2. reads the `[X.Y.Z]` section as the release notes (`scripts/update-changelog.sh`);
3. builds with that version and packages the zip (`scripts/package.sh --version X.Y.Z`);
4. tags the merge commit `<Mod>-vX.Y.Z` (a re-run keeps a tag it already made on the same commit);
5. creates the GitHub release `<Mod> X.Y.Z` with the zip (a hyphenated version is a pre-release).

**By hand:** pushing a tag on `main` (`git tag -a <Mod>-vX.Y.Z -m "..." && git push origin <Mod>-vX.Y.Z`) still releases.
Then the workflow renames `Unreleased` on its own copy for the notes, and afterwards pushes a
`release/changelog-<Mod>-X.Y.Z` branch with the renamed section and tries to open a pull request `Changelog: release
<Mod> X.Y.Z` for it. That needs "Allow GitHub Actions to create pull requests" under Settings → Actions → General;
without it, open the pull request yourself and merge it before other changes touch the changelog. It skips this with a
warning if the mod's `Unreleased` entries changed on `main` since the tag; then rename the section by hand.

**Dry run:** Actions → Release → *Run workflow*, pick the mod and a version. It builds the package from `main`
and uploads it as a workflow artifact. No tag, no release, no commit. (`/release-mod <Mod> dry-run` starts it for you.)

Releases are listed newest first in one flat list for the whole repo; GitHub cannot group them by mod. The `<Mod> X.Y.Z`
titles and `<Mod>-v...` tags keep them tellable apart. Players get a per-mod listing on Thunderstore.

Publishing to Thunderstore is not part of the workflow: use `tcli` as described above.
