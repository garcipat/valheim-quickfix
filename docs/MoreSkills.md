# More Skills

Adds new skills to the game without Jotunn. Player-facing description: [package/MoreSkills/README.md](../package/MoreSkills/README.md).

## How a custom skill gets into the game

Vanilla only knows the skills in `Skills.SkillType`. `Core/` adds the rest:

- **Identity:** `CustomSkill` turns an identifier string into a `SkillType` value with `GetStableHashCode` (the
  scheme Jotunn uses). That number is what the character file stores, so the identifier must never change.
- **Definition:** a Postfix on `Skills.Awake` adds a `SkillDef` to `m_skills`. Awake runs before the character is
  loaded.
- **Load:** `Skills.Load` drops types for which the private `IsSkillValid` (`Enum.IsDefined`) is false. A Postfix
  accepts registered types. `Save` already writes every entry in `m_skillData`.
- **Name:** the dialog and skill-up message localize `$skill_<number>`. `SkillWords` adds the English name after
  every `Localization.SetLanguage`, through the private `Localization.AddWord` (called by name; the compile-time
  libs are publicized, so calling it directly builds but fails in the game).
- **Icon:** each skill supplies a sprite at runtime (Hauling reuses the Cart piece icon). It is filled in at `Awake`,
  or later in `GetSkillList` if the game had not loaded it yet.
- **Turned off:** `GetSkillList` leaves the skill out (hidden), `LowerAllSkills` restores its level afterwards
  (frozen), and the skill's own patches return early (no levelling, no effect). The level stays in the save.
- **Console:** a Prefix on `CheatRaiseSkill` handles `raiseskill <name> <value>` for custom skills.

## Hauling

- **Effect:** Postfix on the private `Vagon.UpdateMass` (every 5 s, owner only). While the local player is attached,
  the mass is set again to `m_baseMass + cargo * (1 - MaxCargoReduction * skillFactor)` through the private
  `SetMass`. Postfixes on `AttachTo` and `Detach` run `UpdateMass` immediately.
- **Levelling:** Postfix on `Vagon.Update`. While the local player is attached, the cart's movement is counted only
  in frames where the player gives movement input (`GetMoveDir`), so hitching, standing still or being dragged
  downhill earns nothing; a jump of more than 2 m in one frame (teleport) is ignored. Once per second, if that
  averaged at least 1 m/s, `RaiseSkill(Hauling, meters * 0.03 * clamp01(cargo / 300))`. Gain follows
  distance, not time. At a full load and cart speed that is about 0.1 per second, a tenth of vanilla Swim and Run
  (their pace felt far too fast for hauling in testing): level 10 after ~13 minutes of pulling, 50 after ~10 hours,
  100 after ~56 hours. Tune with `GainPerMeter`.
- **Identity:** the skill number is the identifier's stable hash with the sign bit cleared. A negative number breaks
  the name, because the localizer ends the `$skill_` word at the minus sign.
- **Multiplayer:** only the cart's owner (the puller) simulates and sets its mass, and skills live in each player's
  character file, so remote players are unaffected.

## Adding a skill

1. Add a static class in `Patches/<Skill>Patch.cs` with an `Identifier`, a `Type` and a `Create(enabled)` like
   `Hauling`.
2. In `Plugin.Awake`, bind a section named after the skill with an `Enabled` toggle, then
   `SkillRegistry.Add(<Skill>.Create(enabled))`.
3. Level it with `player.RaiseSkill(<Skill>.Type, factor)` and read it with `GetSkillFactor`, both behind the
   toggle.
4. Document it here and in the package README, and add a changelog line.

## Known limits

- Names and descriptions are English in every language.
- Removing the mod drops the skills from the character at its next load (vanilla discards unknown types).
- The world modifier that resets skills on death (`Skills.Clear`) also resets disabled custom skills.
- With the skill-cap world setting, enabled custom skills count toward the total like vanilla ones.
