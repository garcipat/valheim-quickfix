using System;
using System.Reflection;
using HarmonyLib;
using UnityEngine;

namespace ValheimQuickFix.Patches
{
    /// <summary>
    /// A portal's E press only opens the tag dialog, and the opening itself cannot be aimed at. Now a tap on E sends you
    /// through the portal and holding E opens the tag dialog, and the opening reacts to the crosshair like the frame does.
    /// </summary>
    /// <remarks>
    /// The trip is the game's own <c>TeleportWorld.Teleport</c>, so an unlinked portal, the no-portal rules, boss events
    /// and the metal check all behave as when walking in. The dialog is opened by re-entering the game's own
    /// <c>Interact</c>, so the private-area check and the dialog are the vanilla ones. Its input field is read-only until
    /// E comes up, because a dialog opened under a held key would be typed full of that key's auto-repeat. The opening is a trigger volume on a layer the hover ray
    /// ignores, so a copy of it is added on a layer the ray does see.
    /// </remarks>
    internal static class PortalPressOrHoldPatch
    {
        // How long E must stay down before it counts as a hold. The game repeats the interaction about every 0.2 s.
        private const float HoldSeconds = 0.5f;
        private const string HoverHint = " <color=#cccccc>(Hold to set tag)</color>";
        private const string HoverColliderName = "QuickFixPortalHover";
        private const string HoverLayer = "piece_nonsolid";

        private static readonly FieldInfo InteractMask = AccessTools.Field(typeof(Player), "m_interactMask");

        // The dialog's TextMeshPro input field is not in the compile-time libs, so it is reached by name.
        private static readonly FieldInfo InputField = AccessTools.Field(typeof(TextInput), "m_inputField");

        private static bool _loggedMissing;

        // The portal whose E press is being held, when it was pressed, and whether the hold already opened the dialog, and the frame E was last seen up while the field is locked.
        private static TeleportWorld _portal;
        private static float _pressedAt;
        private static bool _opened;
        private static bool _locked;
        private static int _unlockFrame;
        private static bool _reentering;

        // The field is a LayerMask in some game versions and a plain int in others.
        private static int ReadInteractMask(Player player)
        {
            var value = InteractMask.GetValue(player);
            return value is LayerMask mask ? mask.value : Convert.ToInt32(value);
        }

        private static bool Available()
        {
            if (InteractMask != null && InputField != null)
            {
                return true;
            }

            if (!_loggedMissing)
            {
                _loggedMissing = true;
                Plugin.Log.LogError("PortalPressOrHold disabled: Player.m_interactMask or TextInput.m_inputField not found in this game version");
            }

            return false;
        }

        [HarmonyPatch(typeof(TeleportWorld), nameof(TeleportWorld.Interact))]
        private static class InteractPatch
        {
            // Returning false skips the game's own Interact, which would open the tag dialog on the first frame.
            private static bool Prefix(TeleportWorld __instance, Humanoid human, bool hold)
            {
                try
                {
                    var player = Player.m_localPlayer;
                    if (_reentering || !Plugin.PortalPressOrHoldEnabled.Value || player == null || human != player || !Available())
                    {
                        return true;
                    }

                    if (!hold)
                    {
                        _portal = __instance;
                        _pressedAt = Time.time;
                        _opened = false;
                        return false;
                    }

                    if (_portal == __instance && !_opened && Time.time - _pressedAt >= HoldSeconds)
                    {
                        _opened = true;
                        Reenter(__instance, player);
                        LockInput();
                    }

                    return false;
                }
                catch (Exception e)
                {
                    Plugin.Log.LogError($"PortalPressOrHold failed: {e}");
                    return true;
                }
            }
        }

        private static bool EDown()
        {
            return ZInput.GetButton("Use") || ZInput.GetButton("JoyUse") || Input.GetKey(KeyCode.E);
        }

        private static void SetReadOnly(bool value)
        {
            var field = TextInput.instance == null ? null : InputField.GetValue(TextInput.instance);
            field?.GetType().GetProperty("readOnly")?.SetValue(field, value, null);
        }

        private static void LockInput()
        {
            if (TextInput.instance == null || !TextInput.instance.m_panel.activeSelf)
            {
                return;
            }

            SetReadOnly(true);
            _locked = true;
            _unlockFrame = -1;
        }

        private static void Reenter(TeleportWorld portal, Player player)
        {
            _reentering = true;
            try
            {
                portal.Interact(player, false, false);
            }
            finally
            {
                _reentering = false;
            }
        }

        // Once E comes up, a tap sends you through and a hold unlocks the dialog's input field. The unlock waits a frame so
        // the last auto-repeated character of the release is not typed.
        [HarmonyPatch(typeof(Player), "Update")]
        private static class ReleasePatch
        {
            private static void Postfix(Player __instance)
            {
                if (__instance != Player.m_localPlayer || EDown())
                {
                    return;
                }

                try
                {
                    if (_locked)
                    {
                        if (_unlockFrame < 0)
                        {
                            _unlockFrame = Time.frameCount;
                        }
                        else if (Time.frameCount > _unlockFrame)
                        {
                            _locked = false;
                            SetReadOnly(false);
                        }
                    }

                    if (_portal == null)
                    {
                        return;
                    }

                    var portal = _portal;
                    var opened = _opened;
                    _portal = null;
                    if (!opened)
                    {
                        portal.Teleport(__instance);
                    }
                }
                catch (Exception e)
                {
                    Plugin.Log.LogError($"PortalPressOrHold failed: {e}");
                }
            }
        }

        [HarmonyPatch(typeof(TeleportWorld), nameof(TeleportWorld.GetHoverText))]
        private static class HoverTextPatch
        {
            private static void Postfix(ref string __result)
            {
                try
                {
                    if (!Plugin.PortalPressOrHoldEnabled.Value || Player.m_localPlayer == null)
                    {
                        return;
                    }

                    var setTag = Localization.instance.Localize("$piece_portal_settag");
                    var index = string.IsNullOrEmpty(setTag) ? -1 : __result.LastIndexOf(setTag, StringComparison.Ordinal);
                    __result = index < 0
                        ? __result + HoverHint
                        : __result.Substring(0, index) + "Go through" + HoverHint + __result.Substring(index + setTag.Length);
                }
                catch (Exception e)
                {
                    Plugin.Log.LogError($"PortalPressOrHold hover text failed: {e}");
                }
            }
        }

        // The opening is a trigger that only reports walkers, on a layer the hover ray skips. Put a script-less copy of it
        // on a layer the ray hits and the player does not collide with, so aiming at the opening hovers the portal.
        [HarmonyPatch(typeof(TeleportWorld), "Awake")]
        private static class HoverColliderPatch
        {
            private static void Postfix(TeleportWorld __instance)
            {
                try
                {
                    if (!Plugin.PortalPressOrHoldEnabled.Value)
                    {
                        return;
                    }

                    var trigger = __instance.GetComponentInChildren<TeleportWorldTrigger>(true);
                    if (trigger == null || trigger.transform.Find(HoverColliderName) != null)
                    {
                        return;
                    }

                    var source = trigger.GetComponent<BoxCollider>();
                    var layer = LayerMask.NameToLayer(HoverLayer);
                    if (source == null || layer < 0)
                    {
                        Plugin.Log.LogWarning($"PortalPressOrHold: portal opening not hoverable (collider {trigger.GetComponent<Collider>()?.GetType().Name ?? "none"}, layer '{HoverLayer}' = {layer})");
                        return;
                    }

                    var child = new GameObject(HoverColliderName) { layer = layer };
                    child.transform.SetParent(trigger.transform, false);
                    var copy = child.AddComponent<BoxCollider>();
                    copy.isTrigger = true;
                    copy.center = source.center;
                    copy.size = source.size;

                    var player = Player.m_localPlayer;
                    var inMask = player != null && Available() ? (ReadInteractMask(player) & (1 << layer)) != 0 : (bool?)null;
                    Plugin.Log.LogInfo($"PortalPressOrHold: added hover collider to portal opening (trigger layer {LayerMask.LayerToName(trigger.gameObject.layer)}, size {source.size}, '{HoverLayer}' in interact mask: {(inMask.HasValue ? inMask.ToString() : "unknown")})");
                }
                catch (Exception e)
                {
                    Plugin.Log.LogError($"PortalPressOrHold hover collider failed: {e}");
                }
            }
        }
    }
}
