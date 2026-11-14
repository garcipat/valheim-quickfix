using System;
using HarmonyLib;
using UnityEngine;

namespace ValheimCampfireCompanion
{
    /// <summary>
    /// Stands the chosen creature at the main menu campfire, left of the fire as the menu camera sees it, and moves the
    /// character selection camera back so it stays in that shot too.
    /// Creatures are not part of the menu scene, but their prefabs are already in memory there: the menu scene's asset
    /// bundle depends on the shared bundle that holds them. If one is not, the loaded bundles are searched for it.
    /// Only the prefab's <c>Visual</c> child is copied, under an inactive holder so no game script on it ever wakes up;
    /// the game scripts and physics are stripped before it is shown, leaving the animator to play its idle loop. The
    /// copy lives in the menu scene and goes away with it.
    /// </summary>
    internal static class CompanionSpawner
    {
        // Metres the creature stands further from the menu camera than the character, so it is beside the fire, not in
        // front of it.
        private const float DepthOffset = 0.91f;
        // Where the campfire burns, from the character's spot: to the left, and a little toward the camera.
        private const float FireLeft = 2.12f;
        private const float FireTowardCamera = 0.15f;
        // Metres the character selection camera moves back, so the companion stays in view next to the character.
        private const float CharacterCameraPullBack = 2f;

        private static GameObject _current;
        private static FejdStartup _pulledBackMenu;

        private static bool _pending;

        /// <summary>
        /// The menu's own setup is still running in <c>Start</c>, so the companion is placed on the next frame, once
        /// the scene stands as the camera will show it.
        /// </summary>
        internal static void OnMenuStart(FejdStartup menu)
        {
            _pending = true;
        }

        /// <summary>Called every frame by the plugin; places the companion one frame after the menu started.</summary>
        internal static void Tick()
        {
            if (!_pending)
            {
                return;
            }

            _pending = false;
            Refresh();
        }

        /// <summary>Swaps the companion right away when the setting changes while the menu is open.</summary>
        internal static void Refresh()
        {
            try
            {
                if (FejdStartup.instance != null)
                {
                    Show(FejdStartup.instance);
                }
            }
            catch (Exception e)
            {
                Plugin.Log.LogError($"Campfire companion failed: {e}");
            }
        }

        private static void Show(FejdStartup menu)
        {
            if (_current != null)
            {
                UnityEngine.Object.Destroy(_current);
                _current = null;
            }

            var setup = Companions.Get(Plugin.Choice.Value);
            if (setup == null)
            {
                return;
            }

            var prefab = FindPrefab(setup.Prefab);
            if (prefab == null)
            {
                Plugin.Log.LogWarning($"No creature prefab '{setup.Prefab}' is loaded in the menu; the menu stays as it is");
                return;
            }

            _current = Spawn(prefab, setup, Plugin.Stars.Value, menu.m_characterPreviewPoint, menu.m_cameraMarkerMain);
            if (_pulledBackMenu != menu)
            {
                _pulledBackMenu = menu;
                var marker = menu.m_cameraMarkerCharacter;
                marker.position -= marker.forward * CharacterCameraPullBack;
            }
        }

        private static GameObject FindPrefab(string name)
        {
            foreach (var loaded in Resources.FindObjectsOfTypeAll<GameObject>())
            {
                if (loaded.name == name && !loaded.scene.IsValid() && loaded.transform.parent == null)
                {
                    return loaded;
                }
            }

            var suffix = "/" + name.ToLowerInvariant() + ".prefab";
            foreach (var bundle in AssetBundle.GetAllLoadedAssetBundles())
            {
                if (bundle == null || bundle.isStreamedSceneAssetBundle)
                {
                    continue;
                }

                foreach (var path in bundle.GetAllAssetNames())
                {
                    if (path.StartsWith("assets/characters/", StringComparison.Ordinal) && path.EndsWith(suffix, StringComparison.Ordinal))
                    {
                        return bundle.LoadAsset<GameObject>(path);
                    }
                }
            }

            return null;
        }

        private static GameObject Spawn(GameObject prefab, CompanionSetup setup, int stars, Transform anchor, Transform cameraMarker)
        {
            var left = -cameraMarker.right;
            left.y = 0f;
            left.Normalize();
            var away = cameraMarker.forward;
            away.y = 0f;
            away.Normalize();

            // The character's spot is the reference: it is fixed in the menu scene and doesn't change with what the
            // character wears. (The nearest point light is not a safe stand-in for the fire: glowing gear carries one.)
            var centre = anchor.position;

            var holder = new GameObject("CampfireCompanion");
            holder.SetActive(false);
            holder.transform.position = centre;
            // Standing left of the fire, looking right at it.
            holder.transform.rotation = Quaternion.LookRotation(-left) * Quaternion.Euler(0f, setup.Turn, 0f);

            var source = prefab.transform.Find("Visual");
            if (source == null)
            {
                source = prefab.transform;
            }

            var visual = UnityEngine.Object.Instantiate(source.gameObject, holder.transform, false);
            visual.transform.localPosition = Vector3.zero;
            visual.transform.localRotation = Quaternion.identity;
            visual.transform.localScale = source.lossyScale;
            ApplyStars(source, visual, stars);
            Strip(visual);

            foreach (var animator in visual.GetComponentsInChildren<Animator>(true))
            {
                animator.applyRootMotion = false;
                animator.updateMode = AnimatorUpdateMode.Normal;
                animator.cullingMode = AnimatorCullingMode.AlwaysAnimate;
            }

            holder.AddComponent<KeepIdle>();
            holder.SetActive(true);

            // Renderer bounds are only known once active; the gap is kept between the fire and the creature's side.
            var bounds = Bounds(visual);
            var scale = setup.Scale;
            var gap = setup.Gap;
            holder.transform.localScale = Vector3.one * scale;
            var halfLength = Mathf.Max(bounds.extents.x, bounds.extents.z) * scale;

            var position = centre + left * (gap + halfLength) + away * DepthOffset;
            position.y = anchor.position.y;
            var ground = "nothing";
            if (Physics.Raycast(position + Vector3.up * 5f, Vector3.down, out var hit, 20f))
            {
                position.y = hit.point.y;
                ground = $"{hit.collider.name} (layer {LayerMask.LayerToName(hit.collider.gameObject.layer)})";
            }

            holder.transform.position = position;

            // Look at the fire itself, which is a little nearer the camera than the creature.
            var fire = centre + left * FireLeft - away * FireTowardCamera;
            var facing = fire - position;
            facing.y = 0f;
            if (facing.sqrMagnitude > 0.01f)
            {
                holder.transform.rotation = Quaternion.LookRotation(facing) * Quaternion.Euler(0f, setup.Turn, 0f);
            }
            Plugin.Log.LogDebug($"{prefab.name}: {bounds.size.y:0.00} m tall, half length {halfLength:0.00} m; scale {scale:0.00}, gap {gap:0.00} m; at {position} on {ground}");
            return holder;
        }

        /// <summary>
        /// What the creature's <c>LevelEffects</c> does for a starred creature: a size step, a recoloured main material,
        /// and swapped parts. The component can sit on the prefab's root, outside the copied <c>Visual</c>, so it is
        /// read from the prefab and the objects it names are looked up in the copy by their path below the source.
        /// </summary>
        private static void ApplyStars(Transform source, GameObject visual, int stars)
        {
            if (stars <= 0)
            {
                return;
            }

            var effects = source.root.GetComponentInChildren<LevelEffects>(true);
            if (effects == null || effects.m_levelSetups.Count < stars)
            {
                Plugin.Log.LogInfo($"{source.root.name} has no {stars}-star look");
                return;
            }

            var setup = effects.m_levelSetups[stars - 1];
            var scaled = InCopy(source, visual, effects.transform) ?? visual.transform;
            scaled.localScale *= setup.m_scale;

            var main = InCopy(source, visual, effects.m_mainRender != null ? effects.m_mainRender.transform : null);
            var renderer = main != null ? main.GetComponent<Renderer>() : null;
            if (renderer != null)
            {
                var materials = renderer.sharedMaterials;
                materials[0] = new Material(materials[0]);
                materials[0].SetFloat("_Hue", setup.m_hue);
                materials[0].SetFloat("_Saturation", setup.m_saturation);
                materials[0].SetFloat("_Value", setup.m_value);
                // Newer than the compile-time game libraries, so read by name.
                var newer = Traverse.Create(setup);
                if (newer.Field("m_setEmissiveColor").FieldExists() && newer.Field<bool>("m_setEmissiveColor").Value)
                {
                    materials[0].SetColor("_EmissionColor", newer.Field<Color>("m_emissiveColor").Value);
                }

                renderer.sharedMaterials = materials;
            }

            var baseObject = InCopy(source, visual, effects.m_baseEnableObject != null ? effects.m_baseEnableObject.transform : null);
            if (baseObject != null)
            {
                baseObject.gameObject.SetActive(false);
            }

            var starObject = InCopy(source, visual, setup.m_enableObject != null ? setup.m_enableObject.transform : null);
            if (starObject != null)
            {
                starObject.gameObject.SetActive(true);
            }
        }

        /// <summary>The copy's counterpart of a transform below <paramref name="source"/>; null if it is not below it.</summary>
        private static Transform InCopy(Transform source, GameObject visual, Transform original)
        {
            if (original == null)
            {
                return null;
            }

            var path = "";
            for (var t = original; t != source; t = t.parent)
            {
                if (t == null)
                {
                    return null;
                }

                path = path.Length == 0 ? t.name : t.name + "/" + path;
            }

            return path.Length == 0 ? visual.transform : visual.transform.Find(path);
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

        private static Bounds Bounds(GameObject visual)
        {
            var bounds = new Bounds(visual.transform.position, Vector3.zero);
            foreach (var renderer in visual.GetComponentsInChildren<Renderer>())
            {
                if (renderer is SkinnedMeshRenderer || renderer is MeshRenderer)
                {
                    bounds.Encapsulate(renderer.bounds);
                }
            }

            return bounds;
        }

    }
}
