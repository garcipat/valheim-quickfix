using System;
using System.Collections.Generic;
using System.Reflection;
using System.Text.RegularExpressions;
using HarmonyLib;
using UnityEngine;

namespace ValheimSignIcons.Patches
{
    /// <summary>
    /// Lets sign text contain &lt;icon=ItemPrefabName&gt; tags, like &lt;color=#hex&gt;, and draws the item's icon
    /// at that position. The tag is plain sign text, so vanilla already syncs it; only players with this mod see icons.
    /// </summary>
    /// <remarks>
    /// Sign.m_textWidget is a TextMeshProUGUI, but no NuGet package ships TextMeshPro, so it is driven through reflection
    /// (<see cref="SignIconRenderer"/>). The sign re-applies its text every 2 s, so the postfix runs on that cadence.
    /// </remarks>
    [HarmonyPatch(typeof(Sign), "OnCheckPermissionCompleted")]
    internal static class SignIconPatch
    {
        private static void Postfix(Sign __instance)
        {
            try
            {
                SignIconRenderer.Refresh(__instance);
            }
            catch (Exception e)
            {
                SignIconRenderer.ReportFailure(e);
            }
        }
    }

    /// <summary>
    /// Sign.GetText reads the text back from the widget, which now holds placeholders. Give the edit dialog and
    /// hover text the original text with its tags, so editing a sign doesn't lose its icons.
    /// </summary>
    [HarmonyPatch(typeof(Sign), "GetText")]
    internal static class SignGetTextPatch
    {
        private static void Postfix(Sign __instance, ref string __result)
        {
            var state = __instance.GetComponent<SignIconState>();
            if (state != null && state.Active && state.RawText != null)
            {
                __result = state.RawText;
            }
        }
    }

    public class SignIconState : MonoBehaviour
    {
        internal bool Active;
        internal string RawText;
        internal string ProcessedText;
        internal List<string> IconOnlyNames;
        internal readonly List<GameObject> Icons = new List<GameObject>();

        internal void DestroyIcons()
        {
            foreach (var icon in Icons)
            {
                if (icon != null)
                {
                    Destroy(icon);
                }
            }

            Icons.Clear();
        }
    }

    internal static class SignIconRenderer
    {
        private const string LinkPrefix = "vqf:";

        // Invisible placeholder the icon is laid over. Wrapped in a <link> so its glyph can be found after layout.
        private const string PlaceholderFormat = "<link=\"" + LinkPrefix + "{0}\"><alpha=#00>M<alpha=#FF></link>";

        private static readonly Regex IconTag = new Regex(@"<icon=([^>]+)>", RegexOptions.IgnoreCase | RegexOptions.Compiled);

        private static readonly FieldInfo WidgetField = AccessTools.Field(typeof(Sign), "m_textWidget");

        private static bool _resolved;
        private static bool _broken;
        private static bool _loggedFailure;

        private static PropertyInfo _textProp;
        private static PropertyInfo _textInfoProp;
        private static MethodInfo _forceMeshUpdate;
        private static object[] _forceMeshUpdateArgs;
        private static FieldInfo _linkCount;
        private static FieldInfo _linkInfo;
        private static FieldInfo _characterInfo;
        private static MethodInfo _getLinkId;
        private static FieldInfo _linkFirstChar;
        private static FieldInfo _bottomLeft;
        private static FieldInfo _topRight;
        private static FieldInfo _ascender;
        private static FieldInfo _descender;

        private static Dictionary<string, GameObject> _itemsByName;
        private static int _itemsCount = -1;

        public static void ReportFailure(Exception e)
        {
            if (_loggedFailure)
            {
                return;
            }

            _loggedFailure = true;
            Plugin.Log.LogError($"Sign icons failed (further errors suppressed): {e}");
        }

        public static void Refresh(Sign sign)
        {
            if (_broken || WidgetField == null)
            {
                return;
            }

            var widget = WidgetField.GetValue(sign) as Component;
            if (widget == null || !Resolve(widget.GetType()))
            {
                return;
            }

            var raw = _textProp.GetValue(widget, null) as string;
            var state = sign.GetComponent<SignIconState>();

            if (string.IsNullOrEmpty(raw) || !IconTag.IsMatch(raw))
            {
                if (state != null && state.Active)
                {
                    state.DestroyIcons();
                    state.Active = false;
                    state.RawText = null;
                    state.ProcessedText = null;
                    state.IconOnlyNames = null;
                }

                return;
            }

            if (state == null)
            {
                state = sign.gameObject.AddComponent<SignIconState>();
            }

            var changed = !state.Active || state.RawText != raw;
            if (changed)
            {
                state.RawText = raw;

                // A sign holding nothing but icons shows them large, filling the text area; no text is drawn.
                List<string> names;
                state.IconOnlyNames = TryGetIconOnly(raw, out names) ? names : null;
                state.ProcessedText = state.IconOnlyNames != null
                    ? string.Empty
                    : IconTag.Replace(raw, match =>
                    {
                        var name = match.Groups[1].Value.Trim();
                        return FindIcon(name) != null ? string.Format(PlaceholderFormat, name) : match.Value;
                    });
            }

            state.Active = true;

            // The game reset the widget to the raw text; show the placeholder version again.
            _textProp.SetValue(widget, state.ProcessedText, null);

            if (changed)
            {
                BuildIcons(widget, state);
            }
        }

        // Fraction of the text area the icons use when a sign holds only icons.
        private const float FillFraction = 0.9f;

        // True when the text, apart from valid icon tags, holds no visible characters. Unknown icon names stay
        // visible as text, so they keep a sign from counting as icon-only.
        private static bool TryGetIconOnly(string raw, out List<string> names)
        {
            var found = new List<string>();
            var rest = IconTag.Replace(raw, match =>
            {
                var name = match.Groups[1].Value.Trim();
                if (FindIcon(name) == null)
                {
                    return match.Value;
                }

                found.Add(name);
                return string.Empty;
            });

            names = found;
            if (found.Count == 0 || rest.IndexOf("<icon=", StringComparison.OrdinalIgnoreCase) >= 0)
            {
                return false;
            }

            return Regex.Replace(rest, "<[^>]*>", string.Empty).Trim().Length == 0;
        }

        private static void BuildIcons(Component widget, SignIconState state)
        {
            state.DestroyIcons();

            var canvas = widget.GetComponentInParent<Canvas>();
            if (state.IconOnlyNames != null)
            {
                BuildFilledIcons(widget, canvas, state);
            }
            else
            {
                BuildInlineIcons(widget, canvas, state);
            }
        }

        // Icons laid in a row, centered in the widget's rect, as large as the rect allows.
        private static void BuildFilledIcons(Component widget, Canvas canvas, SignIconState state)
        {
            var rectTransform = widget.transform as RectTransform;
            if (rectTransform == null)
            {
                return;
            }

            var rect = rectTransform.rect;
            var count = state.IconOnlyNames.Count;
            var size = Mathf.Min(rect.height, rect.width / count) * FillFraction;
            var left = rect.center.x - size * count * 0.5f + size * 0.5f;

            for (var i = 0; i < count; i++)
            {
                var sprite = FindIcon(state.IconOnlyNames[i]);
                if (sprite != null)
                {
                    CreateIcon(widget, canvas, state, sprite, new Vector3(left + size * i, rect.center.y, 0f), size);
                }
            }
        }

        // Icons placed over invisible placeholders in the text, sized to the placeholder.
        private static void BuildInlineIcons(Component widget, Canvas canvas, SignIconState state)
        {
            _forceMeshUpdate.Invoke(widget, _forceMeshUpdateArgs);

            var textInfo = _textInfoProp.GetValue(widget, null);
            var linkCount = (int)_linkCount.GetValue(textInfo);
            var links = (Array)_linkInfo.GetValue(textInfo);
            var characters = (Array)_characterInfo.GetValue(textInfo);

            for (var i = 0; i < linkCount; i++)
            {
                var link = links.GetValue(i);
                var id = _getLinkId.Invoke(link, null) as string;
                if (id == null || !id.StartsWith(LinkPrefix, StringComparison.Ordinal))
                {
                    continue;
                }

                var sprite = FindIcon(id.Substring(LinkPrefix.Length));
                if (sprite == null)
                {
                    continue;
                }

                var character = characters.GetValue((int)_linkFirstChar.GetValue(link));
                var bottomLeft = (Vector3)_bottomLeft.GetValue(character);
                var topRight = (Vector3)_topRight.GetValue(character);
                var ascender = (float)_ascender.GetValue(character);
                var descender = (float)_descender.GetValue(character);

                var size = (topRight.x - bottomLeft.x) * 1.15f;
                var center = new Vector3((bottomLeft.x + topRight.x) * 0.5f, (ascender + descender) * 0.5f, 0f);
                CreateIcon(widget, canvas, state, sprite, center, size);
            }
        }

        // The icon sits on the text's own plane (local z = 0) and is drawn above the text by sort order, so it
        // stays flat on the sign instead of floating in front of it.
        private static void CreateIcon(Component widget, Canvas canvas, SignIconState state, Sprite sprite, Vector3 center, float size)
        {
            var icon = new GameObject("VQFIcon");
            icon.layer = widget.gameObject.layer;
            icon.transform.SetParent(widget.transform, false);
            icon.transform.localPosition = center;

            var spriteSize = Mathf.Max(Mathf.Max(sprite.bounds.size.x, sprite.bounds.size.y), 0.0001f);
            var scale = size / spriteSize;
            icon.transform.localScale = new Vector3(scale, scale, 1f);

            var renderer = icon.AddComponent<SpriteRenderer>();
            renderer.sprite = sprite;
            if (canvas != null)
            {
                renderer.sortingLayerID = canvas.sortingLayerID;
                renderer.sortingOrder = canvas.sortingOrder + 1;
            }

            state.Icons.Add(icon);
        }

        private static Sprite FindIcon(string name)
        {
            var db = ObjectDB.instance;
            if (db == null || db.m_items == null)
            {
                return null;
            }

            if (_itemsByName == null || _itemsCount != db.m_items.Count)
            {
                _itemsByName = new Dictionary<string, GameObject>(StringComparer.OrdinalIgnoreCase);
                foreach (var item in db.m_items)
                {
                    if (item != null)
                    {
                        _itemsByName[item.name] = item;
                    }
                }

                _itemsCount = db.m_items.Count;
            }

            if (!_itemsByName.TryGetValue(name, out var prefab))
            {
                return null;
            }

            var drop = prefab.GetComponent<ItemDrop>();
            var icons = drop != null ? drop.m_itemData.m_shared.m_icons : null;
            return icons != null && icons.Length > 0 ? icons[0] : null;
        }

        // TextMeshPro members, looked up once from the widget's runtime type.
        private static bool Resolve(Type widgetType)
        {
            if (_resolved)
            {
                return !_broken;
            }

            _resolved = true;
            try
            {
                _textProp = widgetType.GetProperty("text");
                _textInfoProp = widgetType.GetProperty("textInfo");

                var forceUpdate = widgetType.GetMethod("ForceMeshUpdate", new[] { typeof(bool), typeof(bool) });
                if (forceUpdate != null)
                {
                    _forceMeshUpdate = forceUpdate;
                    _forceMeshUpdateArgs = new object[] { false, true };
                }
                else
                {
                    _forceMeshUpdate = widgetType.GetMethod("ForceMeshUpdate", Type.EmptyTypes);
                    _forceMeshUpdateArgs = new object[0];
                }

                var textInfoType = _textInfoProp.PropertyType;
                _linkCount = textInfoType.GetField("linkCount");
                _linkInfo = textInfoType.GetField("linkInfo");
                _characterInfo = textInfoType.GetField("characterInfo");

                var linkType = _linkInfo.FieldType.GetElementType();
                _getLinkId = linkType.GetMethod("GetLinkID");
                _linkFirstChar = linkType.GetField("linkTextfirstCharacterIndex");

                var characterType = _characterInfo.FieldType.GetElementType();
                _bottomLeft = characterType.GetField("bottomLeft");
                _topRight = characterType.GetField("topRight");
                _ascender = characterType.GetField("ascender");
                _descender = characterType.GetField("descender");

                var missing = new List<string>();
                foreach (var pair in new Dictionary<string, object>
                {
                    ["text"] = _textProp, ["textInfo"] = _textInfoProp, ["ForceMeshUpdate"] = _forceMeshUpdate,
                    ["linkCount"] = _linkCount, ["linkInfo"] = _linkInfo, ["characterInfo"] = _characterInfo,
                    ["GetLinkID"] = _getLinkId, ["linkTextfirstCharacterIndex"] = _linkFirstChar,
                    ["bottomLeft"] = _bottomLeft, ["topRight"] = _topRight, ["ascender"] = _ascender, ["descender"] = _descender,
                })
                {
                    if (pair.Value == null)
                    {
                        missing.Add(pair.Key);
                    }
                }

                if (missing.Count > 0)
                {
                    throw new MissingMemberException("TextMeshPro members not found: " + string.Join(", ", missing.ToArray()));
                }
            }
            catch (Exception e)
            {
                _broken = true;
                Plugin.Log.LogError($"Sign icons disabled, cannot drive TextMeshPro through reflection: {e.Message}");
            }

            return !_broken;
        }
    }
}
