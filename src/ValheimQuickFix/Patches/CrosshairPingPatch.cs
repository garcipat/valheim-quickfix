using System;
using System.Collections;
using System.Reflection;
using HarmonyLib;
using UnityEngine;

namespace ValheimQuickFix.Patches
{
    /// <summary>
    /// The game can only ping from the map: middle-click there drops a ping everyone in range sees in the world and on
    /// their map. Now a key pings the spot under the crosshair, without opening the map.
    /// </summary>
    /// <remarks>
    /// The ping is the game's own "ChatMessage" RPC with the ping type, sent exactly as <c>Chat.SendPing</c> sends it,
    /// so other players need no mod and see a normal ping. The one difference is that the hit point keeps its height,
    /// where <c>SendPing</c> moves it to the player's height, so the label sits on what was aimed at. The game hides a
    /// ping farther than <c>Minimap.m_nomapPingDistance</c> (50 m) while the map is closed; that limit is lifted for
    /// your own pings so a far ping still shows up for you.
    ///
    /// The key is a button of the game's own input system, so it is saved and rebound like any other control, and a
    /// row for it is added to the Controls page of the settings, copied from the row above it.
    /// </remarks>
    internal static class CrosshairPingPatch
    {
        private const float MaxDistance = 500f;
        private const int PingType = 3; // Talker.Type.Ping
        private const string ButtonName = "CrosshairPing";
        // Bindings are physical key positions; H is unbound and in the same place on QWERTY, QWERTZ and AZERTY.
        private const string DefaultPath = "<Keyboard>/h";
        private const string RowLabel = "Ping";
        private const string InsertAfter = "AutoPickup";

        // These are newer than the compile-time libs, so they are reached by name.
        private static readonly MethodInfo TakeInput = AccessTools.Method(typeof(Player), "TakeInput");
        private static readonly MethodInfo GetLocalUser = AccessTools.Method(AccessTools.TypeByName("UserInfo"), "GetLocalUser");
        private static readonly PropertyInfo LocalCharacterId = AccessTools.Property(typeof(ZNet), "LocalPlayerCharacterID");
        private static readonly PropertyInfo CharacterUserId = AccessTools.Property(typeof(ZDOID), "UserID");
        private static readonly FieldInfo NomapPingDistance = AccessTools.Field(typeof(Minimap), "m_nomapPingDistance");

        private static readonly MethodInfo AddButton = AccessTools.Method(typeof(ZInput), "AddButton",
            new[] { typeof(string), typeof(string), typeof(bool), typeof(bool), typeof(bool), typeof(float), typeof(float) });
        private static readonly FieldInfo Buttons = AccessTools.Field(typeof(ZInput), "m_buttons");
        private static readonly MethodInfo LoadInput = AccessTools.Method(typeof(ZInput), "Load");

        private static readonly Type ControlsTab = AccessTools.TypeByName("Valheim.SettingsGui.KeyboardMouseSettings");
        private static readonly Type KeySetting = AccessTools.TypeByName("Valheim.SettingsGui.KeySetting");
        private static readonly FieldInfo Keys = AccessTools.Field(ControlsTab, "m_keys");
        private static readonly FieldInfo KeyName = AccessTools.Field(KeySetting, "m_keyName");
        private static readonly FieldInfo KeyTransform = AccessTools.Field(KeySetting, "m_keyTransform");
        private static readonly FieldInfo BlockedButtons = AccessTools.Field(KeySetting, "m_blockedButtons");
        private static readonly Type TextType = AccessTools.TypeByName("TMPro.TMP_Text");
        private static readonly PropertyInfo TextProperty = AccessTools.Property(TextType, "text");
        private static readonly Type ButtonType = AccessTools.TypeByName("UnityEngine.UI.Button");
        private static readonly Type GridType = AccessTools.TypeByName("UnityEngine.UI.GridLayoutGroup");
        private static readonly PropertyInfo CellSize = AccessTools.Property(GridType, "cellSize");
        private static readonly PropertyInfo CellSpacing = AccessTools.Property(GridType, "spacing");
        private static readonly Type LocalizeType = AccessTools.TypeByName("Localize");

        private static readonly int RayMask = LayerMask.GetMask(
            "Default", "static_solid", "Default_small", "piece", "piece_nonsolid", "terrain", "vehicle",
            "character", "character_net", "character_noenv", "item");

        private static readonly RaycastHit[] Hits = new RaycastHit[32];

        private static bool _loggedMissing;
        private static bool _registered;

        private static bool Available()
        {
            if (TakeInput != null && GetLocalUser != null && LocalCharacterId != null && CharacterUserId != null && NomapPingDistance != null)
            {
                return true;
            }

            if (!_loggedMissing)
            {
                _loggedMissing = true;
                Plugin.Log.LogError("CrosshairPing disabled: Player.TakeInput, UserInfo.GetLocalUser, ZNet.LocalPlayerCharacterID or Minimap.m_nomapPingDistance not found in this game version");
            }

            return false;
        }

        private static bool IsRegistered(ZInput input)
        {
            return Buttons.GetValue(input) is IDictionary buttons && buttons.Contains(ButtonName);
        }

        // The game may have built its buttons before the patches were applied; reloading rebuilds them with ours and
        // applies its saved binding.
        internal static void RegisterWithRunningInput()
        {
            try
            {
                if (AddButton != null && Buttons != null && LoadInput != null && ZInput.instance != null && !IsRegistered(ZInput.instance))
                {
                    LoadInput.Invoke(ZInput.instance, null);
                }
            }
            catch (Exception e)
            {
                Plugin.Log.LogError($"CrosshairPing: could not add the ping key to the game's controls: {e}");
            }
        }

        // The rows sit in a fixed-size grid filled column by column, two columns wide, and every slot is taken: a new
        // row would start a third column outside the visible area. Growing the grid by one row gives each column one
        // more slot, and the new row goes into the first column, after AutoPickup, so the second column keeps its rows.
        private static void PlaceRow(RectTransform row, IList keys)
        {
            var grid = row.parent as RectTransform;
            var layout = grid != null && GridType != null ? grid.GetComponent(GridType) : null;
            if (layout == null || CellSize == null || CellSpacing == null)
            {
                Plugin.Log.LogWarning("CrosshairPing: the Controls rows are no longer in a grid, so the Ping row may be out of view");
                return;
            }

            var cell = (Vector2)CellSize.GetValue(layout, null);
            var spacing = (Vector2)CellSpacing.GetValue(layout, null);
            var perColumn = Mathf.FloorToInt((grid.rect.height + spacing.y) / (cell.y + spacing.y));
            var columns = Mathf.Max(1, Mathf.FloorToInt((grid.rect.width + spacing.x) / (cell.x + spacing.x)));
            var shown = 0;
            for (var i = 0; i < grid.childCount; i++)
            {
                shown += grid.GetChild(i).gameObject.activeSelf ? 1 : 0;
            }

            if (shown > perColumn * columns)
            {
                grid.sizeDelta += new Vector2(0f, cell.y + spacing.y);
                perColumn++;
            }

            var index = perColumn - 1;
            foreach (var key in keys)
            {
                if ((string)KeyName.GetValue(key) == InsertAfter)
                {
                    index = Mathf.Min(((RectTransform)KeyTransform.GetValue(key)).GetSiblingIndex() + 1, perColumn - 1);
                }
            }

            row.SetSiblingIndex(index);
        }

        private static void AddControlsRow(object tab)
        {
            var keys = (IList)Keys.GetValue(tab);
            if (keys == null || keys.Count == 0)
            {
                return;
            }

            foreach (var key in keys)
            {
                if ((string)KeyName.GetValue(key) == ButtonName)
                {
                    return;
                }
            }

            // The row goes under AutoPickup, so that row is the template: rows in the second column are mirrored (key
            // left, name right). Otherwise the last visible row; the last entries (camera zoom) are hidden rows.
            object template = null;
            for (var i = keys.Count - 1; i >= 0; i--)
            {
                var candidate = (RectTransform)KeyTransform.GetValue(keys[i]);
                if (candidate == null || !candidate.gameObject.activeSelf)
                {
                    continue;
                }

                if (template == null || (string)KeyName.GetValue(keys[i]) == InsertAfter)
                {
                    template = keys[i];
                }
            }

            if (template == null)
            {
                return;
            }

            var templateRow = (RectTransform)KeyTransform.GetValue(template);
            var row = (RectTransform)UnityEngine.Object.Instantiate(templateRow.gameObject, templateRow.parent).transform;
            row.name = "CrosshairPingKey";
            row.gameObject.SetActive(true);
            PlaceRow(row, keys);

            // The action name is the text that is not on the key button; the button's text is filled in by the tab.
            foreach (var label in row.GetComponentsInChildren(TextType, true))
            {
                if (((Component)label).GetComponentInParent(ButtonType) != null)
                {
                    continue;
                }

                if (LocalizeType != null && ((Component)label).GetComponent(LocalizeType) is Component localize)
                {
                    UnityEngine.Object.DestroyImmediate(localize);
                }

                TextProperty.SetValue(label, RowLabel);
            }

            var entry = Activator.CreateInstance(KeySetting);
            KeyName.SetValue(entry, ButtonName);
            KeyTransform.SetValue(entry, row);
            BlockedButtons.SetValue(entry, BlockedButtons.GetValue(template) ?? new KeyCode[0]);
            keys.Add(entry);
        }

        // The nearest hit along the camera's forward ray that is not the player.
        private static bool FindTarget(Player player, out Vector3 point)
        {
            point = Vector3.zero;
            var camera = GameCamera.instance.transform;
            var count = Physics.RaycastNonAlloc(camera.position, camera.forward, Hits, MaxDistance, RayMask);
            var best = float.MaxValue;
            for (var i = 0; i < count; i++)
            {
                var hit = Hits[i];
                var body = hit.collider.attachedRigidbody;
                if (hit.distance >= best || (body != null && body.gameObject == player.gameObject))
                {
                    continue;
                }

                best = hit.distance;
                point = hit.point;
            }

            return best < float.MaxValue;
        }

        [HarmonyPatch(typeof(ZInput), "ResetKBMButtons")]
        private static class RegisterButtonPatch
        {
            private static bool Prepare()
            {
                if (AddButton != null && Buttons != null)
                {
                    return true;
                }

                Plugin.Log.LogWarning("CrosshairPing disabled: ZInput.AddButton or ZInput.m_buttons is missing in this game version");
                return false;
            }

            // Runs whenever the game (re)builds its keyboard buttons, so the ping key is there after every reload.
            private static void Postfix(ZInput __instance)
            {
                try
                {
                    if (!IsRegistered(__instance))
                    {
                        AddButton.Invoke(__instance, new object[] { ButtonName, DefaultPath, false, true, true, 0f, 0f });
                    }

                    _registered = IsRegistered(__instance);
                }
                catch (Exception e)
                {
                    Plugin.Log.LogError($"CrosshairPing: could not add the ping key to the game's controls: {e}");
                }
            }
        }

        [HarmonyPatch]
        private static class ControlsRowPatch
        {
            private static MethodBase TargetMethod()
            {
                return AccessTools.Method(ControlsTab, "Initialize");
            }

            private static bool Prepare()
            {
                if (TargetMethod() != null && Keys != null && KeyName != null && KeyTransform != null
                    && BlockedButtons != null && TextProperty != null && ButtonType != null)
                {
                    return true;
                }

                Plugin.Log.LogWarning("CrosshairPing: the Controls settings page changed in this game version, so the ping key has no row there");
                return false;
            }

            // Before Initialize, so the game wires up the new row's button and fills in its key like the others.
            private static void Prefix(object __instance)
            {
                try
                {
                    AddControlsRow(__instance);
                }
                catch (Exception e)
                {
                    Plugin.Log.LogError($"CrosshairPing: could not add the ping key to the Controls page: {e}");
                }
            }
        }

        [HarmonyPatch(typeof(Player), "Update")]
        private static class UpdatePatch
        {
            private static void Postfix(Player __instance)
            {
                try
                {
                    if (!_registered || __instance != Player.m_localPlayer || !ZInput.GetButtonDown(ButtonName)
                        || GameCamera.instance == null || ZRoutedRpc.instance == null || !Available()
                        || !(bool)TakeInput.Invoke(__instance, null))
                    {
                        return;
                    }

                    if (FindTarget(__instance, out var point))
                    {
                        ZRoutedRpc.instance.InvokeRoutedRPC(0L, "ChatMessage", point, PingType, GetLocalUser.Invoke(null, null), "");
                    }
                }
                catch (Exception e)
                {
                    Plugin.Log.LogError($"CrosshairPing patch failed: {e}");
                }
            }
        }

        [HarmonyPatch(typeof(Chat), nameof(Chat.OnNewChatMessage))]
        private static class ShowOwnPingPatch
        {
            // The local player's own message is handled synchronously, so the limit is back before anything else runs.
            private static void Prefix(long senderID, Talker.Type type, ref float __state)
            {
                __state = -1f;
                if (type != Talker.Type.Ping || Minimap.instance == null
                    || ZNet.instance == null || !Available()
                    || senderID != (long)CharacterUserId.GetValue(LocalCharacterId.GetValue(ZNet.instance)))
                {
                    return;
                }

                __state = (float)NomapPingDistance.GetValue(Minimap.instance);
                NomapPingDistance.SetValue(Minimap.instance, float.MaxValue);
            }

            private static void Finalizer(float __state)
            {
                if (__state >= 0f && Minimap.instance != null)
                {
                    NomapPingDistance.SetValue(Minimap.instance, __state);
                }
            }
        }
    }
}
