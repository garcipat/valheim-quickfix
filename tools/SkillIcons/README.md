# Skill icons

SVG sources for MoreSkills skill icons that the game has no fitting sprite for. Most skills reuse a game icon (see each
skill's `Create` in `src/MoreSkills/Patches/`); the ones here are drawn instead.

- `svg/Exploring.svg` — compass rose with a red north, for Exploring.

`make-icons.sh` bakes each SVG into `src/MoreSkills/Assets/<name>.png` (128 px, soft shadow), which the build embeds.
Needs ImageMagick. Run it after changing an icon and commit the PNGs. Not packaged.
