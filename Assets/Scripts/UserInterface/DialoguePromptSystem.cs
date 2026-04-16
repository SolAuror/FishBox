using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Sol.HUD
{
    public sealed class DialoguePromptSystem : MenuSystemBase<DialoguePromptSystem>
    {
        [SerializeField] private Image _icon;
        [SerializeField] private TMP_Text _titleText;
        [SerializeField] private TMP_Text _descriptionText;
        [SerializeField] private Button _confirmButton;
        [SerializeField] private Button _cancelButton;

        private Action _confirmAction;
        private Action _cancelAction;

        protected override void Awake()
        {
            base.Awake();
            if (_instance != this) return;
            AutoWire();
            SetOpen(false);
        }

        protected override void PostResolve() => AutoWire();

        public void Show(
            string title,
            string description,
            Action confirmAction,
            Action cancelAction = null,
            string confirmLabel = "Confirm",
            string cancelLabel = "Cancel",
            Sprite icon = null)
        {
            AutoWire();
            UIStateOwnership.CloseConflictingUi(nameof(DialoguePromptSystem));

            _confirmAction = confirmAction;
            _cancelAction = cancelAction;

            if (_titleText != null)
                _titleText.text = string.IsNullOrWhiteSpace(title) ? "Prompt" : title;

            if (_descriptionText != null)
                _descriptionText.text = description ?? string.Empty;

            if (_icon != null)
            {
                _icon.sprite = icon;
                _icon.gameObject.SetActive(icon != null);
            }

            SetButtonLabel(_confirmButton, confirmLabel);
            SetButtonLabel(_cancelButton, cancelLabel);
            SetOpen(true);
            MenuUiUtility.BringToFront(transform.parent);
            MenuUiUtility.BringToFront(transform);
            MenuUiUtility.SetBackgroundUiRaycasts(transform, false);
            UIStateOwnership.SetUiCapture(true);
            MenuUiUtility.SelectButton(_confirmButton != null ? _confirmButton : _cancelButton);
        }

        public void Close()
        {
            if (!IsOpen)
                return;

            _confirmAction = null;
            _cancelAction = null;
            SetOpen(false);
            MenuUiUtility.SetBackgroundUiRaycasts(transform, true);
            UIStateOwnership.SetUiCapture(false);
        }

        private void OnConfirm()
        {
            Action action = _confirmAction;
            Close();
            action?.Invoke();
        }

        private void OnCancel()
        {
            Action action = _cancelAction;
            Close();
            action?.Invoke();
        }

        private void AutoWire()
        {
            _icon ??= MenuUiUtility.FindDeepComponent<Image>(transform, "Icon");
            _titleText ??= MenuUiUtility.FindTextByNames(transform, "Title", "Header");
            _descriptionText ??= MenuUiUtility.FindTextByNames(transform, "Description", "Body", "PromptText");
            _confirmButton ??= MenuUiUtility.FindButtonByNames(transform, "Confirm", "ConfirmButton", "Okay");
            _cancelButton ??= MenuUiUtility.FindButtonByNames(transform, "Cancel", "CancelButton", "Back");

            MenuUiUtility.WireButton(_confirmButton, OnConfirm);
            MenuUiUtility.WireButton(_cancelButton, OnCancel);
        }

        private static void SetButtonLabel(Button button, string label)
        {
            if (button == null || string.IsNullOrWhiteSpace(label))
                return;

            TMP_Text text = button.GetComponentInChildren<TMP_Text>(true);
            if (text != null)
                text.text = label;
        }
    }
}
