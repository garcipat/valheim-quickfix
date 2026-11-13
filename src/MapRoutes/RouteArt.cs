using System;
using System.IO;
using UnityEngine;

namespace ValheimMapRoutes
{
    /// <summary>
    /// Sprites and colors. The quill and its cursor are PNGs embedded in the DLL (drawn by
    /// tools/MapRoutesArt/make_art.py, the same feather as the mod icon); the dot color is the red of the cross the
    /// game draws over crossed-out pins.
    /// </summary>
    internal static class RouteArt
    {
        private const int SampleSize = 32;
        // Where make_art.py puts the nib in quill-cursor.png, counted from the top-left.
        private static readonly Vector2 CursorHotspot = new Vector2(3f, 28f);

        // Used when the cross over checked pins can't be read.
        private static readonly Color FallbackRed = new Color(0.8f, 0.15f, 0.1f, 1f);

        private static Sprite _dot;
        private static Sprite _quill;
        private static bool _quillTried;
        private static Texture2D _cursor;
        private static bool _cursorTried;
        private static bool _cursorShown;
        private static Color? _red;

        internal static Sprite Dot
        {
            get
            {
                if (_dot == null)
                {
                    _dot = MakeDot();
                }
                return _dot;
            }
        }

        internal static Sprite Quill()
        {
            if (!_quillTried)
            {
                _quillTried = true;
                var tex = LoadPng("quill.png");
                if (tex != null)
                {
                    _quill = Sprite.Create(tex, new Rect(0, 0, tex.width, tex.height), new Vector2(0.5f, 0.5f));
                }
            }
            return _quill;
        }

        internal static Color Red(Minimap minimap)
        {
            if (_red == null)
            {
                _red = Brighten(ReadCrossColor(minimap) ?? FallbackRed);
                Plugin.Log.LogInfo($"Route color {_red.Value}");
            }
            return _red.Value;
        }

        internal static void ShowQuillCursor(bool show)
        {
            if (show == _cursorShown)
            {
                return;
            }
            if (show && !_cursorTried)
            {
                _cursorTried = true;
                _cursor = LoadPng("quill-cursor.png");
            }
            if (show && _cursor == null)
            {
                return;
            }
            _cursorShown = show;
            Cursor.SetCursor(show ? _cursor : null, show ? CursorHotspot : Vector2.zero, CursorMode.Auto);
        }

        internal static void ResetCursor()
        {
            if (_cursorShown)
            {
                _cursorShown = false;
                Cursor.SetCursor(null, Vector2.zero, CursorMode.Auto);
            }
        }

        private static Sprite MakeDot()
        {
            const int size = 32;
            var tex = new Texture2D(size, size, TextureFormat.RGBA32, false) { filterMode = FilterMode.Bilinear };
            var center = (size - 1) / 2f;
            var radius = size / 2f - 1f;
            for (var y = 0; y < size; y++)
            {
                for (var x = 0; x < size; x++)
                {
                    var d = Mathf.Sqrt((x - center) * (x - center) + (y - center) * (y - center));
                    tex.SetPixel(x, y, new Color(1f, 1f, 1f, Mathf.Clamp01(radius - d + 0.5f)));
                }
            }
            tex.Apply();
            return Sprite.Create(tex, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f));
        }

        // Dots are small, so a dark red reads as black. Keep the hue, but at least this bright.
        private static Color Brighten(Color color)
        {
            Color.RGBToHSV(color, out var h, out var s, out var v);
            return Color.HSVToRGB(h, s, Mathf.Max(v, 0.8f));
        }

        // The cross is a child "Checked" of the pin prefab. If it is tinted, that tint is the color; if the red is in
        // the sprite itself, take the average of its colorful pixels (its dark outline and shading left out).
        private static Color? ReadCrossColor(Minimap minimap)
        {
            try
            {
                var cross = minimap.m_pinPrefab == null ? null : minimap.m_pinPrefab.transform.Find("Checked");
                var image = cross == null ? null : Ui.GetImage(cross);
                if (image == null)
                {
                    return null;
                }
                var tint = Ui.GetColor(image);
                if (tint.r < 0.95f || tint.g < 0.95f || tint.b < 0.95f)
                {
                    Plugin.Log.LogInfo($"Route color from the cross tint {tint}");
                    return new Color(tint.r, tint.g, tint.b, 1f);
                }
                var sprite = Ui.GetSprite(image);
                if (sprite == null)
                {
                    return null;
                }
                var pixels = Rasterize(sprite, SampleSize).GetPixels();
                float r = 0f, g = 0f, b = 0f, weight = 0f;
                foreach (var p in pixels)
                {
                    Color.RGBToHSV(p, out _, out var saturation, out var value);
                    if (p.a > 0.5f && saturation > 0.4f && value > 0.25f)
                    {
                        r += p.r * p.a;
                        g += p.g * p.a;
                        b += p.b * p.a;
                        weight += p.a;
                    }
                }
                Plugin.Log.LogInfo($"Route color from the cross sprite, {weight:0} weight of colorful pixels");
                return weight > 0f ? new Color(r / weight, g / weight, b / weight, 1f) : (Color?)null;
            }
            catch (Exception e)
            {
                Plugin.Log.LogError($"Reading the crossed-out pin color failed: {e}");
                return null;
            }
        }

        // UI sprites live in packed, unreadable atlases, so copy the sprite's part of it through a render texture. The
        // game renders in linear space; an sRGB render texture converts back, so the pixels read as the sprite's.
        private static Texture2D Rasterize(Sprite sprite, int size)
        {
            var source = sprite.texture;
            var rect = sprite.textureRect;
            var rt = RenderTexture.GetTemporary(size, size, 0, RenderTextureFormat.ARGB32, RenderTextureReadWrite.sRGB);
            var previous = RenderTexture.active;
            try
            {
                Graphics.Blit(source, rt,
                    new Vector2(rect.width / source.width, rect.height / source.height),
                    new Vector2(rect.x / source.width, rect.y / source.height));
                RenderTexture.active = rt;
                var copy = new Texture2D(size, size, TextureFormat.RGBA32, false);
                copy.ReadPixels(new Rect(0, 0, size, size), 0, 0);
                copy.Apply();
                return copy;
            }
            finally
            {
                RenderTexture.active = previous;
                RenderTexture.ReleaseTemporary(rt);
            }
        }

        private static Texture2D LoadPng(string name)
        {
            try
            {
                using (var stream = typeof(RouteArt).Assembly.GetManifestResourceStream("ValheimMapRoutes." + name))
                {
                    if (stream == null)
                    {
                        Plugin.Log.LogError($"Embedded {name} not found");
                        return null;
                    }
                    var bytes = new byte[stream.Length];
                    var read = 0;
                    while (read < bytes.Length)
                    {
                        var n = stream.Read(bytes, read, bytes.Length - read);
                        if (n <= 0)
                        {
                            throw new EndOfStreamException();
                        }
                        read += n;
                    }
                    var tex = new Texture2D(2, 2, TextureFormat.RGBA32, false) { filterMode = FilterMode.Bilinear };
                    return tex.LoadImage(bytes) ? tex : null;
                }
            }
            catch (Exception e)
            {
                Plugin.Log.LogError($"Loading {name} failed: {e}");
                return null;
            }
        }
    }
}
