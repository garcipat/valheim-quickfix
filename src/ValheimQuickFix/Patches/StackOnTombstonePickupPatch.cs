using System;
using System.Collections.Generic;
using HarmonyLib;

namespace ValheimQuickFix.Patches
{
    /// <summary>
    /// Taking everything from a tombstone puts each item back at its original grid slot first, which never merges
    /// into another stack. A partial stack from the corpse can therefore sit next to a partial stack of the same item
    /// you were already carrying. After a successful take-all, top those partial stacks up.
    /// </summary>
    /// <remarks>
    /// TombStone.OnTakeAllSuccess is private, so it is patched by name. It runs only for the local player, after the
    /// items have been moved into their inventory.
    /// </remarks>
    [HarmonyPatch(typeof(TombStone), "OnTakeAllSuccess")]
    internal static class StackOnTombstonePickupPatch
    {
        private static bool _loggedMissingField;

        private static void Postfix()
        {
            try
            {
                var player = Player.m_localPlayer;
                if (!Plugin.StackOnTombstonePickupEnabled.Value || player == null)
                {
                    return;
                }

                // Without the equipped flag an equipped stack could be merged away, so do nothing.
                if (ItemFields.Equipped == null)
                {
                    if (!_loggedMissingField)
                    {
                        _loggedMissingField = true;
                        Plugin.Log.LogError("StackOnTombstonePickup disabled: ItemData has no equipped field in this game version");
                    }

                    return;
                }

                MergePartialStacks(player.GetInventory());
            }
            catch (Exception e)
            {
                Plugin.Log.LogError($"StackOnTombstonePickup failed: {e}");
            }
        }

        private static void MergePartialStacks(Inventory inventory)
        {
            // Same rule the game uses to decide that two items share a stack (Inventory.AddItem / FindFreeStackItem).
            var groups = new Dictionary<string, List<ItemDrop.ItemData>>();
            foreach (var item in inventory.GetAllItems())
            {
                var max = item.m_shared.m_maxStackSize;

                // Equipped stacks (arrows used as ammo) are left alone, and so is anything already over the limit.
                if (max <= 1 || item.m_stack >= max || ItemFields.IsEquipped(item))
                {
                    continue;
                }

                var key = ItemFields.StackKey(item);
                if (!groups.TryGetValue(key, out var group))
                {
                    group = new List<ItemDrop.ItemData>();
                    groups[key] = group;
                }

                group.Add(item);
            }

            var changed = false;
            foreach (var group in groups.Values)
            {
                if (group.Count > 1 && MergeGroup(inventory, group))
                {
                    changed = true;
                }
            }

            if (changed)
            {
                inventory.m_onChanged?.Invoke();
            }
        }

        // Partial stacks of one item, earliest slot first. Top up the earliest from the latest until at most one is left.
        private static bool MergeGroup(Inventory inventory, List<ItemDrop.ItemData> partials)
        {
            partials.Sort((a, b) => a.m_gridPos.y != b.m_gridPos.y
                ? a.m_gridPos.y.CompareTo(b.m_gridPos.y)
                : a.m_gridPos.x.CompareTo(b.m_gridPos.x));

            var changed = false;
            var target = 0;
            var donor = partials.Count - 1;
            while (target < donor)
            {
                var to = partials[target];
                var from = partials[donor];

                var move = Math.Min(to.m_shared.m_maxStackSize - to.m_stack, from.m_stack);
                to.m_stack += move;
                from.m_stack -= move;
                changed = true;

                if (from.m_stack <= 0)
                {
                    inventory.RemoveItem(from);
                    donor--;
                }

                if (to.m_stack >= to.m_shared.m_maxStackSize)
                {
                    target++;
                }
            }

            return changed;
        }
    }
}
