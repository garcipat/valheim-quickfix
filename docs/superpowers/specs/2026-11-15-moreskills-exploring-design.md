# MoreSkills: Exploring — design

Date: 2026-11-15
Status: approved in chat

## Intent

A fifth skill that levels by uncovering the map and, as it levels, uncovers a wider circle around you. Like the other
skills: doing the thing makes the thing better.

## Approaches

- **A. Only newly revealed map counts** (picked). Walking known ground earns nothing, so it can't be farmed and the
  name fits. Levels fast early, slower once most of the world is open.
- **B. Any travel, more for new map.** Steady all game, but too close to vanilla Run.

## Mechanism (vanilla)

`Minimap.UpdateExplore` calls the private `Explore(Vector3 p, float radius)` every `m_exploreInterval` (2 s) with
`m_exploreRadius` (100 m) around the local player. That reveals each fog pixel in the circle through
`Explore(int x, int y)`, which returns true only for a pixel not yet in the player's own explored layer. Nothing else
calls `Explore(Vector3, float)`; loading the map and `exploremap` call the pixel overload directly, so they never count.

Cartography table data goes to a separate layer (`m_exploredOthers`, via `ExploreOthers`). Walking through an area a
friend shared still reveals it into your own layer, so it counts. Agreed in chat: you level by going somewhere new to
you.

## Design

- **Effect:** a Prefix on `Explore(Vector3, float)` scales the `radius` argument by
  `1 + MaxRadiusBonus * skillFactor`. No field is changed, so nothing needs restoring. Level 0 is vanilla.
- **Levelling:** the same Prefix starts counting, a Postfix on `Explore(int, int)` counts pixels that returned true,
  and a Postfix on `Explore(Vector3, float)` turns the count into meters of new ground:
  `pixels * m_pixelSize² / (2 * radius)`. Dividing by the circle's width keeps a bigger radius from speeding up its
  own levelling and makes the pace independent of the map's pixel size. `RaiseSkill(Exploring, meters * GainPerMeter)`.
- **Teleports and loading:** the first tick after a gap (more than 5 s since the last one: load, respawn, logout) and any tick that moved more than `MaxStep` (60 m in 2 s, faster than any ship) only reset the reference
  position. Arriving through a portal is not exploring.
- **Sailing counts**, through the same rule; a ship is about twice running speed.
- **Rate (my call, tune in-game):** `GainPerMeter` 0.02. Running through fresh land at about 7 m/s is about 0.14 per
  second. Exploring comes up on every trip, unlike pulling a cart, so it stays below the faster Hauling rate.
- **Icon:** the cartography table piece icon (`piece_cartographytable`), null until ZNetScene exists.
- **Config:** `[Exploring] Enabled` (true), `MaxRadiusBonus` (1.0 = 200 m at level 100, range 0 to 2).
- **Off:** both patches return early; vanilla radius, no levelling.

## Non-goals

- Reveals from other players or cartography tables earn nothing.
- No change to the explore interval, fog rendering or the shared map.
