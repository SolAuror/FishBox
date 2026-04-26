using System;
using System.Collections.Generic;
using TMPro;
using Sol.Grab;
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

            return target.GetComponent<CanvasGroup>();
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

        public static bool IsDescendantOf(Transform child, Transform ancestor)
        {
            if (child == null || ancestor == null)
                return false;

            Transform cursor = child;
            while (cursor != null)
            {
                if (cursor == ancestor)
                    return true;

                cursor = cursor.parent;
            }

            return false;
        }

        public static Graphic EnsureRaycastGraphic(Button button)
        {
            if (button == null)
                return null;

            Graphic graphic = button.targetGraphic;
            if (graphic == null || graphic.gameObject != button.gameObject)
                graphic = button.GetComponent<Graphic>();

            return graphic;
        }

        public static void MakeButtonClickable(Button button)
        {
            if (button == null)
                return;

            Graphic graphic = EnsureRaycastGraphic(button);
            if (graphic != null)
            {
                button.targetGraphic = graphic;
                graphic.raycastTarget = true;
            }

            if (graphic != null)
            {
                TMP_Text[] textChildren = button.GetComponentsInChildren<TMP_Text>(true);
                for (int i = 0; i < textChildren.Length; i++)
                    textChildren[i].raycastTarget = textChildren[i] == graphic;
            }
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
                Canvas canvas = cursor.GetComponent<Canvas>();
                if (canvas != null)
                    return canvas.rootCanvas != null ? canvas.rootCanvas.transform : canvas.transform;

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
            if (group == null)
                return;

            group.blocksRaycasts = enabled;
            group.interactable = enabled;
        }
    }

    public static class ItemPresentationUtility
    {
        public static string BuildDisplayName(ItemComponent item, bool showReferenceCodes)
        {
            if (item == null)
                return string.Empty;

            if (showReferenceCodes && !string.IsNullOrWhiteSpace(item.ItemId))
                return $"{item.ItemName} [{item.ItemId}]";

            return item.ItemName;
        }

        public static string BuildTypeDisplayText(ItemComponent item)
        {
            return item == null ? string.Empty : item.TypeDisplayName;
        }

        public static string GetItemIdCode(ItemComponent item)
        {
            if (item == null)
                return string.Empty;

            CaughtFishItem caughtFish = item.GetComponent<CaughtFishItem>();
            if (caughtFish != null && !string.IsNullOrWhiteSpace(caughtFish.FishCode))
                return caughtFish.FishCode;

            if (!string.IsNullOrWhiteSpace(item.ItemId))
                return item.ItemId;

            return string.Empty;
        }

        public static bool ShouldShowStolenIndicator(ItemComponent item)
        {
            return item != null && item.IsStolen;
        }

        public static string BuildDetailedStatsText(ItemComponent item, bool showReferenceCodes, bool includeCurrencySuffix)
        {
            if (item == null)
                return string.Empty;

            List<string> stats = new(4);
            if (item.Damage > 0f)
                stats.Add($"Damage: {item.Damage:0.#}");
            if (item.Defense > 0f)
                stats.Add($"Defense: {item.Defense:0.#}");
            if (item.Value > 0)
                stats.Add(includeCurrencySuffix ? $"Value: {item.Value} g" : $"Value: {item.Value}");
            if (showReferenceCodes && !string.IsNullOrWhiteSpace(item.ItemOwnerId))
                stats.Add($"Owner ID: {item.ItemOwnerId}");

            return string.Join("\n", stats);
        }

        public static string[] BuildCompactInventoryStatTexts(ItemComponent item)
        {
            List<string> values = new(3);
            if (item != null && item.Damage > 0f)
                values.Add($"{item.Damage:0.#} dmg");
            if (item != null && item.Defense > 0f)
                values.Add($"{item.Defense:0.#} def");

            while (values.Count < 3)
                values.Add(string.Empty);

            return values.ToArray();
        }

        public static string[] BuildCompactTradeStatTexts(ItemComponent item)
        {
            return new[]
            {
                string.Empty,
                string.Empty,
                item != null && item.Value > 0 ? $"{item.Value} g" : string.Empty
            };
        }
    }
}
