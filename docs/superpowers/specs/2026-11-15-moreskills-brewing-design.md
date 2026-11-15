# MoreSkills: Brewing — design

Date: 2026-11-15
Status: approved in chat

## Intent

Potions come from tapping a fermenter, and vanilla ties no skill to that: a batch is always the recipe's fixed count
(6 for meads). A Brewing skill levels by filling and tapping fermenters and gives a chance at a bigger batch. Brewing
is rare (a barrel takes about 40 real minutes), so the curve is set by batches, not by the game's XP table.

Vanilla already covers the cauldron: mead bases are a stackable craft at a station with a skill (Cooking), so they get
the general crafting bonus chance. That stays untouched.

## Approaches

- **Bonus:** a chance per tap (picked, like vanilla's Cooking bonus) vs. guaranteed extra potions growing with level
  (a flat buff with no surprise).
- **Curve:** a fixed XP per potion on the game's own curve made the first batch give several levels, which felt
  unnatural. Picked: the skill sets its own curve in batches and converts it into XP.

## Mechanism (vanilla)

- `Fermenter.Interact` (local) on an empty fermenter calls the private `AddItem(Humanoid, ItemData)`, which removes
  the mead base from the user's inventory and sends `RPC_AddItem` to the owner. It returns true only when the base was
  taken. Using an item on the fermenter goes through the same `AddItem`.
- On a ready fermenter, `Interact` sends `RPC_Tap`; the owner (not necessarily the tapping player) spawns
  `m_producedItems` of `m_to` at `m_outputPoint` after `m_tapDelay` (1.5 s) and empties the ZDO.
- `Skill.Raise` grants at most one level per call and resets the accumulator, so leftover XP is dropped.
- XP per level is fixed for every skill: `0.5 * (level + 1)^1.5 + 0.5`.

## Design

- **Curve:** level N → N+1 takes `6 * (1 + 0.06 * N)` potions' worth: level 1 is one batch, level 2 about 1.07
  batches, level 10 about 1.6, level 50 about 4, level 100 about 7. Totals: about 13 batches for level 10, 56 for 30,
  124 for 50, 397 for 100. Each potion's worth raises `vanillaRequirement(N) / potionsNeeded(N)`, recomputed per
  potion so a level-up mid-grant uses the new level and little XP is lost. The world's skill gain rate still applies.
- **Filling** (Postfix on `AddItem`, true result, local player) gives half the batch's worth; **tapping** gives the
  other half. A batch always totals the curve; the first batch is 0.5 XP at filling and 0.5 XP at tapping, level 1.
  Filling can't be farmed: mead base never comes back out.
- **Tap detection:** a Prefix on `Interact` (not hold, local player, status Ready, `PrivateArea.CheckAccess` without
  flashing) remembers the batch; the Postfix acts when `Interact` returned true. Each batch is identified by its ZDO
  and start time and rewards only once, so spamming the key before the owner's reset arrives does nothing.
- **Bonus:** chance `MaxBonusChance * skillFactor` (default 0.3 at level 100), rolled at the tap with the level before
  this tap's XP. A bonus adds a third of the batch, rounded up (6 → 8). After `m_tapDelay` the tapping client spawns
  them at `m_outputPoint`, next to the owner's normal output, and shows `+N` like vanilla's bonus text.
- **Icon:** the fermenter piece icon (`fermenter`), null until ZNetScene exists.
- **Config:** `[Brewing] Enabled` (true), `MaxBonusChance` (0.3, range 0 to 1).
- **Off:** both patches return early.

## Non-goals

- Faster fermenting: the timer lives in the owner's ZDO, not client-side.
- Cauldron mead bases: vanilla Cooking already covers them.
- A friend filling and you tapping each get their own half; nothing is shared.
