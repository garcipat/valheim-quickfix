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
  averaged at least 1 m/s, `RaiseSkill(Hauling, meters * 0.075)` if the cart holds any item, half that
  if it is empty. Gain follows distance, not time, and not load: scaling by cargo weight (full gain at 300, none
  empty) made most hauling earn almost nothing. At cart speed that is about 0.25 per second, a quarter of vanilla Swim and Run
  (their pace felt far too fast for hauling in testing, and a tenth of it barely levelled, since carts are pulled
  rarely): level 10 after ~5 minutes of pulling, 50 after ~4 hours, 100 after ~22 hours. Tune with `GainPerMeter`.
- **Identity:** the skill number is the identifier's stable hash with the sign bit cleared. A negative number breaks
  the name, because the localizer ends the `$skill_` word at the minus sign.
- **Multiplayer:** only the cart's owner (the puller) simulates and sets its mass, and skills live in each player's
  character file, so remote players are unaffected.

## Landscaping

- **Effect and levelling come from the game.** Vanilla build tools level the skill on their piece table
  (`PieceTable.m_skill`): every placement raises it (with remove debt, like the hammer), and `GetBuildStamina` lowers
  the tool's stamina by `50% * skillFactor`. Hoe terrain edits go through the same placement path. Postfixes on
  `ObjectDB.Awake` and `CopyOtherDB` set the hoe's table to Landscaping, but only if it has no skill (otherwise they
  log a warning and leave it alone). The toggle's `SettingChanged` puts it back to `None`, and only if we set it.
  Durability (`m_placementDurabilitySkill`) is not touched.
- **Rate:** vanilla raises a piece table skill with factor 1, so the skill definition's step (`increaseStep` on
  `CustomSkill`) is 1, the same a pickaxe hit gives Pickaxes. The hammer's 0.25 for Crafting barely levelled, since the
  hoe is used far less. Level 10 after about 75 uses, 50 after about 3,600.
- `m_skill` is not in the compile-time libs, so it is accessed by name.

## Endurance

- **Effect:** `Player.UpdateStats(float)` (local player only) drains `m_encumberedStaminaDrain` (10/s) while you move
  over the weight limit, and blocks regen. A Prefix scales that field by `1 - MaxDrainReduction * skillFactor` and
  a Finalizer restores it, so the rest of the method stays vanilla.
- **Levelling:** the Postfix gives `RaiseSkill(Endurance, 0.35)` per second while encumbered, not attached, giving
  movement input and actually moving (at least 0.5 m/s, so being stuck at zero stamina earns nothing). That is a bit
  above Hauling's pace, since overweight walking comes in short stretches: level 10 after about 4 minutes of it, 50
  after about 3 hours. 0.2 per second barely levelled and 0.5 felt too fast; 1 per second (Run's pace) reached level 10 in under 2 minutes, far too fast in testing. Tune with
  `GainPerInterval`.

## Paddling

- **Effect:** `Ship.CustomFixedUpdate` simulates on the ship's owner and pushes with `m_backwardForce` (50) in
  `Speed.Slow` and `Speed.Back` only. While the local player steers (`ShipControlls.GetUser()`, called by name since the
  compile-time libs have an older signature) and owns the ship, a Prefix scales that force by
  `1 + MaxPaddleBonus * skillFactor` and a Finalizer restores it.
- **Levelling:** the Postfix counts how far the ship moves while the local player steers in Slow or Back (steps over
  2 m are ignored). Once per second, if that averaged at least 0.5 m/s, `RaiseSkill(Paddling, meters * 0.125)`.
  That is about 0.25 to 0.4 per second (0.05 per meter barely levelled). Tune with `GainPerMeter`.
- **Multiplayer:** the owner simulates the ship, and that can be a passenger rather than the helmsman. Then the
  helmsman paddles at vanilla speed. Remote players are never affected.

## Exploring

- **Effect:** `Minimap.UpdateExplore` calls the private `Explore(Vector3 p, float radius)` every 2 s with
  `m_exploreRadius` (100 m) around the local player, and nothing else calls that overload. A Prefix scales the `radius`
  argument by `1 + MaxRadiusBonus * skillFactor`; no field changes.
- **Levelling:** while that call runs, a Postfix on `Explore(int, int)` counts pixels it returns true for (not yet in
  the player's own explored layer). The Postfix on `Explore(Vector3, float)` turns them into meters of new ground,
  `pixels * m_pixelSize² / (2 * radius)`, and raises `meters * 0.02`. Dividing by the circle's width keeps a wider
  circle from speeding up its own levelling. About 0.14 per second running through fresh land. Tune with
  `GainPerMeter`.
- **Not counted:** the first call after a gap of more than 5 s (load, respawn) and a jump of more than 60 m between
  calls (portal). Loading the map and `exploremap` reveal through the pixel overload directly, outside the counted call.
- **Shared maps:** cartography table data goes into a separate layer (`m_exploredOthers`), so walking through an area
  someone shared still counts, and sharing gives nothing to anyone.

## Brewing

- **Curve:** brewing is rare, so the skill sets its own pace in potions. Level N to N+1 takes `6 * (1 + 0.06 * N)`
  potions: level 1 is one batch, level 10 about 1.6 batches, level 50 about 4, level 100 about 7 (in total about 13
  batches for level 10, 124 for 50, 397 for 100). `Brewing.Grant` raises one potion at a time by the game's XP for the
  current level (`0.5 * (level + 1)^1.5 + 0.5`) divided by that count, so a batch never gives more than one level
  (`Skill.Raise` grants one level per call and drops the rest). Tune with `PotionsPerLevel` and `CurveGrowth`.
- **Filling:** Postfix on the private `Fermenter.AddItem`, which runs on the filling player and returns true only after
  taking the mead base from their inventory. Gives half a batch's worth.
- **Tapping:** a Prefix on `Fermenter.Interact` remembers the batch if the local player taps a ready fermenter they
  may use (`GetStatus`, called by name; `PrivateArea.CheckAccess` without flashing). When `Interact` returned true,
  the Postfix gives the other half and rolls `MaxBonusChance * skillFactor` with the level before this tap. A bonus is
  a third of the batch, rounded up (6 → 8), spawned by this client at `m_outputPoint` after `m_tapDelay`, next to the
  owner's normal output, with vanilla's `+N` bonus text. A batch (ZDO and start time) pays out once.
- **Compile-time libs:** `GetContent`, `GetItemConversion`, `ZDOVars`, `ItemDrop.OnCreateNew` and
  `DamageText.TextType.Bonus` differ from or are missing in the libs, so the patch reads the ZDO keys by their names
  and calls the rest by name.
- **Vanilla overlap:** none. Mead bases are a cauldron craft, which vanilla Cooking's crafting bonus already covers.

## Adding a skill

1. Add a static class in `Patches/<Skill>Patch.cs` with an `Identifier`, a `Type` and a `Create(enabled)` like
   `Hauling`. Pass an `increaseStep` if the game raises the skill with factor 1 (as for a piece table skill).
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
- Paddling only speeds up the ship when the helmsman also owns it (see Paddling).
