using System.Collections.Generic;
using System.Globalization;
using System.Text;

namespace ValheimItemCompare
{
    /// <summary>
    /// Builds the "old -> new" lines shown under an item's tooltip. Stats are read from the two ItemData objects, not
    /// parsed out of the vanilla tooltip text, and labelled with the game's own localization keys.
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

        /// <summary>The comparison block for <paramref name="item"/> against what the local player wears, or "".</summary>
        internal static string Build(ItemDrop.ItemData item, Player player)
        {
            var group = GroupOf(item);
            if (group == null || ItemAccess.IsEquipped(item))
            {
                return "";
            }

            var worn = FindEquipped(player, group);
            if (worn == null)
            {
                return "";
            }

            var lines = new StringBuilder();
            var oldStats = Stats(worn, group);
            foreach (var stat in Stats(item, group).Values)
            {
                var old = oldStats.TryGetValue(stat.Label, out var o) ? o : Zero(stat);
                oldStats.Remove(stat.Label);
                Append(lines, old, stat);
            }

            // Stats only the worn item has: the hovered one drops to zero.
            foreach (var old in oldStats.Values)
            {
                Append(lines, old, Zero(old));
            }

            return lines.Length == 0 ? "" : "\n\n<color=#AAAAAA>vs. " + worn.m_shared.m_name + "</color>" + lines;
        }

        private static void Append(StringBuilder lines, Stat old, Stat now)
        {
            var oldText = Format(old);
            var nowText = Format(now);
            if (oldText == nowText)
            {
                return;
            }

            var better = now.HigherIsBetter ? now.Value > old.Value : now.Value < old.Value;
            lines.Append("\n").Append(now.Label).Append(": ").Append(oldText).Append(" -> ")
                .Append("<color=").Append(better ? Better : Worse).Append(">").Append(nowText).Append("</color>");
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
                if (ItemAccess.IsEquipped(candidate) && GroupOf(candidate) == group)
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
