using System;
using HarmonyLib;
using UnityEngine;

namespace ValheimSignIcons.Patches
{
    /// <summary>
    /// A sign placed on a chest covers it, so the chest can't be opened through it. When the chest is right behind the
    /// sign you're looking at, Use opens the chest and AltPlace + Use edits the sign.
    /// </summary>
    [HarmonyPatch(typeof(Sign), nameof(Sign.Interact))]
    internal static class SignOnChestInteractPatch
    {
        private static bool Prefix(Sign __instance, Humanoid character, bool hold, bool alt, ref bool __result)
        {
            if (hold || alt || character != Player.m_localPlayer)
            {
                return true;
            }

            var container = SignOnChest.ContainerBehind(__instance);
            if (container == null)
            {
                return true;
            }

            __result = container.Interact(character, hold, alt);
            return false;
        }
    }

    /// <summary>
    /// Shows the chest's hover text and the key that edits the sign, in place of the sign's own Use line.
    /// </summary>
    [HarmonyPatch(typeof(Sign), nameof(Sign.GetHoverText))]
    internal static class SignOnChestHoverPatch
    {
        private static void Postfix(Sign __instance, ref string __result)
        {
            var container = SignOnChest.ContainerBehind(__instance);
            if (container == null)
            {
                return;
            }

            // Only rewrite the text in the shape vanilla builds it; without ward access it has no Use line.
            var useLine = "\n" + Localization.instance.Localize(
                __instance.m_name + "\n[<color=yellow><b>$KEY_Use</b></color>] $piece_use");
            if (!__result.EndsWith(useLine, StringComparison.Ordinal))
            {
                return;
            }

            __result = __result.Substring(0, __result.Length - useLine.Length)
                + "\n" + container.GetHoverText()
                + Localization.instance.Localize(
                    "\n[<color=yellow><b>$KEY_AltPlace + $KEY_Use</b></color>] $piece_use " + __instance.m_name);
        }
    }

    internal static class SignOnChest
    {
        // How far behind the sign's surface a chest still counts as covered by it.
        private const float MaxGap = 0.75f;

        private static readonly AccessTools.FieldRef<Player, int> InteractMask =
            AccessTools.FieldRefAccess<Player, int>("m_interactMask");

        private static readonly RaycastHit[] Hits = new RaycastHit[32];

        private static int _cachedFrame = -1;
        private static Sign _cachedSign;
        private static Container _cachedContainer;

        /// <summary>
        /// The chest directly behind <paramref name="sign"/> along the player's line of sight, or null. Follows the same
        /// camera ray the game uses to pick the hovered object, so it is the chest the sign hides from the crosshair.
        /// </summary>
        internal static Container ContainerBehind(Sign sign)
        {
            if (_cachedFrame == Time.frameCount && _cachedSign == sign)
            {
                return _cachedContainer;
            }

            _cachedFrame = Time.frameCount;
            _cachedSign = sign;
            _cachedContainer = Find(sign);
            return _cachedContainer;
        }

        private static Container Find(Sign sign)
        {
            var player = Player.m_localPlayer;
            if (player == null || GameCamera.instance == null)
            {
                return null;
            }

            var camera = GameCamera.instance.transform;
            var count = Physics.RaycastNonAlloc(camera.position, camera.forward, Hits, 50f, InteractMask(player));
            Array.Sort(Hits, 0, count, Comparer.Instance);

            var signDistance = -1f;
            for (var i = 0; i < count; i++)
            {
                var hit = Hits[i];
                var body = hit.collider.attachedRigidbody;
                if (body != null && body.gameObject == player.gameObject)
                {
                    continue;
                }

                if (hit.collider.GetComponentInParent<Sign>() == sign)
                {
                    if (signDistance < 0f)
                    {
                        signDistance = hit.distance;
                    }

                    continue;
                }

                // Anything in front of the sign, or the first thing behind it that isn't a chest, means no chest.
                if (signDistance < 0f || hit.distance - signDistance > MaxGap)
                {
                    return null;
                }

                return hit.collider.GetComponentInParent<Container>();
            }

            return null;
        }

        private sealed class Comparer : System.Collections.Generic.IComparer<RaycastHit>
        {
            internal static readonly Comparer Instance = new Comparer();

            public int Compare(RaycastHit x, RaycastHit y) => x.distance.CompareTo(y.distance);
        }
    }
}
