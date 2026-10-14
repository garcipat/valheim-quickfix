using System;
using HarmonyLib;

namespace ValheimQuickFix.Patches
{
    /// <summary>
    /// Vanilla drops the shield when a bow or two-handed weapon is equipped and never brings it back.
    /// Remember the shield on the way out, and re-equip it when a one-handed weapon is equipped again.
    /// </summary>
    /// <remarks>
    /// The compile-time game libs are publicized, but the real game assembly is not: Humanoid.m_leftItem is
    /// protected at runtime. It is read through Harmony's ___m_leftItem field injection, never directly.
    /// </remarks>
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

        private static void Prefix(Humanoid __instance, ItemDrop.ItemData item, ItemDrop.ItemData ___m_leftItem, out ItemDrop.ItemData __state)
        {
            __state = null;
            try
            {
                if (!Plugin.RestoreShieldEnabled.Value || item == null || !IsLocalPlayer(__instance))
                {
                    return;
                }

                if (IsTwoHanded(item) && ___m_leftItem != null && ___m_leftItem.m_shared.m_itemType == ItemDrop.ItemData.ItemType.Shield)
                {
                    __state = ___m_leftItem;
                }
            }
            catch (Exception e)
            {
                Plugin.Log.LogError($"RestoreShield prefix failed: {e}");
            }
        }

        private static void Postfix(Humanoid __instance, ItemDrop.ItemData item, bool __result, ItemDrop.ItemData ___m_leftItem, ItemDrop.ItemData __state)
        {
            try
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

                if (___m_leftItem == null && __instance.GetInventory().ContainsItem(shield))
                {
                    __instance.EquipItem(shield);
                }
            }
            catch (Exception e)
            {
                Plugin.Log.LogError($"RestoreShield postfix failed: {e}");
            }
        }
    }
}
