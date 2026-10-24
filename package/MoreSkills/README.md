# More Skills

New skills that you level by playing, shown in the skills screen next to the vanilla ones.

Client-side only: it works on any server, and other players do not need it.

## Skills

### Hauling

Pulling a loaded cart gets easier the more you do it. The cargo in a cart you pull weighs less on you as Hauling
levels, up to half its weight at level 100 (configurable with `MaxCargoReduction`). At level 0 carts behave exactly
like vanilla, and the empty cart's own weight never changes.

You level Hauling while you pull a moving cart: the heavier the load, the faster you level, and an empty cart gives
nothing. The lighter feel only applies while you are the one pulling; a parked cart keeps its full weight.

## Configuration

`BepInEx/config/dev.pgarcia.valheim.moreskills.cfg` has one section per skill. Turning a skill off hides it from
the skills screen and stops its levelling and its effect. Your level is kept and comes back when you turn it on again.

Removing the mod drops these skills from your character the next time it loads. The character itself is unaffected.
