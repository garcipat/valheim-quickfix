using HarmonyLib;

namespace ValheimQuickFix.Patches
{
    /// <summary>
    /// Vanilla drops the shield when a bow or two-handed weapon is equipped and never brings it back.
    /// Remember the shield on the way out, and re-equip it when a one-handed weapon is equipped again.
    /// </summary>
    [HarmonyPatch(typeof(Humanoid), nameof(Humanoid.EquipItem))]
    internal static class RestoreShieldPatch
    {
        private static ItemDrop.ItemData _rememberedShield;

        private static bool IsTwoHanded(ItemDrop.ItemData item)
        {
            var type = item.m_shared.m_itemType;
            // TwoHandedWeaponLeft only exists in newer game versions; match it by name so we also build against older libs.
            return type == ItemDrop.ItemData.ItemType.Bow
                || type == ItemDrop.ItemData.ItemType.TwoHandedWeapon
                || type.ToString() == "TwoHandedWeaponLeft";
        }

        private static bool IsLocalPlayer(Humanoid humanoid)
        {
            return Player.m_localPlayer != null && humanoid == Player.m_localPlayer;
        }

        private static void Prefix(Humanoid __instance, ItemDrop.ItemData item, out ItemDrop.ItemData __state)
        {
            __state = null;
            if (!Plugin.RestoreShieldEnabled.Value || item == null || !IsLocalPlayer(__instance))
            {
                return;
            }

            var left = __instance.m_leftItem;
            if (IsTwoHanded(item) && left != null && left.m_shared.m_itemType == ItemDrop.ItemData.ItemType.Shield)
            {
                __state = left;
            }
        }

        private static void Postfix(Humanoid __instance, ItemDrop.ItemData item, bool __result, ItemDrop.ItemData __state)
        {
            if (!Plugin.RestoreShieldEnabled.Value || item == null || !__result || !IsLocalPlayer(__instance))
            {
                return;
            }

            if (__state != null)
            {
                _rememberedShield = __state;
                return;
            }

            var type = item.m_shared.m_itemType;

            // Equipping any shield by hand supersedes the remembered one.
            if (type == ItemDrop.ItemData.ItemType.Shield)
            {
                _rememberedShield = null;
                return;
            }

            if (type != ItemDrop.ItemData.ItemType.OneHandedWeapon || _rememberedShield == null)
            {
                return;
            }

            var shield = _rememberedShield;
            _rememberedShield = null;

            if (__instance.m_leftItem == null && __instance.GetInventory().ContainsItem(shield))
            {
                __instance.EquipItem(shield);
            }
        }
    }
}
