using System;
using System.Reflection;
using HarmonyLib;
using UnityEngine;
using UnityEngine.Events;

namespace ValheimMapRoutes
{
    /// <summary>
    /// UnityEngine.UI is not available as a package (see docs/Architecture.md), so Image, RawImage and Button are
    /// reached by name.
    /// </summary>
    internal static class Ui
    {
        internal static readonly Type ImageType = AccessTools.TypeByName("UnityEngine.UI.Image");
        private static readonly Type ButtonType = AccessTools.TypeByName("UnityEngine.UI.Button");
        private static readonly Type LayoutGroupType = AccessTools.TypeByName("UnityEngine.UI.LayoutGroup");

        private static readonly PropertyInfo SpriteProperty = AccessTools.Property(ImageType, "sprite");
        private static readonly PropertyInfo PreserveAspectProperty = AccessTools.Property(ImageType, "preserveAspect");
        private static readonly PropertyInfo ColorProperty = AccessTools.Property(AccessTools.TypeByName("UnityEngine.UI.Graphic"), "color");
        private static readonly PropertyInfo RaycastTargetProperty = AccessTools.Property(AccessTools.TypeByName("UnityEngine.UI.Graphic"), "raycastTarget");
        private static readonly PropertyInfo UvRectProperty = AccessTools.Property(AccessTools.TypeByName("UnityEngine.UI.RawImage"), "uvRect");
        private static readonly PropertyInfo OnClickProperty = AccessTools.Property(ButtonType, "onClick");

        internal static bool Available => ImageType != null && ButtonType != null && SpriteProperty != null
            && ColorProperty != null && RaycastTargetProperty != null && UvRectProperty != null && OnClickProperty != null;

        internal static Behaviour GetImage(Component component)
        {
            return component == null ? null : component.GetComponent(ImageType) as Behaviour;
        }

        internal static Behaviour AddImage(GameObject go)
        {
            return go.AddComponent(ImageType) as Behaviour;
        }

        internal static Sprite GetSprite(Behaviour image)
        {
            return SpriteProperty.GetValue(image, null) as Sprite;
        }

        internal static void SetSprite(Behaviour image, Sprite sprite, bool preserveAspect = false)
        {
            SpriteProperty.SetValue(image, sprite, null);
            PreserveAspectProperty?.SetValue(image, preserveAspect, null);
        }

        internal static Color GetColor(Behaviour graphic)
        {
            return (Color)ColorProperty.GetValue(graphic, null);
        }

        internal static void SetColor(Behaviour graphic, Color color)
        {
            ColorProperty.SetValue(graphic, color, null);
        }

        internal static void SetRaycastTarget(Behaviour graphic, bool value)
        {
            RaycastTargetProperty.SetValue(graphic, value, null);
        }

        internal static Rect UvRect(Component rawImage)
        {
            return (Rect)UvRectProperty.GetValue(rawImage, null);
        }

        internal static bool HasLayoutGroup(Transform transform)
        {
            return LayoutGroupType != null && transform.GetComponent(LayoutGroupType) != null;
        }

        internal static void OnClick(GameObject go, UnityAction action)
        {
            var button = go.GetComponent(ButtonType) ?? go.AddComponent(ButtonType);
            ((UnityEvent)OnClickProperty.GetValue(button, null)).AddListener(action);
        }
    }
}
