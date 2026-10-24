# Valheim mods — Agent Instructions

Four small client-side BepInEx plugins (C#, .NET Framework 4.8, HarmonyX) built from one repo:

- **ValheimQuickFix** — a bundle of small, independent quality-of-life fixes for things the game gets wrong.
- **SignIcons** — shows item icons on signs. A separate mod because it is new functionality, not a fix, and it
  carries different risk (it drives TextMeshPro through reflection).
- **ItemCompare** — compares a hovered item's stats with the equipped one in the tooltip. Another separate mod: new
  functionality that reads and appends to tooltip text.
- **MoreSkills** — adds new skills (Hauling: pulling loaded carts). A separate mod: new functionality that adds skill
  types the game does not know, so it carries its own save-compatibility risk.

New work that isn't a fix for something the game gets wrong belongs in its own mod, not in QuickFix.

## Layout

- `src/Directory.Build.props` — target framework, package references, and the optional deploy step shared by all mods.
- `src/<Mod>/<Mod>.csproj` — one project per mod; `Plugin.cs` is the entry point (config binding, Harmony bootstrap).
- `src/<Mod>/Patches/` — one file per fix/feature, each with its own `[HarmonyPatch]` class(es).
- `package/<Mod>/` — the Thunderstore package skeleton: `manifest.json`, `thunderstore.toml` (tcli publish config with
  the categories), `icon.png`, player-facing `README.md`, `CHANGELOG.md`.
- `scripts/package.sh` — builds mods and writes `dist/<Mod>-<version>.zip`.
- `docs/` — reference docs for developers.

## Conventions

- **QuickFix: one fix = one patch file = one config toggle.** Each fix is enabled via a `ConfigEntry<bool>`
  bound in the plugin's startup, and every patch method returns early when its toggle is off.
- **Config sections are fixed: `Inventory`, `Gear`, `Crafting`, `Building & Travel`, `Enemies & Drops`,
  `General`.** Put a new setting in the one that fits. Never add a section without asking first; there are too many config pages already.
- **Client-side only.** No server sync, no networking, no RPCs. Remote players must never be affected.
- **Minimal patches.** Prefer Prefix/Postfix over Transpilers. Never replace vanilla methods
  wholesale — keep the game's own logic running.
- **No dependencies** beyond BepInEx, HarmonyX, Unity, and the game assemblies. Do not add
  Jotunn or other mods as dependencies without a reason documented in `docs/Architecture.md`.
- Match the existing code style (4-space indent, block-scoped namespaces, `internal` by default).
- **Versions live in the release tag, not in files.** A release is a tag `<Mod>-vX.Y.Z` pushed on `main`; the Release
  workflow builds that version into the DLL and stamps it into the
  zip's manifest. `manifest.json` and `thunderstore.toml` hold a `0.0.0` placeholder, and local builds report `0.0.0-dev`.
  Never write a real version into those files.
- To release, use the `release-mod` skill (`/release-mod <Mod>`): it reads the mod's changelog, proposes the version,
  and after one confirmation pushes the tag. The workflow does the rest and opens a pull request with the renamed changelog section
  back to `main`. A new mod must be added to the workflow's options and tag patterns.
- Every player-visible change adds a line under `## [Unreleased]` in `package/<Mod>/CHANGELOG.md` in the same PR;
  those lines become the release notes, and a release without any stops.
- `package/<Mod>/README.md` ships inside the package and is shown as the mod description, so keep it player-facing.
  Build and development notes go in `docs/`.

## Build

```bash
dotnet build src/<Mod> -c Release
./scripts/package.sh [Mod ...]
```

Builds without a Valheim install. There is no automated test suite — patches only run inside the
game — so verify behavior in-game and record the manual check in the PR description.

## Adding a fix to QuickFix

Follow [docs/AddingAFix.md](docs/AddingAFix.md). Document the behavior in
[docs/Fixes.md](docs/Fixes.md) and add an entry under `Unreleased` in `package/ValheimQuickFix/CHANGELOG.md`.

## Git

- Work on feature branches; `main` stays buildable (CI builds and packages every mod on each push and PR).
- Never commit unless the user asks.
- Don't add AI/tool attribution (co-author trailers, "generated with" lines) to commits or PR descriptions.
- When stacking PRs, set each PR's base to `main`, not to the previous branch, and merge in order. A PR merged
  into its stack base never reaches `main`.
