using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Sol.HUD
{
    public sealed class SkillMenuSystem : MenuSystemBase<SkillMenuSystem>
    {
        [SerializeField] private ScrollRect _scrollRect;
        [SerializeField] private Transform _contentParent;
        [SerializeField] private Button _closeButton;
        [SerializeField] private TMP_Text _placeholderText;

        protected override void Awake()
        {
            base.Awake();
            if (_instance != this) return;
            AutoWire();
            SetOpen(false);
        }

        protected override void PostResolve() => AutoWire();

        public void Open()
        {
            AutoWire();
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
            _placeholderText ??= MenuUiUtility.FindTextByNames(transform, "Placeholder", "PlaceholderText");
            MenuUiUtility.WireButton(_closeButton, Close);
        }
    }
}
