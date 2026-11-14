# Campfire companion

Behavior and implementation notes for the CampfireCompanion mod. Player-facing docs:
[package/CampfireCompanion/README.md](../package/CampfireCompanion/README.md).

**Config:** `General.Companion` (default `None`; `Random` picks one per menu load) and `General.Stars` (0 to 2).
Changing either while the menu is open swaps the companion right away.
**Patches:** `FejdStartup.Start` (Postfix, in `src/CampfireCompanion/Patches/MenuStartPatch.cs`). The menu scene is
loaded fresh at startup and after every logout, so `Start` runs each time.

**Behavior:**

1. The chosen creature stands left of the campfire as the menu camera sees it, looking at the fire, and plays its idle
   animation. The character is on the other side of the fire.
2. Each creature has its own setup in `Companion.cs`, tuned by eye in the menu: a scale relative to its world size
   (the biggest ones are shrunk to fit the shot), the gap between the fire and its nearest side as the renderer
   bounds measure it (wide models such as the Fallen Valkyrie's wings get a small or negative gap), and a turn for
   models that don't face their own forward (Haldor sits facing back).
3. The character selection camera moves 2 m back, so the companion stays in that shot. Only while a companion is
   shown.
4. With stars, the creature gets what its `LevelEffects` gives it in the world: the size step, the recoloured main
   material and the swapped parts.
5. Scenery only: no sound, AI, collision or network object. It lives in the menu scene and goes away with it.

**Where the creature comes from:** the live game ships only the startup scene as a plain scene; everything else,
the world scene included, is in asset bundles under `valheim_Data/StreamingAssets/SoftRef/Bundles`. Nearly every
creature prefab is in one shared bundle that the menu scene's bundle depends on, so it is loaded in the menu and the
prefabs are in memory: `Resources.FindObjectsOfTypeAll<GameObject>()` finds them by name. If one is not, the loaded
bundles are searched for `assets/characters/**/<name>.prefab`. `SoftRef/manifest_extended` in the game folder lists
every asset with its bundle, for checking where a prefab lives.

**How it is built:** only the prefab's `Visual` child is copied, under an inactive holder, so `Awake` of the game
scripts on it never runs. Stars are applied from the copy's `LevelEffects`, then every `MonoBehaviour`, collider and
rigidbody is destroyed before the holder is activated. The animator stays, with root motion off, and plays the
controller's default state. Without the creature's scripts some controllers drift into another state or end on a clip
that doesn't loop (the drake froze), so `KeepIdle` restarts the first state whenever that happens. The gap is measured from the creature's renderer bounds, which are only known once the
holder is active.

**Reference point:** everything is measured from the character's spot, `m_characterPreviewPoint`, which is fixed in the
menu scene. The campfire burns 2.12 m to its left and 0.15 m toward the camera; the creature stands 0.91 m further
from the camera than the character and looks at that point. Left and away are the menu camera marker's
(`m_cameraMarkerMain`) right and forward, flattened. The nearest point light is not used to find the fire: glowing
gear on the character carries one, so the companion moved with what the character wore.

**Known limits:** weapons and armour that a creature gets at runtime (from `VisEquipment`) are not shown, since the
scripts are stripped. Creatures whose default animator state is not a standing idle may look odd; those are left off
the list.
