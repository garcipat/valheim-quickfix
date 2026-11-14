#!/usr/bin/env bash
# Bakes the pin icons in svg/ into the sprite sheet MapRoutes embeds: white icons with a soft dark backdrop, one
# 128 px cell each, 7 per row, in the order below. The order must match PinIcons.Icons in src/MapRoutes/PinIcons.cs.
# Needs ImageMagick (convert, montage). Run it after changing an icon and commit the PNG.
set -euo pipefail

here="$(cd "$(dirname "$0")" && pwd)"
out="$here/../../src/MapRoutes/Assets/pin-icons.png"

icons=(Cave Crypt Dungeon Village Portal Ship Ore
       Tar Berries Mushroom Egg Nest Surtling Leviathan
       Forest Swamp Mountain Plains Mistlands Ashlands DeepNorth
       Lox Mine)

work="$(mktemp -d)"
trap 'rm -r -- "$work"' EXIT

cells=()
for name in "${icons[@]}"; do
    convert -background none -density 1200 "$here/svg/$name.svg" -resize 512x512 "$work/$name-big.png"
    if [ "$name" = Nest ]; then
        # evil-tree is a tree cut out of a white block; clear the block from both top corners so the tree stands free.
        convert "$work/$name-big.png" -alpha set -fuzz 75% -fill none \
            -draw 'color 60,60 floodfill' -draw 'color 450,60 floodfill' "$work/$name-big.png"
    fi
    convert "$work/$name-big.png" -resize 104x104 -gravity center -background none -extent 128x128 "$work/$name-white.png"
    convert "$work/$name-white.png" -alpha extract -morphology Dilate Disk:6 -blur 0x3 \
        -background black -alpha shape -channel A -evaluate multiply 0.75 +channel "$work/$name-shadow.png"
    convert "$work/$name-shadow.png" "$work/$name-white.png" -composite "$work/$name.png"
    cells+=("$work/$name.png")
done

montage "${cells[@]}" -tile 7x -geometry 128x128+0+0 -background none "PNG32:$out"
echo "Wrote $out"
