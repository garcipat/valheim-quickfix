using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;
using HarmonyLib;
using UnityEngine;

namespace ValheimMapRoutes
{
    /// <summary>
    /// The local player's routes for the current world, kept in <c>Player.m_customData</c> so they are saved with the
    /// character and never leave this machine.
    /// </summary>
    internal static class RouteStore
    {
        private const string KeyPrefix = "dev.pgarcia.maproutes.Routes.";

        // The compile-time libs predate Player.m_customData.
        private static readonly AccessTools.FieldRef<Player, Dictionary<string, string>> CustomDataRef =
            AccessTools.FieldRefAccess<Player, Dictionary<string, string>>("m_customData");

        private static Player _loadedFor;
        private static string _loadedKey;

        /// <summary>Each route is a list of world points (y unused).</summary>
        internal static readonly List<List<Vector3>> Routes = new List<List<Vector3>>();

        /// <summary>
        /// Reloads when the player or world changed. Returns false when there is no local player or world yet.
        /// </summary>
        internal static bool Refresh()
        {
            var player = Player.m_localPlayer;
            var key = Key();
            if (player == null || key == null)
            {
                return false;
            }
            if (player == _loadedFor && key == _loadedKey)
            {
                return true;
            }
            _loadedFor = player;
            _loadedKey = key;
            Routes.Clear();
            try
            {
                if (CustomDataRef(player).TryGetValue(key, out var data) && !string.IsNullOrEmpty(data))
                {
                    Parse(data);
                }
            }
            catch (Exception e)
            {
                Plugin.Log.LogError($"Loading routes failed: {e}");
            }
            return true;
        }

        internal static void Save()
        {
            var player = Player.m_localPlayer;
            if (player == null || player != _loadedFor || _loadedKey == null)
            {
                return;
            }
            try
            {
                var customData = CustomDataRef(player);
                var data = Format();
                if (data.Length == 0)
                {
                    customData.Remove(_loadedKey);
                }
                else
                {
                    customData[_loadedKey] = data;
                }
            }
            catch (Exception e)
            {
                Plugin.Log.LogError($"Saving routes failed: {e}");
            }
        }

        private static string Key()
        {
            return ZNet.instance == null ? null : KeyPrefix + ZNet.instance.GetWorldUID().ToString(CultureInfo.InvariantCulture);
        }

        // One route per line, points as x,z separated by ';'.
        private static void Parse(string data)
        {
            foreach (var line in data.Split('\n'))
            {
                var route = new List<Vector3>();
                foreach (var point in line.Split(';'))
                {
                    var parts = point.Split(',');
                    if (parts.Length == 2
                        && float.TryParse(parts[0], NumberStyles.Float, CultureInfo.InvariantCulture, out var x)
                        && float.TryParse(parts[1], NumberStyles.Float, CultureInfo.InvariantCulture, out var z))
                    {
                        route.Add(new Vector3(x, 0f, z));
                    }
                }
                if (route.Count >= 2)
                {
                    Routes.Add(route);
                }
            }
        }

        private static string Format()
        {
            var sb = new StringBuilder();
            foreach (var route in Routes)
            {
                if (route.Count < 2)
                {
                    continue;
                }
                if (sb.Length > 0)
                {
                    sb.Append('\n');
                }
                for (var i = 0; i < route.Count; i++)
                {
                    if (i > 0)
                    {
                        sb.Append(';');
                    }
                    sb.Append(route[i].x.ToString("0.#", CultureInfo.InvariantCulture)).Append(',')
                        .Append(route[i].z.ToString("0.#", CultureInfo.InvariantCulture));
                }
            }
            return sb.ToString();
        }
    }
}
