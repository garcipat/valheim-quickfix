using System;
using System.Collections.Generic;
using System.Reflection;
using HarmonyLib;
using UnityEngine;

namespace ValheimQuickFix.Patches
{
    /// <summary>
    /// A wrong server password makes the server answer with <c>RPC_Error(ErrorPassword)</c>; <c>Game.FixedUpdate</c>
    /// then sees the error status and logs out to the start scene, which shows "Wrong password" and forgets the
    /// server. Instead, the error is remembered and handled at the end of the same <c>ZNet.Update</c>, before any
    /// <c>FixedUpdate</c> can see it: the server peer is dropped and <c>ClientConnect</c> connects to the same host
    /// again, which sets the status back to Connecting. The server then asks for the password again, and the dialog's
    /// label is switched to the game's own "Wrong password" text so the player knows why it is back.
    /// The vanilla password dialog has no way out, so Escape (or B on a gamepad) while it is open logs out the same
    /// way a failed join does.
    /// <c>m_peers</c>, <c>RPC_Error</c> and <c>Game.Logout</c> differ in visibility or signature between game
    /// versions, and <c>FejdStartup.ServerPassword</c> is newer than the compile-time libs, so they are reached
    /// through reflection.
    /// </summary>
    internal static class RetryWrongPasswordPatch
    {
        private static readonly FieldInfo Peers = AccessTools.Field(typeof(ZNet), "m_peers");
        private static readonly MethodInfo ClientConnect = AccessTools.Method(typeof(ZNet), "ClientConnect");
        private static readonly PropertyInfo CommandLinePassword = AccessTools.Property(typeof(FejdStartup), "ServerPassword");
        // Logout() takes optional parameters in newer game versions, so a compiled call would not bind.
        private static readonly MethodInfo Logout = AccessTools.Method(typeof(Game), "Logout");
        // TextMeshPro is not referenced at compile time.
        private static readonly Type TextType = AccessTools.TypeByName("TMPro.TMP_Text");
        private static readonly Type InputType = AccessTools.TypeByName("TMPro.TMP_InputField");
        private static readonly PropertyInfo TextProperty = AccessTools.Property(TextType, "text");

        private static bool _retryPending;

        [HarmonyPatch(typeof(ZNet), "RPC_Error")]
        internal static class NoticeWrongPassword
        {
            private static bool Prepare()
            {
                if (Peers != null && ClientConnect != null)
                {
                    return true;
                }

                Plugin.Log.LogWarning("RetryWrongPassword disabled: ZNet.m_peers or ZNet.ClientConnect is missing in this game version");
                return false;
            }

            private static void Postfix(ZNet __instance, int error)
            {
                try
                {
                    // A password from the -password launch option would be sent again unchanged.
                    _retryPending = Plugin.RetryWrongPasswordEnabled.Value
                        && error == (int)ZNet.ConnectionStatus.ErrorPassword
                        && !__instance.IsServer()
                        && CommandLinePassword?.GetValue(null) == null;
                }
                catch (Exception e)
                {
                    Plugin.Log.LogError($"RetryWrongPassword patch failed: {e}");
                }
            }
        }

        [HarmonyPatch(typeof(ZNet), "Update")]
        internal static class RetryOrCancel
        {
            private static void Postfix(ZNet __instance)
            {
                try
                {
                    if (_retryPending)
                    {
                        _retryPending = false;
                        Reconnect(__instance);
                        return;
                    }

                    if (Plugin.RetryWrongPasswordEnabled.Value && Logout != null && Game.instance != null
                        && __instance.m_passwordDialog.gameObject.activeSelf
                        && (Input.GetKeyDown(KeyCode.Escape) || ZInput.GetButtonDown("JoyButtonB")))
                    {
                        __instance.m_passwordDialog.gameObject.SetActive(false);
                        var parameters = Logout.GetParameters();
                        var args = new object[parameters.Length];
                        for (var i = 0; i < parameters.Length; i++)
                        {
                            args[i] = parameters[i].DefaultValue;
                        }

                        Logout.Invoke(Game.instance, args);
                    }
                }
                catch (Exception e)
                {
                    Plugin.Log.LogError($"RetryWrongPassword patch failed: {e}");
                }
            }
        }

        private static void Reconnect(ZNet znet)
        {
            var peers = (List<ZNetPeer>)Peers.GetValue(znet);
            foreach (var peer in peers.ToArray())
            {
                if (peer.m_server)
                {
                    znet.Disconnect(peer);
                }
            }

            ShowWrongPassword(znet.m_passwordDialog);
            Plugin.Log.LogInfo("RetryWrongPassword: wrong password, connecting again");
            ClientConnect.Invoke(znet, null);
        }

        // Puts the game's "Wrong password" text on the dialog's first label (not the input field's own texts), so it
        // reads as the reason the dialog came back. The dialog belongs to the game scene, so it resets on the next join.
        private static void ShowWrongPassword(RectTransform dialog)
        {
            if (TextType == null || InputType == null || TextProperty == null)
            {
                return;
            }

            var input = dialog.GetComponentInChildren(InputType, true);
            foreach (var label in dialog.GetComponentsInChildren(TextType, true))
            {
                if (input != null && label.transform.IsChildOf(input.transform))
                {
                    continue;
                }

                TextProperty.SetValue(label, Localization.instance.Localize("$error_password"));
                return;
            }
        }
    }
}
