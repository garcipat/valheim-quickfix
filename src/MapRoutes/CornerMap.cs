using System;
using System.Collections.Generic;
using System.Reflection;
using System.Text;
using HarmonyLib;
using UnityEngine;

namespace ValheimMapRoutes
{
    /// <summary>
    /// Makes the small map in the corner round and/or bigger. The round shape is a UI mask around the map image, so
    /// the routes drawn under it are cut to the circle too; the square frame behind it is faded out and a thin rim is
    /// drawn instead. The size scales the whole corner map while the corner of the screen it sits in stays put.
    /// </summary>
    internal static class CornerMap
    {
        private static readonly FieldInfo SmallMapField = AccessTools.Field(typeof(Minimap), "m_mapImageSmall");
        private static readonly Type MaskType = AccessTools.TypeByName("UnityEngine.UI.Mask");
        private static readonly PropertyInfo ShowMaskGraphicProperty = AccessTools.Property(MaskType, "showMaskGraphic");
        private static readonly Type GraphicType = AccessTools.TypeByName("UnityEngine.UI.Graphic");
        private static readonly PropertyInfo MaterialProperty = AccessTools.Property(GraphicType, "material");
        private static readonly PropertyInfo MaterialForRenderingProperty = AccessTools.Property(GraphicType, "materialForRendering");

        // The rim's color: the dark of the game's own map frame, a little see-through.
        private static readonly Color RimColor = new Color(0.12f, 0.1f, 0.08f, 0.85f);
        // Rim thickness as a share of the radius.
        private const float RimWidth = 0.025f;
        // The mask's own settings on the copied material, kept when the game's values are copied over.
        private static readonly string[] StencilProperties =
            { "_Stencil", "_StencilComp", "_StencilOp", "_StencilReadMask", "_StencilWriteMask", "_ColorMask" };

        private static Minimap _owner;
        private static RectTransform _image;
        private static RectTransform _wrapper;
        private static Behaviour _mask;
        private static Behaviour _maskImage;
        private static GameObject _rim;
        private static readonly List<KeyValuePair<Behaviour, Color>> FadedFrames = new List<KeyValuePair<Behaviour, Color>>();

        private static RectTransform _scaled;
        private static Vector3 _originalScale;
        private static Vector3 _originalPosition;
        private static float _appliedScale = 1f;
        // Things in the corner map that keep their size when it grows, with their own scale.
        private static readonly Dictionary<Transform, Vector3> Unscaled = new Dictionary<Transform, Vector3>();
        // Newer than the compile-time libs.
        private static readonly FieldInfo PinNameRootField = AccessTools.Field(typeof(Minimap), "m_pinNameRootSmall");
        private static readonly FieldInfo SmallMaterialField = AccessTools.Field(typeof(Minimap), "m_mapSmallShader");
        private static Vector2Int _appliedScreen;
        private static bool _appliedRound;
        private static bool _loggedLayout;
        private static bool _broken;

        // The biome name sits in a corner of the square; on the round map it moves to the top middle.
        private static readonly FieldInfo BiomeNameField = AccessTools.Field(typeof(Minimap), "m_biomeNameSmall");
        private static RectTransform _biomeName;
        private static Vector2 _biomePivot;
        private static Vector3 _biomePosition;
        private static object _biomeAlignment;
        private static Component _biomeFontText;
        private static float _biomeFontSize;

        private static Sprite _circle;
        private static Sprite _ring;
        private static float _ringScale;
        private static Behaviour _rimImage;

        internal static bool Round { get; private set; }

        // VikingMap: a painted shield with crossed axe and club behind the round map (viking-shield.png). The map
        // covers the shield up to just inside the rim's rivets: its radius is 290 of the image's 1024 pixels.
        private const float ShieldSize = 1024f / (2f * 290f);

        // The status effect icons sit left of the corner map; they move further left when the shield or a bigger map
        // would cover them.
        private static RectTransform _statusRoot;
        private static Vector3 _statusPosition;
        private static GameObject _shield;
        private static Sprite _shieldSprite;
        private static bool _shieldFailed;

        /// <summary>How much bigger the corner map is drawn; it shows that much more of the map.</summary>
        internal static float Scale => _scaled != null ? _appliedScale : 1f;

        internal static void Update(Minimap minimap)
        {
            if (_broken || minimap.m_mode != Minimap.MapMode.Small)
            {
                return;
            }
            if (_owner != minimap || _image == null)
            {
                // A new map instance (another session); the old objects went with the old one.
                Reset();
                _owner = minimap;
                _image = (SmallMapField?.GetValue(minimap) as Component)?.transform as RectTransform;
                if (_image == null)
                {
                    _broken = true;
                    Plugin.Log.LogError("Round/bigger corner map disabled: the corner map image was not found");
                    return;
                }
            }
            var round = Plugin.VikingMap.Value;
            if (round != _appliedRound)
            {
                SetRound(minimap, round);
            }
            if (Round)
            {
                SyncMaskedMaterial();
                PlaceBiomeName(minimap);
            }
            UpdateShield();
            var scale = Mathf.Clamp(Plugin.MinimapScale.Value, 1f, 1.5f);
            var screen = new Vector2Int(Screen.width, Screen.height);
            if (!Mathf.Approximately(scale, _appliedScale) || (_scaled != null && screen != _appliedScreen))
            {
                SetScale(minimap, scale);
                if (_rimImage != null)
                {
                    Ui.SetSprite(_rimImage, Ring());
                }
            }
            KeepSizes(minimap);
            MakeRoomForStatusEffects();
        }

        /// <summary>
        /// The mask draws the map with a copy of its material. The game sets the map's position, zoom and fog on the
        /// original every frame, so those are copied over, keeping the mask's own stencil settings.
        /// </summary>
        private static void SyncMaskedMaterial()
        {
            var graphic = GraphicType == null ? null : _image.GetComponent(GraphicType);
            if (graphic == null || MaterialForRenderingProperty == null)
            {
                return;
            }
            var source = MaterialProperty?.GetValue(graphic, null) as Material;
            var rendered = MaterialForRenderingProperty.GetValue(graphic, null) as Material;
            if (source == null || rendered == null || source == rendered)
            {
                return;
            }
            var kept = new float[StencilProperties.Length];
            for (var i = 0; i < kept.Length; i++)
            {
                kept[i] = rendered.HasProperty(StencilProperties[i]) ? rendered.GetFloat(StencilProperties[i]) : 0f;
            }
            rendered.CopyPropertiesFromMaterial(source);
            for (var i = 0; i < kept.Length; i++)
            {
                if (rendered.HasProperty(StencilProperties[i]))
                {
                    rendered.SetFloat(StencilProperties[i], kept[i]);
                }
            }
        }

        private static void PlaceBiomeName(Minimap minimap)
        {
            if (_biomeName == null)
            {
                var text = BiomeNameField?.GetValue(minimap) as Component;
                if (text == null)
                {
                    return;
                }
                _biomeName = (RectTransform)text.transform;
                _biomePivot = _biomeName.pivot;
                _biomePosition = _biomeName.localPosition;
                var alignment = AccessTools.Property(text.GetType(), "alignment");
                if (alignment != null)
                {
                    _biomeAlignment = alignment.GetValue(text, null);
                    alignment.SetValue(text, Enum.Parse(alignment.PropertyType, "Top"), null);
                }
            }
            // Just inside the rim, where the circle is still wide enough for the name.
            var map = WorldRect(_wrapper);
            _biomeName.pivot = new Vector2(0.5f, 1f);
            _biomeName.position = new Vector3(map.center.x, map.yMax - map.height * 0.06f, _biomeName.position.z);
        }

        private static void RestoreBiomeName()
        {
            if (_biomeName == null)
            {
                return;
            }
            _biomeName.pivot = _biomePivot;
            _biomeName.localPosition = _biomePosition;
            var text = _owner == null ? null : BiomeNameField?.GetValue(_owner) as Component;
            var alignment = text == null ? null : AccessTools.Property(text.GetType(), "alignment");
            if (alignment != null && _biomeAlignment != null)
            {
                alignment.SetValue(text, _biomeAlignment, null);
            }
            _biomeName = null;
        }

        /// <summary>A pin outside the circle is hidden, as the game hides pins outside the square.</summary>
        internal static bool InsideCircle(Minimap minimap, Vector3 world, Component map)
        {
            if (!Round || map == null || _image == null || map.transform != _image)
            {
                return true;
            }
            var uv = Ui.UvRect(map);
            var half = (int)minimap.m_textureSize / 2;
            var mx = (world.x / minimap.m_pixelSize + half) / minimap.m_textureSize;
            var my = (world.z / minimap.m_pixelSize + half) / minimap.m_textureSize;
            var dx = (mx - uv.center.x) / (uv.width * 0.5f);
            var dy = (my - uv.center.y) / (uv.height * 0.5f);
            return dx * dx + dy * dy <= 1f;
        }

        private static void UpdateShield()
        {
            var show = Round && _wrapper != null;
            if (!show)
            {
                if (_shield != null)
                {
                    _shield.SetActive(false);
                }
                return;
            }
            if (_shield == null && !CreateShield())
            {
                return;
            }
            _shield.SetActive(true);
        }

        private static bool CreateShield()
        {
            if (_shieldFailed)
            {
                return false;
            }
            if (_shieldSprite == null)
            {
                var tex = RouteArt.LoadPng("viking-shield.png", mipmaps: true);
                if (tex == null)
                {
                    _shieldFailed = true;
                    return false;
                }
                _shieldSprite = Sprite.Create(tex, new Rect(0, 0, tex.width, tex.height), new Vector2(0.5f, 0.5f));
            }
            _shield = new GameObject("MapRoutesShield", typeof(RectTransform));
            var rect = (RectTransform)_shield.transform;
            rect.SetParent(_wrapper.parent, false);
            // Behind the map, so the map covers the middle of the shield.
            rect.SetSiblingIndex(_wrapper.GetSiblingIndex());
            // Centered on the map, in the same parent and units as the map's mask.
            rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(0.5f, 0.5f);
            rect.localScale = _wrapper.localScale;
            rect.localRotation = _wrapper.localRotation;
            rect.sizeDelta = _wrapper.rect.size * ShieldSize;
            rect.position = _wrapper.TransformPoint(_wrapper.rect.center);
            var image = Ui.AddImage(_shield);
            Ui.SetSprite(image, _shieldSprite, preserveAspect: true);
            Ui.SetRaycastTarget(image, false);
            return true;
        }

        // The game lays the icons out leftward from the list root, so the root is their right edge.
        private static void MakeRoomForStatusEffects()
        {
            var root = Hud.instance != null ? Hud.instance.m_statusEffectListRoot : null;
            if (root == null)
            {
                return;
            }
            if (root != _statusRoot)
            {
                _statusRoot = root;
                _statusPosition = root.localPosition;
            }
            root.localPosition = _statusPosition;
            var left = float.MaxValue;
            if (_shield != null && _shield.activeInHierarchy)
            {
                left = WorldRect((RectTransform)_shield.transform).xMin;
            }
            if (_scaled != null && _appliedScale > 1f)
            {
                left = Mathf.Min(left, WorldRect(_scaled).xMin);
            }
            if (left == float.MaxValue || _wrapper == null && _scaled == null)
            {
                return;
            }
            var gap = WorldRect(_wrapper != null ? _wrapper : _scaled).width * 0.05f;
            var shift = left - gap - root.position.x;
            if (shift < 0f)
            {
                root.position += new Vector3(shift, 0f, 0f);
            }
        }

        private static void SetRound(Minimap minimap, bool round)
        {
            _appliedRound = round;
            if (round && !_loggedLayout)
            {
                _loggedLayout = true;
                LogLayout(minimap);
            }
            if (round && _wrapper == null && !CreateMask())
            {
                return;
            }
            if (_mask != null)
            {
                _mask.enabled = round;
                _maskImage.enabled = round;
            }
            if (_rim != null)
            {
                _rim.SetActive(round);
            }
            if (round)
            {
                FadeFrames();
            }
            else
            {
                RestoreFrames();
                RestoreBiomeName();
            }
            Round = round;
        }

        // Puts the map image into a new parent of the same size that carries a round mask.
        private static bool CreateMask()
        {
            if (MaskType == null || ShowMaskGraphicProperty == null)
            {
                _broken = true;
                Plugin.Log.LogError("Round corner map disabled: UnityEngine.UI.Mask not found in this game version");
                return false;
            }
            var parent = _image.parent;
            var go = new GameObject("MapRoutesRoundMask", typeof(RectTransform));
            _wrapper = (RectTransform)go.transform;
            _wrapper.SetParent(parent, false);
            _wrapper.SetSiblingIndex(_image.GetSiblingIndex());
            _wrapper.anchorMin = _image.anchorMin;
            _wrapper.anchorMax = _image.anchorMax;
            _wrapper.pivot = _image.pivot;
            _wrapper.anchoredPosition = _image.anchoredPosition;
            _wrapper.sizeDelta = _image.sizeDelta;
            _wrapper.localRotation = _image.localRotation;
            _wrapper.localScale = _image.localScale;

            _image.SetParent(_wrapper, false);
            _image.anchorMin = Vector2.zero;
            _image.anchorMax = Vector2.one;
            _image.pivot = new Vector2(0.5f, 0.5f);
            _image.anchoredPosition = Vector2.zero;
            _image.sizeDelta = Vector2.zero;
            _image.localRotation = Quaternion.identity;
            _image.localScale = Vector3.one;

            _maskImage = Ui.AddImage(go);
            Ui.SetSprite(_maskImage, Circle());
            Ui.SetRaycastTarget(_maskImage, false);
            _mask = go.AddComponent(MaskType) as Behaviour;
            ShowMaskGraphicProperty.SetValue(_mask, false, null);

            _rim = new GameObject("MapRoutesRoundRim", typeof(RectTransform));
            var rim = (RectTransform)_rim.transform;
            rim.SetParent(parent, false);
            rim.SetSiblingIndex(_wrapper.GetSiblingIndex() + 1);
            rim.anchorMin = _wrapper.anchorMin;
            rim.anchorMax = _wrapper.anchorMax;
            rim.pivot = _wrapper.pivot;
            rim.anchoredPosition = _wrapper.anchoredPosition;
            rim.sizeDelta = _wrapper.sizeDelta;
            var rimImage = _rimImage = Ui.AddImage(_rim);
            Ui.SetSprite(rimImage, Ring());
            Ui.SetColor(rimImage, RimColor);
            Ui.SetRaycastTarget(rimImage, false);

            var graphic = GraphicType == null ? null : _image.GetComponent(GraphicType);
            var material = graphic == null ? null : MaterialProperty?.GetValue(graphic, null) as Material;
            if (material != null && !material.HasProperty("_Stencil"))
            {
                Plugin.Log.LogWarning($"The corner map's shader ({material.shader.name}) may not support masks; " +
                    "the map may stay square");
            }
            return true;
        }

        // The game's square frame or background behind the map would show around the circle, so it is faded out.
        // Only images about the size of the map are touched, never pins, markers or text.
        private static void FadeFrames()
        {
            RestoreFrames();
            var mapRect = WorldRect(_wrapper);
            for (var t = _wrapper.parent; t != null && t != _owner.m_smallRoot.transform.parent; t = t.parent)
            {
                foreach (Component component in t.GetComponentsInChildren(Ui.ImageType, true))
                {
                    var image = component as Behaviour;
                    if (image == null || image == _maskImage || image.transform.IsChildOf(_wrapper)
                        || image.gameObject == _rim || !SimilarSize(WorldRect((RectTransform)image.transform), mapRect))
                    {
                        continue;
                    }
                    if (FadedFrames.Exists(p => p.Key == image))
                    {
                        continue;
                    }
                    var color = Ui.GetColor(image);
                    FadedFrames.Add(new KeyValuePair<Behaviour, Color>(image, color));
                    Ui.SetColor(image, new Color(color.r, color.g, color.b, 0f));
                }
            }
        }

        private static void RestoreFrames()
        {
            foreach (var pair in FadedFrames)
            {
                if (pair.Key != null)
                {
                    Ui.SetColor(pair.Key, pair.Value);
                }
            }
            FadedFrames.Clear();
        }

        // A frame is a little larger than the map, a background about the same; pins and markers are much smaller.
        private static bool SimilarSize(Rect a, Rect map)
        {
            return a.width >= map.width * 0.9f && a.width <= map.width * 1.3f
                && a.height >= map.height * 0.9f && a.height <= map.height * 1.3f
                && Mathf.Abs(a.center.x - map.center.x) <= map.width * 0.15f
                && Mathf.Abs(a.center.y - map.center.y) <= map.height * 0.15f;
        }

        /// <summary>
        /// After the game centers the corner map, its window onto the map is widened by the scale, so a bigger corner
        /// map shows more of the map at the same detail instead of the same area larger.
        /// </summary>
        internal static void Widen(Minimap minimap)
        {
            var scale = Scale;
            if (minimap.m_mode != Minimap.MapMode.Small || scale <= 1f || _image == null)
            {
                return;
            }
            var image = SmallMapField?.GetValue(minimap) as Component;
            if (image == null)
            {
                return;
            }
            // The game just set the window to the zoom, centered on the player.
            var uv = Ui.UvRect(image);
            var zoom = uv.height;
            var center = uv.center;
            uv.width = zoom * scale;
            uv.height = zoom * scale;
            uv.center = center;
            Ui.SetUvRect(image, uv);
            if (SmallMaterialField?.GetValue(minimap) is Material material)
            {
                material.SetFloat("_zoom", zoom * scale);
                material.SetFloat("_pixelSize", 200f / (zoom * scale));
            }
        }

        // Pins, markers and the biome name are counter-scaled, so they keep the game's size on a bigger corner map.
        private static void KeepSizes(Minimap minimap)
        {
            var scale = Scale;
            if (scale <= 1f && Unscaled.Count == 0)
            {
                return;
            }
            if (Unscaled.Count > 200)
            {
                // Pins come and go; forget the destroyed ones.
                var dead = new List<Transform>();
                foreach (var t in Unscaled.Keys)
                {
                    if (t == null)
                    {
                        dead.Add(t);
                    }
                }
                foreach (var t in dead)
                {
                    Unscaled.Remove(t);
                }
            }
            Keep(minimap.m_smallMarker, scale);
            Keep(minimap.m_smallShipMarker, scale);
            Keep(minimap.m_windMarker, scale);
            KeepFontSize(BiomeNameField?.GetValue(minimap) as Component, scale);
            foreach (var root in new[] { minimap.m_pinRootSmall, PinNameRootField?.GetValue(minimap) as RectTransform })
            {
                if (root == null)
                {
                    continue;
                }
                foreach (Transform pin in root)
                {
                    Keep(pin, scale);
                }
            }
        }

        // The biome name's scale is driven by its pulse animation, so its font size is made smaller instead.
        private static void KeepFontSize(Component text, float scale)
        {
            if (text == null || _scaled == null || !text.transform.IsChildOf(_scaled))
            {
                return;
            }
            var fontSize = AccessTools.Property(text.GetType(), "fontSize");
            if (fontSize == null)
            {
                return;
            }
            if (_biomeFontText != text)
            {
                _biomeFontText = text;
                _biomeFontSize = (float)fontSize.GetValue(text, null);
            }
            fontSize.SetValue(text, _biomeFontSize / scale, null);
        }

        private static void Keep(Transform t, float scale)
        {
            // Only what grows with the corner map; a part outside it keeps its size anyway.
            if (t == null || _scaled == null || !t.IsChildOf(_scaled))
            {
                return;
            }
            if (!Unscaled.TryGetValue(t, out var original))
            {
                original = t.localScale;
                Unscaled[t] = original;
            }
            t.localScale = original / scale;
        }

        private static void SetScale(Minimap minimap, float scale)
        {
            if (_scaled == null)
            {
                _scaled = PickScaleTarget(minimap);
                if (_scaled == null)
                {
                    _broken = true;
                    Plugin.Log.LogError("Bigger corner map disabled: no part of the corner map to scale was found");
                    return;
                }
                _originalScale = _scaled.localScale;
                _originalPosition = _scaled.localPosition;
            }
            // Scale from the original, then move it back so its middle stays where the game puts it.
            _scaled.localScale = _originalScale;
            _scaled.localPosition = _originalPosition;
            var before = WorldRect(_scaled).center;
            _scaled.localScale = _originalScale * scale;
            var after = WorldRect(_scaled).center;
            _scaled.position += (Vector3)(before - after);
            _appliedScale = scale;
            _appliedScreen = new Vector2Int(Screen.width, Screen.height);
        }

        // The largest part of the corner map, from the map image up to the corner map's root, that is still a corner
        // panel and not a screen-wide container.
        private static RectTransform PickScaleTarget(Minimap minimap)
        {
            RectTransform pick = null;
            var root = minimap.m_smallRoot != null ? minimap.m_smallRoot.transform : null;
            for (var t = (_wrapper != null ? _wrapper : _image).parent; t != null; t = t.parent)
            {
                if (t is RectTransform rect)
                {
                    var r = WorldRect(rect);
                    if (r.width > Screen.width * 0.5f || r.height > Screen.height * 0.6f)
                    {
                        break;
                    }
                    pick = rect;
                }
                if (t == root)
                {
                    break;
                }
            }
            return pick;
        }

        private static Rect WorldRect(RectTransform rect)
        {
            var corners = new Vector3[4];
            rect.GetWorldCorners(corners);
            return Rect.MinMaxRect(corners[0].x, corners[0].y, corners[2].x, corners[2].y);
        }

        private static void Reset()
        {
            RestoreFrames();
            _biomeName = null;
            _image = null;
            _wrapper = null;
            _mask = null;
            _maskImage = null;
            _rim = null;
            _scaled = null;
            _rimImage = null;
            _shield = null;
            if (_statusRoot != null)
            {
                _statusRoot.localPosition = _statusPosition;
            }
            _statusRoot = null;
            _biomeFontText = null;
            Unscaled.Clear();
            _appliedScale = 1f;
            _appliedRound = false;
            Round = false;
        }

        // Logged once so the layout can be checked from LogOutput.log when the shape or size looks off in the game.
        private static void LogLayout(Minimap minimap)
        {
            if (minimap.m_smallRoot == null)
            {
                return;
            }
            var text = new StringBuilder("Corner map layout:");
            Describe(minimap.m_smallRoot.transform, 0, text);
            Plugin.Log.LogInfo(text.ToString());
        }

        private static void Describe(Transform t, int depth, StringBuilder text)
        {
            if (depth > 4)
            {
                return;
            }
            text.Append('\n').Append(' ', depth * 2).Append(t.name);
            if (t is RectTransform rect)
            {
                var r = WorldRect(rect);
                text.Append($" [{r.width:0}x{r.height:0} at {r.center.x:0},{r.center.y:0}]");
            }
            foreach (var component in t.GetComponents<Component>())
            {
                if (!(component is Transform))
                {
                    text.Append(' ').Append(component.GetType().Name);
                }
            }
            if (t.name.StartsWith("pin", StringComparison.OrdinalIgnoreCase) && t.childCount > 3)
            {
                text.Append($" ({t.childCount} children)");
                return;
            }
            foreach (Transform child in t)
            {
                Describe(child, depth + 1, text);
            }
        }

        private static Sprite Circle()
        {
            return _circle != null ? _circle : _circle = MakeRing(0f);
        }

        private static Sprite Ring()
        {
            // Thinner on a bigger corner map, so the rim keeps its width on screen.
            if (_ring == null || !Mathf.Approximately(_ringScale, Scale))
            {
                _ringScale = Scale;
                if (_ring != null)
                {
                    UnityEngine.Object.Destroy(_ring.texture);
                    UnityEngine.Object.Destroy(_ring);
                }
                _ring = MakeRing(1f - RimWidth / _ringScale);
            }
            return _ring;
        }

        // A white disc (inner 0) or ring with soft edges; inner is the hole's radius as a share of the outer one.
        private static Sprite MakeRing(float inner)
        {
            const int size = 256;
            var tex = new Texture2D(size, size, TextureFormat.RGBA32, false) { filterMode = FilterMode.Bilinear };
            var pixels = new Color32[size * size];
            var outer = size * 0.5f;
            var hole = outer * inner;
            for (var y = 0; y < size; y++)
            {
                for (var x = 0; x < size; x++)
                {
                    var dx = x + 0.5f - outer;
                    var dy = y + 0.5f - outer;
                    var d = Mathf.Sqrt(dx * dx + dy * dy);
                    var a = Mathf.Clamp01(outer - d);
                    if (inner > 0f)
                    {
                        a = Mathf.Min(a, Mathf.Clamp01(d - hole));
                    }
                    pixels[y * size + x] = new Color32(255, 255, 255, (byte)(a * 255f));
                }
            }
            tex.SetPixels32(pixels);
            tex.Apply();
            return Sprite.Create(tex, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f));
        }
    }
}
