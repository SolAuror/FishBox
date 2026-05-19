using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace Sol.HUD
{
    public sealed class InventoryCategoryTabButton : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler
    {
        [SerializeField] private InventoryCategoryTabId _tabId;
        [SerializeField] private Image _background;
        [SerializeField] private Image _icon;
        [SerializeField] private TextMeshProUGUI _label;

        private InventoryCategoryTabs _owner;
        private InventoryCategoryTabDefinition _definition;
        private Button _button;
        private Sprite _defaultSprite;
        private Sprite _hoverSprite;
        private Sprite _activeSprite;
        private Sprite _disabledSprite;
        private Color _inactiveIconColor;
        private Color _inactiveLabelColor;
        private Color _activeIconColor;
        private Color _activeLabelColor;
        private bool _active;
        private bool _hovered;

        public InventoryCategoryTabId TabId => _tabId;

        public void Configure(
            InventoryCategoryTabs owner,
            InventoryCategoryTabDefinition definition,
            Sprite defaultSprite,
            Sprite hoverSprite,
            Sprite activeSprite,
            Sprite disabledSprite,
            Color inactiveIconColor,
            Color inactiveLabelColor,
            Color activeIconColor,
            Color activeLabelColor)
        {
            _owner = owner;
            _definition = definition;
            _defaultSprite = defaultSprite;
            _hoverSprite = hoverSprite;
            _activeSprite = activeSprite;
            _disabledSprite = disabledSprite;
            _inactiveIconColor = inactiveIconColor;
            _inactiveLabelColor = inactiveLabelColor;
            _activeIconColor = activeIconColor;
            _activeLabelColor = activeLabelColor;

            ResolveReferences();

            if (_button != null)
            {
                _button.onClick.RemoveListener(OnClicked);
                _button.onClick.AddListener(OnClicked);
                _button.interactable = definition == null || definition.Enabled;
            }

            if (_icon != null && definition?.Icon != null && _icon.sprite == null)
                _icon.sprite = definition.Icon;

            if (_label != null && !string.IsNullOrWhiteSpace(definition?.Label))
                _label.text = definition.Label;

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

        private void OnDestroy()
        {
            if (_button != null)
                _button.onClick.RemoveListener(OnClicked);
        }

        private void OnClicked()
        {
            _owner?.Select(_tabId);
        }

        private void ResolveReferences()
        {
            _button ??= GetComponent<Button>();
            _background ??= _button != null ? _button.targetGraphic as Image : null;
            _background ??= GetComponent<Image>();
            _icon ??= MenuUiUtility.FindDeepComponent<Image>(transform, "Icon");
            _label ??= MenuUiUtility.FindTextByNames(transform, "Label", "Text") as TextMeshProUGUI;
        }

        private void RefreshVisual()
        {
            ResolveReferences();

            bool enabled = _definition == null || _definition.Enabled;
            Sprite sprite = !enabled
                ? _disabledSprite
                : _active
                    ? _activeSprite
                    : _hovered
                        ? _hoverSprite
                        : _defaultSprite;

            if (_background != null && sprite != null)
            {
                _background.sprite = sprite;
                _background.type = Image.Type.Sliced;
            }

            float alpha = enabled ? 1f : 0.42f;
            if (_icon != null)
                _icon.color = _active ? _activeIconColor : WithAlpha(_inactiveIconColor, alpha);
            if (_label != null)
                _label.color = _active ? _activeLabelColor : WithAlpha(_inactiveLabelColor, alpha);
        }

        private static Color WithAlpha(Color color, float alpha)
        {
            color.a *= alpha;
            return color;
        }
    }
}
