using HarmonyLib;
using UnityEngine;

namespace ValheimQuickFix.Patches
{
    /// <summary>
    /// Adds a "[E] Push boat" interaction to every ship, usable from outside the boat.
    /// The hull colliders already receive the player's hover ray, which looks for a Hoverable on the
    /// collider's parents, so the component lives on the ship root and needs no collider of its own.
    /// </summary>
    [HarmonyPatch(typeof(Ship), nameof(Ship.Awake))]
    internal static class PushBoatPatch
    {
        private static void Postfix(Ship __instance)
        {
            if (__instance.GetComponent<BoatPush>() == null)
            {
                __instance.gameObject.AddComponent<BoatPush>();
            }
        }
    }

    internal class BoatPush : MonoBehaviour, Hoverable, Interactable
    {
        // A tap pushes for this long; holding E keeps extending it.
        private const float PushDuration = 0.3f;

        private Ship _ship;
        private float _pushUntil;
        private Vector3 _pushDirection;

        private void Awake()
        {
            _ship = GetComponent<Ship>();
        }

        private bool CanPush(Humanoid user)
        {
            return Plugin.PushBoatEnabled.Value
                && _ship != null
                && user != null
                && user.GetStandingOnShip() != _ship;
        }

        public string GetHoverText()
        {
            if (!CanPush(Player.m_localPlayer))
            {
                return string.Empty;
            }

            return Localization.instance.Localize("[<color=yellow><b>$KEY_Use</b></color>] Push boat");
        }

        public string GetHoverName()
        {
            return string.Empty;
        }

        public bool Interact(Humanoid user, bool hold, bool alt)
        {
            if (!CanPush(user))
            {
                return false;
            }

            // Physics is simulated by the object's owner; without ownership the force is ignored.
            if (_ship.m_nview != null && _ship.m_nview.IsValid() && !_ship.m_nview.IsOwner())
            {
                _ship.m_nview.ClaimOwnership();
            }

            // Push away from the side the player is standing on.
            var away = _ship.transform.position - user.transform.position;
            away.y = 0f;
            if (away.sqrMagnitude < 0.0001f)
            {
                return false;
            }

            _pushDirection = away.normalized;
            _pushUntil = Time.time + PushDuration;
            return true;
        }

        public bool UseItem(Humanoid user, ItemDrop.ItemData item)
        {
            return false;
        }

        private void FixedUpdate()
        {
            if (Time.time >= _pushUntil || _ship == null || _ship.m_body == null)
            {
                return;
            }

            if (_ship.m_nview != null && _ship.m_nview.IsValid() && !_ship.m_nview.IsOwner())
            {
                return;
            }

            // Acceleration mode ignores mass, so the slider means the same thing for every boat.
            _ship.m_body.AddForce(_pushDirection * Plugin.PushStrength.Value, ForceMode.Acceleration);
        }
    }
}
