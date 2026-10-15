# Valheim mods — Agent Instructions

Two small client-side BepInEx plugins (C#, .NET Framework 4.8, HarmonyX) built from one repo:

- **ValheimQuickFix** — a bundle of small, independent quality-of-life fixes for things the game gets wrong.
- **SignIcons** — shows item icons on signs. A separate mod because it is new functionality, not a fix, and it
  carries different risk (it drives TextMeshPro through reflection).

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
  bound in `Plugin.Awake`, and every patch method returns early when its toggle is off.
- **Client-side only.** No server sync, no networking, no RPCs. Guard patches with
  `Player.m_localPlayer` so remote players are never affected.
- **Minimal patches.** Prefer Prefix/Postfix over Transpilers. Never replace vanilla methods
  wholesale — keep the game's own logic running.
- **No dependencies** beyond BepInEx, HarmonyX, Unity, and the game assemblies. Do not add
  Jotunn or other mods as dependencies without a reason documented in `docs/Architecture.md`.
- **Never access private game members directly.** The compile-time game libs (`Valheim.GameLibs`) are
  *publicized*, so `Humanoid.m_leftItem`, `Ship.m_body`, etc. compile, but the real game assembly keeps
  them private/protected and the call throws `FieldAccessException` at runtime — even when the code path
  is disabled by a config toggle. Use Harmony field injection (`ref T ___m_field` in patch parameters)
  or `AccessTools.FieldRefAccess`/reflection. Wrap patch bodies in try/catch and log via `Plugin.Log`.
- **Fields drift too.** Game fields get renamed or added between versions (`ItemData.m_equiped` became `m_equipped`,
  `m_worldLevel` appeared later), so code touching one the compile-time libs may not match looks it up by name with
  `AccessTools.Field` and disables itself, with a log line, if it is missing.
- **Interfaces drift.** Game interfaces (`Hoverable`, ...) gain members between versions, and the compile-time libs
  are older than the live game. A class implementing one must declare any newer members as `public virtual`.
- Match the existing code style (4-space indent, block-scoped namespaces, `internal` by default).
- Bump a mod's version only when cutting a release, not on every change. Keep `Plugin.PluginVersion`, the csproj
  `<Version>`, `package/<Mod>/manifest.json`, `package/<Mod>/thunderstore.toml`, and `package/<Mod>/CHANGELOG.md` in
  sync when you do (`scripts/package.sh` fails if the manifest and toml disagree).
- Author credit (`garcipat`) lives in each package README header, the root README, the DLL metadata
  (`Authors`/`Company` in `src/Directory.Build.props`), and an `author` field in each `manifest.json`. Gale reads
  that field to show the author for locally imported mods; Thunderstore's spec doesn't list it (it takes the author
  from the upload team), so confirm it is accepted when publishing.
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
