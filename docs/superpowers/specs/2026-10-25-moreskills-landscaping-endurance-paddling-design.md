# MoreSkills: Landscaping, Endurance, Paddling — design

Date: 2026-10-25
Status: approved in chat

## Scope

Three new skills on the existing MoreSkills plumbing (`Core/`), each in its own `Patches/<Skill>Patch.cs` with its
own config section and `Enabled` toggle, following "Adding a skill" in `docs/MoreSkills.md`.

Dropped after checking vanilla and Thunderstore:

- **Carpentry (hammer):** vanilla Crafting already levels from building and repairing, and lowers hammer stamina and
  durability use by up to 50%.
- **Cooking speed:** vanilla Cooking already shortens cauldron crafting and gives a bonus portion at cooking stations.
  A faster grill is not client-side: the cook timer lives in the station's ZDO and its owner (not necessarily
  the local player) advances it.
- **Carry weight:** too strong. PackHorse and CarryWeightSkill already do it.

## Plumbing change

`CustomSkill` gets an optional `increaseStep` (default 1, which keeps Hauling unchanged), and
`SkillRegistrationPatch` uses it for `SkillDef.m_increseStep`. Landscaping needs it because vanilla calls
`RaiseSkill(skill)` with factor 1 for every placement.

## Landscaping (hoe)

- **Mechanism:** vanilla build tools level the skill named on their piece table (`PieceTable.m_skill`). Placing a
  piece raises it, `GetBuildStamina` lowers the tool's stamina by `50% * skillFactor`, and removing pieces adds
  remove debt. Hoe terrain edits go through the same placement path.
- **Hook:** Postfixes on `ObjectDB.Awake` and `ObjectDB.CopyOtherDB` look up the `Hoe` item and set its
  `m_shared.m_buildPieces.m_skill` to Landscaping, but only if it is `None`. Otherwise they log a warning and leave
  it alone, so an existing vanilla or mod skill is never taken over. `SettingChanged` on the toggle sets it back to
  `None` when the toggle is off (only if we were the ones who set it) and to Landscaping when it is on again.
- **Effect:** stamina only, up to 50% less at level 100, through the game's own rule. Durability stays vanilla
  (`m_placementDurabilitySkill` is not touched).
- **Levelling:** 0.25 per hoe use (`increaseStep`), the same step the hammer earns for Crafting.
- **Icon:** the hoe's item icon.
- **Config:** `[Landscaping] Enabled` (true).

## Endurance (overweight)

- **Mechanism:** `Player.UpdateStats(float dt)` runs for the local player only. While over the weight limit and
  moving (`m_moveDir.magnitude > 0.1`), it calls `UseStamina(m_encumberedStaminaDrain * dt)` (10/s) and blocks
  regen.
- **Effect:** a Prefix scales the instance's `m_encumberedStaminaDrain` by
  `1 - MaxDrainReduction * skillFactor`, and a Postfix restores the original. Only for `Player.m_localPlayer`. The
  rest stays vanilla: no regen, no running, no dodging.
- **Levelling:** in the same Postfix, while `IsEncumbered()` and moving (the same condition the drain uses), count
  time; once per second `RaiseSkill(Endurance, 0.2)`, twice Hauling's pace (1 per second, Run's pace, was far too fast in
  testing). The constant lives in the patch file.
- **Icon:** Megingjord (`BeltStrength`) item icon.
- **Config:** `[Endurance] Enabled` (true), `MaxDrainReduction` (0.5, range 0 to 0.9).

## Paddling (ship in Slow or Back)

- **Mechanism:** `Ship.CustomFixedUpdate` returns early on non-owners. On the owner, in `Speed.Slow` and
  `Speed.Back`, it pushes with `m_backwardForce` (50). Half and Full sail don't use that force, so scaling it
  only affects paddling.
- **Effect:** a Prefix scales the instance's `m_backwardForce` by `1 + MaxPaddleBonus * skillFactor`, and a Postfix
  restores it. It applies only when the local player is the helmsman (`m_shipControlls.GetUser() == GetPlayerID()`)
  and the ship's owner (`m_nview.IsOwner()`).
- **Levelling:** in the Postfix, while the local player is the helmsman and the speed (private `m_speed`) is Slow or
  Back, add up how far the ship moves (position delta; steps over 2 m are ignored as teleports). Once per second, if
  that averaged at least 0.5 m/s, `RaiseSkill(Paddling, meters * GainPerMeter)` with `GainPerMeter` = 0.05
  (about 0.1–0.15 per second at paddling speed). The constants live in the patch file. Levelling does not need
  ownership, because skills are local.
- **Icon:** the Karve piece icon.
- **Config:** `[Paddling] Enabled` (true), `MaxPaddleBonus` (0.4, range 0 to 1): 40% more paddle force at
  level 100.
- **Known limit:** with other players aboard, a passenger can own the ship. Then the helmsman paddles at vanilla
  speed. Remote players are never affected.

## Docs

`docs/MoreSkills.md` (one section per skill and the Paddling ownership limit), `package/MoreSkills/README.md`
(player-facing), and one `CHANGELOG.md` line per skill under `## [Unreleased]`.

## Manual verification (in-game)

1. All three skills show in the skills dialog with their names and icons at 0.
2. Landscaping: using the hoe raises it, and the hoe's stamina cost at `raiseskill landscaping 100` is half that at 0.
   The log shows no "hoe already has a skill" warning. Hammer and cultivator are unchanged.
3. Endurance: walking overweight raises it, and standing still does not. At level 100 the overweight drain is half
   that at 0 (time to empty the stamina bar).
4. Paddling: paddling forward or back raises it, and sailing does not. At level 100 paddling is clearly faster (time
   a fixed distance), and half or full sail is unchanged.
5. Each toggle off: the skill is hidden, doesn't level, and has vanilla behaviour (the hoe goes back to vanilla
   stamina without a restart). Turning it on again restores the level.
6. Levels survive logout/login and drop with the death penalty.
