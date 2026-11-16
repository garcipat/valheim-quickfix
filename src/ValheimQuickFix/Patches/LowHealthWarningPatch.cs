using System;
using System.Reflection;
using HarmonyLib;
using UnityEngine;

namespace ValheimQuickFix.Patches
{
    /// <summary>
    /// Nothing on screen tells you how close to death you are except the health bar. Below
    /// <see cref="Threshold"/> of max health, a thin red border pulses slowly around the screen.
    /// It looks the same at any health below that.
    /// </summary>
    /// <remarks>
    /// The border starts as a copy of the game's hit flash (<c>Hud.m_damageScreen</c>), which gives it a full-screen
    /// place under the HUD's canvas, so it hides with the HUD and the hit flash keeps drawing on top of it. The game's
    /// image reddens most of the screen, so the copy gets its own drawn image instead: a thin red band that fades in
    /// from the screen's edges and leaves the middle clear.
    /// After <c>Hud.Update</c> its alpha moves smoothly toward a target, so it fades in and out instead of popping.
    /// UnityEngine.UI is not available as a package (see docs/Architecture.md), so Image is reached by name.
    /// </remarks>
    [HarmonyPatch(typeof(Hud), "Update")]
    internal static class LowHealthWarningPatch
    {
        private const float Threshold = 0.1f;
        // The border's alpha; the pulse swings this much above and below it.
        private const float Alpha = 0.5f;
        private const float PulseAmplitude = 0.1f;
        // Pulses per second.
        private const float Rate = 0.6f;
        // How fast the alpha follows its target, per second.
        private const float FadeSpeed = 2f;
        // How far the red band reaches in from each edge, as a share of the screen's width or height.
        private const float Band = 0.12f;
        private const int TextureSize = 256;
        private static readonly Color BandColor = new Color(0.7f, 0.03f, 0.03f);
        private const string OverlayName = "QuickFixLowHealth";

        private static readonly FieldInfo DamageScreen = AccessTools.Field(typeof(Hud), "m_damageScreen");
        private static readonly Type GraphicType = AccessTools.TypeByName("UnityEngine.UI.Graphic");
        private static readonly PropertyInfo ColorProperty = AccessTools.Property(GraphicType, "color");
        private static readonly Type ImageType = AccessTools.TypeByName("UnityEngine.UI.Image");
        private static readonly PropertyInfo SpriteProperty = AccessTools.Property(ImageType, "sprite");
        private static readonly PropertyInfo ImageTypeProperty = AccessTools.Property(ImageType, "type");

        private static Sprite _sprite;

        private static Component _overlay;
        private static float _alpha;
        private static bool _warned;

        private static void Postfix(Hud __instance)
        {
            try
            {
                if (DamageScreen == null || ColorProperty == null || SpriteProperty == null)
                {
                    if (!_warned)
                    {
                        _warned = true;
                        Plugin.Log.LogWarning("LowHealthWarning: the hit flash was not found in this game version; no border is shown");
                    }

                    return;
                }

                var target = Plugin.LowHealthWarningEnabled.Value ? TargetAlpha() : 0f;
                _alpha = Mathf.MoveTowards(_alpha, target, Time.deltaTime * FadeSpeed);

                if (_overlay == null)
                {
                    if (_alpha <= 0f)
                    {
                        return;
                    }

                    _overlay = CreateOverlay(__instance);
                    if (_overlay == null)
                    {
                        return;
                    }
                }

                var color = (Color)ColorProperty.GetValue(_overlay, null);
                color.a = _alpha;
                ColorProperty.SetValue(_overlay, color, null);
                _overlay.gameObject.SetActive(_alpha > 0f);
            }
            catch (Exception e)
            {
                Plugin.Log.LogError($"LowHealthWarning patch failed: {e}");
            }
        }

        private static float TargetAlpha()
        {
            var player = Player.m_localPlayer;
            if (player == null || player.IsDead())
            {
                return 0f;
            }

            var health = player.GetHealthPercentage();
            if (health >= Threshold)
            {
                return 0f;
            }

            var pulse = Mathf.Sin(Time.time * Rate * 2f * Mathf.PI);
            return Mathf.Clamp01(Alpha + pulse * PulseAmplitude);
        }

        private static Component CreateOverlay(Hud hud)
        {
            var source = DamageScreen.GetValue(hud) as Component;
            if (source == null)
            {
                return null;
            }

            var copy = UnityEngine.Object.Instantiate(source.gameObject, source.transform.parent, false);
            copy.name = OverlayName;
            // Just below the hit flash, so a hit still flashes over the border.
            copy.transform.SetSiblingIndex(source.transform.GetSiblingIndex());

            // Fill the screen exactly, whatever size the hit flash has.
            var rect = (RectTransform)copy.transform;
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;
            rect.localScale = Vector3.one;

            var image = copy.GetComponent(ImageType);
            if (image == null)
            {
                return null;
            }

            SpriteProperty.SetValue(image, BandSprite(), null);
            if (ImageTypeProperty != null)
            {
                // Simple: stretch the sprite over the whole rect.
                ImageTypeProperty.SetValue(image, Enum.ToObject(ImageTypeProperty.PropertyType, 0), null);
            }

            ColorProperty.SetValue(image, new Color(1f, 1f, 1f, 0f), null);
            return image;
        }

        // Red, transparent in the middle and growing opaque over the last Band of each edge. Stretched over the screen,
        // so the band is the same share of the width at the sides as of the height at the top and bottom.
        private static Sprite BandSprite()
        {
            if (_sprite != null)
            {
                return _sprite;
            }

            var texture = new Texture2D(TextureSize, TextureSize, TextureFormat.RGBA32, false)
            {
                wrapMode = TextureWrapMode.Clamp,
                filterMode = FilterMode.Bilinear,
            };
            var pixels = new Color[TextureSize * TextureSize];
            for (var y = 0; y < TextureSize; y++)
            {
                for (var x = 0; x < TextureSize; x++)
                {
                    var u = (x + 0.5f) / TextureSize;
                    var v = (y + 0.5f) / TextureSize;
                    var edge = Mathf.Min(Mathf.Min(u, 1f - u), Mathf.Min(v, 1f - v));
                    var t = Mathf.Clamp01(1f - edge / Band);
                    var color = BandColor;
                    color.a = t * t;
                    pixels[y * TextureSize + x] = color;
                }
            }

            texture.SetPixels(pixels);
            texture.Apply();
            _sprite = Sprite.Create(texture, new Rect(0, 0, TextureSize, TextureSize), new Vector2(0.5f, 0.5f));
            return _sprite;
        }
    }
}
