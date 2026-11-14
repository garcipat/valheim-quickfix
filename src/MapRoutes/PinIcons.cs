using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using HarmonyLib;
using UnityEngine;

namespace ValheimMapRoutes
{
    /// <summary>
    /// Circle pins whose whole name is a known name are drawn with a matching icon. The pin keeps its type and name;
    /// only its image changes, and for an icon's own name or alias the name label is hidden.
    /// </summary>
    internal static class PinIcons
    {
        // The game's circle pin, fourth in the pin picker.
        private const Minimap.PinType CirclePin = Minimap.PinType.Icon3;

        private const int CellSize = 128;
        private const int Columns = 7;

        private sealed class Icon
        {
            internal readonly string[] Plain;
            internal readonly string[] Labeled;

            internal Icon(string[] plain, string[] labeled = null)
            {
                Plain = plain;
                Labeled = labeled ?? new string[0];
            }
        }

        // Same order as the cells in pin-icons.png (tools/PinIcons/make-sheet.sh). Plain names show only the icon;
        // labeled names keep the name under it.
        private static readonly Icon[] Icons =
        {
            new Icon(new[] { "Cave" }, new[] { "Troll cave", "Frost cave" }),
            new Icon(new[] { "Crypt" }, new[] { "Burial chamber", "Sunken crypt" }),
            new Icon(new[] { "Dungeon" }),
            new Icon(new[] { "Village", "Camp" }, new[] { "Fuling village", "Fuling camp" }),
            new Icon(new[] { "Portal" }),
            new Icon(new[] { "Ship" }, new[] { "Boat", "Karve", "Longship" }),
            new Icon(new[] { "Ore" }, new[] { "Copper", "Tin", "Silver", "Iron", "Obsidian" }),
            new Icon(new[] { "Tar" }, new[] { "Tar pit" }),
            new Icon(new[] { "Berries" }, new[] { "Raspberries", "Blueberries", "Cloudberries" }),
            new Icon(new[] { "Mushroom" }, new[] { "Yellow mushroom", "Blue mushroom", "Jotun puffs", "Magecap" }),
            new Icon(new[] { "Egg" }, new[] { "Dragon egg" }),
            new Icon(new[] { "Nest" }, new[] { "Greydwarf nest" }),
            new Icon(new[] { "Surtling" }, new[] { "Fire geyser" }),
            new Icon(new[] { "Leviathan", "Turtle" }),
            new Icon(new[] { "Forest" }, new[] { "Black forest" }),
            new Icon(new[] { "Swamp" }),
            new Icon(new[] { "Mountain" }),
            new Icon(new[] { "Plains" }),
            new Icon(new[] { "Mistlands", "Mist" }),
            new Icon(new[] { "Ashlands", "Ash" }),
            new Icon(new[] { "Deep North", "North" }),
            new Icon(new[] { "Lox" }),
            new Icon(new[] { "Mine" }, new[] { "Infested mine" }),
        };

        private static readonly Dictionary<string, KeyValuePair<int, bool>> Names = BuildNames();

        private static readonly FieldInfo PinsField = AccessTools.Field(typeof(Minimap), "m_pins");
        private static readonly FieldInfo IconElementField = AccessTools.Field(typeof(Minimap.PinData), "m_iconElement");
        // The name label is newer than the compile-time game libraries, so it is reached by name.
        private static readonly FieldInfo NameDataField = AccessTools.Field(typeof(Minimap.PinData), "m_NamePinData");
        private static readonly PropertyInfo NameObjectProperty = NameDataField == null
            ? null
            : AccessTools.Property(NameDataField.FieldType, "PinNameGameObject");

        private static Sprite[] _sprites;
        private static bool _spritesTried;
        private static bool _failed;

        internal static void Apply(Minimap minimap)
        {
            if (_failed)
            {
                return;
            }
            try
            {
                var first = !_spritesTried;
                var sprites = Sprites();
                if (first && sprites != null)
                {
                    // Which picker icon the circle is, so a wrong guess shows up in the log.
                    var circle = minimap.m_icons.Find(icon => icon.m_name == CirclePin).m_icon;
                    Plugin.Log.LogInfo($"Pin icons replace the {CirclePin} pin, sprite '{(circle == null ? "none" : circle.name)}'");
                }
                if (sprites == null || !(PinsField?.GetValue(minimap) is IList pins))
                {
                    return;
                }
                foreach (Minimap.PinData pin in pins)
                {
                    if (pin.m_type == CirclePin)
                    {
                        ApplyTo(pin, sprites);
                    }
                }
            }
            catch (Exception e)
            {
                // UpdatePins runs often; log once instead of every time.
                _failed = true;
                Plugin.Log.LogError($"Drawing pin icons failed, pins keep the game's icons: {e}");
            }
        }

        private static void ApplyTo(Minimap.PinData pin, Sprite[] sprites)
        {
            var image = IconElementField.GetValue(pin) as Behaviour;
            if (image == null)
            {
                return;
            }
            var matched = TryMatch(pin.m_name, out var index, out var showName);
            var sprite = matched ? sprites[index] : pin.m_icon;
            if (Ui.GetSprite(image) != sprite)
            {
                Ui.SetSprite(image, sprite);
            }

            // The game turns the label on and off by zoom and delays it after creating it, so scale it instead. A pin
            // without a name has no label data.
            var nameData = NameObjectProperty == null ? null : NameDataField.GetValue(pin);
            var label = nameData == null ? null : NameObjectProperty.GetValue(nameData, null) as GameObject;
            if (label != null)
            {
                var scale = matched && !showName ? Vector3.zero : Vector3.one;
                if (label.transform.localScale != scale)
                {
                    label.transform.localScale = scale;
                }
            }
        }

        private static bool TryMatch(string name, out int index, out bool showName)
        {
            if (!string.IsNullOrEmpty(name) && Names.TryGetValue(name.Trim(), out var entry))
            {
                index = entry.Key;
                showName = entry.Value;
                return true;
            }
            index = -1;
            showName = true;
            return false;
        }

        private static Dictionary<string, KeyValuePair<int, bool>> BuildNames()
        {
            var names = new Dictionary<string, KeyValuePair<int, bool>>(StringComparer.OrdinalIgnoreCase);
            for (var i = 0; i < Icons.Length; i++)
            {
                foreach (var name in Icons[i].Plain)
                {
                    names[name] = new KeyValuePair<int, bool>(i, false);
                }
                foreach (var name in Icons[i].Labeled)
                {
                    names[name] = new KeyValuePair<int, bool>(i, true);
                }
            }
            return names;
        }

        private static Sprite[] Sprites()
        {
            if (_spritesTried)
            {
                return _sprites;
            }
            _spritesTried = true;
            if (IconElementField == null || PinsField == null || !Ui.Available)
            {
                Plugin.Log.LogError("Pin icons are off: the game's pin fields were not found");
                return null;
            }
            var sheet = RouteArt.LoadPng("pin-icons.png", mipmaps: true);
            if (sheet == null)
            {
                return null;
            }
            var rows = sheet.height / CellSize;
            var sprites = new Sprite[Icons.Length];
            for (var i = 0; i < Icons.Length; i++)
            {
                // Texture rows count from the bottom; the sheet is laid out from the top.
                var x = i % Columns * CellSize;
                var y = (rows - 1 - i / Columns) * CellSize;
                sprites[i] = Sprite.Create(sheet, new Rect(x, y, CellSize, CellSize), new Vector2(0.5f, 0.5f));
            }
            _sprites = sprites;
            Plugin.Log.LogInfo($"Pin icons ready: {Icons.Length} icons, {Names.Count} names");
            return _sprites;
        }
    }
}
