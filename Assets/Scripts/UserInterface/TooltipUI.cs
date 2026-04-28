using UnityEngine;
using UnityEngine.UI;
using UnityEngine.InputSystem;
using TMPro;
using Sol.Grab;
using UnityEngine.EventSystems;

namespace Sol.HUD
{
    /// <summary>
    /// Tooltip panel shown on inventory item hover. Displays item info and optional 3D preview.
    /// </summary>
    public class TooltipUI : MonoBehaviour
    {
        #region Inspector Settings
        
        [Header("Layout")]
        
        [Tooltip("Inspector: tunes panel.")]
        [SerializeField] private GameObject _panel;
        
        [Tooltip("Inspector: tunes rect transform.")]
        [SerializeField] private RectTransform _rectTransform;

        
        [Header("Text Fields")]
        
        [Tooltip("Inspector: tunes name text.")]
        [SerializeField] private TextMeshProUGUI _nameText;
        [SerializeField] private TextMeshProUGUI _typeText;
        
        [Tooltip("Inspector: tunes flavour text.")]
        [SerializeField] private TextMeshProUGUI _flavourText;
        [SerializeField] private TextMeshProUGUI _statsText;
        
        [Tooltip("Inspector: tunes id text.")]
        [SerializeField] private TextMeshProUGUI _idText;
        [Header("Reference Codes")]
        
        [Tooltip("Inspector: tunes show reference codes.")]
        [SerializeField] private bool _showReferenceCodes = true;
        [Header("Stolen Indicator")]
        
        [Tooltip("Inspector: tunes name stolen icon.")]
        [SerializeField] private Image _nameStolenIcon;
        
        [Tooltip("Inspector: tunes stolen icon sprite.")]
        [SerializeField] private Sprite _stolenIconSprite;

        [Header("Preview")]
        
        [Tooltip("Inspector: tunes preview image.")]
        [SerializeField] private RawImage _previewImage;
        
        #endregion

        public static TooltipUI Instance { get; private set; }

        private ItemComponent _currentItem;
        private bool _layoutDirty;

        private Canvas _canvas;
        private RectTransform _canvasRect;

        private void Awake()
        {
            if (Instance != null && Instance != this) { Destroy(gameObject); return; }
            Instance = this;
            UIStateOwnership.Register<TooltipUI>(this);
            AutoResolveReferences();
            _canvas = GetComponentInParent<Canvas>();
            if (_canvas != null) _canvasRect = _canvas.transform as RectTransform;

            // Prevent the tooltip from intercepting pointer events; otherwise it steals
            // focus from the slot beneath, triggering OnPointerExit ? hide ? flicker.
            if (_panel != null)
            {
                var cg = _panel.GetComponent<CanvasGroup>();
                if (cg != null)
                {
                    cg.blocksRaycasts = false;
                    cg.interactable = false;
                }
                else
                {
                    Debug.LogWarning($"[TooltipUI] CanvasGroup is not assigned on '{_panel.name}'. Author it in the prefab to disable tooltip raycasts.", this);
                }

                _panel.SetActive(false);
            }
        }

        private void AutoResolveReferences()
        {
            if (_panel == null)
                _panel = gameObject;

            if (_rectTransform == null)
            {
                _rectTransform = _panel != null
                    ? _panel.GetComponent<RectTransform>()
                    : transform as RectTransform;
            }
        }

        private void OnDestroy()
        {
            UIStateOwnership.Unregister<TooltipUI>();
            if (Instance == this) Instance = null;
        }

        public static TooltipUI ResolveInstance()
        {
            if (Instance != null)
                return Instance;

            TooltipUI[] found = Object.FindObjectsByType<TooltipUI>(
                FindObjectsInactive.Include,
                FindObjectsSortMode.None);
            if (found == null || found.Length == 0)
                return null;

            TooltipUI resolved = found[0];
            if (resolved == null)
                return null;

            if (!resolved.gameObject.activeSelf)
                resolved.gameObject.SetActive(true);
            if (!resolved.enabled)
                resolved.enabled = true;

            return Instance ?? resolved;
        }

        public void Show(ItemComponent item)
        {
            if (item == null) return;

            _currentItem = item;

            if (_nameText != null) _nameText.text = ItemPresentationUtility.BuildDisplayName(item, _showReferenceCodes);
            UpdateStolenIndicator(item);
            if (_typeText != null) _typeText.text = ItemPresentationUtility.BuildTypeDisplayText(item);
            if (_flavourText != null)
            {
                _flavourText.text = item.FlavourText;
                _flavourText.gameObject.SetActive(!string.IsNullOrEmpty(item.FlavourText));
            }

            if (_statsText != null)
                _statsText.text = ItemPresentationUtility.BuildDetailedStatsText(item, _showReferenceCodes, includeCurrencySuffix: false);

            if (_idText != null)
            {
                string idCode = GetItemIdCode(item);
                _idText.text = idCode;
                _idText.gameObject.SetActive(!string.IsNullOrEmpty(idCode));
            }

            ItemPreviewRenderer previewRenderer = ItemPreviewRenderer.Instance
                ?? UIStateOwnership.Resolve<ItemPreviewRenderer>(activateIfInactive: true);

            if (_previewImage != null && previewRenderer != null)
            {
                _previewImage.texture = previewRenderer.RenderTexture;
                _previewImage.gameObject.SetActive(true);
                previewRenderer.Show(item);
            }
            else if (_previewImage != null)
            {
                _previewImage.texture = null;
                _previewImage.gameObject.SetActive(false);
            }

            if (_panel != null) _panel.SetActive(true);
            _layoutDirty = true;
        }

        public void Hide()
        {
            _currentItem = null;
            UpdateStolenIndicator(null);
            if (_panel != null) _panel.SetActive(false);
            ItemPreviewRenderer.Instance?.Clear();
        }

        private void LateUpdate()
        {
            if (_panel == null || !_panel.activeSelf) return;

            var mouse = Mouse.current;
            if (mouse == null) return;
            Vector2 mp = mouse.position.ReadValue();

            // Use the panel's RectTransform for both sizing and positioning.
            // This avoids mismatches when _rectTransform is a child of transform.
            RectTransform rt = _rectTransform != null ? _rectTransform : transform as RectTransform;
            if (rt == null) return;

            // Force layout rebuild once after content changes.
            if (_layoutDirty)
            {
                LayoutRebuilder.ForceRebuildLayoutImmediate(rt);
                _layoutDirty = false;
            }

            // Size in screen pixels.
            float scaler = _canvas != null ? _canvas.scaleFactor : 1f;
            Vector2 size = rt.rect.size * scaler;
            Vector2 pivot = rt.pivot;

            // ---- Compute position so the bottom-right corner sits at cursor ----
            // Given rt.position corresponds to the pivot point in screen pixels:
            //   leftEdge   = pos.x - pivot.x * size.x
            //   rightEdge  = pos.x + (1-pivot.x) * size.x
            //   bottomEdge = pos.y - pivot.y * size.y
            //   topEdge    = pos.y + (1-pivot.y) * size.y
            //
            // Want rightEdge = mp.x, bottomEdge = mp.y:
            float posX = mp.x - (1f - pivot.x) * size.x;
            float posY = mp.y + pivot.y * size.y;

            // Flip horizontally if left edge goes off-screen.
            if (posX - pivot.x * size.x < 0f)
                posX = mp.x + pivot.x * size.x;

            // Flip vertically if top edge goes off-screen.
            if (posY + (1f - pivot.y) * size.y > Screen.height)
                posY = mp.y - (1f - pivot.y) * size.y;

            // Final clamp; ensure nothing leaks off any edge.
            float minX = pivot.x * size.x;
            float maxX = Screen.width - (1f - pivot.x) * size.x;
            float minY = pivot.y * size.y;
            float maxY = Screen.height - (1f - pivot.y) * size.y;
            posX = Mathf.Clamp(posX, minX, maxX);
            posY = Mathf.Clamp(posY, minY, maxY);

            rt.position = new Vector3(posX, posY, 0f);
        }

        private string GetItemIdCode(ItemComponent item)
        {
            return ItemPresentationUtility.GetItemIdCode(item);
        }

        private void UpdateStolenIndicator(ItemComponent item)
        {
            if (_nameStolenIcon == null)
                return;

            if (_stolenIconSprite != null)
                _nameStolenIcon.sprite = _stolenIconSprite;

            bool show = ItemPresentationUtility.ShouldShowStolenIndicator(item);
            _nameStolenIcon.gameObject.SetActive(show);
        }
    }
}
