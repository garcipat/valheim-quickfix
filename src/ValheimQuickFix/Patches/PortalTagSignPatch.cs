using System;
using System.Reflection;
using System.Text.RegularExpressions;
using HarmonyLib;
using UnityEngine;

namespace ValheimQuickFix.Patches
{
    /// <summary>
    /// A portal's tag only shows while hovering it, so players hang a sign inside each portal by hand. Now every tagged
    /// portal shows a sign with its tag in white inside its ring, kept in step with the tag.
    /// </summary>
    /// <remarks>
    /// The sign is a copy of the vanilla <c>sign</c> piece created like the placement ghost (no ZDO), with every game
    /// script and collider removed: it is never saved or sent, can't be hit or hovered, and doesn't block the opening.
    /// It is not a child of the portal, because the hammer highlights every renderer under the piece it aims at; a
    /// <see cref="PortalTagSignLink"/> on the portal owns it and destroys it with the portal. It is placed from the
    /// opening's trigger box, so every portal type gets one. The text is read through <c>TeleportWorld.GetText</c>, the
    /// same filtered tag the hover shows. <c>UpdatePortal</c> runs every 0.5 s.
    /// </remarks>
    [HarmonyPatch(typeof(TeleportWorld), "UpdatePortal")]
    internal static class PortalTagSignPatch
    {
        private const string SignName = "QuickFixPortalTagSign";
        private const string SignPrefab = "sign";
        private const string TextPrefix = "<color=#ffffff>";
        // How far above the opening's centre the sign hangs, as a fraction of the opening's height.
        private const float RaiseFraction = 0.55f;
        // How far the sign is moved out of the opening, in metres, towards the side its text is read from.
        private const float ForwardOffset = 0.2f;
        // Turns the sign so its text faces the portal's front.
        private const float Yaw = 0f;

        // Sign.m_textWidget is a TextMeshProUGUI, which is not in the compile-time libs, so it is reached by name.
        private static readonly FieldInfo WidgetField = AccessTools.Field(typeof(Sign), "m_textWidget");
        private static readonly Regex RichText = new Regex("<[^>]*>");

        private static bool _failed;
        private static bool _warnedNoBox;

        private static void Postfix(TeleportWorld __instance)
        {
            if (_failed)
            {
                return;
            }

            try
            {
                var link = __instance.GetComponent<PortalTagSignLink>();
                var tag = Plugin.PortalTagSignEnabled.Value ? RichText.Replace(__instance.GetText() ?? "", "").Trim() : "";
                if (tag.Length == 0)
                {
                    if (link != null && link.Sign != null)
                    {
                        link.Sign.SetActive(false);
                    }

                    return;
                }

                if (link == null || link.Sign == null)
                {
                    link = Create(__instance);
                    if (link == null)
                    {
                        return;
                    }
                }

                link.Sign.SetActive(true);
                link.SetText(TextPrefix + tag);
            }
            catch (Exception e)
            {
                _failed = true;
                Plugin.Log.LogError($"PortalTagSign disabled after an error: {e}");
            }
        }

        private static PortalTagSignLink Create(TeleportWorld portal)
        {
            var trigger = portal.GetComponentInChildren<TeleportWorldTrigger>(true);
            var box = trigger == null ? null : trigger.GetComponent<BoxCollider>();
            var prefab = ZNetScene.instance == null ? null : ZNetScene.instance.GetPrefab(SignPrefab);
            if (prefab == null || WidgetField == null)
            {
                Plugin.Log.LogWarning($"PortalTagSign disabled: sign prefab {prefab != null}, text field {WidgetField != null}");
                _failed = true;
                return null;
            }

            if (box == null)
            {
                if (!_warnedNoBox)
                {
                    _warnedNoBox = true;
                    Plugin.Log.LogWarning($"PortalTagSign: {portal.name} has no box-shaped opening, so it gets no sign");
                }

                return null;
            }

            var centre = box.center + Vector3.up * (box.size.y * RaiseFraction);
            var position = box.transform.TransformPoint(centre);
            var rotation = portal.transform.rotation * Quaternion.Euler(0f, Yaw, 0f);

            GameObject sign;
            ZNetView.m_forceDisableInit = true;
            try
            {
                sign = UnityEngine.Object.Instantiate(prefab, position, rotation);
            }
            finally
            {
                ZNetView.m_forceDisableInit = false;
            }

            sign.name = SignName;

            // The widget lives on a child, so grab it before the Sign script goes.
            var widget = WidgetField.GetValue(sign.GetComponent<Sign>()) as Component;

            // Removed at once, so nothing that scans for colliders or pieces sees them. Scripts go in reverse order so one
            // that requires another (WearNTear needs Piece) is removed first.
            foreach (var collider in sign.GetComponentsInChildren<Collider>(true))
            {
                UnityEngine.Object.DestroyImmediate(collider);
            }

            var scripts = sign.GetComponentsInChildren<MonoBehaviour>(true);
            for (var i = scripts.Length - 1; i >= 0; i--)
            {
                if (scripts[i] != null && scripts[i].GetType().Assembly == typeof(Sign).Assembly)
                {
                    UnityEngine.Object.DestroyImmediate(scripts[i]);
                }
            }

            var textProp = widget == null ? null : widget.GetType().GetProperty("text", typeof(string));
            if (textProp == null || !textProp.CanWrite)
            {
                UnityEngine.Object.Destroy(sign);
                Plugin.Log.LogWarning("PortalTagSign disabled: the sign prefab has no writable text widget");
                _failed = true;
                return null;
            }

            // A UI canvas is read looking along its forward axis, so its readable face points back along it.
            sign.transform.position -= widget.transform.forward * ForwardOffset;

            var link = portal.GetComponent<PortalTagSignLink>() ??portal.gameObject.AddComponent<PortalTagSignLink>();
            link.Sign = sign;
            link.Widget = widget;
            link.TextProp = textProp;
            return link;
        }
    }

    /// <summary>Ties a portal's tag sign to the portal: holds its text widget and destroys the sign with the portal.</summary>
    internal class PortalTagSignLink : MonoBehaviour
    {
        internal GameObject Sign;
        internal Component Widget;
        internal PropertyInfo TextProp;

        internal void SetText(string text)
        {
            if (Widget != null && !string.Equals(TextProp.GetValue(Widget, null) as string, text, StringComparison.Ordinal))
            {
                TextProp.SetValue(Widget, text, null);
            }
        }

        private void OnDestroy()
        {
            if (Sign != null)
            {
                Destroy(Sign);
            }
        }
    }
}
