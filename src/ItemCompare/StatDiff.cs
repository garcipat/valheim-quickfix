using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;

namespace ValheimItemCompare
{
    /// <summary>
    /// Writes "value (+15)" differences into an item's tooltip. Stats are read from the two ItemData objects, not
    /// parsed out of the vanilla tooltip text; the text is only searched for the line to put each difference on.
    /// </summary>
    internal static class StatDiff
    {
        private const string Better = "#6FE36F";
        private const string Worse = "#FF5A5A";

        // Item types missing from the lib's ItemType enum are matched by their number in the live game.
        private const int TwoHandedWeaponLeft = 22;
        private const int Trinket = 24;

        private struct Stat
        {
            public string Label;
            public float Value;
            public string Format;
            public string Suffix;
            public bool HigherIsBetter;
        }

        /// <summary>
        /// What <paramref name="item"/> compares with: the local player's equipped (or put-away) item of its group, or
        /// null when it is not equipment, is itself worn, or nothing of its group is worn.
        /// </summary>
        internal static ItemDrop.ItemData FindWorn(ItemDrop.ItemData item, Player player)
        {
            var group = GroupOf(item);
            if (group == null || ItemAccess.IsEquipped(item) || ItemAccess.IsHidden(player, item))
            {
                return null;
            }

            return FindEquipped(player, group);
        }

        /// <summary>
        /// Writes the difference to <paramref name="worn"/> behind each matching stat line of the tooltip
        /// <paramref name="text"/>, localized or still holding <c>$</c> keys, as "value (+15)" with only the number coloured. Stats with no line in the text get
        /// a short line of their own under a "vs. <paramref name="title"/>" footer.
        /// </summary>
        internal static string Inline(string text, ItemDrop.ItemData item, ItemDrop.ItemData worn, string title)
        {
            var group = GroupOf(item);
            if (group == null || GroupOf(worn) != group)
            {
                return text;
            }

            var result = new StringBuilder(text);
            var extra = new StringBuilder();
            var changed = false;
            var oldStats = Stats(worn, group);
            foreach (var now in Stats(item, group).Values)
            {
                var old = oldStats.TryGetValue(now.Label, out var o) ? o : Zero(now);
                oldStats.Remove(now.Label);
                changed |= InsertDelta(result, extra, old, now);
            }

            foreach (var old in oldStats.Values)
            {
                changed |= InsertDelta(result, extra, old, Zero(old));
            }

            if (!changed)
            {
                return text;
            }

            return result.Append("\n<color=#AAAAAA>vs. ").Append(Localization.instance.Localize(title)).Append("</color>")
                .Append(extra).ToString();
        }

        private static bool InsertDelta(StringBuilder text, StringBuilder extra, Stat old, Stat now)
        {
            if (Format(old) == Format(now))
            {
                return false;
            }

            var better = now.HigherIsBetter ? now.Value > old.Value : now.Value < old.Value;
            var signed = "+" + now.Format + ";-" + now.Format;
            var delta = " (<color=" + (better ? Better : Worse) + ">"
                + (now.Value - old.Value).ToString(signed, CultureInfo.InvariantCulture) + now.Suffix + "</color>)";

            // The vanilla line is "<label>: <color=...>value</color>...": the delta goes right after the value.
            var label = Localization.instance.Localize(now.Label);
            var current = text.ToString();
            var start = FindLine(current, now.Label + ":");
            if (start < 0)
            {
                start = FindLine(current, label + ":");
            }

            if (start < 0)
            {
                extra.Append("\n").Append(label).Append(": ").Append(Format(now)).Append(delta);
                return true;
            }

            var end = current.IndexOf('\n', start);
            if (end < 0)
            {
                end = current.Length;
            }

            var close = current.IndexOf("</color>", start, end - start, StringComparison.Ordinal);
            text.Insert(close < 0 ? end : close + "</color>".Length, delta);
            return true;
        }

        private static int FindLine(string text, string prefix)
        {
            for (var at = text.IndexOf(prefix, StringComparison.Ordinal); at >= 0; at = text.IndexOf(prefix, at + 1, StringComparison.Ordinal))
            {
                if (at == 0 || text[at - 1] == '\n')
                {
                    return at;
                }
            }

            return -1;
        }

        private static string Format(Stat stat)
        {
            return stat.Value.ToString(stat.Format, CultureInfo.InvariantCulture) + stat.Suffix;
        }

        private static Stat Zero(Stat stat)
        {
            stat.Value = 0f;
            return stat;
        }

        // Items only compare within a group: armor within its slot, any main-hand weapon with the wielded one, etc.
        private static string GroupOf(ItemDrop.ItemData item)
        {
            switch ((int)item.m_shared.m_itemType)
            {
                case (int)ItemDrop.ItemData.ItemType.OneHandedWeapon:
                case (int)ItemDrop.ItemData.ItemType.TwoHandedWeapon:
                case TwoHandedWeaponLeft:
                case (int)ItemDrop.ItemData.ItemType.Bow:
                    return "weapon";
                case (int)ItemDrop.ItemData.ItemType.Shield:
                case (int)ItemDrop.ItemData.ItemType.Torch:
                case (int)ItemDrop.ItemData.ItemType.Tool:
                    return "hand" + (int)item.m_shared.m_itemType;
                case (int)ItemDrop.ItemData.ItemType.Helmet:
                case (int)ItemDrop.ItemData.ItemType.Chest:
                case (int)ItemDrop.ItemData.ItemType.Legs:
                case (int)ItemDrop.ItemData.ItemType.Shoulder:
                case (int)ItemDrop.ItemData.ItemType.Utility:
                case Trinket:
                    return "armor" + (int)item.m_shared.m_itemType;
                default:
                    return null;
            }
        }

        private static ItemDrop.ItemData FindEquipped(Player player, string group)
        {
            foreach (var candidate in player.GetInventory().GetAllItems())
            {
                if ((ItemAccess.IsEquipped(candidate) || ItemAccess.IsHidden(player, candidate)) && GroupOf(candidate) == group)
                {
                    return candidate;
                }
            }

            return null;
        }

        private static Dictionary<string, Stat> Stats(ItemDrop.ItemData item, string group)
        {
            var stats = new Dictionary<string, Stat>();
            var shared = item.m_shared;

            void Add(string label, float value, string format, bool higherIsBetter, string suffix = "")
            {
                stats[label] = new Stat { Label = label, Value = value, Format = format, Suffix = suffix, HigherIsBetter = higherIsBetter };
            }

            if (group == "weapon")
            {
                var damage = item.GetDamage();
                Add("$inventory_damage", damage.m_damage, "0", true);
                Add("$inventory_blunt", damage.m_blunt, "0", true);
                Add("$inventory_slash", damage.m_slash, "0", true);
                Add("$inventory_pierce", damage.m_pierce, "0", true);
                Add("$inventory_fire", damage.m_fire, "0", true);
                Add("$inventory_frost", damage.m_frost, "0", true);
                Add("$inventory_lightning", damage.m_lightning, "0", true);
                Add("$inventory_poison", damage.m_poison, "0", true);
                Add("$inventory_spirit", damage.m_spirit, "0", true);
                Add("$item_knockback", shared.m_attackForce, "0.#", true);
                Add("$item_staminause", shared.m_attack.m_attackStamina, "0.#", false);
            }

            if (group == "weapon" || group.StartsWith("hand"))
            {
                Add("$item_blockarmor", item.GetBaseBlockPower(), "0.#", true);
                Add("$item_blockforce", item.GetDeflectionForce(), "0.#", true);
                Add("$item_parrybonus", shared.m_timedBlockBonus > 1f ? shared.m_timedBlockBonus : 0f, "0.##", true, "x");
            }

            if (group.StartsWith("armor"))
            {
                Add("$item_armor", item.GetArmor(), "0.#", true);
            }

            Add("$item_weight", ItemAccess.Weight(item), "0.0", false);
            if (shared.m_useDurability)
            {
                Add("$item_durability", item.GetMaxDurability(), "0", true);
            }

            // Armor and some weapons slow the player down; a smaller penalty is better.
            Add("$item_movement_modifier", shared.m_movementModifier * 100f, "0.#", true, "%");

            return stats;
        }
    }
}
