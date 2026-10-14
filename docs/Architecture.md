# Architecture

## Overview

Valheim QuickFix is a single [BepInEx 5](https://github.com/BepInEx/BepInEx) plugin. On startup
`Plugin.Awake` binds config entries and calls `Harmony.PatchAll()`, which discovers every
`[HarmonyPatch]` class in the assembly. Each fix lives in its own class under
`src/ValheimQuickFix/Patches/`.

```
Plugin (BaseUnityPlugin)
 ├─ Config: one ConfigEntry<bool> per fix
 └─ Harmony.PatchAll()
     └─ Patches/<Fix>Patch.cs   (Prefix / Postfix on vanilla methods)
```

## Dependencies

| Package            | Purpose                                              |
| ------------------ | ---------------------------------------------------- |
| BepInEx.BaseLib    | Plugin base class, config system, logging            |
| HarmonyX           | Runtime method patching                              |
| UnityEngine.Modules| Unity types the game assemblies reference            |
| Valheim.GameLibs   | Publicized game assemblies (BepInEx NuGet feed)      |

`Valheim.GameLibs` lets the project build in CI without owning the game. It is older than the
live game; patched signatures are stable, but re-check after major game updates.

## Design decisions

- **Client-side only.** Fixes change local behavior and never touch networking, so the mod works
  on any server and is not required on dedicated servers.
- **Patches over replacements.** Prefix/Postfix keep vanilla logic intact and coexist better
  with other mods patching the same methods.
- **No Jotunn.** The mod adds no items, recipes, or assets, so the extra dependency isn't needed.
