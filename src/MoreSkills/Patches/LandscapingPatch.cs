using System;
using BepInEx.Configuration;
using HarmonyLib;
using UnityEngine;
using ValheimMoreSkills.Core;

namespace ValheimMoreSkills.Patches
{
    /// <summary>
    /// Landscaping: levels with every use of the hoe and lowers its stamina cost.
    /// </summary>
    /// <remarks>
    /// Vanilla build tools level the skill named on their piece table (<c>PieceTable.m_skill</c>): every placement
    /// raises it, and <c>GetBuildStamina</c> lowers the tool's stamina by up to 50% at level 100 (the same rule
    /// Crafting gives the hammer). The hoe has no skill there, so pointing it at Landscaping lets the game's own
    /// placement code do the levelling and the effect. Durability stays vanilla.
    /// </remarks>
    internal static class Landscaping
    {
        internal const string Identifier = "dev.pgarcia.valheim.moreskills.landscaping";

        internal static readonly Skills.SkillType Type = CustomSkill.TypeOf(Identifier);

        private const string HoePrefab = "Hoe";

        // The game raises a piece table skill with factor 1 per use; 0.25 is what a hammer placement gives Crafting.
        private const float IncreaseStep = 0.25f;

        // Not in the compile-time libs (they predate it), so it is read and written by name.
        private static readonly AccessTools.FieldRef<PieceTable, Skills.SkillType> Skill =
            AccessTools.FieldRefAccess<PieceTable, Skills.SkillType>("m_skill");

        // The piece table this mod pointed at Landscaping, so turning the skill off only undoes our own change.
        private static PieceTable _assigned;
        private static bool _warned;

        internal static CustomSkill Create(ConfigEntry<bool> enabled)
        {
            return new CustomSkill(
                Identifier,
                "Landscaping",
                "Working the ground with the hoe. Higher levels make the hoe cost less stamina.",
                HoeIcon,
                enabled,
                IncreaseStep);
        }

        /// <summary>Points the hoe's piece table at Landscaping, or back at no skill when the toggle is off.</summary>
        internal static void Apply()
        {
            try
            {
                var table = HoeTable();
                if (table == null)
                {
                    return;
                }

                if (!Plugin.LandscapingEnabled.Value)
                {
                    if (_assigned == table && Skill(table) == Type)
                    {
                        Skill(table) = Skills.SkillType.None;
                    }

                    _assigned = null;
                    return;
                }

                if (Skill(table) == Type)
                {
                    _assigned = table;
                    return;
                }

                if (Skill(table) != Skills.SkillType.None)
                {
                    if (!_warned)
                    {
                        _warned = true;
                        Plugin.Log.LogWarning(
                            $"The hoe already levels {Skill(table)}; Landscaping leaves it alone and will not level.");
                    }

                    return;
                }

                Skill(table) = Type;
                _assigned = table;
            }
            catch (Exception e)
            {
                Plugin.Log.LogError($"Landscaping setup failed: {e}");
            }
        }

        private static PieceTable HoeTable()
        {
            var item = HoeItem();
            return item == null ? null : item.m_shared.m_buildPieces;
        }

        private static ItemDrop.ItemData HoeItem()
        {
            if (ObjectDB.instance == null)
            {
                return null;
            }

            var prefab = ObjectDB.instance.GetItemPrefab(HoePrefab);
            var drop = prefab == null ? null : prefab.GetComponent<ItemDrop>();
            return drop == null ? null : drop.m_itemData;
        }

        // The hoe's own item icon; null before ObjectDB has loaded it.
        private static Sprite HoeIcon()
        {
            var item = HoeItem();
            return item == null ? null : item.GetIcon();
        }
    }

    /// <summary>
    /// The item prefabs are ready after <c>ObjectDB.Awake</c> (in the world) and <c>CopyOtherDB</c> (main menu). Both
    /// point the hoe at Landscaping.
    /// </summary>
    [HarmonyPatch]
    internal static class LandscapingSetupPatch
    {
        [HarmonyPostfix]
        [HarmonyPatch(typeof(ObjectDB), "Awake")]
        private static void AfterAwake()
        {
            Landscaping.Apply();
        }

        [HarmonyPostfix]
        [HarmonyPatch(typeof(ObjectDB), nameof(ObjectDB.CopyOtherDB))]
        private static void AfterCopy()
        {
            Landscaping.Apply();
        }
    }
}
