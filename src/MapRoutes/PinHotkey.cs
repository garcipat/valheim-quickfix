using System;
using System.Collections;
using System.Linq;
using System.Reflection;
using BepInEx.Configuration;
using HarmonyLib;
using UnityEngine;

namespace ValheimMapRoutes
{
    /// <summary>
    /// Pressing the pin key while looking at something <see cref="PinTargets"/> knows puts its pin on the map; a circle
    /// pin's name makes the pin icons draw it as its icon. The hover text says so.
    /// </summary>
    internal static class PinHotkey
    {
        // A pin of the same type and name this close counts as the same place.
        private const float SameSpot = 10f;

        private static readonly FieldInfo PinsField = AccessTools.Field(typeof(Minimap), "m_pins");
        private static readonly MethodInfo TakeInputMethod = AccessTools.Method(typeof(Player), "TakeInput");
        // AddPin and Message gained parameters since the compile-time game libraries, so both are called by name and
        // the extra parameters get their defaults.
        private static readonly MethodInfo AddPinMethod = AccessTools.Method(typeof(Minimap), nameof(Minimap.AddPin));
        private static readonly MethodInfo MessageMethod = AccessTools.Method(typeof(Character), nameof(Character.Message));

        private static bool _failed;

        private static KeyboardShortcut Key => Plugin.PinHotkey.Value;

        internal static string Hint(Player player)
        {
            if (Key.MainKey == KeyCode.None || player == null || player.InInterior()
                || !PinTargets.TryGet(player.GetHoverObject(), out _, out _, out _))
            {
                return null;
            }
            return $"[<color=yellow><b>{Key.MainKey}</b></color>] Pin on map";
        }

        internal static void Update(Player player)
        {
            if (_failed || player != Player.m_localPlayer || Key.MainKey == KeyCode.None || !Key.IsDown()
                || Minimap.instance == null || player.InInterior() || !TakesInput(player))
            {
                return;
            }
            try
            {
                var hoverObject = player.GetHoverObject();
                if (hoverObject == null)
                {
                    return;
                }
                var found = PinTargets.TryGet(hoverObject, out var name, out var type, out var position);
                Plugin.Log.LogInfo($"Pin hotkey on {PinTargets.Describe(hoverObject)}: {(found ? $"{type} '{name}'" : "nothing to pin")}");
                if (!found)
                {
                    return;
                }
                if (HasPinNear(Minimap.instance, type, name, position))
                {
                    Message(player, "Already pinned");
                    return;
                }
                AddPin(Minimap.instance, position, type, name);
                Message(player, name.Length == 0 ? "Pinned portal" : $"Pinned {name}");
            }
            catch (Exception e)
            {
                _failed = true;
                Plugin.Log.LogError($"Pin hotkey failed and is off until restart: {e}");
            }
        }

        private static bool TakesInput(Player player)
        {
            return TakeInputMethod == null || (bool)TakeInputMethod.Invoke(player, Args(TakeInputMethod));
        }

        private static bool HasPinNear(Minimap minimap, Minimap.PinType type, string name, Vector3 position)
        {
            if (!(PinsField?.GetValue(minimap) is IList pins))
            {
                return false;
            }
            foreach (Minimap.PinData pin in pins)
            {
                var dx = pin.m_pos.x - position.x;
                var dz = pin.m_pos.z - position.z;
                if (pin.m_type == type && dx * dx + dz * dz <= SameSpot * SameSpot
                    && string.Equals(pin.m_name?.Trim(), name, StringComparison.OrdinalIgnoreCase))
                {
                    return true;
                }
            }
            return false;
        }

        private static void AddPin(Minimap minimap, Vector3 position, Minimap.PinType type, string name)
        {
            AddPinMethod.Invoke(minimap, Args(AddPinMethod, position, type, name, true, false));
        }

        private static void Message(Player player, string text)
        {
            MessageMethod.Invoke(player, Args(MessageMethod, MessageHud.MessageType.TopLeft, text));
        }

        // The given arguments first, then each remaining parameter's default.
        private static object[] Args(MethodInfo method, params object[] given)
        {
            return method.GetParameters()
                .Select((parameter, i) => i < given.Length ? given[i] : DefaultOf(parameter))
                .ToArray();
        }

        // A struct parameter defaulted with `default` can report null, DBNull or Missing depending on the runtime.
        private static object DefaultOf(ParameterInfo parameter)
        {
            var value = parameter.DefaultValue;
            var type = parameter.ParameterType;
            if ((value == null || value is DBNull || value == Missing.Value) && type.IsValueType)
            {
                return Activator.CreateInstance(type);
            }
            return value is DBNull || value == Missing.Value ? null : value;
        }
    }
}
