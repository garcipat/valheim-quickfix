using System;
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
            try
            {
                if (__instance.GetComponent<BoatPush>() == null)
                {
                    var component = __instance.gameObject.AddComponent<BoatPush>();
                    if (component == null)
                    {
                        Plugin.Log.LogError($"PushBoat: Unity could not create the component on '{__instance.name}'");
                        LogWhyItFailed();
                        return;
                    }

                    Plugin.Log.LogInfo($"PushBoat: attached to '{__instance.name}' (layer {LayerMask.LayerToName(__instance.gameObject.layer)})");
                }
            }
            catch (Exception e)
            {
                Plugin.Log.LogError($"PushBoat patch failed: {e}");
            }
        }

        private static bool _loggedFailure;

        // Unity only says "could not be instantiated"; surface the real reason once.
        private static void LogWhyItFailed()
        {
            if (_loggedFailure)
            {
                return;
            }

            _loggedFailure = true;

            foreach (var type in new[] { typeof(Hoverable), typeof(Interactable) })
            {
                foreach (var method in type.GetMethods())
                {
                    Plugin.Log.LogError($"PushBoat: game interface {type.Name}.{method.Name}({method.GetParameters().Length} params) -> {method.ReturnType.Name}");
                }

                try
                {
                    typeof(BoatPush).GetInterfaceMap(type);
                }
                catch (Exception e)
                {
                    Plugin.Log.LogError($"PushBoat: BoatPush does not satisfy {type.Name}: {e.Message}");
                }
            }

            try
            {
                System.Runtime.CompilerServices.RuntimeHelpers.RunClassConstructor(typeof(BoatPush).TypeHandle);
            }
            catch (Exception e)
            {
                Plugin.Log.LogError($"PushBoat: BoatPush static initialization failed: {e}");
            }
        }
    }

    public class BoatPush : MonoBehaviour, Hoverable, Interactable
    {
        // A tap pushes for this long; the game repeats Interact every 0.2 s while E is held, which keeps extending it.
        private const float PushDuration = 0.3f;

        // Acceleration in m/s². Found by testing in-game: lower values barely move a grounded boat.
        private const float PushStrength = 20f;

        // Ship.m_body and Ship.m_nview are private in the real game; the compile-time libs are publicized,
        // so they must be read through AccessTools rather than directly.
        private static bool _loggedHover;

        private Ship _ship;
        private float _pushUntil;
        private Vector3 _pushDirection;

        private System.Reflection.FieldInfo _bodyField;
        private System.Reflection.FieldInfo _nviewField;

        private void Awake()
        {
            _ship = GetComponent<Ship>();
            _bodyField = FindShipField("m_body");
            _nviewField = FindShipField("m_nview");
        }

        private static System.Reflection.FieldInfo FindShipField(string name)
        {
            var field = AccessTools.Field(typeof(Ship), name);
            if (field == null)
            {
                Plugin.Log.LogError($"PushBoat: Ship.{name} not found in this game version");
            }

            return field;
        }

        private ZNetView NView => _ship != null && _nviewField != null ? _nviewField.GetValue(_ship) as ZNetView : null;

        private Rigidbody Body => _ship != null && _bodyField != null ? _bodyField.GetValue(_ship) as Rigidbody : null;

        // Character.IsSwimming exists in the real game but not in the compile-time libs, so look it up at runtime.
        private static Func<Character, bool> _isSwimming;
        private static bool _swimLookupDone;

        private static bool IsSwimming(Character character)
        {
            if (!_swimLookupDone)
            {
                _swimLookupDone = true;
                try
                {
                    var method = AccessTools.Method(typeof(Character), "IsSwimming");
                    if (method != null)
                    {
                        _isSwimming = (Func<Character, bool>)Delegate.CreateDelegate(typeof(Func<Character, bool>), method);
                    }
                }
                catch (Exception e)
                {
                    Plugin.Log.LogError($"PushBoat: cannot look up Character.IsSwimming: {e.Message}");
                }
            }

            return _isSwimming != null && _isSwimming(character);
        }

        private bool CanPush(Humanoid user)
        {
            return Plugin.PushBoatEnabled.Value
                && _ship != null
                && user != null
                && user.GetStandingOnShip() != _ship
                && !IsSwimming(user);
        }

        public string GetHoverText()
        {
            if (!_loggedHover)
            {
                _loggedHover = true;
                Plugin.Log.LogInfo($"PushBoat: hover text requested (enabled={Plugin.PushBoatEnabled.Value})");
            }

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

        // Added to Hoverable in newer game versions. Extra reach on top of the default 5 m interact distance.
        public virtual float GetHoverOffset()
        {
            return 0f;
        }

        public bool Interact(Humanoid user, bool hold, bool alt)
        {
            if (!CanPush(user))
            {
                return false;
            }

            // Physics is simulated by the object's owner; without ownership the force is ignored.
            var nview = NView;
            if (nview != null && nview.IsValid() && !nview.IsOwner())
            {
                nview.ClaimOwnership();
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

            // Holding E repeats this call with hold == true; returning false then avoids replaying the interact animation.
            return !hold;
        }

        public bool UseItem(Humanoid user, ItemDrop.ItemData item)
        {
            return false;
        }

        private void FixedUpdate()
        {
            if (Time.time >= _pushUntil)
            {
                return;
            }

            var body = Body;
            if (body == null)
            {
                return;
            }

            var nview = NView;
            if (nview != null && nview.IsValid() && !nview.IsOwner())
            {
                return;
            }

            // Acceleration mode ignores mass, so the slider means the same thing for every boat.
            body.AddForce(_pushDirection * PushStrength, ForceMode.Acceleration);
        }
    }
}
