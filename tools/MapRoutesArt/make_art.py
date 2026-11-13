"""Draws the MapRoutes art from one feather: the package icon, the quill button sprite and the quill cursor.

Usage (from the repo root): python3 tools/MapRoutesArt/make_art.py
Needs Pillow. Writes package/MapRoutes/icon.png and src/MapRoutes/Assets/quill*.png.
"""
import math
from pathlib import Path

from PIL import Image, ImageDraw, ImageFilter

ROOT = Path(__file__).resolve().parents[2]
SS = 4  # supersampling
BG = (31, 35, 43)  # the other mods' icon background
RED = (232, 78, 64)
CLEAR = (0, 0, 0, 0)


def feather(draw, nib, angle, length, vane, shaft, cut):
    """A feather from its nib (x, y) along angle (degrees, y down), length to the vane tip; all in output pixels."""
    ux, uy = math.cos(math.radians(angle)), math.sin(math.radians(angle))
    nx, ny = -uy, ux
    k = length / 160  # the shape was designed at length 160

    def at(t, w):
        return ((nib[0] + (ux * t + nx * w) * k) * SS, (nib[1] + (uy * t + ny * w) * k) * SS)

    outline = []
    for i in range(41):
        s = i / 40
        outline.append(at(28 + 132 * s, 30 * math.sin(math.pi * min(1, s * 1.05)) ** 0.8))
    for i in range(40, -1, -1):
        s = i / 40
        outline.append(at(28 + 132 * s, -23 * math.sin(math.pi * min(1, s * 1.05)) ** 0.8))
    draw.polygon(outline, fill=vane)
    for t, side in [(70, 1), (100, -1), (118, 1)]:
        draw.polygon([at(t, 0), at(t + 16, 30 * side), at(t + 4, 30 * side)], fill=cut)
    draw.line([at(0, 0), at(170, 0)], fill=shaft, width=max(1, round(5 * k * SS)))
    draw.polygon([at(0, 0), at(30, 7), at(30, -7)], fill=shaft)


def icon():
    size = 256
    im = Image.new("RGB", (size * SS, size * SS), BG)
    d = ImageDraw.Draw(im)
    p = [(46, 206), (30, 110), (150, 210), (136, 124)]

    def bez(t):
        u = 1 - t
        return tuple(u**3 * a + 3 * u * u * t * b + 3 * u * t * t * c + t**3 * e for a, b, c, e in zip(*p))

    points = [bez(i / 2000) for i in range(2001)]
    dots, run, last = [points[0]], 0, points[0]
    for q in points[1:]:
        run += math.dist(q, last)
        last = q
        if run >= 17:
            dots.append(q)
            run = 0
    for x, y in dots:
        if math.dist((x, y), p[0]) > 24 and math.dist((x, y), p[3]) > 6:
            d.ellipse([(x - 6.5) * SS, (y - 6.5) * SS, (x + 6.5) * SS, (y + 6.5) * SS], fill=RED)
    x, y = p[0]
    d.ellipse([(x - 15) * SS, (y - 15) * SS, (x + 15) * SS, (y + 15) * SS], outline=RED, width=7 * SS)
    feather(d, p[3], -50, 160, (232, 232, 228), (150, 150, 150), BG)
    im.resize((size, size), Image.LANCZOS).save(ROOT / "package/MapRoutes/icon.png")


def quill(size, pad):
    """Light quill like the game's map icons, with their soft shadow below it. The nib is at the bottom-left,
    (pad, size - 1 - pad), the vane runs up to the top-right corner."""
    im = Image.new("RGBA", (size * SS, size * SS), CLEAR)
    d = ImageDraw.Draw(im)
    span = (size - 1 - 2 * pad) * math.sqrt(2)
    feather(d, (pad, size - 1 - pad), -45, span / 170 * 160, (232, 232, 232, 255), (170, 170, 170, 255), CLEAR)
    im = im.resize((size, size), Image.LANCZOS)
    # The game's icons cast a soft dark shadow a little below them; it also keeps the cursor visible over snow.
    offset = max(1, round(size * 0.025))
    shadow_alpha = im.getchannel("A").filter(ImageFilter.GaussianBlur(max(0.8, size * 0.025)))
    shadow_alpha = shadow_alpha.point(lambda a: round(a * 0.8))
    shadow = Image.new("RGBA", im.size, (0, 0, 0, 255))
    shadow.putalpha(shadow_alpha)
    back = Image.new("RGBA", im.size, CLEAR)
    back.paste(shadow, (0, offset))
    return Image.alpha_composite(back, im)


if __name__ == "__main__":
    icon()
    assets = ROOT / "src/MapRoutes/Assets"
    assets.mkdir(exist_ok=True)
    # The margin matches the padding around the game's pin icons, so the quill looks as big as they do.
    quill(128, 27).save(assets / "quill.png")
    # The cursor hotspot in RouteArt (3, 28) is this nib position, counted from the top-left.
    quill(32, 3).save(assets / "quill-cursor.png")
