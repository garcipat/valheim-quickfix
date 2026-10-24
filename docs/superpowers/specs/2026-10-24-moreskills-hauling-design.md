# MoreSkills: Hauling — design

Date: 2026-10-24
Status: approved

## Problem

A loaded cart becomes nearly unusable, worst of all solo. `Vagon.UpdateMass` (owner only, every 5 s) sets the cart's
mass to `m_baseMass + inventoryWeight * m_itemWeightMassFactor` (20 + weight × 1 on the live prefab). The player is
connected to the cart by a spring joint and moves at normal speed; the cart's mass drags them back, and uphill, gravity
on that mass beats their pull. Stamina plays no part.

A flat weight reduction was rejected as too cheaty. Instead, pulling strength becomes something the player earns: a
new **Hauling** skill.

## Scope

- A new mod, **MoreSkills**, that adds new skills to the game. It is new functionality, not a fix, so it does not go
  into QuickFix.
- It ships with one skill, Hauling. The plumbing is reusable, but no other skill is designed yet (YAGNI).

## Mod layout

Follows the existing mods:

- `src/MoreSkills/MoreSkills.csproj`, `Plugin.cs` (config binding, Harmony bootstrap).
- `src/MoreSkills/Core/`: shared skill plumbing (registration, load, name, icon, disabled-skill handling, console
  command). It is not called `Skills/` because a `Skills` namespace would shadow the game's `Skills` class.
- `src/MoreSkills/Patches/HaulingPatch.cs`: the Hauling levelling and effect.
- `package/MoreSkills/`: `manifest.json` and `thunderstore.toml` (version `0.0.0`), `icon.png`, player-facing
  `README.md`, `CHANGELOG.md` with an `## [Unreleased]` entry.
- Release workflow: add `MoreSkills` to the options list and `MoreSkills-v*` to the tag patterns.
- `docs/MoreSkills.md`: developer notes (how the plumbing works, how to add a skill).

Client-side only, no dependencies beyond BepInEx/HarmonyX/Unity/game assemblies, Prefix/Postfix only.

## Skill plumbing

Vanilla only knows the skills in the `Skills.SkillType` enum. For each custom skill, the plumbing does this:

1. **Identity.** The skill gets a fixed `SkillType` value from a stable hash of its identifier with the sign bit
   cleared, so it never changes between versions. It must be positive: the localizer ends the `$skill_` word at a
   minus sign, which showed the name as `[skill_]-309775219` in testing.
2. **Definition.** A `Skills.SkillDef` (type, icon, description, `m_increseStep`) is added to the player's
   `Skills.m_skills` list when the player is set up, unless it is already there.
3. **Load.** `Skills.Load` drops any type for which `IsSkillValid` (`Enum.IsDefined`) is false. A Postfix on
   `Skills.IsSkillValid` returns true for our types. `Skills.Save` already writes every entry in `m_skillData`, so it
   needs no change.
4. **Name.** The skills dialog and the skill-up message use `"$skill_" + type.ToString().ToLower()`, which for our
   value is the number. The plumbing adds that localization key, plus the description, in English for every language.
5. **Icon.** Each skill supplies a sprite at runtime. Hauling reuses the game's own Cart piece icon
   (`ZNetScene.GetPrefab("Cart")` → `Piece.m_icon`), so it matches the vanilla look and no asset ships.
6. **Console.** `raiseskill` (`Skills.CheatRaiseSkill`) only searches the `SkillType` enum. A Prefix handles our
   skill names (`raiseskill hauling 10`) and lets vanilla handle everything else.

Behaves like vanilla with no extra work: the death penalty (`LowerAllSkills`), the skills dialog row and level bar, and
the skill-up message. The world modifier that resets all skills on death (`Skills.Clear`) also resets ours, even while
it is turned off.

## Turning a skill off

Each skill has a toggle in its own config section (`Hauling` for now). When it is off:

- **Hidden:** a Postfix on `Skills.GetSkillList` filters the skill out, so it does not show in the skills dialog.
- **Frozen:** it does not level, and the death penalty does not lower it. A Prefix/Postfix pair on
  `Skills.LowerAllSkills` saves and restores its level and accumulator.
- **No effect:** the effect patches return early.
- **Kept:** the level stays in `m_skillData` and in the save. Turning the skill back on restores it unchanged.

The level is lost only if the mod is removed: vanilla then drops the unknown type at the next load. The character file
stays valid.

## Hauling

### Levelling

- While the local player is attached to a cart (`Vagon.IsAttached(player)`), the cart's movement counts only while
  the player gives movement input, so hitching, standing still or being dragged downhill earns nothing. Once per
  second, if the counted movement averaged at least 1 m/s, the player gains skill via
  `Player.RaiseSkill(type, meters * 0.03 * clamp01(cargoWeight / 300))`.
- Gain follows distance hauled, not time attached (time-based gain gave two levels for a 3 m pull in testing). A load
  of 300 or more gains fastest, lighter loads slower, an empty cart nothing. At a full load and a cart's usual speed
  that is about 0.1 per second, a tenth of vanilla Swim and Run (Swim's pace, about 16 s per level around level 10,
  felt far too fast): level 10 after ~13 minutes of pulling, 50 after ~10 hours, 100 after ~56 hours. The constants live in `HaulingPatch.cs`. No config for the rate.

### Effect

- Cargo mass is scaled by `1 - MaxCargoReduction * skillFactor`, where `skillFactor = level / 100` (the game's
  `GetSkillFactor`). The cart's `m_baseMass` is never scaled.
- `MaxCargoReduction` is configurable, default 0.5 (50% lighter cargo at level 100), range 0 to 0.9.
- It applies only while the **local player** is attached to that cart. A parked cart, or one pulled by someone else,
  keeps its vanilla mass.
- Hook: a Postfix on `Vagon.UpdateMass` re-applies the mass with the reduced cargo part (through the game's own
  `SetMass` split across bodies). The game's formula, timing and physics keep running.
- Immediate refresh: Postfixes on `Vagon.AttachTo` and `Vagon.Detach` call `UpdateMass`, so the mass changes at
  hitch/unhitch instead of up to 5 s later.

### Multiplayer

Only the cart's owner (the player pulling it) simulates its physics and sets its mass, and skills are stored in each
player's own character file. Nothing is synced, and remote players see vanilla behaviour from their side.

## Config summary

| Section   | Key                 | Type  | Default | Notes                                   |
|-----------|---------------------|-------|---------|-----------------------------------------|
| `Hauling` | `Enabled`           | bool  | true    | Off: hidden, frozen, no effect          |
| `Hauling` | `MaxCargoReduction` | float | 0.5     | 0–0.9, cargo weight reduction at lvl 100 |

## Manual verification (in-game)

1. A fresh character has Hauling at 0 in the skills dialog, with its name and icon. A full cart of stone up a fixed
   slope behaves like vanilla.
2. Pulling a loaded cart raises the skill and shows the skill-up message. Pulling an empty cart does not.
3. After `raiseskill hauling 50` and then 100, the same cart up the same slope is clearly easier, and at 100 the cargo
   weight is halved (log the computed mass).
4. Unhitching restores the vanilla mass immediately, and hitching applies the reduction immediately.
5. The level survives a logout/login.
6. With `Enabled = false`: the skill is hidden, does not level, survives a death unchanged, and the cart has vanilla
   mass. Turning it back on shows the old level.
7. Death with the skill enabled lowers Hauling like the other skills.

## Out of scope

- Other skills (Building, Sailing, ...): later, each with its own design.
- Translations beyond English.
- Run-skill effects on pulling.
- Megingjord making carts easier to pull: planned separately as a QuickFix toggle, not part of MoreSkills. If both are
  on, they must stack predictably (both scale only the cargo part of the mass).
