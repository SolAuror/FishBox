using System;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;
using Sol.ToD;

namespace Sol.HUD
{
    public sealed class RadialMenuSystem : MenuSystemBase<RadialMenuSystem>
    {
        #region Inspector Settings
        [Tooltip("Inspector: tunes clock hand.")]
        [SerializeField] private RectTransform _clockHand;
        [SerializeField] private TMP_Text _timeDisplay;
        [Tooltip("Inspector: tunes hours label.")]
        [SerializeField] private TMP_Text _hoursLabel;
        [SerializeField] private TMP_Text _staminaPreview;
        [Tooltip("Inspector: tunes time preview.")]
        [SerializeField] private TMP_Text _timePreview;
        [SerializeField] private TMP_Text _currentTimeDisplay;
        [Tooltip("Inspector: tunes current date display.")]
        [SerializeField] private TMP_Text _currentDateDisplay;
        [SerializeField] private Button _confirmButton;
        [Tooltip("Inspector: tunes cancel button.")]
        [SerializeField] private Button _cancelButton;
        [SerializeField] private RectTransform _circleCenter;
        [Tooltip("Inspector: tunes min hours.")]
        [SerializeField] private int _minHours = 1;
        [SerializeField] private int _maxHours = 24;
        [Tooltip("Inspector: tunes stamina per hour.")]
        [SerializeField] private float _staminaPerHour = 12.5f;
        [Tooltip("Inspector: tunes start hour.")]
        [SerializeField] private int _startHour = 6;
        #endregion

        public int SelectedHours => _selectedHours;

        private Canvas _rootCanvas;
        private Action<int> _confirmAction;
        private Action _cancelAction;
        private Func<int, string> _secondaryPreviewProvider;
        private int _selectedHours = 8;
        private bool _isDragging;

        protected override void Awake()
        {
            base.Awake();
            if (_instance != this) return;
            AutoWire();
            ApplySelection(_startHour);
            SetOpen(false);
        }

        protected override void PostResolve() => AutoWire();

        private void Update()
        {
            if (!IsOpen)
                return;

            Mouse mouse = Mouse.current;
            if (mouse == null)
                return;

            HandlePointerInput();

            float scrollY = mouse.scroll.ReadValue().y;
            if (scrollY > 0f)
                ApplySelection(_selectedHours + 1);
            else if (scrollY < 0f)
                ApplySelection(_selectedHours - 1);
        }

        public void Open(Action<int> onConfirm = null, Action onCancel = null, int initialHours = 8)
        {
            _confirmAction = onConfirm;
            _cancelAction = onCancel;
            _isDragging = false;
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

            _isDragging = false;
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
            _rootCanvas ??= GetComponentInParent<Canvas>();
            _circleCenter ??= MenuUiUtility.FindDeep(transform, "CircleBackground") as RectTransform;
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

            MenuUiUtility.WireButton(_confirmButton, Confirm);
            MenuUiUtility.WireButton(_cancelButton, Close);
        }

        private void HandlePointerInput()
        {
            if (_circleCenter == null)
                return;

            Mouse mouse = Mouse.current;
            if (mouse == null)
                return;

            bool mousePressed = mouse.leftButton.wasPressedThisFrame;
            bool mouseHeld = mouse.leftButton.isPressed;

            if (mousePressed && TryGetCircleLocalPoint(out Vector2 pressedPoint))
            {
                float radius = _circleCenter.rect.width * 0.5f;
                if (pressedPoint.magnitude <= radius)
                {
                    _isDragging = true;
                    ApplySelection(HoursFromLocalPoint(pressedPoint));
                }
            }

            if (!mouseHeld)
                _isDragging = false;

            if (_isDragging && TryGetCircleLocalPoint(out Vector2 currentPoint))
                ApplySelection(HoursFromLocalPoint(currentPoint));
        }

        private void ApplySelection(int hours)
        {
            _selectedHours = Mathf.Clamp(hours, Mathf.Max(1, _minHours), Mathf.Max(_minHours, _maxHours));

            float currentHour = ResolveCurrentHour();
            float resultHour = Mathf.Repeat(currentHour + _selectedHours, 24f);
            string previewText = BuildRecoveryPreview(_selectedHours);

            if (_hoursLabel != null)
            {
                _hoursLabel.text = _selectedHours == 1
                    ? "Wait 1 hour"
                    : $"Wait {_selectedHours} hours";
            }

            if (_timeDisplay != null)
                _timeDisplay.text = FormatHour(resultHour);

            if (_staminaPreview != null)
                _staminaPreview.text = previewText;

            if (_timePreview != null)
            {
                bool useFallbackPreview = _staminaPreview == null && !string.IsNullOrWhiteSpace(previewText);
                _timePreview.gameObject.SetActive(useFallbackPreview);
                _timePreview.text = useFallbackPreview ? previewText : string.Empty;
            }

            if (_clockHand != null)
                _clockHand.localEulerAngles = new Vector3(0f, 0f, -_selectedHours * GetDegreesPerHour());

            if (_currentTimeDisplay != null)
                _currentTimeDisplay.text = $"Current time: {FormatHour(currentHour)}";

            if (_currentDateDisplay != null)
                _currentDateDisplay.text = $"Date: {ResolveCurrentDate()}";
        }

        private bool TryGetCircleLocalPoint(out Vector2 localPoint)
        {
            localPoint = Vector2.zero;
            if (_circleCenter == null)
                return false;

            Mouse mouse = Mouse.current;
            if (mouse == null)
                return false;

            _rootCanvas ??= GetComponentInParent<Canvas>();
            Camera eventCamera = null;
            if (_rootCanvas != null && _rootCanvas.renderMode != RenderMode.ScreenSpaceOverlay)
                eventCamera = _rootCanvas.worldCamera;

            return RectTransformUtility.ScreenPointToLocalPointInRectangle(
                _circleCenter,
                mouse.position.ReadValue(),
                eventCamera,
                out localPoint);
        }

        private int HoursFromLocalPoint(Vector2 localPoint)
        {
            float angle = Mathf.Atan2(localPoint.x, localPoint.y) * Mathf.Rad2Deg;
            if (angle < 0f)
                angle += 360f;

            int hours = Mathf.RoundToInt(angle / GetDegreesPerHour());
            if (hours <= 0)
                hours = _maxHours;

            return Mathf.Clamp(hours, _minHours, _maxHours);
        }

        private float ResolveCurrentHour()
        {
            TimeOfDay timeOfDay = UnityEngine.Object.FindFirstObjectByType<TimeOfDay>();
            return timeOfDay != null ? Mathf.Repeat(timeOfDay.Hour, 24f) : Mathf.Clamp(_startHour, 0f, 23f);
        }

        private string ResolveCurrentDate()
        {
            Calendar calendar = UnityEngine.Object.FindFirstObjectByType<Calendar>();
            return calendar != null ? calendar.DateString : "Unknown date";
        }

        private string BuildRecoveryPreview(int hours)
        {
            if (_secondaryPreviewProvider != null)
                return _secondaryPreviewProvider(hours);

            float energyRecoveryPercent = Mathf.Clamp(hours * _staminaPerHour, 0f, 100f);
            return $"You will recover {energyRecoveryPercent:0.#}% Energy";
        }

        private float GetDegreesPerHour()
        {
            return 360f / Mathf.Max(1, _maxHours);
        }

        private static string FormatHour(float hour24)
        {
            int totalMinutes = Mathf.FloorToInt(Mathf.Repeat(hour24, 24f) * 60f);
            int hour = totalMinutes / 60;
            int minute = totalMinutes % 60;
            int displayHour = hour % 12;
            if (displayHour == 0)
                displayHour = 12;

            string meridiem = hour >= 12 ? "PM" : "AM";
            return $"{displayHour}:{minute:00} {meridiem}";
        }
    }
}
