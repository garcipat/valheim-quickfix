using System;
using HarmonyLib;
using UnityEngine;

namespace ValheimQuickFix.Patches
{
    /// <summary>
    /// Stands a smaller Lox at the main menu campfire, left of the fire as the menu camera sees it, and moves the
    /// character selection camera back so the Lox stays in that shot too.
    /// Creatures are not part of the menu scene, but the Lox prefab is already in memory there: the menu scene's asset
    /// bundle depends on the shared bundle that holds it. If it is not, the loaded bundles are searched for it. Only the prefab's
    /// <c>Visual</c> child is copied, under an inactive holder so no game script on it ever wakes up; the game scripts
    /// and colliders are stripped before it is shown, leaving the animator to play its idle loop. The copy lives in the
    /// menu scene and goes away with it.
    /// </summary>
    [HarmonyPatch(typeof(FejdStartup), "Start")]
    internal static class MenuLoxPatch
    {
        private const string LoxPath = "assets/characters/lox/lox.prefab";
        // Metres from the campfire, seen from the menu camera: to the left, and further away from the camera.
        private const float SideOffset = 4.5f;
        private const float DepthOffset = 2f;
        // A full-size Lox fills the menu shot, so it stands a bit smaller.
        private const float LoxScale = 0.7f;
        private const float FireSearchRadius = 15f;
        // Metres the character selection camera moves back, so the Lox stays in view next to the character.
        private const float CharacterCameraPullBack = 2f;

        private static void Postfix(FejdStartup __instance)
        {
            try
            {
                if (!Plugin.MenuLoxEnabled.Value)
                {
                    return;
                }

                var prefab = FindLoxPrefab();
                if (prefab == null)
                {
                    Plugin.Log.LogWarning($"MenuLox: no loaded asset bundle holds {LoxPath}; the menu stays as it is");
                    return;
                }

                Spawn(prefab, __instance.m_characterPreviewPoint, __instance.m_cameraMarkerMain);
                PullBack(__instance.m_cameraMarkerCharacter);
            }
            catch (Exception e)
            {
                Plugin.Log.LogError($"MenuLox patch failed: {e}");
            }
        }

        private static GameObject FindLoxPrefab()
        {
            foreach (var loaded in Resources.FindObjectsOfTypeAll<GameObject>())
            {
                if (loaded.name == "Lox" && !loaded.scene.IsValid())
                {
                    return loaded;
                }
            }

            foreach (var bundle in AssetBundle.GetAllLoadedAssetBundles())
            {
                if (bundle == null || bundle.isStreamedSceneAssetBundle)
                {
                    continue;
                }

                foreach (var name in bundle.GetAllAssetNames())
                {
                    if (string.Equals(name, LoxPath, StringComparison.OrdinalIgnoreCase))
                    {
                        return bundle.LoadAsset<GameObject>(name);
                    }
                }
            }

            return null;
        }

        private static void Spawn(GameObject prefab, Transform anchor, Transform cameraMarker)
        {
            var left = -cameraMarker.right;
            left.y = 0f;
            left.Normalize();
            var away = cameraMarker.forward;
            away.y = 0f;
            away.Normalize();

            // The campfire is the reference; without one, fall back to the character.
            var fire = FindFire(anchor.position);
            var centre = fire ?? anchor.position;
            var position = centre + left * SideOffset + away * DepthOffset;
            position.y = anchor.position.y;
            if (Physics.Raycast(position + Vector3.up * 5f, Vector3.down, out var hit, 20f))
            {
                position.y = hit.point.y;
            }

            var facing = centre - position;
            facing.y = 0f;

            var holder = new GameObject("QuickFix_MenuLox");
            holder.SetActive(false);
            holder.transform.position = position;
            holder.transform.localScale = Vector3.one * LoxScale;
            holder.transform.rotation = facing.sqrMagnitude > 0.01f ? Quaternion.LookRotation(facing) : anchor.rotation;

            var source = prefab.transform.Find("Visual");
            if (source == null)
            {
                source = prefab.transform;
            }

            var visual = UnityEngine.Object.Instantiate(source.gameObject, holder.transform, false);
            visual.transform.localPosition = Vector3.zero;
            visual.transform.localRotation = Quaternion.identity;
            visual.transform.localScale = source.lossyScale;
            Strip(visual);

            foreach (var animator in visual.GetComponentsInChildren<Animator>(true))
            {
                animator.applyRootMotion = false;
                animator.updateMode = AnimatorUpdateMode.Normal;
                animator.cullingMode = AnimatorCullingMode.AlwaysAnimate;
            }

            holder.SetActive(true);
            Plugin.Log.LogDebug($"MenuLox: placed at {position}, character at {anchor.position}, fire at {(fire.HasValue ? fire.Value.ToString() : "not found")}, camera at {cameraMarker.position}");
        }

        /// <summary>
        /// The character selection camera sits close to the character; the marker is a menu scene object, so moving
        /// it back lasts only as long as this menu.
        /// </summary>
        private static void PullBack(Transform marker)
        {
            marker.position -= marker.forward * CharacterCameraPullBack;
        }

        /// <summary>Removes the game's own scripts and all physics, which expect a live character around them.</summary>
        private static void Strip(GameObject visual)
        {
            foreach (var behaviour in visual.GetComponentsInChildren<MonoBehaviour>(true))
            {
                UnityEngine.Object.DestroyImmediate(behaviour);
            }

            foreach (var collider in visual.GetComponentsInChildren<Collider>(true))
            {
                UnityEngine.Object.DestroyImmediate(collider);
            }

            foreach (var body in visual.GetComponentsInChildren<Rigidbody>(true))
            {
                UnityEngine.Object.DestroyImmediate(body);
            }
        }

        /// <summary>The campfire is the nearest point light to the character; null if the scene has none nearby.</summary>
        private static Vector3? FindFire(Vector3 near)
        {
            Vector3? best = null;
            var bestDistance = FireSearchRadius;
            foreach (var light in UnityEngine.Object.FindObjectsOfType<Light>())
            {
                if (light.type != LightType.Point)
                {
                    continue;
                }

                var distance = Vector3.Distance(light.transform.position, near);
                if (distance < bestDistance)
                {
                    bestDistance = distance;
                    best = light.transform.position;
                }
            }

            return best;
        }
    }
}
