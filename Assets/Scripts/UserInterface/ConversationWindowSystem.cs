using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

namespace Sol.HUD
{
    public class ConversationWindowSystem : MonoBehaviour
    {
        [SerializeField] private Image _backdropImage;
        [SerializeField] private RectTransform _panelRoot;
        [SerializeField] private Image _panelImage;
        [SerializeField] private Image _icon;
        [SerializeField] private TextMeshProUGUI _titleText;
        [SerializeField] private TextMeshProUGUI _descriptionText;
        [SerializeField] private Button _confirmButton;
        [SerializeField] private RectTransform _optionsRoot;
        [SerializeField] private Button _optionTemplateButton;
        [SerializeField] private Button _cancelButton;

        public static ConversationWindowSystem Instance { get; private set; }
        public bool IsVisible => _panelRoot != null && _panelRoot.gameObject.activeSelf;

        private readonly List<Button> _spawnedOptionButtons = new();
        private Action<int> _onOptionSelected;
        private Action _onClosed;

        private void Awake()
        {
            if (Instance != null && Instance != this) { Destroy(gameObject); return; }
            Instance = this;
            AutoWire();
            EnsureLayout();
            SetWindowVisible(false);
        }

        private void OnDestroy()
        {
            if (_cancelButton != null) _cancelButton.onClick.RemoveListener(Hide);
            if (Instance == this) Instance = null;
        }

        private void Update()
        {
            if (IsVisible && Keyboard.current != null && Keyboard.current.escapeKey.wasPressedThisFrame)
                Hide();
        }

        public static ConversationWindowSystem ResolveInstance(bool createIfMissing = false)
        {
            if (Instance != null) return Instance;
            ConversationWindowSystem[] found = UnityEngine.Object.FindObjectsByType<ConversationWindowSystem>(FindObjectsInactive.Include, FindObjectsSortMode.None);
            if (found != null && found.Length > 0) return found[0];
            if (!createIfMissing) return null;

            Canvas canvas = FindCanvasHost();
            GameObject host;
            if (canvas != null)
            {
                host = new GameObject("ConversationWindowSystem", typeof(RectTransform));
                RectTransform rect = host.GetComponent<RectTransform>();
                rect.SetParent(canvas.transform, false);
                rect.SetAsLastSibling();
            }
            else
            {
                host = CreateCanvasHost();
            }

            return host.AddComponent<ConversationWindowSystem>();
        }

        public void ShowConversation(string speakerName, string dialogueLine, IReadOnlyList<string> options, Action<int> onOptionSelected, Action onClosed = null, Sprite speakerIcon = null)
        {
            AutoWire();
            EnsureLayout();
            UIStateOwnership.CloseConflictingUi(nameof(ConversationWindowSystem));

            _onOptionSelected = onOptionSelected;
            _onClosed = onClosed;

            if (_titleText != null) _titleText.text = string.IsNullOrWhiteSpace(speakerName) ? "Conversation" : speakerName;
            if (_descriptionText != null) _descriptionText.text = string.IsNullOrWhiteSpace(dialogueLine) ? string.Empty : dialogueLine;
            if (_icon != null)
            {
                bool showIcon = speakerIcon != null;
                _icon.gameObject.SetActive(showIcon);
                if (showIcon) _icon.sprite = speakerIcon;
            }

            RebuildOptions(options);
            if (transform is RectTransform hostRect) hostRect.SetAsLastSibling();
            SetWindowVisible(true);
            MenuUiUtility.BringToFront(transform.parent);
            MenuUiUtility.BringToFront(transform);
            MenuUiUtility.SetBackgroundUiRaycasts(transform, false);
            UIStateOwnership.SetUiCapture(true);
            if (_spawnedOptionButtons.Count > 0)
                MenuUiUtility.SelectButton(_spawnedOptionButtons[0]);
            else
                MenuUiUtility.SelectButton(_cancelButton);
        }

        public void Hide()
        {
            if (!IsVisible) return;
            SetWindowVisible(false);
            ClearSpawnedOptions();
            MenuUiUtility.SetBackgroundUiRaycasts(transform, true);
            UIStateOwnership.SetUiCapture(false);
            Action closed = _onClosed;
            _onOptionSelected = null;
            _onClosed = null;
            closed?.Invoke();
        }

        private void RebuildOptions(IReadOnlyList<string> options)
        {
            EnsureLayout();
            ClearSpawnedOptions();
            if (_optionTemplateButton == null || _optionsRoot == null) return;

            int count = options != null ? options.Count : 0;
            if (count <= 0) { SpawnOption(0, "Continue"); return; }
            for (int i = 0; i < count; i++) SpawnOption(i, string.IsNullOrWhiteSpace(options[i]) ? "..." : options[i]);
        }

        private void SpawnOption(int index, string label)
        {
            Button button = Instantiate(_optionTemplateButton, _optionsRoot);
            button.gameObject.SetActive(true);
            button.name = $"Option_{index + 1:00}";
            button.onClick.RemoveAllListeners();
            button.onClick.AddListener(() => _onOptionSelected?.Invoke(index));
            StyleOptionButton(button);
            TextMeshProUGUI labelText = button.GetComponentInChildren<TextMeshProUGUI>(true);
            if (labelText != null) labelText.text = $"> {label}";
            _spawnedOptionButtons.Add(button);
        }

        private void ClearSpawnedOptions()
        {
            for (int i = 0; i < _spawnedOptionButtons.Count; i++) if (_spawnedOptionButtons[i] != null) Destroy(_spawnedOptionButtons[i].gameObject);
            _spawnedOptionButtons.Clear();
        }

        private void AutoWire()
        {
            if (_backdropImage == null) _backdropImage = FindDeep(transform, "Backdrop")?.GetComponent<Image>();
            if (_panelRoot == null) _panelRoot = FindDeep(transform, "Panel") as RectTransform;
            if (_panelImage == null && _panelRoot != null) _panelImage = _panelRoot.GetComponent<Image>();
            if (_icon == null) _icon = FindDeep(transform, "Icon")?.GetComponent<Image>();
            if (_titleText == null) _titleText = FindDeep(transform, "Title")?.GetComponent<TextMeshProUGUI>();
            if (_descriptionText == null) _descriptionText = FindDeep(transform, "Description")?.GetComponent<TextMeshProUGUI>();
            if (_confirmButton == null) _confirmButton = FindDeep(transform, "Confirm")?.GetComponent<Button>();
            if (_optionsRoot == null) _optionsRoot = FindDeep(transform, "OptionsList") as RectTransform;
            if (_optionTemplateButton == null) _optionTemplateButton = FindDeep(transform, "OptionTemplate")?.GetComponent<Button>();
            if (_cancelButton == null) _cancelButton = FindDeep(transform, "Cancel")?.GetComponent<Button>();
            if (_optionTemplateButton == null)
                _optionTemplateButton = _confirmButton;
        }

        private void EnsureLayout()
        {
            RectTransform host = EnsureHostRect();
            EnsureBackdrop(host);
            EnsurePanel(host);
            EnsureHeader(host);
            EnsureOptions();
            EnsureCancelButton();
        }

        private RectTransform EnsureHostRect()
        {
            RectTransform host = transform as RectTransform ?? gameObject.AddComponent<RectTransform>();
            host.anchorMin = Vector2.zero;
            host.anchorMax = Vector2.one;
            host.offsetMin = Vector2.zero;
            host.offsetMax = Vector2.zero;
            host.pivot = new Vector2(0.5f, 0.5f);
            return host;
        }

        private void EnsureBackdrop(RectTransform host)
        {
            if (_backdropImage == null)
            {
                GameObject go = new GameObject("Backdrop", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
                RectTransform rect = go.GetComponent<RectTransform>();
                rect.SetParent(host, false);
                rect.anchorMin = Vector2.zero;
                rect.anchorMax = Vector2.one;
                rect.offsetMin = Vector2.zero;
                rect.offsetMax = Vector2.zero;
                _backdropImage = go.GetComponent<Image>();
            }
            _backdropImage.color = new Color(0.02f, 0.03f, 0.05f, 0.72f);
            _backdropImage.raycastTarget = true;
        }

        private void EnsurePanel(RectTransform host)
        {
            if (_panelRoot == null)
            {
                GameObject go = new GameObject("Panel", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
                _panelRoot = go.GetComponent<RectTransform>();
                _panelRoot.SetParent(host, false);
            }

            _panelRoot.anchorMin = new Vector2(0.5f, 0.5f);
            _panelRoot.anchorMax = new Vector2(0.5f, 0.5f);
            _panelRoot.pivot = new Vector2(0.5f, 0.5f);
            _panelRoot.anchoredPosition = Vector2.zero;
            _panelRoot.sizeDelta = new Vector2(860f, 460f);
            _panelImage = _panelImage ?? _panelRoot.GetComponent<Image>() ?? _panelRoot.gameObject.AddComponent<Image>();
            _panelImage.color = new Color(0.08f, 0.08f, 0.1f, 0.96f);
            _panelImage.raycastTarget = true;
        }

        private void EnsureHeader(RectTransform parent)
        {
            if (_icon == null) _icon = EnsureImageChild(parent, "Icon", new Vector2(0.05f, 0.83f), new Vector2(0.14f, 0.94f));
            if (_titleText == null) _titleText = EnsureTextChild(parent, "Title", 32f, FontStyles.Bold, TextAlignmentOptions.Left, new Color(0.93f, 0.88f, 0.76f, 1f));
            if (_descriptionText == null) _descriptionText = EnsureTextChild(parent, "Description", 22f, FontStyles.Normal, TextAlignmentOptions.TopLeft, new Color(0.92f, 0.92f, 0.92f, 0.96f));

            RectTransform titleRect = _titleText.rectTransform;
            titleRect.anchorMin = new Vector2(0.17f, 0.86f);
            titleRect.anchorMax = new Vector2(0.92f, 0.95f);
            titleRect.offsetMin = Vector2.zero;
            titleRect.offsetMax = Vector2.zero;

            RectTransform descriptionRect = _descriptionText.rectTransform;
            descriptionRect.anchorMin = new Vector2(0.08f, 0.63f);
            descriptionRect.anchorMax = new Vector2(0.92f, 0.8f);
            descriptionRect.offsetMin = Vector2.zero;
            descriptionRect.offsetMax = Vector2.zero;
        }

        private void EnsureOptions()
        {
            if (_optionsRoot == null)
            {
                GameObject go = new GameObject("OptionsList", typeof(RectTransform), typeof(VerticalLayoutGroup), typeof(ContentSizeFitter));
                _optionsRoot = go.GetComponent<RectTransform>();
                _optionsRoot.SetParent(_panelRoot, false);
            }

            _optionsRoot.anchorMin = new Vector2(0.08f, 0.18f);
            _optionsRoot.anchorMax = new Vector2(0.92f, 0.56f);
            _optionsRoot.offsetMin = Vector2.zero;
            _optionsRoot.offsetMax = Vector2.zero;

            VerticalLayoutGroup layout = _optionsRoot.GetComponent<VerticalLayoutGroup>();
            layout.childAlignment = TextAnchor.UpperLeft;
            layout.childControlWidth = true;
            layout.childControlHeight = true;
            layout.childForceExpandWidth = true;
            layout.childForceExpandHeight = false;
            layout.spacing = 8f;
            layout.padding = new RectOffset(8, 8, 8, 8);

            ContentSizeFitter fitter = _optionsRoot.GetComponent<ContentSizeFitter>();
            fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;
            fitter.horizontalFit = ContentSizeFitter.FitMode.Unconstrained;

            if (_optionTemplateButton == null)
            {
                _optionTemplateButton = CreateButton(_optionsRoot, "OptionTemplate", "> Topic");
                StyleOptionButton(_optionTemplateButton);
            }

            _optionTemplateButton.transform.SetParent(_optionsRoot, false);
            _optionTemplateButton.gameObject.SetActive(false);
            _confirmButton = _optionTemplateButton;
        }

        private void EnsureCancelButton()
        {
            if (_cancelButton == null) _cancelButton = CreateButton(_panelRoot, "Cancel", "Goodbye");
            RectTransform rect = _cancelButton.GetComponent<RectTransform>();
            rect.anchorMin = new Vector2(0.73f, 0.06f);
            rect.anchorMax = new Vector2(0.92f, 0.13f);
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;
            _cancelButton.onClick.RemoveListener(Hide);
            _cancelButton.onClick.AddListener(Hide);
            TextMeshProUGUI label = _cancelButton.GetComponentInChildren<TextMeshProUGUI>(true);
            if (label != null) label.text = "Goodbye";
        }

        private void SetWindowVisible(bool visible)
        {
            if (_backdropImage != null) _backdropImage.gameObject.SetActive(visible);
            if (_panelRoot != null) _panelRoot.gameObject.SetActive(visible);
        }

        private static Canvas FindCanvasHost()
        {
            Canvas[] canvases = UnityEngine.Object.FindObjectsByType<Canvas>(FindObjectsInactive.Include, FindObjectsSortMode.None);
            if (canvases == null || canvases.Length == 0) return null;
            Canvas fallback = null;
            for (int i = 0; i < canvases.Length; i++)
            {
                Canvas canvas = canvases[i];
                if (canvas == null) continue;
                if (fallback == null) fallback = canvas;
                if (canvas.isRootCanvas && canvas.gameObject.activeInHierarchy) return canvas;
            }
            return fallback;
        }

        private static GameObject CreateCanvasHost()
        {
            GameObject host = new GameObject("ConversationCanvas", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
            Canvas canvas = host.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 250;
            CanvasScaler scaler = host.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);
            scaler.matchWidthOrHeight = 0.5f;
            return host;
        }

        private static Image EnsureImageChild(RectTransform parent, string name, Vector2 anchorMin, Vector2 anchorMax)
        {
            Transform existing = FindDeep(parent, name);
            RectTransform rect;
            Image image;
            if (existing != null) { rect = existing as RectTransform; image = existing.GetComponent<Image>() ?? existing.gameObject.AddComponent<Image>(); }
            else
            {
                GameObject go = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
                rect = go.GetComponent<RectTransform>();
                rect.SetParent(parent, false);
                image = go.GetComponent<Image>();
            }
            rect.anchorMin = anchorMin;
            rect.anchorMax = anchorMax;
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;
            image.raycastTarget = false;
            image.color = Color.white;
            return image;
        }

        private static TextMeshProUGUI EnsureTextChild(RectTransform parent, string name, float fontSize, FontStyles fontStyle, TextAlignmentOptions alignment, Color color)
        {
            Transform existing = FindDeep(parent, name);
            RectTransform rect;
            TextMeshProUGUI text;
            if (existing != null) { rect = existing as RectTransform; text = existing.GetComponent<TextMeshProUGUI>() ?? existing.gameObject.AddComponent<TextMeshProUGUI>(); }
            else
            {
                GameObject go = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(TextMeshProUGUI));
                rect = go.GetComponent<RectTransform>();
                rect.SetParent(parent, false);
                text = go.GetComponent<TextMeshProUGUI>();
            }
            text.fontSize = fontSize;
            text.fontStyle = fontStyle;
            text.alignment = alignment;
            text.color = color;
            text.raycastTarget = false;
            text.textWrappingMode = TextWrappingModes.Normal;
            return text;
        }

        private static Button CreateButton(RectTransform parent, string name, string label)
        {
            GameObject buttonGo = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image), typeof(Button), typeof(LayoutElement));
            RectTransform rect = buttonGo.GetComponent<RectTransform>();
            rect.SetParent(parent, false);
            rect.anchorMin = new Vector2(0f, 1f);
            rect.anchorMax = new Vector2(1f, 1f);
            rect.pivot = new Vector2(0.5f, 1f);
            rect.sizeDelta = new Vector2(0f, 42f);

            Image image = buttonGo.GetComponent<Image>();
            image.color = new Color(0.16f, 0.12f, 0.08f, 0.82f);
            Button button = buttonGo.GetComponent<Button>();
            button.targetGraphic = image;
            LayoutElement layout = buttonGo.GetComponent<LayoutElement>();
            layout.preferredHeight = 42f;
            layout.flexibleWidth = 1f;

            GameObject labelGo = new GameObject("Label", typeof(RectTransform), typeof(CanvasRenderer), typeof(TextMeshProUGUI));
            RectTransform labelRect = labelGo.GetComponent<RectTransform>();
            labelRect.SetParent(rect, false);
            labelRect.anchorMin = Vector2.zero;
            labelRect.anchorMax = Vector2.one;
            labelRect.offsetMin = new Vector2(14f, 0f);
            labelRect.offsetMax = new Vector2(-14f, 0f);

            TextMeshProUGUI text = labelGo.GetComponent<TextMeshProUGUI>();
            text.text = label;
            text.fontSize = 22f;
            text.fontStyle = FontStyles.Bold;
            text.alignment = TextAlignmentOptions.Left;
            text.color = new Color(0.93f, 0.88f, 0.74f, 1f);
            text.raycastTarget = false;
            return button;
        }

        private static void StyleOptionButton(Button button)
        {
            if (button == null) return;
            if (button.TryGetComponent(out Image image)) image.color = new Color(0.12f, 0.12f, 0.14f, 0.92f);
            ColorBlock colors = button.colors;
            colors.normalColor = new Color(0.12f, 0.12f, 0.14f, 0.92f);
            colors.highlightedColor = new Color(0.22f, 0.18f, 0.1f, 0.96f);
            colors.pressedColor = new Color(0.17f, 0.14f, 0.08f, 1f);
            colors.selectedColor = colors.highlightedColor;
            colors.disabledColor = new Color(0.2f, 0.2f, 0.2f, 0.45f);
            colors.colorMultiplier = 1f;
            button.colors = colors;
        }

        private static Transform FindDeep(Transform parent, string name)
        {
            if (parent == null) return null;
            if (parent.name == name) return parent;
            for (int i = 0; i < parent.childCount; i++)
            {
                Transform result = FindDeep(parent.GetChild(i), name);
                if (result != null) return result;
            }
            return null;
        }
    }
}
