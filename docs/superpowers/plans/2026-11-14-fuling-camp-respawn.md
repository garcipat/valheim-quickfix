# Fuling Camps Respawn Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** A QuickFix toggle (off by default) that lets cleared Fuling spawners near a standing totem respawn, at most one
Fuling per totem per interval.

**Architecture:** One Harmony Prefix/Postfix pair on `CreatureSpawner.UpdateSpawner`. The prefix, for one-shot Fuling
spawners whose creature is gone and that have a ready totem in range, temporarily sets `m_respawnTimeMinuts` to the
configured interval so vanilla's own respawn path runs; the postfix restores the field and, if the spawner's spawn
connection changed, starts that totem's cooldown.

**Tech Stack:** C#, .NET Framework 4.8, HarmonyX, BepInEx 5, Valheim game libs (compile-time libs are older than the
live game; decompile `~/share/assembly_valheim.dll` with `ilspycmd` to check members).

**Spec:** `docs/superpowers/specs/2026-11-14-fuling-camp-respawn-design.md`

## Global Constraints

- Config section `Enemies & Drops`; keys `FulingCampsRespawn` (bool, default `false`), `FulingCampsRespawnMinutes`
  (float, 10–120, default `30`; waves of up to 2 Fulings per totem), `FulingCampsTotemRadius` (float, default `50`).
- Fuling prefabs: `Goblin`, `GoblinBrute`, `GoblinShaman`. Totem prefab: `goblin_totem` (confirm in Task 2).
- Only touch spawners with `m_respawnTimeMinuts <= 0`. Never leave a changed `m_respawnTimeMinuts` behind.
- No networking, no RPCs, no new dependencies, no Transpiler, vanilla `UpdateSpawner` always runs.
- Style: 4-space indent, block-scoped namespace, `internal`, XML doc summary + remarks like `MoreBearsPatch.cs`.
- No commits unless the user asks; no AI attribution in commits or PRs.

## Review Focus

- **Spawner in a spawn group** (`m_spawnGroup != null`): vanilla calls `SpawnWeighted()`, which may spawn from a
  different spawner of the group. Expect: no double spawn and the totem cooldown still starts. Check in Task 2 whether
  village spawners use groups; if they do, start the cooldown from any group member's connection change (compare each
  member's `Spawned` connection before/after), not just `__instance`'s.
- **Several spawners due in the same tick near one totem:** expect only the first to spawn. The prefix must mark the
  totem "reserved" for the rest of the frame (or check the cooldown again in the postfix order) so a second spawner in
  the same frame is refused.
- **Exception inside vanilla `UpdateSpawner`:** expect the field restored anyway; use a Harmony `Finalizer` or restore
  in the postfix and also in a finalizer.
- **Totem smashed while a Fuling it fed is alive:** expect that Fuling to stay; only future respawns stop.
- **Remote zone owner:** `UpdateSpawner` returns early when not owner; expect nothing to happen and no cost beyond the
  owner check (do the owner check first in the prefix).

---

### Task 1: Patch and settings

**Files:**
- Create: `src/ValheimQuickFix/Patches/FulingCampsRespawnPatch.cs`
- Modify: `src/ValheimQuickFix/Plugin.cs` (fields next to `MoreWrithansEnabled`; binds after the `MoreWrithans` bind)

**Interfaces:**
- Produces: `Plugin.FulingCampsRespawnEnabled` (`ConfigEntry<bool>`), `Plugin.FulingCampsRespawnMinutes`
  (`ConfigEntry<float>`), `Plugin.FulingCampsTotemRadius` (`ConfigEntry<float>`).

- [ ] **Step 1: Bind the three settings in `Plugin.Awake`**

Descriptions (player-facing, follow the MoreBears tone):
- `FulingCampsRespawn`: "Fuling camps slowly refill while a totem stands: a cleared Fuling near a totem comes back
  after a while, one at a time per totem. Smash the totem to clear the camp for good. Only works when your machine runs
  the spawning (singleplayer or hosting); on a server, everyone sees the respawned Fulings."
- `FulingCampsRespawnMinutes`: "Minutes between two Fulings coming back at the same totem (Fuling camps respawn)."
- `FulingCampsTotemRadius`: "How far from a totem, in metres, Fuling spawners refill (Fuling camps respawn)."

- [ ] **Step 2: Write `FulingCampsRespawnPatch`**

`[HarmonyPatch(typeof(CreatureSpawner), "UpdateSpawner")] internal static class FulingCampsRespawnPatch` with:

- `private static void Prefix(CreatureSpawner __instance, ref State __state)` and
  `private static void Postfix(CreatureSpawner __instance, State __state)` plus
  `private static Exception Finalizer(CreatureSpawner __instance, State __state, Exception __exception)` that restores
  the field if the postfix did not (return `__exception`).
- `private struct State { public bool Active; public float OldMinutes; public ZDOID OldSpawn; public ZDO Totem; }`
- `private static readonly Dictionary<ZDOID, DateTime> NextAllowed` keyed by totem ZDO uid, using `ZNet.instance.GetTime()`.

Prefix order (cheapest first, return without touching anything on any miss):
1. toggle on; `__instance` has a ZNetView whose ZDO is valid and `IsOwner()`; `m_respawnTimeMinuts <= 0`;
   `m_creaturePrefab` name in the Fuling set.
2. spawn connection (`GetConnectionZDOID(ZDOExtraData.ConnectionType.Spawned)`) is not none and
   `ZDOMan.instance.GetZDO(id) == null` (creature gone).
3. nearest standing totem within the radius (helper below) whose `NextAllowed` entry is absent or `<= now`.
4. Set `__state` (old minutes, old spawn id, totem), set `m_respawnTimeMinuts = FulingCampsRespawnMinutes.Value`
   (clamp to at least 1), and set `NextAllowed[totem] = now + 1 s` as a same-frame reservation (Review Focus 2).

Postfix: if `__state.Active`, restore the field; if the spawn connection id now differs from `OldSpawn`, set
`NextAllowed[totem] = now + interval` and log at Info `FulingCampsRespawn: respawned <prefab> at <pos>, totem <dist> m`.

- [ ] **Step 3: Write `private static ZDO FindTotem(Vector3 pos, float radius)`**

Iterate `ZNetScene.instance`'s private `m_instances` (`AccessTools.FieldRefAccess<ZNetScene, Dictionary<ZDO, ZNetView>>`
cached in a static field) and return the closest ZDO with `GetPrefab() == "goblin_totem".GetStableHashCode()` within
`radius`. Cache the result per spawner (`Dictionary<ZDOID, (ZDO totem, float time)>`, 30 s by `Time.time`) and treat a
cached ZDO as gone when `!totem.IsValid()` or `ZDOMan.instance.GetZDO(totem.m_uid) == null`.

- [ ] **Step 4: Build**

Run: `dotnet build src/ValheimQuickFix -c Release`
Expected: `Build succeeded.` with 0 errors and no new warnings. If a member is missing from the compile-time libs, use
`AccessTools` by name, as `SpawnTuning.cs` does.

### Task 2: In-game check, defaults and docs

**Files:**
- Modify: `docs/Fixes.md` (new `## Fuling camps respawn` after `## More Writhans`, same layout: Config, Patch,
  Problem, Behavior, Known limits; describe behavior, not patched methods)
- Modify: `package/ValheimQuickFix/README.md` (config table row after `MoreWrithans`, default `false`)
- Modify: `package/ValheimQuickFix/CHANGELOG.md` (`### Added` under `## [Unreleased]`, one player-facing entry ending
  "Turn it on with `FulingCampsRespawn`.")

- [ ] **Step 1: Package a dev build** — `./scripts/package.sh ValheimQuickFix`, copy the zip to `~/share` as
  `ValheimQuickFix-dev.zip` for the user.
- [ ] **Step 2: User checks in-game** (singleplayer, toggle on, interval set to 1 for testing): totem prefab name is
  `goblin_totem`; a cleared village refills one Fuling at a time; nothing refills after smashing the totem; 50 m covers
  the huts; whether village spawners use spawn groups (Review Focus 1). Read `~/share/LogOutput.log` for the Info lines
  and errors.
- [ ] **Step 3: Adjust** prefab names, radius default or group handling from what the check showed; rebuild.
- [ ] **Step 4: Write the docs** listed above.
- [ ] **Step 5: Build** — `dotnet build src/ValheimQuickFix -c Release`, expected `Build succeeded.`
