using System.Reflection;
using HarmonyLib;

namespace ValheimQuickFix
{
    /// <summary>
    /// ItemData members that differ between the compile-time libs (0.202) and the live game, looked up by name at
    /// runtime: the equipped flag was spelled m_equiped before it became m_equipped, and m_worldLevel is newer.
    /// </summary>
    internal static class ItemFields
    {
        internal static readonly FieldInfo Equipped =
            AccessTools.Field(typeof(ItemDrop.ItemData), "m_equipped") ?? AccessTools.Field(typeof(ItemDrop.ItemData), "m_equiped");

        internal static readonly FieldInfo WorldLevel = AccessTools.Field(typeof(ItemDrop.ItemData), "m_worldLevel");

        internal static bool IsEquipped(ItemDrop.ItemData item)
        {
            return Equipped != null && (bool)Equipped.GetValue(item);
        }

        // The game's rule for two items sharing a stack (Inventory.AddItem / FindFreeStackItem): same name, quality,
        // and world level.
        internal static string StackKey(ItemDrop.ItemData item)
        {
            var worldLevel = WorldLevel != null ? WorldLevel.GetValue(item) : 0;
            return $"{item.m_shared.m_name}|{item.m_quality}|{worldLevel}";
        }
    }
}
