using System.Reflection;
using HarmonyLib;

namespace ValheimItemCompare
{
    /// <summary>
    /// ItemData members whose shape differs between the compile-time libs (0.202) and the live game, resolved by name
    /// at runtime: the equipped flag was spelled m_equiped before it became m_equipped, and GetWeight gained an
    /// optional stack-override parameter.
    /// </summary>
    internal static class ItemAccess
    {
        private static readonly FieldInfo Equipped =
            AccessTools.Field(typeof(ItemDrop.ItemData), "m_equipped") ?? AccessTools.Field(typeof(ItemDrop.ItemData), "m_equiped");

        private static readonly MethodInfo GetWeightMethod = AccessTools.Method(typeof(ItemDrop.ItemData), "GetWeight");

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
    }
}
