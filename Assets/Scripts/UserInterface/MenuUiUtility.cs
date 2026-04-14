using System;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace Sol.HUD
{
    internal static class MenuUiUtility
    {
        public static Transform FindDeep(Transform parent, string name)
        {
            if (parent == null)
                return null;

            if (string.Equals(parent.name, name, StringComparison.Ordinal))
                return parent;

            for (int i = 0; i < parent.childCount; i++)
            {
                Transform result = FindDeep(parent.GetChild(i), name);
                if (result != null)
                    return result;
            }

            return null;
        }

        public static T FindDeepComponent<T>(Transform parent, string name) where T : Component
        {
            return FindDeep(parent, name)?.GetComponent<T>();
        }

        public static Button FindButtonByNames(Transform root, params string[] names)
        {
            for (int i = 0; i < names.Length; i++)
            {
                Button button = FindDeepComponent<Button>(root, names[i]);
                if (button != null)
                    return button;
            }

            return null;
        }

        public static TMP_Text FindTextByNames(Transform root, params string[] names)
        {
            for (int i = 0; i < names.Length; i++)
            {
                TMP_Text text = FindDeepComponent<TMP_Text>(root, names[i]);
                if (text != null)
                    return text;
            }

            return null;
        }

        public static RectTransform FindRectByNames(Transform root, params string[] names)
        {
            for (int i = 0; i < names.Length; i++)
            {
                RectTransform rect = FindDeep(root, names[i]) as RectTransform;
                if (rect != null)
                    return rect;
            }

            return null;
        }

        public static void WireButton(Button button, UnityEngine.Events.UnityAction action)
        {
            if (button == null || action == null)
                return;

            button.onClick.RemoveListener(action);
            button.onClick.AddListener(action);
        }

        public static void SetVisible(GameObject target, bool visible)
        {
            if (target != null && target.activeSelf != visible)
                target.SetActive(visible);
        }

        public static CanvasGroup EnsureCanvasGroup(GameObject target)
        {
            if (target == null)
                return null;

            CanvasGroup group = target.GetComponent<CanvasGroup>();
            return group != null ? group : target.AddComponent<CanvasGroup>();
        }

        public static void SetCanvasGroupVisible(CanvasGroup group, bool visible)
        {
            if (group == null)
                return;

            group.alpha = visible ? 1f : 0f;
            group.interactable = visible;
            group.blocksRaycasts = visible;
        }

        public static void BringToFront(Transform target)
        {
            if (target == null)
                return;

            target.SetAsLastSibling();
        }

        public static void SelectButton(Button button)
        {
            if (button == null || button.gameObject == null || !button.gameObject.activeInHierarchy)
                return;

            EventSystem current = EventSystem.current;
            if (current == null)
                return;

            current.SetSelectedGameObject(null);
            current.SetSelectedGameObject(button.gameObject);
        }

        public static void SetBackgroundUiRaycasts(Transform modalRoot, bool enabled)
        {
            Transform canvasRoot = FindCanvasRoot(modalRoot);
            if (canvasRoot == null)
                return;

            SetNamedRootRaycasts(canvasRoot, "UI_HUD", enabled);
            SetNamedRootRaycasts(canvasRoot, "UI_DataPanels", enabled);
        }

        private static Transform FindCanvasRoot(Transform current)
        {
            Transform cursor = current;
            while (cursor != null)
            {
                if (cursor.GetComponent<Canvas>() != null)
                    return cursor;

                cursor = cursor.parent;
            }

            return null;
        }

        private static void SetNamedRootRaycasts(Transform root, string childName, bool enabled)
        {
            Transform child = FindDeep(root, childName);
            if (child == null)
                return;

            CanvasGroup group = EnsureCanvasGroup(child.gameObject);
            group.blocksRaycasts = enabled;
            group.interactable = enabled;
        }
    }
}
