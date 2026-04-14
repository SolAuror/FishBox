using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Sol.HUD
{
    public sealed class SkillMenuSystem : MonoBehaviour
    {
        [SerializeField] private ScrollRect _scrollRect;
        [SerializeField] private Transform _contentParent;
        [SerializeField] private Button _closeButton;

        public static SkillMenuSystem Instance { get; private set; }
        public bool IsOpen => gameObject.activeSelf;

        private CanvasGroup _canvasGroup;
        private TMP_Text _placeholderText;

        private void Awake()
        {
            if (Instance != null && Instance != this) { Destroy(gameObject); return; }
            Instance = this;
            _canvasGroup = MenuUiUtility.EnsureCanvasGroup(gameObject);
            AutoWire();
            EnsurePlaceholder();
            SetOpen(false);
        }

        private void OnDestroy()
        {
            if (Instance == this)
                Instance = null;
        }

        public static SkillMenuSystem ResolveInstance(bool activateIfInactive = true)
        {
            SkillMenuSystem resolved = Instance ?? UIStateOwnership.Resolve<SkillMenuSystem>(activateIfInactive);
            if (resolved != null)
                resolved.AutoWire();
            return resolved;
        }

        public void Open()
        {
            AutoWire();
            EnsurePlaceholder();
            UIStateOwnership.CloseConflictingUi(nameof(SkillMenuSystem));
            SetOpen(true);
            MenuUiUtility.BringToFront(transform.parent);
            MenuUiUtility.BringToFront(transform);
            MenuUiUtility.SetBackgroundUiRaycasts(transform, false);
            UIStateOwnership.SetUiCapture(true);
            MenuUiUtility.SelectButton(_closeButton);
        }

        public void Close()
        {
            if (!IsOpen)
                return;

            SetOpen(false);
            MenuUiUtility.SetBackgroundUiRaycasts(transform, true);
            UIStateOwnership.SetUiCapture(false);
        }

        private void AutoWire()
        {
            _scrollRect ??= GetComponentInChildren<ScrollRect>(true);
            _contentParent ??= MenuUiUtility.FindDeep(transform, "Content")
                ?? MenuUiUtility.FindDeep(transform, "ContentArea")
                ?? (_scrollRect != null ? _scrollRect.content : null);
            _closeButton ??= MenuUiUtility.FindButtonByNames(transform, "CloseButton", "Back", "Close");
            MenuUiUtility.WireButton(_closeButton, Close);
        }

        private void EnsurePlaceholder()
        {
            if (_contentParent == null || _placeholderText != null)
                return;

            GameObject textObject = new GameObject("Placeholder", typeof(RectTransform), typeof(TextMeshProUGUI));
            textObject.transform.SetParent(_contentParent, false);
            _placeholderText = textObject.GetComponent<TextMeshProUGUI>();
            _placeholderText.fontSize = 24f;
            _placeholderText.alignment = TextAlignmentOptions.Center;
            _placeholderText.text = "Skills UI imported.\nGameplay progression wiring can layer in next.";
        }

        private void SetOpen(bool open)
        {
            MenuUiUtility.SetVisible(gameObject, open);
            _canvasGroup ??= MenuUiUtility.EnsureCanvasGroup(gameObject);
            MenuUiUtility.SetCanvasGroupVisible(_canvasGroup, open);
        }
    }
}
