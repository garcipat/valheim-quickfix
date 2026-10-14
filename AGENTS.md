# Valheim QuickFix — Agent Instructions

A small BepInEx plugin (C#, .NET Framework 4.8, HarmonyX) bundling client-side quality-of-life
fixes for Valheim. One plugin, many small independent fixes.

## Layout

- `src/ValheimQuickFix/Plugin.cs` — plugin entry point, config binding, Harmony bootstrap.
- `src/ValheimQuickFix/Patches/` — one file per fix, each with its own `[HarmonyPatch]` class.
- `docs/` — reference docs (see below). `CHANGELOG.md` — user-facing changes.

## Conventions

- **One fix = one patch file = one config toggle.** Each fix is enabled via a `ConfigEntry<bool>`
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
  or `AccessTools.FieldRefAccess`. Wrap patch bodies in try/catch and log via `Plugin.Log`.
- Match the existing code style (4-space indent, block-scoped namespaces, `internal` by default).
- Bump the version only when cutting a release, not on every change. Keep `Plugin.PluginVersion`, the csproj
  `<Version>`, `package/manifest.json`, and `CHANGELOG.md` in sync when you do. `./scripts/package.sh` builds the Gale/Thunderstore zip.

## Build

```bash
dotnet build src/ValheimQuickFix -c Release
```

Builds without a Valheim install. There is no automated test suite — patches only run inside the
game — so verify behavior in-game and record the manual check in the PR description.

## Adding a fix

Follow [docs/AddingAFix.md](docs/AddingAFix.md). Document the behavior in
[docs/Fixes.md](docs/Fixes.md) and add a `CHANGELOG.md` entry under `Unreleased`.

## Git

- Work on feature branches; `main` stays buildable (CI builds every push and PR).
- Never commit unless the user asks.
