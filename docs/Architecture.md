# Architecture

## Overview

The repo holds three independent [BepInEx 5](https://github.com/BepInEx/BepInEx) plugins. On startup each
`Plugin.Awake` binds its config entries and calls `Harmony.PatchAll()`, which discovers every `[HarmonyPatch]`
class in that assembly.

```
src/
 ├─ Directory.Build.props        shared target framework, packages, deploy step
 ├─ ValheimQuickFix/             fixes for things the game gets wrong
 │   ├─ Plugin.cs                one ConfigEntry<bool> per fix
 │   └─ Patches/<Fix>Patch.cs    Prefix / Postfix on vanilla methods
 ├─ SignIcons/                   item icons on signs
 │   ├─ Plugin.cs
 │   └─ Patches/SignIconPatch.cs
 └─ ItemCompare/                 old -> new stats against the equipped item in tooltips
     ├─ Plugin.cs                Mode / CompareKey config
     ├─ StatDiff.cs              which stats, how they are compared and coloured
     └─ Patches/TooltipComparePatch.cs
package/<Mod>/                   Thunderstore skeleton (manifest, thunderstore.toml, icon, README, CHANGELOG)
```

The mods share nothing at runtime. They are separate plugins with separate GUIDs, configs, and releases, so a
break in one (for example after a game update) doesn't affect the other.

## Dependencies

| Package            | Purpose                                              |
| ------------------ | ---------------------------------------------------- |
| BepInEx.BaseLib    | Plugin base class, config system, logging            |
| HarmonyX           | Runtime method patching                              |
| UnityEngine.Modules| Unity types the game assemblies reference            |
| Valheim.GameLibs   | Publicized game assemblies (BepInEx NuGet feed)      |

`Valheim.GameLibs` lets the project build in CI without owning the game. It is older than the live game
(0.202 vs 1.0.x); patched signatures are mostly stable, but see the notes in `AGENTS.md` about private members
and interface drift. TextMeshPro and `UnityEngine.UI` are not available as packages, so code that needs them
(SignIcons) uses reflection.

## Design decisions

- **Client-side only.** Mods change local behavior and never add networking, so they work on any server and are
  not required on dedicated servers.
- **Patches over replacements.** Prefix/Postfix keep vanilla logic intact and coexist better with other mods
  patching the same methods.
- **No Jotunn.** The mods add no items, recipes, or assets, so the extra dependency isn't needed.
- **Several mods, one repo.** Shared build setup and CI, but separate packages: new functionality gets its own mod
  instead of growing QuickFix.
