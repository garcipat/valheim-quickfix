using System;
using System.Reflection;
using HarmonyLib;
using UnityEngine;

namespace ValheimQuickFix.Patches
{
    /// <summary>
    /// The player's arrow on the minimap and the large map is small and has no edge, so it gets lost on bright terrain
    /// and among pins. After <c>Minimap.UpdatePlayerMarker</c> places both arrows, this draws them larger with a dark
    /// outline. The large map's arrow also gets a thin ring as a child that keeps expanding and fading; the minimap's
    /// arrow always sits in its centre, so it gets none. The ring has no outline: Unity draws an outline as four offset
    /// copies, which read as several rings. It is timed on real time, so it keeps pulsing while
    /// <see cref="PauseOnMapPatch"/> holds the game. Turning the fix off restores the arrows.
    /// UnityEngine.UI is not available as a package (see docs/Architecture.md), so Image and Outline are reached by
    /// name.
    /// </summary>
    [HarmonyPatch(typeof(Minimap), "UpdatePlayerMarker")]
    internal static class VisiblePlayerMarkerPatch
    {
        private const float MarkerScale = 1.3f;
        // One ring grows for PulseSeconds, then nothing shows for RestSeconds before the next one.
        private const float PulseSeconds = 2.2f;
        private const float RestSeconds = 1.3f;
        // Relative to the arrow, which is already scaled by MarkerScale.
        private const float RingStartScale = 1.5f;
        private const float RingEndScale = 3.9f;
        private const float RingStartAlpha = 0.9f;
        private const string RingName = "QuickFixPlayerRing";

        private static readonly Color RingColor = new Color(1f, 0.85f, 0.3f);
        private static readonly Color OutlineColor = new Color(0f, 0f, 0f, 0.85f);

        private static readonly Type ImageType = AccessTools.TypeByName("UnityEngine.UI.Image");
        private static readonly Type GraphicType = AccessTools.TypeByName("UnityEngine.UI.Graphic");
        private static readonly Type OutlineType = AccessTools.TypeByName("UnityEngine.UI.Outline");
        private static readonly PropertyInfo SpriteProperty = AccessTools.Property(ImageType, "sprite");
        private static readonly PropertyInfo ColorProperty = AccessTools.Property(GraphicType, "color");
        private static readonly PropertyInfo RaycastTargetProperty = AccessTools.Property(GraphicType, "raycastTarget");
        private static readonly PropertyInfo EffectColorProperty = AccessTools.Property(OutlineType, "effectColor");
        private static readonly PropertyInfo EffectDistanceProperty = AccessTools.Property(OutlineType, "effectDistance");

        private static readonly bool Available = ImageType != null && OutlineType != null && SpriteProperty != null
            && ColorProperty != null && RaycastTargetProperty != null && EffectColorProperty != null
            && EffectDistanceProperty != null;

        private static Sprite _ringSprite;
        private static bool _warned;

        private static void Postfix(Minimap __instance)
        {
            try
            {
                if (!Available)
                {
                    if (!_warned)
                    {
                        _warned = true;
                        Plugin.Log.LogWarning("VisiblePlayerMarker: UnityEngine.UI types not found in this game version; the map marker is unchanged");
                    }

                    return;
                }

                var on = Plugin.VisiblePlayerMarkerEnabled.Value;
                Apply(__instance.m_smallMarker, on, false);
                Apply(__instance.m_largeMarker, on, true);
            }
            catch (Exception e)
            {
                Plugin.Log.LogError($"VisiblePlayerMarker patch failed: {e}");
            }
        }

        // The ring child is created on both arrows and also marks an arrow this fix has changed.
        private static void Apply(RectTransform marker, bool on, bool showRing)
        {
            if (marker == null)
            {
                return;
            }

            var ring = marker.Find(RingName);
            if (!on)
            {
                if (ring != null)
                {
                    ring.gameObject.SetActive(false);
                    marker.localScale = Vector3.one;
                    if (marker.GetComponent(OutlineType) is Behaviour outline)
                    {
                        outline.enabled = false;
                    }
                }

                return;
            }

            if (ring == null)
            {
                ring = CreateRing(marker);
            }

            ring.gameObject.SetActive(showRing);
            marker.localScale = new Vector3(MarkerScale, MarkerScale, 1f);
            if (marker.GetComponent(OutlineType) is Behaviour markerOutline)
            {
                markerOutline.enabled = true;
            }

            if (!showRing)
            {
                return;
            }

            var t = Mathf.Clamp01(Time.unscaledTime % (PulseSeconds + RestSeconds) / PulseSeconds);
            // Ease out, so the ring starts fast and settles instead of growing at a constant speed.
            var grow = 1f - (1f - t) * (1f - t);
            var scale = Mathf.Lerp(RingStartScale, RingEndScale, grow);
            ring.localScale = new Vector3(scale, scale, 1f);
            var color = RingColor;
            color.a = RingStartAlpha * (1f - t) * (1f - t);
            ColorProperty.SetValue(ring.GetComponent(ImageType), color, null);
        }

        private static Transform CreateRing(RectTransform marker)
        {
            if (marker.GetComponent(GraphicType) != null && marker.GetComponent(OutlineType) == null)
            {
                AddOutline(marker.gameObject, OutlineColor, new Vector2(1.5f, -1.5f));
            }

            var go = new GameObject(RingName, typeof(RectTransform), ImageType);
            var rect = (RectTransform)go.transform;
            rect.SetParent(marker, false);
            rect.anchorMin = rect.anchorMax = new Vector2(0.5f, 0.5f);
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.anchoredPosition = Vector2.zero;
            var size = Mathf.Max(marker.rect.width, marker.rect.height);
            rect.sizeDelta = new Vector2(size, size);

            var image = go.GetComponent(ImageType);
            SpriteProperty.SetValue(image, RingSprite(), null);
            RaycastTargetProperty.SetValue(image, false, null);
            return rect;
        }

        private static void AddOutline(GameObject go, Color color, Vector2 distance)
        {
            var outline = go.AddComponent(OutlineType);
            EffectColorProperty.SetValue(outline, color, null);
            EffectDistanceProperty.SetValue(outline, distance, null);
        }

        private static Sprite RingSprite()
        {
            if (_ringSprite != null)
            {
                return _ringSprite;
            }

            const int size = 64;
            const float thickness = 2.5f;
            var tex = new Texture2D(size, size, TextureFormat.RGBA32, false) { filterMode = FilterMode.Bilinear };
            var center = (size - 1) / 2f;
            var outer = size / 2f - 1f;
            var inner = outer - thickness;
            for (var y = 0; y < size; y++)
            {
                for (var x = 0; x < size; x++)
                {
                    var d = Mathf.Sqrt((x - center) * (x - center) + (y - center) * (y - center));
                    var alpha = Mathf.Clamp01(outer - d + 0.5f) * Mathf.Clamp01(d - inner + 0.5f);
                    tex.SetPixel(x, y, new Color(1f, 1f, 1f, alpha));
                }
            }

            tex.Apply();
            _ringSprite = Sprite.Create(tex, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f));
            return _ringSprite;
        }
    }
}
