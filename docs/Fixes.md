# Fixes

Behavior of each fix. Implementation lives in `src/ValheimQuickFix/Patches/`.

## Restore shield

**Config:** `Weapon switching` → `RestoreShield` (default `true`)
**Patch:** `Humanoid.EquipItem` (Prefix + Postfix) — `Patches/RestoreShieldPatch.cs`

**Problem:** with a shield and one-handed weapon equipped, equipping a bow (or any two-handed
weapon) unequips the shield. Switching back to the one-handed weapon leaves the shield in the
inventory and you must re-equip it by hand.

**Behavior:**

1. Equipping a bow / two-handed weapon while a shield is in the left hand remembers that shield.
2. Equipping a one-handed weapon afterwards re-equips the remembered shield, if the left hand is
   empty and the shield is still in the inventory.
3. Manually equipping any shield clears the remembered one.

**Known limits:** the remembered shield is held in memory only and is forgotten on game restart.

## Push boat

**Config:** `Boats` → `PushBoat` (default `true`), `PushStrength` (default `3`, range 0.5–20, m/s²)
**Patch:** `Ship.Awake` (Postfix) adds a `BoatPush` component — `Patches/PushBoatPatch.cs`

**Problem:** a boat run aground can't be moved by the player standing next to it.

**Behavior:**

1. Looking at a boat from outside it shows `[E] Push boat`.
2. Pressing E pushes the boat away from the side you stand on, for a short burst. Holding E keeps pushing.
3. No prompt and no push while you are standing on the boat.
4. The boat's owner simulates its physics, so pressing E claims ownership of the boat first.

**Known limits:** no check that the boat is actually grounded; you can push a floating boat too.
Strength is a tuning value, expressed as acceleration so it feels the same for every boat.
**Unverified in-game:** that the hull colliders deliver the hover ray to the component, and that
holding E keeps the push going. See the PR notes.
