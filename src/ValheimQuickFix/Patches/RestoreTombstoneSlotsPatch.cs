using System;
using System.Collections.Generic;
using HarmonyLib;

namespace ValheimQuickFix.Patches
{
    /// <summary>
    /// Taking everything from a tombstone first tries to put each item back at its original grid slot. If that slot now
    /// holds a different item (picked up after respawning), vanilla gives up and drops the corpse item into the first
    /// free slot. Instead, move the item that is in the way to a free slot, so the corpse item gets its old place back.
    /// </summary>
    /// <remarks>
    /// This is a prefix on the take-all response, so the vanilla move runs afterwards and does the actual placing.
    /// It only acts when the container is a tombstone, never for chests. If there is no free slot for the item in the
    /// way, nothing is moved and vanilla behaves as before: the corpse item stays in the tombstone.
    /// </remarks>
    [HarmonyPatch(typeof(Container), "RPC_TakeAllResponse")]
    internal static class RestoreTombstoneSlotsPatch
    {
        private static void Prefix(Container __instance, bool granted)
        {
            try
            {
                if (!Plugin.RestoreTombstoneSlotsEnabled.Value || !granted)
                {
                    return;
                }

                var player = Player.m_localPlayer;
                if (player == null || __instance.GetComponent<TombStone>() == null)
                {
                    return;
                }

                MakeRoom(player.GetInventory(), __instance.GetInventory());
            }
            catch (Exception e)
            {
                Plugin.Log.LogError($"RestoreTombstoneSlots failed: {e}");
            }
        }

        private static void MakeRoom(Inventory target, Inventory corpse)
        {
            var width = target.GetWidth();

            // Slots the corpse items want back; a displaced item must not take one of them.
            var reserved = new HashSet<int>();
            foreach (var item in corpse.GetAllItems())
            {
                reserved.Add(item.m_gridPos.y * width + item.m_gridPos.x);
            }

            var moved = false;
            foreach (var item in corpse.GetAllItems())
            {
                var inTheWay = target.GetItemAt(item.m_gridPos.x, item.m_gridPos.y);

                // An empty slot is fine, and so is a stack of the same item with room left (vanilla merges into it).
                // Two identical non-stackable items, or a full stack, can't merge, so the one in the way must move.
                if (inTheWay == null || CanMerge(inTheWay, item))
                {
                    continue;
                }

                Vector2i slot;
                if (TryFindFreeSlot(target, reserved, out slot))
                {
                    inTheWay.m_gridPos = slot;
                    moved = true;
                }
            }

            if (moved)
            {
                target.m_onChanged?.Invoke();
            }
        }

        private static bool CanMerge(ItemDrop.ItemData inTheWay, ItemDrop.ItemData item)
        {
            return inTheWay.m_shared.m_maxStackSize > 1
                && inTheWay.m_stack < inTheWay.m_shared.m_maxStackSize
                && ItemFields.StackKey(inTheWay) == ItemFields.StackKey(item);
        }

        // First free slot, top row first and left to right.
        private static bool TryFindFreeSlot(Inventory inventory, HashSet<int> reserved, out Vector2i slot)
        {
            var width = inventory.GetWidth();
            var height = inventory.GetHeight();
            for (var y = 0; y < height; y++)
            {
                for (var x = 0; x < width; x++)
                {
                    if (inventory.GetItemAt(x, y) == null && !reserved.Contains(y * width + x))
                    {
                        slot = new Vector2i(x, y);
                        return true;
                    }
                }
            }

            slot = default(Vector2i);
            return false;
        }
    }
}
