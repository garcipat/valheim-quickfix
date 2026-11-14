# Fuling camps respawn — design

A ValheimQuickFix toggle, off by default: Fuling villages that still have a standing totem slowly refill with Fulings,
two at a time, the way tar pits keep producing Growths. Smashing the totem ends it, and the area stays cleared.

## Why

Once a Plains village is cleared it stays empty forever. With this toggle a village with a standing totem becomes a
place to come back to, and the totem becomes the thing to destroy if you want the camp gone for good.

## Goals

- Cleared Fuling spawners near a standing totem respawn their Fuling after a while.
- Never all at once: each totem brings back at most two Fulings per interval, and each Fuling waits the interval after its death.
- Destroying a totem permanently stops respawns in its area.
- Off by default; three settings in `Enemies & Drops`.

## Non-goals and anti-goals

- No new spawners, no more Fulings than the village was built with. Each spawner still holds at most one creature.
- Nothing other than Fulings is affected, even inside a totem's radius.
- Spawners that vanilla already respawns keep their vanilla timing.
- No networking. Spawning is decided by the zone owner; on a server, other players see the respawned Fulings. This
  is accepted because the toggle is opt-in (user's call, against the usual "remote players unaffected" rule).

## Approaches

1. **Village-scoped** — find the village (`Location`) a spawner belongs to and respawn while a totem stands anywhere
   in it. Needs the location lookup and a per-village timer.
2. **Totem radius (picked)** — every standing totem covers a circle; one-shot Fuling spawners inside a circle respawn,
   and the shared one-at-a-time timer belongs to the totem. Simpler, no location lookup, and a village with two
   totems refills faster, which fits "the totems feed the camp".
3. **Remember totems on first visit** — rejected: breaks when the totem is smashed while the player is away.

## Design

- Settings (`Enemies & Drops`): toggle (default off), minutes between waves, 10–120 (default 30; each wave brings back up to 2 Fulings per totem, and each Fuling also waits that long after its death; tar pit Growths, for reference, respawn 60 min after death), totem radius in
  metres (default 50).
- Hook `CreatureSpawner.UpdateSpawner` (vanilla runs every few seconds on the owner). Only for spawners with
  `m_respawnTimeMinuts <= 0` whose `m_creaturePrefab` is a Fuling (`Goblin`, `GoblinBrute`, `GoblinShaman`).
- When such a spawner has spawned before and its creature no longer exists, look for a standing totem within the
  radius. The loaded objects are enough: a spawner only runs while its area is loaded around a player, a totem within
  the radius is loaded with it, and a smashed totem's object is gone whoever smashed it. (Builder's call: the live
  game's sector lookup on `ZDOMan` has a different signature than the compile-time libs.)
- If a totem is found and that totem's cooldown has passed, let this spawner respawn and restart the cooldown.
  Several waiting spawners race for the turn; whichever ticks first wins (spawner tick order is already randomised).
- The vanilla checks still apply: player in trigger range, not in a player base, day/night, global keys.
- Totem cooldowns are kept in memory per session. After a restart, a totem may allow one respawn right away.

## Left to the builder

- Confirm in-game: the totem prefab name (expected `goblin_totem`), the Fuling prefab names, and whether 50 m covers
  a typical village. `tools/CreatureProbe` or a log line can help.
- How to make vanilla treat a one-shot spawner as respawnable for one tick without replacing `UpdateSpawner`
  (e.g. a prefix that temporarily sets `m_respawnTimeMinuts` and a postfix that restores it, or clearing the spawn
  connection before vanilla runs).
- Efficient totem lookup and caching (e.g. cache per spawner for a short time).
