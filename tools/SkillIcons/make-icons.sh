#!/usr/bin/env bash
# Bakes the skill icons in svg/ into the 128 px PNGs MoreSkills embeds (src/MoreSkills/Assets/<name>.png), with a
# soft dark shadow like the game's own skill icons. Only skills without a fitting game sprite have one here.
# Needs ImageMagick (convert). Run it after changing an icon and commit the PNGs.
set -euo pipefail

here="$(cd "$(dirname "$0")" && pwd)"
out="$here/../../src/MoreSkills/Assets"

mkdir -p "$out"
work="$(mktemp -d)"
trap 'rm -r -- "$work"' EXIT

for svg in "$here"/svg/*.svg; do
    name="$(basename "$svg" .svg)"
    convert -background none -density 600 "$svg" -resize 112x112 -gravity center -extent 128x128 "$work/$name-icon.png"
    convert "$work/$name-icon.png" -alpha extract -morphology Dilate Disk:3 -blur 0x3 \
        -background black -alpha shape -channel A -evaluate multiply 0.6 +channel "$work/$name-shadow.png"
    convert "$work/$name-shadow.png" "$work/$name-icon.png" -composite "PNG32:$out/$name.png"
done
echo "Wrote $out/"
