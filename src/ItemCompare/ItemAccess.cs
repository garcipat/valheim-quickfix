using System.Reflection;
using HarmonyLib;

namespace ValheimItemCompare
{
    /// <summary>
    /// ItemData members whose shape differs between the compile-time libs (0.202) and the live game, resolved by name
    /// at runtime: the equipped flag was spelled m_equiped before it became m_equipped, and GetWeight gained an
    /// optional stack-override parameter. The hidden hand items became private, and world level is live-only.
    /// </summary>
    internal static class ItemAccess
    {
        private static readonly FieldInfo Equipped =
            AccessTools.Field(typeof(ItemDrop.ItemData), "m_equipped") ?? AccessTools.Field(typeof(ItemDrop.ItemData), "m_equiped");

        private static readonly MethodInfo GetWeightMethod = AccessTools.Method(typeof(ItemDrop.ItemData), "GetWeight");

        private static readonly FieldInfo HiddenLeft = AccessTools.Field(typeof(Humanoid), "m_hiddenLeftItem");
        private static readonly FieldInfo HiddenRight = AccessTools.Field(typeof(Humanoid), "m_hiddenRightItem");

        private static readonly FieldInfo ItemWorldLevel = AccessTools.Field(typeof(ItemDrop.ItemData), "m_worldLevel");
        private static readonly FieldInfo GameWorldLevel = AccessTools.Field(typeof(Game), "m_worldLevel");

        internal static bool IsEquipped(ItemDrop.ItemData item)
        {
            return Equipped != null && (bool)Equipped.GetValue(item);
        }

        internal static float Weight(ItemDrop.ItemData item)
        {
            if (GetWeightMethod == null)
            {
                return item.m_shared.m_weight;
            }

            var args = GetWeightMethod.GetParameters().Length == 0 ? null : new object[] { -1 };
            return (float)GetWeightMethod.Invoke(item, args);
        }
    
        /// <summary>
        /// Whether <paramref name="item"/> is one of the hand items the player put away. Sheathing (R) and using a
        /// crafting station unequip both hands and remember them as hidden, so they still count as what is wielded.
        /// </summary>
        internal static bool IsHidden(Player player, ItemDrop.ItemData item)
        {
            return (HiddenLeft != null && HiddenLeft.GetValue(player) == item)
                || (HiddenRight != null && HiddenRight.GetValue(player) == item);
        }

        /// <summary>Gives a recipe preview the world level the game crafts at, so its stats match the vanilla text.</summary>
        internal static void SetCraftWorldLevel(ItemDrop.ItemData item)
        {
            if (ItemWorldLevel != null && GameWorldLevel != null)
            {
                ItemWorldLevel.SetValue(item, GameWorldLevel.GetValue(null));
            }
        }
    }
}
