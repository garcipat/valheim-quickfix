using System;
using System.Collections;
using System.Reflection;
using BepInEx.Configuration;
using HarmonyLib;
using UnityEngine;
using ValheimMoreSkills.Core;

namespace ValheimMoreSkills.Patches
{
    /// <summary>
    /// Brewing: levels while you fill and tap fermenters and gives a chance at a bigger batch.
    /// </summary>
    /// <remarks>
    /// Brewing is rare (a barrel takes about 40 minutes), so the curve is set in potions rather than by the game's
    /// XP table: level N to N+1 takes <c>PotionsPerLevel * (1 + CurveGrowth * N)</c> potions. Level 1 is one batch of
    /// 6, level 10 about 1.6 batches, level 50 about 4, level 100 about 7 (in total about 13 batches for level 10, 124
    /// for 50, 397 for 100). Each potion raises the game's requirement for the current level divided by that count,
    /// so no batch ever gives more than one level. Filling a fermenter gives half a batch's worth, tapping the other
    /// half.
    /// </remarks>
    internal static class Brewing
    {
        internal const string Identifier = "dev.pgarcia.valheim.moreskills.brewing";

        internal static readonly Skills.SkillType Type = CustomSkill.TypeOf(Identifier);

        private const float PotionsPerLevel = 6f;
        private const float CurveGrowth = 0.06f;

        private static readonly Func<Skills, Skills.SkillType, Skills.Skill> GetSkill =
            AccessTools.MethodDelegate<Func<Skills, Skills.SkillType, Skills.Skill>>(
                AccessTools.Method(typeof(Skills), "GetSkill"));

        internal static CustomSkill Create(ConfigEntry<bool> enabled)
        {
            return new CustomSkill(
                Identifier,
                "Brewing",
                "Filling and tapping fermenters. Higher levels give a chance at a bigger batch.",
                FermenterIcon,
                enabled);
        }

        // The game's own fermenter piece icon; null before ZNetScene exists (main menu).
        private static Sprite FermenterIcon()
        {
            if (ZNetScene.instance == null)
            {
                return null;
            }

            var prefab = ZNetScene.instance.GetPrefab("fermenter");
            var piece = prefab == null ? null : prefab.GetComponent<Piece>();
            return piece == null ? null : piece.m_icon;
        }

        /// <summary>
        /// Raises Brewing by <paramref name="potions"/> potions' worth, one potion at a time so a level-up in between
        /// uses the new level's rate. A fraction of a potion is granted last.
        /// </summary>
        internal static void Grant(Player player, float potions)
        {
            var skills = player.GetSkills();
            while (potions > 0f)
            {
                var step = Mathf.Min(1f, potions);
                potions -= step;

                var level = Mathf.Floor(GetSkill(skills, Type).m_level);
                if (level >= 100f)
                {
                    return;
                }

                // The game's XP for the next level; the same for every skill.
                var requirement = Mathf.Pow(level + 1f, 1.5f) * 0.5f + 0.5f;
                var potionsNeeded = PotionsPerLevel * (1f + CurveGrowth * level);
                player.RaiseSkill(Type, step * requirement / potionsNeeded);
            }
        }

        internal static Fermenter.ItemConversion Conversion(Fermenter fermenter, int nameHash)
        {
            foreach (var conversion in fermenter.m_conversion)
            {
                if (conversion.m_from != null && conversion.m_from.gameObject.name.GetStableHashCode() == nameHash)
                {
                    return conversion;
                }
            }

            return null;
        }
    }

    /// <summary>
    /// The private <c>Fermenter.AddItem</c> runs on the player filling it: it takes the mead base from their inventory
    /// and only then tells the owner. A true result means the base went in, so the Postfix gives half a batch's worth.
    /// Mead base never comes back out, so this can't be farmed.
    /// </summary>
    [HarmonyPatch(typeof(Fermenter), "AddItem")]
    internal static class BrewingFillPatch
    {
        private static void Postfix(Fermenter __instance, Humanoid user, ItemDrop.ItemData item, bool __result)
        {
            try
            {
                if (!__result || !Plugin.BrewingEnabled.Value || user == null || user != Player.m_localPlayer
                    || item?.m_dropPrefab == null)
                {
                    return;
                }

                var conversion = Brewing.Conversion(__instance, item.m_dropPrefab.name.GetStableHashCode());
                if (conversion != null)
                {
                    Brewing.Grant(Player.m_localPlayer, conversion.m_producedItems * 0.5f);
                }
            }
            catch (Exception e)
            {
                Plugin.Log.LogError($"BrewingFill patch failed: {e}");
            }
        }
    }

    /// <summary>
    /// Tapping a ready fermenter sends <c>RPC_Tap</c> to its owner, who spawns the batch at <c>m_outputPoint</c> after
    /// <c>m_tapDelay</c>. The Prefix remembers the batch if the local player can tap it; when <c>Interact</c> returned
    /// true, the Postfix gives the other half of the batch's worth and rolls the bonus: a chance of
    /// <c>MaxBonusChance * skill factor</c> (with the level before this tap) at a third of the batch more, rounded up
    /// (6 becomes 8). This client spawns the bonus next to the owner's output after the same delay.
    /// </summary>
    /// <remarks>
    /// A batch is identified by its ZDO and start time and pays out once, so pressing the key again before the owner's
    /// reset reaches this client does nothing.
    /// </remarks>
    [HarmonyPatch(typeof(Fermenter), nameof(Fermenter.Interact))]
    internal static class BrewingTapPatch
    {
        // Fermenter.Status.Ready; the enum is private.
        private const int Ready = 3;
        // DamageText.TextType.Bonus; newer than the compile-time libs.
        private const DamageText.TextType BonusText = (DamageText.TextType)7;
        private const float SpawnHeight = 0.3f;

        private static readonly int ContentKey = "Content".GetStableHashCode();
        private static readonly int StartTimeKey = "StartTime".GetStableHashCode();
        private static readonly int CheatedKey = "cheatedQueued".GetStableHashCode();

        private static readonly MethodInfo GetStatus = AccessTools.Method(typeof(Fermenter), "GetStatus");

        private static readonly AccessTools.FieldRef<Fermenter, ZNetView> NView =
            AccessTools.FieldRefAccess<Fermenter, ZNetView>("m_nview");

        // Newer than the compile-time libs, so both are called by name.
        private static readonly MethodInfo OnCreateNew =
            AccessTools.Method(typeof(ItemDrop), "OnCreateNew", new[] { typeof(ItemDrop), typeof(bool) });

        private static readonly MethodInfo ShowText = AccessTools.Method(typeof(DamageText), "ShowText",
            new[] { typeof(DamageText.TextType), typeof(Vector3), typeof(string), typeof(bool) });

        private static ZDO _lastZdo;
        private static long _lastStartTime;

        private sealed class Batch
        {
            internal ZDO Zdo;
            internal long StartTime;
            internal Fermenter.ItemConversion Conversion;
            internal bool Cheated;
        }

        private static void Prefix(Fermenter __instance, Humanoid user, bool hold, out Batch __state)
        {
            __state = null;
            try
            {
                if (hold || !Plugin.BrewingEnabled.Value || user == null || user != Player.m_localPlayer)
                {
                    return;
                }

                var nview = NView(__instance);
                var zdo = nview == null || !nview.IsValid() ? null : nview.GetZDO();
                if (zdo == null || Convert.ToInt32(GetStatus.Invoke(__instance, null)) != Ready
                    || !PrivateArea.CheckAccess(__instance.transform.position, 0f, false, false))
                {
                    return;
                }

                var startTime = zdo.GetLong(StartTimeKey);
                var conversion = Brewing.Conversion(__instance, zdo.GetInt(ContentKey));
                if (conversion == null || (zdo == _lastZdo && startTime == _lastStartTime))
                {
                    return;
                }

                __state = new Batch
                {
                    Zdo = zdo,
                    StartTime = startTime,
                    Conversion = conversion,
                    Cheated = zdo.GetBool(CheatedKey),
                };
            }
            catch (Exception e)
            {
                Plugin.Log.LogError($"BrewingTap patch failed: {e}");
            }
        }

        private static void Postfix(Fermenter __instance, bool __result, Batch __state)
        {
            try
            {
                var player = Player.m_localPlayer;
                if (__state == null || !__result || player == null)
                {
                    return;
                }

                _lastZdo = __state.Zdo;
                _lastStartTime = __state.StartTime;

                var produced = __state.Conversion.m_producedItems;
                var chance = Plugin.MaxBonusChance.Value * player.GetSkills().GetSkillFactor(Brewing.Type);
                Brewing.Grant(player, produced * 0.5f);

                if (UnityEngine.Random.value < chance)
                {
                    var bonus = Mathf.CeilToInt(produced / 3f);
                    __instance.StartCoroutine(SpawnBonus(__instance, __state.Conversion.m_to, bonus, __state.Cheated));
                }
            }
            catch (Exception e)
            {
                Plugin.Log.LogError($"BrewingTap patch failed: {e}");
            }
        }

        private static IEnumerator SpawnBonus(Fermenter fermenter, ItemDrop potion, int count, bool cheated)
        {
            yield return new WaitForSeconds(fermenter.m_tapDelay);
            if (fermenter == null || potion == null)
            {
                yield break;
            }

            try
            {
                var position = fermenter.m_outputPoint.position + Vector3.up * SpawnHeight;
                for (var i = 0; i < count; i++)
                {
                    var item = UnityEngine.Object.Instantiate(potion, position, Quaternion.identity);
                    OnCreateNew?.Invoke(null, new object[] { item, cheated });
                }

                if (DamageText.instance != null && ShowText != null)
                {
                    ShowText.Invoke(DamageText.instance, new object[] { BonusText, position, $"+{count}", true });
                }
            }
            catch (Exception e)
            {
                Plugin.Log.LogError($"BrewingTap bonus failed: {e}");
            }
        }
    }
}
