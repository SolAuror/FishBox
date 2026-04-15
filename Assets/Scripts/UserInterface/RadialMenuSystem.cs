using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using Sol.ToD;

namespace Sol.HUD
{
    public sealed class RadialMenuSystem : MonoBehaviour
    {
        [SerializeField] private RectTransform _clockHand;
        [SerializeField] private TMP_Text _timeDisplay;
        [SerializeField] private TMP_Text _hoursLabel;
        [SerializeField] private TMP_Text _staminaPreview;
        [SerializeField] private TMP_Text _timePreview;
        [SerializeField] private TMP_Text _currentTimeDisplay;
        [SerializeField] private TMP_Text _currentDateDisplay;
        [SerializeField] private Button _confirmButton;
        [SerializeField] private Button _cancelButton;
        [SerializeField] private RectTransform _circleCenter;
        [SerializeField] private int _minHours = 1;
        [SerializeField] private int _maxHours = 24;
        [SerializeField] private float _staminaPerHour = 12.5f;
        [SerializeField] private int _startHour = 6;

        public static RadialMenuSystem Instance { get; private set; }
        public bool IsOpen => gameObject.activeSelf;
        public int SelectedHours => _selectedHours;

        private CanvasGroup _canvasGroup;
        private Action<int> _confirmAction;
        private Action _cancelAction;
        private Func<int, string> _secondaryPreviewProvider;
        private int _selectedHours = 8;

        private void Awake()
        {
            if (Instance != null && Instance != this) { Destroy(gameObject); return; }
            Instance = this;
            _canvasGroup = MenuUiUtility.EnsureCanvasGroup(gameObject);
            AutoWire();
            ApplySelection(_startHour);
            SetOpen(false);
        }

        private void OnDestroy()
        {
            if (Instance == this)
                Instance = null;
        }

        private void Update()
        {
            if (!IsOpen)
                return;

            if (Input.mouseScrollDelta.y > 0f)
                ApplySelection(_selectedHours + 1);
            else if (Input.mouseScrollDelta.y < 0f)
                ApplySelection(_selectedHours - 1);
        }

        public static RadialMenuSystem ResolveInstance(bool activateIfInactive = true)
        {
            RadialMenuSystem resolved = Instance ?? UIStateOwnership.Resolve<RadialMenuSystem>(activateIfInactive);
            if (resolved != null)
                resolved.AutoWire();
            return resolved;
        }

        public void Open(Action<int> onConfirm = null, Action onCancel = null, int initialHours = 8)
        {
            _confirmAction = onConfirm;
            _cancelAction = onCancel;
            AutoWire();
            ApplySelection(initialHours);
            UIStateOwnership.CloseConflictingUi(nameof(RadialMenuSystem));
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

            Action cancel = _cancelAction;
            _confirmAction = null;
            _cancelAction = null;
            SetOpen(false);
            MenuUiUtility.SetBackgroundUiRaycasts(transform, true);
            UIStateOwnership.SetUiCapture(false);
            cancel?.Invoke();
        }

        private void Confirm()
        {
            Action<int> callback = _confirmAction;
            int hours = _selectedHours;
            _cancelAction = null;
            Close();
            callback?.Invoke(hours);
        }

        public void SetHourRange(int minHours, int maxHours)
        {
            _minHours = Mathf.Max(1, minHours);
            _maxHours = Mathf.Max(_minHours, maxHours);
            ApplySelection(_selectedHours);
        }

        public void ConfigureSecondaryPreview(Func<int, string> previewProvider)
        {
            _secondaryPreviewProvider = previewProvider;
            ApplySelection(_selectedHours);
        }

        public void ResetSecondaryPreview()
        {
            _secondaryPreviewProvider = null;
            ApplySelection(_selectedHours);
        }

        private void AutoWire()
        {
            _timeDisplay ??= MenuUiUtility.FindTextByNames(transform, "TimeDisplay", "TimePreview");
            _hoursLabel ??= MenuUiUtility.FindTextByNames(transform, "HoursLabel", "Hours");
            _staminaPreview ??= MenuUiUtility.FindTextByNames(transform, "StaminaPreview");
            _timePreview ??= MenuUiUtility.FindTextByNames(transform, "TimePreview");
            _currentTimeDisplay ??= MenuUiUtility.FindTextByNames(transform, "CurrentTimeDisplay");
            _currentDateDisplay ??= MenuUiUtility.FindTextByNames(transform, "CurrentDateDisplay");
            if (!MenuUiUtility.IsDescendantOf(_confirmButton != null ? _confirmButton.transform : null, transform))
                _confirmButton = MenuUiUtility.FindButtonByNames(transform, "Confirm", "ConfirmButton");
            if (!MenuUiUtility.IsDescendantOf(_cancelButton != null ? _cancelButton.transform : null, transform))
                _cancelButton = MenuUiUtility.FindButtonByNames(transform, "Cancel", "CancelButton", "CloseButton");

            PrepareRadialButton(_confirmButton);
            PrepareRadialButton(_cancelButton);
            SanitizeDecorativeRaycasts();

            MenuUiUtility.WireButton(_confirmButton, Confirm);
            MenuUiUtility.WireButton(_cancelButton, Close);
        }

        private void PrepareRadialButton(Button button)
        {
            if (button == null)
                return;

            MenuUiUtility.MakeButtonClickable(button);
            button.transform.SetAsLastSibling();
        }

        private void SanitizeDecorativeRaycasts()
        {
            Graphic[] graphics = GetComponentsInChildren<Graphic>(true);
            for (int i = 0; i < graphics.Length; i++)
            {
                Graphic graphic = graphics[i];
                if (graphic == null)
                    continue;

                Button ownerButton = graphic.GetComponent<Button>() ?? graphic.GetComponentInParent<Button>();
                if (ownerButton == _confirmButton || ownerButton == _cancelButton)
                {
                    if (graphic is TMP_Text text)
                        text.raycastTarget = false;
                    continue;
                }

                graphic.raycastTarget = false;
            }

            if (_confirmButton != null)
            {
                Image confirmImage = _confirmButton.GetComponent<Image>();
                if (confirmImage != null)
                    confirmImage.raycastTarget = true;
            }

            if (_cancelButton != null)
            {
                Image cancelImage = _cancelButton.GetComponent<Image>();
                if (cancelImage != null)
                    cancelImage.raycastTarget = true;
            }
        }

        private void ApplySelection(int hours)
        {
            _selectedHours = Mathf.Clamp(hours, Mathf.Max(1, _minHours), Mathf.Max(_minHours, _maxHours));

            if (_hoursLabel != null)
                _hoursLabel.text = $"{_selectedHours} HOURS";

            if (_timeDisplay != null)
                _timeDisplay.text = $"{_selectedHours:00}:00";

            if (_timePreview != null)
                _timePreview.text = $"+{_selectedHours}h";

            if (_staminaPreview != null)
            {
                _staminaPreview.text = _secondaryPreviewProvider != null
                    ? _secondaryPreviewProvider(_selectedHours)
                    : $"-{_selectedHours * _staminaPerHour:0.#} STM";
            }

            if (_clockHand != null)
                _clockHand.localEulerAngles = new Vector3(0f, 0f, -(_selectedHours / 24f) * 360f);

            TimeOfDay timeOfDay = UnityEngine.Object.FindFirstObjectByType<TimeOfDay>();
            if (_currentTimeDisplay != null)
                _currentTimeDisplay.text = timeOfDay != null ? $"{timeOfDay.CurrentTime:0.0}h" : string.Empty;

            Calendar calendar = timeOfDay != null ? timeOfDay.Calendar : UnityEngine.Object.FindFirstObjectByType<Calendar>();
            if (_currentDateDisplay != null && calendar != null)
                _currentDateDisplay.text = $"{calendar.Day}/{calendar.Month}/{calendar.Year}";
        }

        private void SetOpen(bool open)
        {
            MenuUiUtility.SetVisible(gameObject, open);
            _canvasGroup ??= MenuUiUtility.EnsureCanvasGroup(gameObject);
            MenuUiUtility.SetCanvasGroupVisible(_canvasGroup, open);
        }
    }
}
