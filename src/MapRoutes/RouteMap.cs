using System;
using System.Collections.Generic;
using System.Reflection;
using HarmonyLib;
using UnityEngine;
using UnityEngine.Events;

namespace ValheimMapRoutes
{
    /// <summary>
    /// Draw mode, the quill button and the dotted lines on the large map. Positions are in the map image's local
    /// space measured from its bottom-left corner, the same space the game places its pins in.
    /// </summary>
    internal static class RouteMap
    {
        // In map UI units, so the dots keep their size and spacing at every zoom.
        private const float DotSize = 4.5f;
        private const float DotSpacing = 9f;
        // Up to this zoom (the share of the map's width on screen) dots have their full size and spacing. Further out
        // they shrink with the zoom's ZoomPower, to MinZoomScale when fully zoomed out, so they stay fine over a
        // larger area.
        private const float FullSizeZoom = 0.05f;
        private const float ZoomPower = 0.4f;
        private const float MinZoomScale = 0.45f;
        private const float HitRadius = 12f;
        // A stroke records a point each time the pointer moved this far, and is thinned to this tolerance when done.
        private const float StrokeStep = 4f;
        private const float StrokeTolerance = 1.5f;
        // A right press that moved more than this many screen pixels was a pan, not a click.
        private const float DragThreshold = 6f;
        private const int MaxDots = 6000;

        // ZInput.pointerPosition is newer than the compile-time libs.
        private static readonly MethodInfo PointerPositionGetter = AccessTools.PropertyGetter(typeof(ZInput), "pointerPosition");
        // Private in the live game.
        private static readonly MethodInfo GetClosestPinToCursorMethod = AccessTools.Method(typeof(Minimap), "GetClosestPinToCursor");
        private static readonly MethodInfo SelectIconMethod = AccessTools.Method(typeof(Minimap), "SelectIcon");
        private static readonly FieldInfo SelectedTypeField = AccessTools.Field(typeof(Minimap), "m_selectedType");
        private static readonly AccessTools.FieldRef<Minimap, Vector3> MapOffsetRef =
            AccessTools.FieldRefAccess<Minimap, Vector3>("m_mapOffset");
        // ZInput's mouse buttons are newer than the compile-time libs.
        private static readonly Func<int, bool> GetMouseButton = MouseButtonMethod("GetMouseButton");
        private static readonly Func<int, bool> GetMouseButtonDown = MouseButtonMethod("GetMouseButtonDown");
        // Typed as UnityEngine.UI components, which this project can't reference.
        private static readonly FieldInfo LargeMapField = AccessTools.Field(typeof(Minimap), "m_mapImageLarge");
        private static readonly FieldInfo SmallMapField = AccessTools.Field(typeof(Minimap), "m_mapImageSmall");
        private static readonly FieldInfo[] VanillaFrameFields =
        {
            AccessTools.Field(typeof(Minimap), "m_selectedIcon0"),
            AccessTools.Field(typeof(Minimap), "m_selectedIcon1"),
            AccessTools.Field(typeof(Minimap), "m_selectedIcon2"),
            AccessTools.Field(typeof(Minimap), "m_selectedIcon3"),
            AccessTools.Field(typeof(Minimap), "m_selectedIcon4"),
            AccessTools.Field(typeof(Minimap), "m_selectedIconDeath"),
            AccessTools.Field(typeof(Minimap), "m_selectedIconBoss"),
            AccessTools.Field(typeof(Minimap), "m_selectedIconPing"),
        };

        private static readonly DotLayer LargeDots = new DotLayer();
        private static readonly DotLayer SmallDots = new DotLayer();
        private static GameObject _button;
        private static Behaviour _buttonFrame;
        private static bool _broken;

        // The stroke being drawn, or null.
        private static List<Vector3> _current;
        // Right-drag panning in draw mode: where the press started and where the pointer was last frame.
        private static bool _rightHeld;
        private static Vector2 _rightPressedAt;
        private static Vector2 _rightLast;

        internal static bool Drawing { get; private set; }

        /// <summary>The pooled dot images on one map (large or corner). They live under that map's image.</summary>
        private sealed class DotLayer
        {
            internal readonly List<Behaviour> Dots = new List<Behaviour>();
            internal readonly List<Color> Colors = new List<Color>();
            internal RectTransform Root;
            internal float Size;
            internal float Spacing;
            internal int Used;
        }

        /// <summary>A map image's geometry for one frame.</summary>
        private struct MapView
        {
            internal RectTransform Rect;
            internal Rect Uv;
            internal Vector2 Size;
            internal float TextureSize;
            internal float PixelSize;

            internal static bool Get(Minimap minimap, out MapView view, bool small = false)
            {
                view = default;
                var image = (small ? SmallMapField : LargeMapField)?.GetValue(minimap) as Component;
                if (image == null)
                {
                    return false;
                }
                view.Rect = (RectTransform)image.transform;
                view.Uv = Ui.UvRect(image);
                view.Size = view.Rect.rect.size;
                view.TextureSize = minimap.m_textureSize;
                view.PixelSize = minimap.m_pixelSize;
                return view.Size.x > 0f && view.Size.y > 0f;
            }

            // Same math as Minimap.WorldToMapPoint and MapPointToLocalGuiPos, which are private.
            internal Vector2 ToLocal(Vector3 world)
            {
                var half = (int)TextureSize / 2;
                var mx = (world.x / PixelSize + half) / TextureSize;
                var my = (world.z / PixelSize + half) / TextureSize;
                return new Vector2((mx - Uv.xMin) / Uv.width * Size.x, (my - Uv.yMin) / Uv.height * Size.y);
            }

            // Same math as Minimap.MapPointToWorld.
            internal Vector3 ToWorld(Vector2 local)
            {
                var half = (int)TextureSize / 2;
                var mx = Uv.xMin + local.x / Size.x * Uv.width;
                var my = Uv.yMin + local.y / Size.y * Uv.height;
                return new Vector3((mx * TextureSize - half) * PixelSize, 0f, (my * TextureSize - half) * PixelSize);
            }

            internal bool Pointer(out Vector2 local)
            {
                local = Vector2.zero;
                if (!RectTransformUtility.ScreenPointToLocalPointInRectangle(Rect, PointerPosition(), null, out var point))
                {
                    return false;
                }
                local = point - Rect.rect.min;
                return Rect.rect.Contains(point);
            }
        }

        internal static void Update(Minimap minimap)
        {
            if (_broken || minimap.m_mode == Minimap.MapMode.None)
            {
                return;
            }
            if (!Ui.Available)
            {
                _broken = true;
                Plugin.Log.LogError("Map Routes disabled: UnityEngine.UI types not found in this game version");
                return;
            }
            if (minimap.m_mode == Minimap.MapMode.Small)
            {
                // The corner map draws the same routes, with dots scaled like its pins.
                if (RouteStore.Refresh() && MapView.Get(minimap, out var corner, small: true))
                {
                    var scale = minimap.m_pinSizeLarge > 0f ? minimap.m_pinSizeSmall / minimap.m_pinSizeLarge : 1f;
                    DrawRoutes(minimap, corner, SmallDots, scale);
                }
                return;
            }
            if (_button == null)
            {
                CreateButton(minimap);
            }
            if (!RouteStore.Refresh() || !MapView.Get(minimap, out var view))
            {
                return;
            }
            // A reload for another character or world dropped the route being drawn.
            if (_current != null && !RouteStore.Routes.Contains(_current))
            {
                _current = null;
            }
            RouteArt.ShowQuillCursor(Drawing);
            if (Drawing)
            {
                ContinueStroke(view);
                Pan(minimap, view);
            }
            DrawRoutes(minimap, view, LargeDots, 1f);
        }

        internal static void ToggleDrawing(Minimap minimap)
        {
            if (Drawing)
            {
                StopDrawing(minimap);
                return;
            }
            Drawing = true;
            foreach (var field in VanillaFrameFields)
            {
                if (field?.GetValue(minimap) is Behaviour frame && frame != null)
                {
                    frame.enabled = false;
                }
            }
            if (_buttonFrame != null)
            {
                _buttonFrame.enabled = true;
            }
        }

        internal static void StopDrawing(Minimap minimap)
        {
            if (!Drawing)
            {
                return;
            }
            Drawing = false;
            EndStroke(minimap);
            _rightHeld = false;
            if (_buttonFrame != null)
            {
                _buttonFrame.enabled = false;
            }
            RouteArt.ShowQuillCursor(false);
            // Gives the selected pin type its frame back.
            var selected = SelectedTypeField?.GetValue(minimap);
            if (selected != null)
            {
                SelectIconMethod?.Invoke(minimap, new[] { selected });
            }
        }

        /// <summary>Left press on the map in draw mode: a new stroke starts at the pointer.</summary>
        internal static void StartStroke(Minimap minimap)
        {
            EndStroke(minimap);
            if (!RouteStore.Refresh() || !MapView.Get(minimap, out var view) || !view.Pointer(out var local))
            {
                return;
            }
            _current = new List<Vector3> { view.ToWorld(local) };
            RouteStore.Routes.Add(_current);
        }

        /// <summary>Left release, or draw mode ending: the stroke becomes a route if it is more than a dot.</summary>
        internal static void EndStroke(Minimap minimap)
        {
            if (_current != null)
            {
                FinishStroke(MapView.Get(minimap, out var view) ? view : (MapView?)null);
            }
        }

        // While the left button is held, add the pointer each time it moved StrokeStep. The release normally ends the
        // stroke; if it was missed (released over another window), the button state ends it here.
        private static void ContinueStroke(MapView view)
        {
            if (_current == null)
            {
                return;
            }
            if (GetMouseButton != null && !GetMouseButton(0))
            {
                FinishStroke(view);
                return;
            }
            if (view.Pointer(out var local) && (view.ToLocal(_current[_current.Count - 1]) - local).magnitude >= StrokeStep)
            {
                _current.Add(view.ToWorld(local));
            }
        }

        private static void FinishStroke(MapView? view)
        {
            if (_current.Count < 2)
            {
                RouteStore.Routes.Remove(_current);
            }
            else if (view != null)
            {
                Thin(_current, view.Value);
            }
            _current = null;
            RouteStore.Save();
        }

        // Right-drag moves the map like the game's left-drag: the offset from the player moves against the pointer.
        private static void Pan(Minimap minimap, MapView view)
        {
            if (GetMouseButton == null || GetMouseButtonDown == null)
            {
                return;
            }
            var pointer = PointerPosition();
            if (GetMouseButtonDown(1) && view.Pointer(out _))
            {
                _rightHeld = true;
                _rightPressedAt = pointer;
                _rightLast = pointer;
                return;
            }
            if (!_rightHeld)
            {
                return;
            }
            if (!GetMouseButton(1))
            {
                _rightHeld = false;
                return;
            }
            if (RectTransformUtility.ScreenPointToLocalPointInRectangle(view.Rect, _rightLast, null, out var from)
                && RectTransformUtility.ScreenPointToLocalPointInRectangle(view.Rect, pointer, null, out var to))
            {
                var worldPerUnit = view.Uv.width * view.TextureSize * view.PixelSize / view.Size.x;
                var delta = (to - from) * worldPerUnit;
                MapOffsetRef(minimap) -= new Vector3(delta.x, 0f, delta.y);
            }
            _rightLast = pointer;
        }

        /// <summary>True when the right button moved since it was pressed in draw mode, so its release isn't a click.</summary>
        internal static bool RightDragged()
        {
            return Drawing && (PointerPosition() - _rightPressedAt).magnitude > DragThreshold;
        }

        // Ramer-Douglas-Peucker in map UI units at the current zoom: drops the points the line doesn't need.
        private static void Thin(List<Vector3> route, MapView view)
        {
            if (route.Count < 3)
            {
                return;
            }
            var local = route.ConvertAll(view.ToLocal);
            var keep = new bool[route.Count];
            keep[0] = true;
            keep[route.Count - 1] = true;
            var spans = new Stack<KeyValuePair<int, int>>();
            spans.Push(new KeyValuePair<int, int>(0, route.Count - 1));
            while (spans.Count > 0)
            {
                var span = spans.Pop();
                var farthest = -1;
                var farthestDistance = StrokeTolerance;
                for (var i = span.Key + 1; i < span.Value; i++)
                {
                    var distance = DistanceToSegment(local[i], local[span.Key], local[span.Value]);
                    if (distance > farthestDistance)
                    {
                        farthestDistance = distance;
                        farthest = i;
                    }
                }
                if (farthest >= 0)
                {
                    keep[farthest] = true;
                    spans.Push(new KeyValuePair<int, int>(span.Key, farthest));
                    spans.Push(new KeyValuePair<int, int>(farthest, span.Value));
                }
            }
            var kept = new List<Vector3>();
            for (var i = 0; i < route.Count; i++)
            {
                if (keep[i])
                {
                    kept.Add(route[i]);
                }
            }
            route.Clear();
            route.AddRange(kept);
        }

        /// <summary>Removes the route under the pointer. Pins under the pointer come first, so then nothing happens.</summary>
        internal static bool RemoveRouteUnderPointer(Minimap minimap)
        {
            if (!RouteStore.Refresh() || !MapView.Get(minimap, out var view) || !view.Pointer(out var local))
            {
                return false;
            }
            if (GetClosestPinToCursorMethod != null && GetClosestPinToCursorMethod.Invoke(minimap, null) != null)
            {
                return false;
            }
            List<Vector3> hit = null;
            var hitDistance = HitRadius;
            foreach (var route in RouteStore.Routes)
            {
                for (var i = 1; i < route.Count; i++)
                {
                    var distance = DistanceToSegment(local, view.ToLocal(route[i - 1]), view.ToLocal(route[i]));
                    if (distance < hitDistance)
                    {
                        hitDistance = distance;
                        hit = route;
                    }
                }
            }
            if (hit == null)
            {
                return false;
            }
            if (hit == _current)
            {
                _current = null;
            }
            RouteStore.Routes.Remove(hit);
            RouteStore.Save();
            return true;
        }

        // A copy of the first pin-type button, placed one step before it (above, in the game's column), with the quill
        // as its icon. The selected frame is the button's child; the button's own Image is the icon (see
        // Minimap.ToggleIconFilter).
        private static void CreateButton(Minimap minimap)
        {
            var frame = VanillaFrameFields[0]?.GetValue(minimap) as Component;
            var source = frame == null ? null : frame.transform.parent as RectTransform;
            if (source == null)
            {
                return;
            }
            try
            {
                LogLayout(minimap);
                var copy = UnityEngine.Object.Instantiate(source.gameObject, source.parent);
                copy.name = "MapRoutesQuill";
                _button = copy;
                var rect = (RectTransform)copy.transform;
                rect.SetSiblingIndex(source.GetSiblingIndex());
                if (!Ui.HasLayoutGroup(source.parent))
                {
                    var nextFrame = VanillaFrameFields[1]?.GetValue(minimap) as Component;
                    var next = nextFrame == null ? null : nextFrame.transform.parent as RectTransform;
                    var step = next == null ? new Vector2(0f, -source.rect.height) : next.anchoredPosition - source.anchoredPosition;
                    MakeRoom(source.parent as RectTransform, -step);
                    rect.anchoredPosition = source.anchoredPosition - step;
                }

                var icon = Ui.GetImage(copy.transform);
                if (icon != null)
                {
                    var quill = RouteArt.Quill();
                    if (quill != null)
                    {
                        Ui.SetSprite(icon, quill, preserveAspect: true);
                    }
                    Ui.SetColor(icon, Color.white);
                }
                var frameTransform = copy.transform.Find(frame.name);
                _buttonFrame = frameTransform == null ? null : Ui.GetImage(frameTransform);
                if (_buttonFrame != null)
                {
                    _buttonFrame.enabled = Drawing;
                }

                Rewire(copy, minimap);
            }
            catch (Exception e)
            {
                Plugin.Log.LogError($"Adding the quill button failed: {e}");
            }
        }

        // The pin buttons hang in a panel (IconPanel) whose gray background is a stretched child. The panel grows by
        // one step toward the quill's side, with its buttons kept where they were, so the background reaches behind
        // the quill. Panels next to it that the grown panel would now overlap (the death and boss box above it) move
        // the same step out, background and all, so the gap between them stays.
        private static void MakeRoom(RectTransform panel, Vector2 growth)
        {
            if (panel == null || panel.anchorMin != panel.anchorMax || growth == Vector2.zero)
            {
                return;
            }
            var kept = new List<KeyValuePair<RectTransform, Vector3>>();
            foreach (Transform child in panel)
            {
                if (child is RectTransform childRect && childRect.anchorMin == childRect.anchorMax)
                {
                    kept.Add(new KeyValuePair<RectTransform, Vector3>(childRect, childRect.position));
                }
            }

            // The edge on the growth side moves out; the opposite edge stays.
            var size = new Vector2(Mathf.Abs(growth.x), Mathf.Abs(growth.y));
            var shift = new Vector2(
                growth.x > 0f ? panel.pivot.x * size.x : -(1f - panel.pivot.x) * size.x,
                growth.y > 0f ? panel.pivot.y * size.y : -(1f - panel.pivot.y) * size.y);
            panel.sizeDelta += size;
            panel.anchoredPosition += shift;
            foreach (var child in kept)
            {
                child.Key.position = child.Value;
            }

            var grown = WorldRect(panel);
            var worldGrowth = panel.TransformVector(growth);
            foreach (Transform sibling in panel.parent)
            {
                if (sibling == panel || !(sibling is RectTransform other) || other.anchorMin != other.anchorMax
                    || Ui.GetImage(sibling) == null || !WorldRect(other).Overlaps(grown))
                {
                    continue;
                }
                other.position += worldGrowth;
            }
        }

        private static Rect WorldRect(RectTransform rect)
        {
            var corners = new Vector3[4];
            rect.GetWorldCorners(corners);
            return Rect.MinMaxRect(corners[0].x, corners[0].y, corners[2].x, corners[2].y);
        }

        // One line per map button, then the objects around them (parents up to the map root and their children),
        // so a misplaced quill or background can be fixed from a player's log.
        private static void LogLayout(Minimap minimap)
        {
            var around = new List<Transform>();
            foreach (var field in VanillaFrameFields)
            {
                var button = (field?.GetValue(minimap) as Component)?.transform.parent as RectTransform;
                if (button == null)
                {
                    continue;
                }
                Plugin.Log.LogInfo($"Map button {field.Name}: {Describe(button)}");
                for (var parent = button.parent; parent != null && parent != minimap.m_largeRoot.transform.parent; parent = parent.parent)
                {
                    if (!around.Contains(parent))
                    {
                        around.Add(parent);
                    }
                }
            }
            foreach (var parent in around)
            {
                Plugin.Log.LogInfo($"Map panel {Path(parent, minimap)}: {Describe(parent)}");
                foreach (Transform child in parent)
                {
                    if (!around.Contains(child) && Ui.GetImage(child) != null)
                    {
                        Plugin.Log.LogInfo($"  child {child.name}: {Describe(child)}");
                    }
                }
            }
        }

        private static string Describe(Transform transform)
        {
            var rect = transform as RectTransform;
            var image = Ui.GetImage(transform);
            var layout = Ui.HasLayoutGroup(transform) ? ", layout group" : "";
            var background = image != null && image.enabled ? $", image {Ui.GetSprite(image)?.name ?? "none"}" : "";
            return rect == null
                ? $"{transform.name} world {transform.position}"
                : $"{transform.name}, anchored {rect.anchoredPosition}, anchors {rect.anchorMin}-{rect.anchorMax}, " +
                    $"pivot {rect.pivot}, size {rect.rect.size}, world {rect.position}{background}{layout}";
        }

        private static string Path(Transform transform, Minimap minimap)
        {
            var path = transform.name;
            for (var parent = transform.parent; parent != null && parent != minimap.transform; parent = parent.parent)
            {
                path = parent.name + "/" + path;
            }
            return path;
        }

        // The copy still carries the source button's wiring (select Icon0, its gamepad key, its tooltip). Clear every
        // UnityEvent on it and listen on the button for the quill only.
        private static void Rewire(GameObject copy, Minimap minimap)
        {
            foreach (var component in copy.GetComponentsInChildren<Component>(true))
            {
                if (component == null)
                {
                    continue;
                }
                var typeName = component.GetType().Name;
                if (typeName == "UIGamePad")
                {
                    UnityEngine.Object.Destroy(component);
                    continue;
                }
                if (typeName == "UITooltip")
                {
                    AccessTools.Field(component.GetType(), "m_text")?.SetValue(component, "Draw route");
                }
                for (var type = component.GetType(); type != null && type != typeof(Component); type = type.BaseType)
                {
                    foreach (var field in type.GetFields(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.DeclaredOnly))
                    {
                        if (typeof(UnityEventBase).IsAssignableFrom(field.FieldType) && !field.FieldType.IsAbstract
                            && field.FieldType.GetConstructor(Type.EmptyTypes) != null)
                        {
                            field.SetValue(component, Activator.CreateInstance(field.FieldType));
                        }
                    }
                }
            }
            Ui.OnClick(copy, () => ToggleDrawing(minimap));
        }

        private static void DrawRoutes(Minimap minimap, MapView view, DotLayer layer, float scale)
        {
            if (layer.Root == null)
            {
                var root = new GameObject("MapRoutes", typeof(RectTransform));
                layer.Root = (RectTransform)root.transform;
                layer.Root.SetParent(view.Rect, false);
                layer.Root.anchorMin = Vector2.zero;
                layer.Root.anchorMax = Vector2.one;
                layer.Root.offsetMin = Vector2.zero;
                layer.Root.offsetMax = Vector2.zero;
                // A new map instance (another session) left the old dots behind with their destroyed root.
                layer.Dots.Clear();
                layer.Colors.Clear();
            }
            scale *= Mathf.Clamp(Mathf.Pow(FullSizeZoom / Mathf.Max(view.Uv.width, 0.0001f), ZoomPower), MinZoomScale, 1f);
            layer.Size = DotSize * scale;
            layer.Spacing = DotSpacing * scale;

            var red = RouteArt.Red(minimap);
            layer.Used = 0;
            foreach (var route in RouteStore.Routes)
            {
                var along = 0f;
                for (var i = 1; i < route.Count; i++)
                {
                    along = DrawSegment(layer, view.ToLocal(route[i - 1]), view.ToLocal(route[i]), along, view.Size, red);
                }
                if (route.Count == 1)
                {
                    var only = view.ToLocal(route[0]);
                    if (only.x >= 0f && only.y >= 0f && only.x <= view.Size.x && only.y <= view.Size.y)
                    {
                        PlaceDot(layer, only, red);
                    }
                }
            }
            for (var i = layer.Used; i < layer.Dots.Count; i++)
            {
                if (layer.Dots[i].gameObject.activeSelf)
                {
                    layer.Dots[i].gameObject.SetActive(false);
                }
            }
        }

        // Dots sit at whole multiples of the spacing along the route (along = the route's length before this segment),
        // so the spacing runs on across a stroke's many short segments and dots don't crawl while panning. The segment
        // is clipped to the map first, so a zoomed-in map doesn't walk thousands of dots off screen. Returns the
        // route's length after this segment.
        private static float DrawSegment(DotLayer layer, Vector2 a, Vector2 b, float along, Vector2 size, Color color)
        {
            var length = (b - a).magnitude;
            if (length < 0.01f || !Clip(a, b, size, out var t0, out var t1))
            {
                return along + length;
            }
            var direction = (b - a) / length;
            for (var d = Mathf.Ceil((along + t0 * length) / layer.Spacing) * layer.Spacing - along; d <= t1 * length; d += layer.Spacing)
            {
                if (!PlaceDot(layer, a + direction * d, color))
                {
                    break;
                }
            }
            return along + length;
        }

        private static bool PlaceDot(DotLayer layer, Vector2 position, Color color)
        {
            if (layer.Used >= MaxDots)
            {
                return false;
            }
            if (layer.Used == layer.Dots.Count)
            {
                var go = new GameObject("Dot", typeof(RectTransform));
                go.transform.SetParent(layer.Root, false);
                var image = Ui.AddImage(go);
                Ui.SetSprite(image, RouteArt.Dot);
                Ui.SetRaycastTarget(image, false);
                Ui.SetColor(image, color);
                var rect = (RectTransform)go.transform;
                rect.anchorMin = Vector2.zero;
                rect.anchorMax = Vector2.zero;
                rect.pivot = new Vector2(0.5f, 0.5f);
                layer.Dots.Add(image);
                layer.Colors.Add(color);
            }
            var dot = layer.Dots[layer.Used];
            // The color goes through reflection, so only set it when it changes.
            if (layer.Colors[layer.Used] != color)
            {
                Ui.SetColor(dot, color);
                layer.Colors[layer.Used] = color;
            }
            layer.Used++;
            var dotRect = (RectTransform)dot.transform;
            dotRect.anchoredPosition = position;
            dotRect.sizeDelta = new Vector2(layer.Size, layer.Size);
            if (!dot.gameObject.activeSelf)
            {
                dot.gameObject.SetActive(true);
            }
            return true;
        }

        // Liang-Barsky: the part of a->b inside the map rect, as fractions of the segment.
        private static bool Clip(Vector2 a, Vector2 b, Vector2 size, out float t0, out float t1)
        {
            t0 = 0f;
            t1 = 1f;
            var d = b - a;
            return ClipEdge(-d.x, a.x, ref t0, ref t1)
                && ClipEdge(d.x, size.x - a.x, ref t0, ref t1)
                && ClipEdge(-d.y, a.y, ref t0, ref t1)
                && ClipEdge(d.y, size.y - a.y, ref t0, ref t1);
        }

        private static bool ClipEdge(float p, float q, ref float t0, ref float t1)
        {
            if (Mathf.Approximately(p, 0f))
            {
                return q >= 0f;
            }
            var t = q / p;
            if (p < 0f)
            {
                if (t > t1)
                {
                    return false;
                }
                t0 = Mathf.Max(t0, t);
            }
            else
            {
                if (t < t0)
                {
                    return false;
                }
                t1 = Mathf.Min(t1, t);
            }
            return true;
        }

        private static float DistanceToSegment(Vector2 p, Vector2 a, Vector2 b)
        {
            var ab = b - a;
            var t = ab.sqrMagnitude < 0.0001f ? 0f : Mathf.Clamp01(Vector2.Dot(p - a, ab) / ab.sqrMagnitude);
            return (p - (a + ab * t)).magnitude;
        }

        private static Func<int, bool> MouseButtonMethod(string name)
        {
            var method = AccessTools.Method(typeof(ZInput), name, new[] { typeof(int) });
            return method == null ? null : AccessTools.MethodDelegate<Func<int, bool>>(method);
        }

        private static Vector2 PointerPosition()
        {
            if (PointerPositionGetter != null)
            {
                return (Vector3)PointerPositionGetter.Invoke(null, null);
            }
            return Input.mousePosition;
        }
    }
}
