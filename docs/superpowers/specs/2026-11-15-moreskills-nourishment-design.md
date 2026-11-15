# MoreSkills: Nourishment — design

Date: 2026-11-15
Status: approved in chat

## Intent

You get more out of your food the more you eat: food stays effective for longer before it wears off. Duration stays
vanilla, so you don't save food; the drop-off at the end is what changes.

## Approaches

- **Food lasts longer** (up to 20% more duration): quietly saves food over a playthrough.
- **Higher food stats:** a direct combat buff, too strong.
- **Gentler drop-off** (picked): thematically "your body gets more out of a meal", and it never goes above a food's
  full value.

## Mechanism (vanilla)

`Player.UpdateFood` ticks each food once per second (times the world's food rate) and sets its health, stamina and
eitr to `full * clamp01(time / burnTime)^0.3`, then calls the private `GetTotalFoodValue` and sets the max bars. Only
`UpdateFood` calls `GetTotalFoodValue`. `EatFood` resets a food to full and runs `UpdateFood(0, force)`. A food can be
eaten again once its time is below half.

## Design

- **Effect:** a Prefix on `GetTotalFoodValue` (local player) rewrites each food's values with the exponent
  `0.3 * (1 - MaxFalloffReduction * skillFactor)` before vanilla adds them up, so the bars are set once with the
  final values (patching after `SetMaxHealth` would clamp current health first). With the default 0.5 the exponent
  is 0.15 at level 100: with 10% of the time left a food gives 71% instead of 50%, on average 87% instead of 77% over
  its duration. Duration, re-eating and the full value are unchanged.
- **Levelling:** a Postfix on `EatFood` (true, local player) grants the food's duration in minutes. Level N to N+1
  takes `90 * (1 + 0.06 * N)` food-minutes: level 1 is one meal of three 30-minute foods, then the same shape as
  Brewing (about 13 meals for level 10, 124 for 50, 397 for 100). Re-eating is gated by vanilla's half-time rule, so it
  can't be farmed.
- **Shared curve:** Brewing's per-unit grant moves to `Core/SkillCurve` and both skills use it.
- **Eitr:** newer than the compile-time libs; its fields are accessed by name.
- **Icon:** cooked meat's item icon (`CookedMeat`), null until ObjectDB has it.
- **Config:** `[Nourishment] Enabled` (true), `MaxFalloffReduction` (0.5, range 0 to 0.9).
- **Off:** both patches return early; vanilla curve, no levelling.

## Non-goals

- Longer food duration, higher food values, more food regen.
