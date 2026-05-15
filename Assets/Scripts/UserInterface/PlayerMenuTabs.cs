using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace Sol.HUD
{
    public enum PlayerMenuTabId
    {
        Inventory,
        Character,
        Spells,
        Skills,
        QuestLog
    }

    [Serializable]
    public class PlayerMenuTabDefinition
    {
        public PlayerMenuTabId Id;
        public string Label;
        public Sprite Icon;
        public GameObject TargetPanel;
        public bool Enabled = true;
    }

    public class PlayerMenuTabs : MonoBehaviour
    {
        [SerializeField] private List<PlayerMenuTabDefinition> _tabs = new();
        [SerializeField] private RectTransform _tabRoot;
        [SerializeField] private Image _placeholderPanel;
        [SerializeField] private TextMeshProUGUI _placeholderTitle;
        [SerializeField] private TextMeshProUGUI _placeholderHint;
        [SerializeField] private Sprite _tabDefaultSprite;
        [SerializeField] private Sprite _tabHoverSprite;
        [SerializeField] private Sprite _tabActiveSprite;
        [SerializeField] private Sprite _tabDisabledSprite;

        private readonly List<PlayerMenuTabButton> _buttons = new();
        private bool _built;
        private PlayerMenuTabId _selected = PlayerMenuTabId.Inventory;

        public PlayerMenuTabId Selected => _selected;

        public void ConfigureInventoryShell(InventoryUI inventoryUi)
        {
            EnsureTabs();
            EnsureArt();
            BuildIfNeeded();
            Select(PlayerMenuTabId.Inventory);
            inventoryUi?.UsePlayerMenuPresentation();
        }

        public void Select(PlayerMenuTabId id)
        {
            _selected = id;

            for (int i = 0; i < _buttons.Count; i++)
                _buttons[i]?.SetActive(_buttons[i].TabId == id);

            bool inventorySelected = id == PlayerMenuTabId.Inventory;
            if (_placeholderPanel != null)
                _placeholderPanel.gameObject.SetActive(!inventorySelected);

            for (int i = 0; i < _tabs.Count; i++)
            {
                PlayerMenuTabDefinition tab = _tabs[i];
                if (tab == null || tab.TargetPanel == null)
                    continue;

                tab.TargetPanel.SetActive(tab.Id == id);
            }

            if (!inventorySelected)
                UpdatePlaceholderText(id);
        }

        private void EnsureTabs()
        {
            if (_tabs.Count > 0)
                return;

            _tabs.Add(new PlayerMenuTabDefinition { Id = PlayerMenuTabId.Inventory, Label = "Inventory" });
            _tabs.Add(new PlayerMenuTabDefinition { Id = PlayerMenuTabId.Character, Label = "Character" });
            _tabs.Add(new PlayerMenuTabDefinition { Id = PlayerMenuTabId.Spells, Label = "Spells" });
            _tabs.Add(new PlayerMenuTabDefinition { Id = PlayerMenuTabId.Skills, Label = "Skills" });
            _tabs.Add(new PlayerMenuTabDefinition { Id = PlayerMenuTabId.QuestLog, Label = "Quest Log" });
        }

        private void EnsureArt()
        {
            _tabDefaultSprite ??= PlayerMenuTabArt.CreateTabSprite(PlayerMenuTabVisualState.Default);
            _tabHoverSprite ??= PlayerMenuTabArt.CreateTabSprite(PlayerMenuTabVisualState.Hover);
            _tabActiveSprite ??= PlayerMenuTabArt.CreateTabSprite(PlayerMenuTabVisualState.Active);
            _tabDisabledSprite ??= PlayerMenuTabArt.CreateTabSprite(PlayerMenuTabVisualState.Disabled);

            for (int i = 0; i < _tabs.Count; i++)
            {
                PlayerMenuTabDefinition tab = _tabs[i];
                if (tab != null && tab.Icon == null)
                    tab.Icon = PlayerMenuTabArt.CreateIconSprite(tab.Id);
            }
        }

        private void BuildIfNeeded()
        {
            if (_built)
                return;

            _built = true;
            RectTransform root = transform as RectTransform;
            if (root == null)
                return;

            _placeholderPanel = CreatePlaceholder(root);
            _tabRoot = CreateTabRoot(root);

            for (int i = 0; i < _tabs.Count; i++)
            {
                PlayerMenuTabDefinition tab = _tabs[i];
                if (tab == null)
                    continue;

                PlayerMenuTabButton button = PlayerMenuTabButton.Create(_tabRoot, tab, this,
                    _tabDefaultSprite, _tabHoverSprite, _tabActiveSprite, _tabDisabledSprite);
                _buttons.Add(button);
            }

            _tabRoot.SetAsLastSibling();
        }

        private RectTransform CreateTabRoot(RectTransform parent)
        {
            RectTransform root = CreateRect("PlayerMenuTabStrip", parent);
            root.anchorMin = new Vector2(0f, 1f);
            root.anchorMax = new Vector2(1f, 1f);
            root.pivot = new Vector2(0.5f, 1f);
            root.anchoredPosition = new Vector2(0f, -8f);
            root.sizeDelta = new Vector2(-28f, 86f);

            HorizontalLayoutGroup layout = root.gameObject.AddComponent<HorizontalLayoutGroup>();
            layout.spacing = 6f;
            layout.padding = new RectOffset(8, 8, 0, 0);
            layout.childAlignment = TextAnchor.MiddleCenter;
            layout.childControlWidth = false;
            layout.childControlHeight = true;
            layout.childForceExpandWidth = false;
            layout.childForceExpandHeight = true;
            return root;
        }

        private Image CreatePlaceholder(RectTransform parent)
        {
            RectTransform panel = CreateRect("PlayerMenuPlaceholderPanel", parent);
            panel.anchorMin = new Vector2(0f, 0f);
            panel.anchorMax = new Vector2(1f, 1f);
            panel.offsetMin = new Vector2(18f, 48f);
            panel.offsetMax = new Vector2(-18f, -122f);
            panel.SetAsLastSibling();

            Image background = panel.gameObject.AddComponent<Image>();
            background.color = new Color(0.23f, 0.18f, 0.11f, 0.96f);
            background.raycastTarget = true;

            VerticalLayoutGroup layout = panel.gameObject.AddComponent<VerticalLayoutGroup>();
            layout.padding = new RectOffset(28, 28, 130, 28);
            layout.spacing = 8f;
            layout.childAlignment = TextAnchor.UpperCenter;
            layout.childControlWidth = true;
            layout.childForceExpandWidth = true;
            layout.childForceExpandHeight = false;

            _placeholderTitle = CreateText("Title", panel, string.Empty, 24f, 1f, TextAlignmentOptions.Center);
            _placeholderHint = CreateText("Hint", panel, string.Empty, 15f, 0.72f, TextAlignmentOptions.Center);
            panel.gameObject.SetActive(false);
            return background;
        }

        private void UpdatePlaceholderText(PlayerMenuTabId id)
        {
            string label = GetLabel(id);
            if (_placeholderTitle != null)
                _placeholderTitle.text = label;
            if (_placeholderHint != null)
                _placeholderHint.text = "Menu panel placeholder";
        }

        private string GetLabel(PlayerMenuTabId id)
        {
            for (int i = 0; i < _tabs.Count; i++)
            {
                PlayerMenuTabDefinition tab = _tabs[i];
                if (tab != null && tab.Id == id && !string.IsNullOrWhiteSpace(tab.Label))
                    return tab.Label;
            }

            return id.ToString();
        }

        private static RectTransform CreateRect(string childName, Transform parent)
        {
            GameObject go = new(childName, typeof(RectTransform));
            go.layer = parent.gameObject.layer;
            RectTransform rect = go.GetComponent<RectTransform>();
            rect.SetParent(parent, false);
            return rect;
        }

        private TextMeshProUGUI CreateText(string childName, Transform parent, string value, float size, float alpha, TextAlignmentOptions alignment)
        {
            RectTransform rect = CreateRect(childName, parent);
            LayoutElement layout = rect.gameObject.AddComponent<LayoutElement>();
            layout.preferredHeight = size + 10f;

            TextMeshProUGUI text = rect.gameObject.AddComponent<TextMeshProUGUI>();
            text.text = value;
            text.fontSize = size;
            text.color = new Color(0.92f, 0.84f, 0.63f, alpha);
            text.alignment = alignment;
            text.raycastTarget = false;
            text.textWrappingMode = TextWrappingModes.Normal;
            return text;
        }
    }

    public class PlayerMenuTabButton : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler
    {
        private PlayerMenuTabs _owner;
        private PlayerMenuTabDefinition _definition;
        private Image _background;
        private Image _icon;
        private TextMeshProUGUI _label;
        private Sprite _defaultSprite;
        private Sprite _hoverSprite;
        private Sprite _activeSprite;
        private Sprite _disabledSprite;
        private bool _active;
        private bool _hovered;

        public PlayerMenuTabId TabId => _definition != null ? _definition.Id : PlayerMenuTabId.Inventory;

        public static PlayerMenuTabButton Create(
            RectTransform parent,
            PlayerMenuTabDefinition definition,
            PlayerMenuTabs owner,
            Sprite defaultSprite,
            Sprite hoverSprite,
            Sprite activeSprite,
            Sprite disabledSprite)
        {
            GameObject go = new(definition.Label + "Tab", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image), typeof(Button), typeof(PlayerMenuTabButton));
            go.layer = parent.gameObject.layer;
            RectTransform rect = go.GetComponent<RectTransform>();
            rect.SetParent(parent, false);

            LayoutElement layout = go.AddComponent<LayoutElement>();
            layout.preferredWidth = 86f;
            layout.preferredHeight = 82f;

            PlayerMenuTabButton tab = go.GetComponent<PlayerMenuTabButton>();
            tab.Initialize(definition, owner, defaultSprite, hoverSprite, activeSprite, disabledSprite);
            return tab;
        }

        private void Initialize(
            PlayerMenuTabDefinition definition,
            PlayerMenuTabs owner,
            Sprite defaultSprite,
            Sprite hoverSprite,
            Sprite activeSprite,
            Sprite disabledSprite)
        {
            _definition = definition;
            _owner = owner;
            _defaultSprite = defaultSprite;
            _hoverSprite = hoverSprite;
            _activeSprite = activeSprite;
            _disabledSprite = disabledSprite;

            _background = GetComponent<Image>();
            _background.type = Image.Type.Sliced;
            _background.sprite = _defaultSprite;
            _background.color = Color.white;

            Button button = GetComponent<Button>();
            button.targetGraphic = _background;
            button.interactable = definition.Enabled;
            button.onClick.AddListener(() => _owner.Select(definition.Id));

            _icon = CreateIcon(definition.Icon);
            _label = CreateLabel(definition.Label);
            RefreshVisual();
        }

        public void SetActive(bool active)
        {
            _active = active;
            RefreshVisual();
        }

        public void OnPointerEnter(PointerEventData eventData)
        {
            _hovered = true;
            RefreshVisual();
        }

        public void OnPointerExit(PointerEventData eventData)
        {
            _hovered = false;
            RefreshVisual();
        }

        private void RefreshVisual()
        {
            if (_background == null || _definition == null)
                return;

            _background.sprite = !_definition.Enabled
                ? _disabledSprite
                : _active
                    ? _activeSprite
                    : _hovered
                        ? _hoverSprite
                        : _defaultSprite;

            float alpha = _definition.Enabled ? 1f : 0.42f;
            if (_icon != null)
                _icon.color = _active ? new Color(1f, 0.93f, 0.62f, alpha) : new Color(0.88f, 0.86f, 0.78f, alpha);
            if (_label != null)
                _label.color = _active ? new Color(1f, 0.90f, 0.58f, alpha) : new Color(0.82f, 0.76f, 0.62f, alpha);
        }

        private Image CreateIcon(Sprite sprite)
        {
            RectTransform rect = CreateRect("Icon", transform);
            rect.anchorMin = new Vector2(0.5f, 1f);
            rect.anchorMax = new Vector2(0.5f, 1f);
            rect.pivot = new Vector2(0.5f, 1f);
            rect.anchoredPosition = new Vector2(0f, -10f);
            rect.sizeDelta = new Vector2(42f, 42f);

            Image image = rect.gameObject.AddComponent<Image>();
            image.sprite = sprite;
            image.preserveAspect = true;
            image.raycastTarget = false;
            return image;
        }

        private TextMeshProUGUI CreateLabel(string value)
        {
            RectTransform rect = CreateRect("Label", transform);
            rect.anchorMin = new Vector2(0f, 0f);
            rect.anchorMax = new Vector2(1f, 0f);
            rect.pivot = new Vector2(0.5f, 0f);
            rect.anchoredPosition = new Vector2(0f, 8f);
            rect.sizeDelta = new Vector2(-8f, 18f);

            TextMeshProUGUI text = rect.gameObject.AddComponent<TextMeshProUGUI>();
            text.text = value;
            text.fontSize = 11f;
            text.alignment = TextAlignmentOptions.Center;
            text.raycastTarget = false;
            text.textWrappingMode = TextWrappingModes.NoWrap;
            text.overflowMode = TextOverflowModes.Ellipsis;
            return text;
        }

        private static RectTransform CreateRect(string childName, Transform parent)
        {
            GameObject go = new(childName, typeof(RectTransform));
            go.layer = parent.gameObject.layer;
            RectTransform rect = go.GetComponent<RectTransform>();
            rect.SetParent(parent, false);
            return rect;
        }
    }

    internal enum PlayerMenuTabVisualState
    {
        Default,
        Hover,
        Active,
        Disabled
    }

    internal static class PlayerMenuTabArt
    {
        public static Sprite CreateTabSprite(PlayerMenuTabVisualState state)
        {
            const int width = 128;
            const int height = 96;
            Texture2D texture = new(width, height, TextureFormat.RGBA32, false)
            {
                name = "RuntimeTab_" + state,
                filterMode = FilterMode.Bilinear,
                wrapMode = TextureWrapMode.Clamp
            };

            Color clear = new(0f, 0f, 0f, 0f);
            Color fill = state switch
            {
                PlayerMenuTabVisualState.Active => new Color(0.50f, 0.38f, 0.19f, 0.98f),
                PlayerMenuTabVisualState.Hover => new Color(0.39f, 0.30f, 0.17f, 0.95f),
                PlayerMenuTabVisualState.Disabled => new Color(0.16f, 0.14f, 0.12f, 0.55f),
                _ => new Color(0.28f, 0.22f, 0.14f, 0.90f)
            };
            Color edge = state == PlayerMenuTabVisualState.Active
                ? new Color(1.00f, 0.76f, 0.28f, 1f)
                : new Color(0.72f, 0.55f, 0.28f, 0.95f);
            Color inner = state == PlayerMenuTabVisualState.Disabled
                ? new Color(0.30f, 0.28f, 0.24f, 0.70f)
                : new Color(0.86f, 0.72f, 0.42f, 0.92f);

            for (int y = 0; y < height; y++)
            {
                for (int x = 0; x < width; x++)
                {
                    float nx = Mathf.Abs((x / (float)(width - 1)) * 2f - 1f);
                    float ny = Mathf.Abs((y / (float)(height - 1)) * 2f - 1f);
                    float rounded = Mathf.Pow(nx, 5f) + Mathf.Pow(ny, 5f);
                    if (rounded > 1.05f)
                    {
                        texture.SetPixel(x, y, clear);
                        continue;
                    }

                    bool border = x < 7 || x > width - 8 || y < 7 || y > height - 8 || rounded > 0.82f;
                    bool line = x == 13 || x == width - 14 || y == 13 || y == height - 14;
                    texture.SetPixel(x, y, border ? edge : line ? inner : fill);
                }
            }

            texture.Apply();
            Sprite sprite = Sprite.Create(texture, new Rect(0, 0, width, height), new Vector2(0.5f, 0.5f), 100f, 0, SpriteMeshType.FullRect, new Vector4(18, 18, 18, 18));
            sprite.name = texture.name;
            return sprite;
        }

        public static Sprite CreateIconSprite(PlayerMenuTabId id)
        {
            const int size = 64;
            Texture2D texture = new(size, size, TextureFormat.RGBA32, false)
            {
                name = "RuntimeTabIcon_" + id,
                filterMode = FilterMode.Bilinear,
                wrapMode = TextureWrapMode.Clamp
            };

            Color clear = new(0f, 0f, 0f, 0f);
            Color ink = new(0.92f, 0.90f, 0.82f, 1f);
            for (int y = 0; y < size; y++)
                for (int x = 0; x < size; x++)
                    texture.SetPixel(x, y, clear);

            switch (id)
            {
                case PlayerMenuTabId.Inventory:
                    DrawRect(texture, 15, 22, 49, 48, ink);
                    DrawLine(texture, 22, 22, 25, 13, ink);
                    DrawLine(texture, 42, 22, 39, 13, ink);
                    DrawLine(texture, 25, 13, 39, 13, ink);
                    break;
                case PlayerMenuTabId.Character:
                    DrawCircle(texture, 32, 18, 9, ink);
                    DrawRect(texture, 22, 30, 42, 51, ink);
                    break;
                case PlayerMenuTabId.Spells:
                    DrawCircle(texture, 32, 32, 18, ink);
                    DrawLine(texture, 32, 10, 32, 54, clear);
                    DrawLine(texture, 10, 32, 54, 32, clear);
                    DrawCircle(texture, 32, 32, 6, clear);
                    break;
                case PlayerMenuTabId.Skills:
                    DrawLine(texture, 18, 48, 46, 16, ink);
                    DrawLine(texture, 46, 48, 18, 16, ink);
                    DrawRect(texture, 14, 45, 24, 53, ink);
                    DrawRect(texture, 40, 45, 50, 53, ink);
                    break;
                case PlayerMenuTabId.QuestLog:
                    DrawRect(texture, 18, 13, 46, 52, ink);
                    DrawLine(texture, 24, 24, 40, 24, clear);
                    DrawLine(texture, 24, 33, 40, 33, clear);
                    DrawLine(texture, 24, 42, 37, 42, clear);
                    break;
            }

            texture.Apply();
            Sprite sprite = Sprite.Create(texture, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), 100f);
            sprite.name = texture.name;
            return sprite;
        }

        private static void DrawRect(Texture2D texture, int minX, int minY, int maxX, int maxY, Color color)
        {
            for (int y = minY; y <= maxY; y++)
                for (int x = minX; x <= maxX; x++)
                    texture.SetPixel(x, y, color);
        }

        private static void DrawCircle(Texture2D texture, int cx, int cy, int radius, Color color)
        {
            int r2 = radius * radius;
            for (int y = cy - radius; y <= cy + radius; y++)
                for (int x = cx - radius; x <= cx + radius; x++)
                    if ((x - cx) * (x - cx) + (y - cy) * (y - cy) <= r2)
                        texture.SetPixel(x, y, color);
        }

        private static void DrawLine(Texture2D texture, int x0, int y0, int x1, int y1, Color color)
        {
            int dx = Mathf.Abs(x1 - x0);
            int sx = x0 < x1 ? 1 : -1;
            int dy = -Mathf.Abs(y1 - y0);
            int sy = y0 < y1 ? 1 : -1;
            int err = dx + dy;

            while (true)
            {
                for (int oy = -1; oy <= 1; oy++)
                    for (int ox = -1; ox <= 1; ox++)
                        texture.SetPixel(Mathf.Clamp(x0 + ox, 0, texture.width - 1), Mathf.Clamp(y0 + oy, 0, texture.height - 1), color);

                if (x0 == x1 && y0 == y1)
                    break;

                int e2 = 2 * err;
                if (e2 >= dy)
                {
                    err += dy;
                    x0 += sx;
                }
                if (e2 <= dx)
                {
                    err += dx;
                    y0 += sy;
                }
            }
        }
    }
}
