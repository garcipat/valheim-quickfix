using System.Collections.Generic;
using UnityEngine;

namespace ValheimSignIcons.Patches
{
    /// <summary>
    /// Draws a sign that shows a single icon at half width (full height), so it sits neatly on a chest. Only the look
    /// changes, and only on this client: the board is drawn by narrower copies of its meshes (the originals are hidden,
    /// not scaled), so the colliders, structure support, and what other players see all stay the same. The icon itself
    /// is not scaled, so it stays square; <see cref="SignIconRenderer"/> fits it into the narrower width.
    /// </summary>
    internal static class SignShrink
    {
        internal const float Scale = 0.5f;

        internal static void Apply(Sign sign, Component widget, bool shrink)
        {
            var state = sign.GetComponent<SignShrinkState>();
            if (!shrink)
            {
                state?.Restore();
                return;
            }

            if (state != null && state.Shrunk)
            {
                return;
            }

            var canvas = widget.GetComponentInParent<Canvas>();

            // Width runs along the sign's right axis, the same axis its text runs along.
            var width = sign.transform.right;
            var originals = new List<MeshRenderer>();
            var bounds = default(Bounds);
            var haveBounds = false;
            foreach (var renderer in sign.GetComponentsInChildren<MeshRenderer>(true))
            {
                if (canvas != null && renderer.transform.IsChildOf(canvas.transform))
                {
                    continue;
                }

                originals.Add(renderer);
                if (renderer.enabled && renderer.gameObject.activeInHierarchy)
                {
                    if (haveBounds)
                    {
                        bounds.Encapsulate(renderer.bounds);
                    }
                    else
                    {
                        bounds = renderer.bounds;
                        haveBounds = true;
                    }
                }
            }

            if (!haveBounds)
            {
                return;
            }

            if (state == null)
            {
                state = sign.gameObject.AddComponent<SignShrinkState>();
            }

            // The board narrows toward its middle, so the sign stays where it was placed.
            var pivot = bounds.center;
            foreach (var original in originals)
            {
                var filter = original.GetComponent<MeshFilter>();
                if (filter == null || filter.sharedMesh == null)
                {
                    continue;
                }

                // A child of the original, so it follows the original when the game swaps the new/worn/broken models.
                var copy = new GameObject("VQFSmallSign");
                copy.layer = original.gameObject.layer;
                copy.transform.SetParent(original.transform, false);
                var offset = original.transform.position - pivot;
                copy.transform.position = original.transform.position - width * (Vector3.Dot(offset, width) * (1f - Scale));
                copy.transform.localScale = LocalWidthScale(original.transform, width);
                copy.AddComponent<MeshFilter>().sharedMesh = filter.sharedMesh;
                var renderer = copy.AddComponent<MeshRenderer>();
                renderer.sharedMaterials = original.sharedMaterials;
                renderer.shadowCastingMode = original.shadowCastingMode;
                renderer.receiveShadows = original.receiveShadows;

                state.Copies.Add(copy);
                if (original.enabled)
                {
                    original.enabled = false;
                    state.Hidden.Add(original);
                }
            }

            state.Shrunk = true;
        }

        // Scale for a child of <paramref name="parent"/> that shrinks the local axis closest to the world direction
        // <paramref name="width"/>, leaving the other two.
        private static Vector3 LocalWidthScale(Transform parent, Vector3 width)
        {
            var local = parent.InverseTransformDirection(width);
            var x = Mathf.Abs(local.x);
            var y = Mathf.Abs(local.y);
            var z = Mathf.Abs(local.z);
            if (x >= y && x >= z)
            {
                return new Vector3(Scale, 1f, 1f);
            }

            return y >= z ? new Vector3(1f, Scale, 1f) : new Vector3(1f, 1f, Scale);
        }
    }

    public class SignShrinkState : MonoBehaviour
    {
        internal bool Shrunk;
        internal readonly List<GameObject> Copies = new List<GameObject>();
        internal readonly List<MeshRenderer> Hidden = new List<MeshRenderer>();

        internal void Restore()
        {
            if (!Shrunk)
            {
                return;
            }

            foreach (var copy in Copies)
            {
                if (copy != null)
                {
                    Destroy(copy);
                }
            }

            foreach (var renderer in Hidden)
            {
                if (renderer != null)
                {
                    renderer.enabled = true;
                }
            }

            Copies.Clear();
            Hidden.Clear();
            Shrunk = false;
        }
    }
}
