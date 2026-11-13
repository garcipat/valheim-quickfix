using System.Collections.Generic;
using UnityEngine;

namespace ValheimSignIcons.Patches
{
    /// <summary>
    /// Draws a sign that shows a single icon at half size, so it sits neatly on a chest. Only the look changes, and
    /// only on this client: the board is drawn by smaller copies of its meshes (the originals are hidden, not scaled),
    /// so the colliders, structure support, and what other players see all stay the same.
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

            // Everything shrinks toward the middle of the board, so the sign stays where it was placed.
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
                copy.transform.position = pivot + (original.transform.position - pivot) * Scale;
                copy.transform.localScale = Vector3.one * Scale;
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

            if (canvas != null)
            {
                state.Canvas = canvas.transform;
                state.CanvasPosition = canvas.transform.position;
                state.CanvasScale = canvas.transform.localScale;
                canvas.transform.position = pivot + (canvas.transform.position - pivot) * Scale;
                canvas.transform.localScale = state.CanvasScale * Scale;
            }

            state.Shrunk = true;
        }
    }

    public class SignShrinkState : MonoBehaviour
    {
        internal bool Shrunk;
        internal readonly List<GameObject> Copies = new List<GameObject>();
        internal readonly List<MeshRenderer> Hidden = new List<MeshRenderer>();
        internal Transform Canvas;
        internal Vector3 CanvasPosition;
        internal Vector3 CanvasScale;

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

            if (Canvas != null)
            {
                Canvas.position = CanvasPosition;
                Canvas.localScale = CanvasScale;
            }

            Copies.Clear();
            Hidden.Clear();
            Canvas = null;
            Shrunk = false;
        }
    }
}
