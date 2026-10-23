using System;
using System.Collections.Generic;
using HarmonyLib;
using UnityEngine;

namespace ValheimQuickFix.Patches
{
    /// <summary>
    /// A player walking into a portal keeps standing in its opening for about two seconds before the game moves them, and
    /// on everyone else's screen that avatar is solid, so the next person cannot get in. While another player is inside
    /// a portal's opening, their avatar is made non-solid and invisible on this client only.
    /// </summary>
    /// <remarks>
    /// The avatar's collider is moved to the game's own <c>character_ghost</c> layer, which the game leaves alone
    /// (<c>Character.UpdateLayer</c> only rewrites <c>character</c> and <c>character_net</c>), and its renderers are
    /// switched off. Nothing is sent over the network, so it only helps the player running it. A component beside the
    /// portal's <c>TeleportWorldTrigger</c> compares remote players' positions with the opening on a short timer, rather
    /// than waiting for <c>OnTriggerEnter</c>/<c>Exit</c>, which depend on the physics layer matrix and fire oddly once a
    /// collider changes layer. While someone is ghosted the layer and renderers are re-applied every frame.
    /// </remarks>
    internal static class GhostPortalPlayersPatch
    {
        private const string GhostLayerName = "character_ghost";

        // How far outside the opening a player's centre may be and still count as in it; also stops flicker at the edge.
        private const float InsideMarginSqr = 0.25f;

        private static int _ghostLayer = -2;

        private static int GhostLayer
        {
            get
            {
                if (_ghostLayer == -2)
                {
                    _ghostLayer = LayerMask.NameToLayer(GhostLayerName);
                    if (_ghostLayer < 0)
                    {
                        Plugin.Log.LogWarning($"GhostPortalPlayers: layer '{GhostLayerName}' not found, players will only be hidden");
                    }
                }

                return _ghostLayer;
            }
        }

        [HarmonyPatch(typeof(TeleportWorld), "Awake")]
        private static class AwakePatch
        {
            private static void Postfix(TeleportWorld __instance)
            {
                try
                {
                    var trigger = __instance.GetComponentInChildren<TeleportWorldTrigger>(true);
                    if (trigger == null || trigger.GetComponent<GhostZone>() != null)
                    {
                        return;
                    }

                    trigger.gameObject.AddComponent<GhostZone>();
                }
                catch (Exception e)
                {
                    Plugin.Log.LogError($"GhostPortalPlayers failed: {e}");
                }
            }
        }

        private sealed class Ghosted
        {
            public Player Player;
            public GameObject ColliderObject;
            public int Layer;
            public readonly HashSet<Renderer> Hidden = new HashSet<Renderer>();
        }

        // Lives on the portal's trigger object next to the game's own TeleportWorldTrigger.
        private sealed class GhostZone : MonoBehaviour
        {
            private const float ScanInterval = 0.1f;
            private const float DiagnosticInterval = 2f;
            private const float DiagnosticRange = 4f;

            private static readonly HashSet<Player> AllGhosted = new HashSet<Player>();

            private readonly List<Ghosted> _ghosted = new List<Ghosted>();
            private Collider _zone;
            private float _nextScan;
            private float _nextDiagnostic;

            private void Awake()
            {
                _zone = GetComponent<Collider>();
                Plugin.Log.LogInfo($"GhostPortalPlayers: zone on '{transform.root.name}' (collider {(_zone != null ? _zone.GetType().Name : "none")}, layer {LayerMask.LayerToName(gameObject.layer)})");
            }

            // Positions are checked on a timer instead of waiting for OnTriggerEnter, which depends on the physics layer
            // matrix letting a remote player's layer touch the opening's.
            private void Update()
            {
                try
                {
                    var enabled = Plugin.GhostPortalPlayersEnabled.Value;

                    for (var i = _ghosted.Count - 1; i >= 0; i--)
                    {
                        var ghosted = _ghosted[i];
                        if (!enabled || ghosted.Player == null || !IsInside(ghosted.Player))
                        {
                            Restore(ghosted);
                            _ghosted.RemoveAt(i);
                        }
                        else
                        {
                            Apply(ghosted);
                        }
                    }

                    if (!enabled || _zone == null || Time.time < _nextScan)
                    {
                        return;
                    }

                    _nextScan = Time.time + ScanInterval;
                    var diagnose = Time.time >= _nextDiagnostic;
                    foreach (var player in Player.GetAllPlayers())
                    {
                        if (player == null || player == Player.m_localPlayer || AllGhosted.Contains(player))
                        {
                            continue;
                        }

                        if (IsInside(player))
                        {
                            Ghost(player);
                        }
                        else if (diagnose)
                        {
                            var distance = Vector3.Distance(_zone.ClosestPoint(Center(player)), Center(player));
                            if (distance < DiagnosticRange)
                            {
                                _nextDiagnostic = Time.time + DiagnosticInterval;
                                Plugin.Log.LogInfo($"GhostPortalPlayers: '{player.GetPlayerName()}' is {distance:F2} m from the opening of '{transform.root.name}' (not inside)");
                            }
                        }
                    }
                }
                catch (Exception e)
                {
                    Plugin.Log.LogError($"GhostPortalPlayers failed: {e}");
                }
            }

            private void OnDisable()
            {
                RestoreAll();
            }

            private void OnDestroy()
            {
                RestoreAll();
            }

            private static Vector3 Center(Player player)
            {
                var collider = player.GetComponent<CapsuleCollider>();
                return collider != null ? collider.bounds.center : player.transform.position + Vector3.up;
            }

            private bool IsInside(Player player)
            {
                if (_zone == null)
                {
                    return false;
                }

                var center = Center(player);
                return (_zone.ClosestPoint(center) - center).sqrMagnitude <= InsideMarginSqr;
            }

            private void Ghost(Player player)
            {
                var collider = player.GetComponent<CapsuleCollider>();
                var ghosted = new Ghosted { Player = player };

                if (collider != null && GhostLayer >= 0)
                {
                    ghosted.ColliderObject = collider.gameObject;
                    ghosted.Layer = ghosted.ColliderObject.layer;
                }

                Apply(ghosted);
                _ghosted.Add(ghosted);
                AllGhosted.Add(player);
                Plugin.Log.LogInfo($"GhostPortalPlayers: ghosted '{player.GetPlayerName()}' at '{transform.root.name}' (layer {LayerMask.LayerToName(ghosted.Layer)}, {ghosted.Hidden.Count} renderers)");
            }

            // Run every frame while ghosted, since the game can switch renderers back on (LOD groups) or equip new gear.
            private static void Apply(Ghosted ghosted)
            {
                if (ghosted.ColliderObject != null && ghosted.ColliderObject.layer != GhostLayer)
                {
                    ghosted.ColliderObject.layer = GhostLayer;
                }

                foreach (var renderer in ghosted.Player.GetComponentsInChildren<Renderer>())
                {
                    if (renderer.enabled)
                    {
                        renderer.enabled = false;
                        ghosted.Hidden.Add(renderer);
                    }
                }
            }

            private void RestoreAll()
            {
                foreach (var ghosted in _ghosted)
                {
                    Restore(ghosted);
                }

                _ghosted.Clear();
            }

            private static void Restore(Ghosted ghosted)
            {
                AllGhosted.Remove(ghosted.Player);

                if (ghosted.ColliderObject != null)
                {
                    ghosted.ColliderObject.layer = ghosted.Layer;
                }

                foreach (var renderer in ghosted.Hidden)
                {
                    if (renderer != null)
                    {
                        renderer.enabled = true;
                    }
                }
            }
        }
    }
}
