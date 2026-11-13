# Map routes

Behavior and implementation notes for the MapRoutes mod. Player-facing docs: [package/MapRoutes/README.md](../package/MapRoutes/README.md).

**Config:** none. Disabling the mod is the off switch; the saved routes stay in the character file.
**Patches** (all in `src/MapRoutes/Patches/MinimapPatches.cs`): `Minimap.Update` (Postfix: button, cursor, stroke,
right-drag pan, dots on the large and the corner map), `Minimap.SetMapMode` (Postfix: leaving the large map ends draw mode), `Minimap.SelectIcon`
(Postfix: picking a pin type ends draw mode), `Minimap.OnMapLeftDown` and `Minimap.OnMapLeftUp` (Prefix, skipped
only in draw mode: start and end a stroke), `Minimap.RemovePinUnderPointer` (Prefix, skipped when a route was
removed or, in draw mode, when the right press was a pan).

**Behavior:**

1. A quill button sits one step before the first pin-type button on the large map (above it, in the game's column).
   Clicking it turns draw mode on; clicking it again, picking a pin type or leaving the large map turns it off. In
   draw mode the quill button shows the selected frame and the vanilla frames are hidden; turning it off calls
   `SelectIcon` with the still-selected pin type, which puts its frame back.
2. In draw mode the hardware cursor is the quill, and drawing is freehand:
   - a left press on the map starts a stroke at the pointer; while the button is held, the pointer is added each
     time it moved 4 UI units; the release ends the stroke. Every stroke is its own route. If the release is missed
     (button let go over another window), the button state ends the stroke on the next frame;
   - a finished stroke is thinned with Ramer-Douglas-Peucker at 1.5 UI units (at the zoom it ends at), so a long
     route stores tens of points, not thousands;
   - the game's left press would start dragging the map and its release would cross out a pin or open the pin name
     input, so neither runs;
   - a right-drag moves the map instead, by shifting `m_mapOffset` against the pointer, as the game's left-drag does.
     The game fires right-click on release even after a drag, so a right press that moved more than 6 screen pixels
     removes nothing.
3. Right-click removes the route closest to the pointer within 12 UI units, in any mode. If a pin is under the
   pointer (`GetClosestPinToCursor`), the game removes the pin instead and the route stays.
4. A stroke with fewer than two points is dropped. Every finished stroke and removal is saved straight away.
5. Dots are drawn 9 UI units apart and 4.5 across up to zoom 0.05 (the map image's `uvRect.width`, the share of the
   map's width on screen). Further out both shrink by `(0.05 / zoom)^0.4`, down to 0.45 of that when fully zoomed out
   (zoom 1), so a zoomed-out map shows fine dots instead of big ones. The corner map draws the
   same routes, scaled by `m_pinSizeSmall / m_pinSizeLarge` like its pins; it has no input, so drawing and removing
   happen on the large map only. They sit at whole
   multiples of the spacing along the whole route, so the spacing runs on across a stroke's short segments and dots
   don't crawl while panning. Their color is the red of the cross over checked pins (rule in "How it works").

**Storage:** `Player.m_customData["dev.pgarcia.maproutes.Routes.<world UID>"]`, one route per line, points as
`x,z` separated by `;`, one decimal. It is saved with the character, like the QuickFix ForgetDeletedPins list. The
store reloads when the local player or world changes.

**How it works:**

- *Geometry.* The dots are `Image`s under a child of `m_mapImageLarge`, stretched over it, anchored at its
  bottom-left. That is the same local space `UpdatePins` places pins in (`WorldToMapPoint` + `MapPointToLocalGuiPos`,
  both private), so the mod repeats that math with the public `m_textureSize`, `m_pixelSize` and the image's
  `uvRect`. Being under the map image, the dots draw below the pin root. Each segment is clipped to the map rect
  (Liang-Barsky) before dots are placed, and at most 6000 dots are drawn, so zooming in never walks thousands of dots
  off screen. Dot objects are pooled and reused each frame.
- *Button.* `m_selectedIcon0.transform.parent` is the first pin-type button: its own `Image` is the icon and the
  selected frame is its child (`Minimap.ToggleIconFilter` relies on the same layout). The five buttons hang from the
  top of `IconPanel`, 64 apart; its gray background is a stretched child `Bkg`. The death and boss toggles are in
  their own box, `IconPanel2`, just above it; ping is in `IconPingPanel` at the top right. The quill is a clone of
  button 0, placed one step (button 0 to button 1) before it. To make room, `IconPanel` grows by that step on that
  side, with its point-anchored children put back where they were, so `Bkg` stretches behind the quill and the pin
  buttons don't move. Sibling panels with a background that the grown panel would overlap (`IconPanel2`) move the
  same step out, so the gap between the boxes stays. None of this runs when the parent has a layout group. The
  position of every map button, and of the panels around them with their background images, is logged once
  (`Map button ...`, `Map panel ...`), so a misplaced quill or background can be fixed from a log. Every `UnityEvent`
  field on the clone is replaced with a fresh one, which clears the inspector-wired `OnPressedIcon0`; `UIGamePad`
  components are removed so the gamepad key for icon 0 doesn't also press the quill; a `UITooltip`, if any, reads
  "Draw route". The quill isn't in `m_selectedIcons`, so gamepad cycling through pin types skips it.
- *Art* (`RouteArt`). The quill button sprite (`Assets/quill.png`, 128 px) and the cursor (`Assets/quill-cursor.png`,
  32 px) are embedded in the DLL. Both are light grey with a soft dark shadow below, like the game's map icons, and
  drawn by `tools/MapRoutesArt/make_art.py` from the same feather as the package icon, which it also writes; rerun
  it after changing the feather. The cursor's nib is at (3, 28) from the top-left, which is the hotspot.
- *Dot color.* The red is the tint of the pin prefab's `Checked` child; if that is white, the red is in the sprite,
  which sits in an unreadable atlas. It is blitted into a 32×32 render texture and the average of its colorful
  pixels (saturation > 0.4, value > 0.25) is used, which leaves out the cross's dark outline. The render texture is
  sRGB: the game renders in linear space, and a linear one read back the pixels much too dark. A fixed red is the
  last resort, and any result is raised to at least 0.8 brightness, keeping its hue, since small dark dots read as
  black. The color and where it came from are logged once (`Route color ...`).
- *UI types.* `UnityEngine.UI` is not available as a package (see [Architecture.md](Architecture.md)), so `Image`,
  `RawImage`, `Button` and `LayoutGroup` are reached by name in `src/MapRoutes/Ui.cs`. Minimap fields typed as UI
  components are read through `FieldInfo`. If the types are missing, the mod logs one error and does nothing.
- *Pointer.* `ZInput.pointerPosition` (newer than the compile-time libs) is read by reflection, falling back to
  `Input.mousePosition`.

**Known limits:** drawing and removing on the large map only. Right-drag pans only in draw mode; outside it the game's
left-drag does. Mouse only; there's no gamepad way to pick the quill.
Routes are not shared through cartography tables. The quill button's position assumes the five pin buttons are in a
row; a UI change in the game could put it somewhere odd, but the button still works.
