using System;
using System.Collections.Generic;
using System.Globalization;
using System.Reflection;
using System.Text;
using HarmonyLib;
using UnityEngine;

namespace ValheimQuickFix.Patches
{
    /// <summary>
    /// Remembers the pins you delete from the map, so reading a map table does not put them back.
    /// <c>Minimap.AddSharedMapData</c> adds every table pin you have no pin within 1 m of, so a pin another player put on
    /// the table comes back each time you read it after you deleted it.
    /// </summary>
    /// <remarks>
    /// Only deletions you make on the map count: right-click (or long touch, or the gamepad button) all go through
    /// <c>RemovePin(Vector3, float)</c>, which removes the pin <c>GetClosestPin</c> finds. The prefix looks up that same
    /// pin rather than patching <c>RemovePin(PinData)</c>, which is small enough to be inlined into its caller. Other
    /// code (boss stones, event and player markers) calls <c>RemovePin(PinData)</c> directly and is not recorded. The list
    /// is kept per world in the character's <c>Player.m_customData</c>, so it is saved with the character.
    /// </remarks>
    [HarmonyPatch(typeof(Minimap), nameof(Minimap.RemovePin), typeof(Vector3), typeof(float))]
    internal static class MinimapRemovePinAtPatch
    {
        private static void Prefix(Minimap __instance, Vector3 pos, float radius)
        {
            ForgetDeletedPins.Remember(__instance, pos, radius);
        }
    }

    [HarmonyPatch(typeof(Minimap), nameof(Minimap.AddSharedMapData))]
    internal static class MinimapAddSharedMapDataPatch
    {
        private static void Prefix(Minimap __instance)
        {
            ForgetDeletedPins.ForgetRestored(__instance);
        }

        private static void Postfix(Minimap __instance)
        {
            ForgetDeletedPins.RemoveForgotten(__instance);
        }
    }

    internal static class ForgetDeletedPins
    {
        private const string KeyPrefix = "dev.pgarcia.quickfix.DeletedPins.";

        // Same distance the game uses to treat two pins as the same one.
        private const float SameSpot = 1f;

        // The compile-time libs predate Player.m_customData; Minimap.m_pins is private.
        private static readonly AccessTools.FieldRef<Player, Dictionary<string, string>> CustomDataRef =
            AccessTools.FieldRefAccess<Player, Dictionary<string, string>>("m_customData");

        private static readonly AccessTools.FieldRef<Minimap, List<Minimap.PinData>> PinsRef =
            AccessTools.FieldRefAccess<Minimap, List<Minimap.PinData>>("m_pins");

        // Private, and it gained a mustBeVisible parameter after the compile-time libs; filled in by parameter count.
        private static readonly MethodInfo GetClosestPinMethod = AccessTools.Method(typeof(Minimap), "GetClosestPin");

        private struct Entry
        {
            internal float X;
            internal float Z;
            internal int Type;
            internal string Name;

            internal bool Matches(Minimap.PinData pin)
            {
                var dx = pin.m_pos.x - X;
                var dz = pin.m_pos.z - Z;
                return (int)pin.m_type == Type && pin.m_name == Name && dx * dx + dz * dz < SameSpot * SameSpot;
            }
        }

        internal static void Remember(Minimap minimap, Vector3 pos, float radius)
        {
            if (!Plugin.ForgetDeletedPinsEnabled.Value || GetClosestPinMethod == null)
            {
                return;
            }
            try
            {
                var args = GetClosestPinMethod.GetParameters().Length == 3
                    ? new object[] { pos, radius, true }
                    : new object[] { pos, radius };
                var pin = GetClosestPinMethod.Invoke(minimap, args) as Minimap.PinData;
                if (pin == null || !pin.m_save || pin.m_type == Minimap.PinType.Death)
                {
                    return;
                }
                var entries = Load();
                if (entries == null || entries.Exists(e => e.Matches(pin)))
                {
                    return;
                }
                entries.Add(new Entry { X = pin.m_pos.x, Z = pin.m_pos.z, Type = (int)pin.m_type, Name = pin.m_name ?? "" });
                Save(entries);
            }
            catch (Exception e)
            {
                Plugin.Log.LogError($"ForgetDeletedPins failed: {e}");
            }
        }

        /// <summary>
        /// Drops entries for pins that are on the map again, for example after reading a table with the fix off.
        /// Having the pin back means you want it, so deleting it again is what forgets it next time.
        /// </summary>
        internal static void ForgetRestored(Minimap minimap)
        {
            if (!Plugin.ForgetDeletedPinsEnabled.Value)
            {
                return;
            }
            try
            {
                var entries = Load();
                if (entries == null || entries.Count == 0)
                {
                    return;
                }
                var pins = PinsRef(minimap);
                if (entries.RemoveAll(e => pins.Exists(p => p.m_save && e.Matches(p))) > 0)
                {
                    Save(entries);
                }
            }
            catch (Exception e)
            {
                Plugin.Log.LogError($"ForgetDeletedPins failed: {e}");
            }
        }

        /// <summary>
        /// Removes the table pins the game just added back. The game always adds them with the other player's ID as
        /// owner, so pins of your own (owner 0) are never touched.
        /// </summary>
        internal static void RemoveForgotten(Minimap minimap)
        {
            if (!Plugin.ForgetDeletedPinsEnabled.Value)
            {
                return;
            }
            try
            {
                var entries = Load();
                if (entries == null || entries.Count == 0)
                {
                    return;
                }
                var pins = PinsRef(minimap);
                for (var i = pins.Count - 1; i >= 0; i--)
                {
                    var pin = pins[i];
                    if (pin.m_ownerID != 0L && entries.Exists(e => e.Matches(pin)))
                    {
                        minimap.RemovePin(pin);
                    }
                }
            }
            catch (Exception e)
            {
                Plugin.Log.LogError($"ForgetDeletedPins failed: {e}");
            }
        }

        private static string Key()
        {
            return ZNet.instance == null ? null : KeyPrefix + ZNet.instance.GetWorldUID().ToString(CultureInfo.InvariantCulture);
        }

        // One pin per line: x|z|type|name. The name goes last, so a '|' in it survives.
        private static List<Entry> Load()
        {
            var player = Player.m_localPlayer;
            var key = Key();
            if (player == null || key == null)
            {
                return null;
            }
            var entries = new List<Entry>();
            if (!CustomDataRef(player).TryGetValue(key, out var data) || string.IsNullOrEmpty(data))
            {
                return entries;
            }
            foreach (var line in data.Split('\n'))
            {
                var parts = line.Split(new[] { '|' }, 4);
                if (parts.Length == 4
                    && float.TryParse(parts[0], NumberStyles.Float, CultureInfo.InvariantCulture, out var x)
                    && float.TryParse(parts[1], NumberStyles.Float, CultureInfo.InvariantCulture, out var z)
                    && int.TryParse(parts[2], NumberStyles.Integer, CultureInfo.InvariantCulture, out var type))
                {
                    entries.Add(new Entry { X = x, Z = z, Type = type, Name = parts[3] });
                }
            }
            return entries;
        }

        private static void Save(List<Entry> entries)
        {
            var player = Player.m_localPlayer;
            var key = Key();
            if (player == null || key == null)
            {
                return;
            }
            var customData = CustomDataRef(player);
            if (entries.Count == 0)
            {
                customData.Remove(key);
                return;
            }
            var sb = new StringBuilder();
            foreach (var e in entries)
            {
                if (sb.Length > 0)
                {
                    sb.Append('\n');
                }
                sb.Append(e.X.ToString("R", CultureInfo.InvariantCulture)).Append('|')
                    .Append(e.Z.ToString("R", CultureInfo.InvariantCulture)).Append('|')
                    .Append(e.Type.ToString(CultureInfo.InvariantCulture)).Append('|')
                    .Append(e.Name.Replace('\n', ' '));
            }
            customData[key] = sb.ToString();
        }
    }
}
