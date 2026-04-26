using System;
using System.Collections;
using Sol.Actions;
using Sol.AI;
using Sol.HUD;
using Sol.Locomotion;
using Sol.ToD;
using UnityEngine;
using UnityEngine.UI;

namespace Sol
{
    /// <summary>
    /// Bed-specific sleep interaction that reuses the imported sister radial menu
    /// without depending on the broader RestPoint / AI rest framework.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class BedSleepInteractable : MonoBehaviour, IInteractable
    {
        #region Inspector Settings
        [Tooltip("Inspector: tunes prompt.")]
        [SerializeField] private string _prompt = "Sleep";
        [SerializeField] private string _displayName = "Bed";
        [Tooltip("Inspector: tunes default hours.")]
        [SerializeField] [Min(1)] private int _defaultHours = 8;
        [SerializeField] [Min(1)] private int _minHours = 1;
        [Tooltip("Inspector: tunes max hours.")]
        [SerializeField] [Min(1)] private int _maxHours = 24;
        [SerializeField] [Range(0f, 100f)] private float _healthRecoveryPercentPerHour = 12.5f;
        [Tooltip("Inspector: tunes sleep fade duration.")]
        [SerializeField] [Min(0f)] private float _sleepFadeDuration = 0.5f;
        #endregion

        private bool _sleepSessionActive;
        private Interactor _activeInteractor;
        private LocomotionInput _activeLocomotionInput;
        private bool _hadControlSnapshot;
        private bool _previousPlayerControlEnabled;
        private bool _isCleaningUp;
        private Coroutine _sleepRoutine;
        private GameObject _sleepFadeCanvasObject;
        private CanvasGroup _sleepFadeCanvasGroup;

        public string DisplayName => _displayName;
        public string InteractionPrompt => _sleepSessionActive ? "Sleeping..." : _prompt;

        public bool CanInteract(Interactor interactor)
        {
            return !_sleepSessionActive
                && interactor != null
                && interactor.IsPlayer
                && interactor.Owner != null;
        }

        public GameAction GetInteraction(Interactor interactor)
        {
            if (!CanInteract(interactor))
                return null;

            return new OpenSleepMenuAction(this, interactor);
        }

        public bool TryOpenSleepMenu(Interactor interactor)
        {
            if (!CanInteract(interactor))
                return false;

            RadialMenuSystem radialMenu = RadialMenuSystem.ResolveInstance();
            if (radialMenu == null)
            {
                Debug.LogWarning("[BedSleepInteractable] RadialMenuSystem is unavailable.", this);
                return false;
            }

            BeginSleepSession(interactor);

            radialMenu.SetHourRange(_minHours, _maxHours);
            radialMenu.ConfigureSecondaryPreview(BuildRecoveryPreview);
            radialMenu.Open(HandleSleepConfirmed, HandleSleepCancelled, _defaultHours);

            if (!radialMenu.IsOpen)
            {
                CleanupSleepSession();
                return false;
            }

            return true;
        }

        private void BeginSleepSession(Interactor interactor)
        {
            _sleepSessionActive = true;
            _activeInteractor = interactor;
            _activeLocomotionInput = interactor.Owner.GetComponent<LocomotionInput>();

            if (_activeLocomotionInput != null)
            {
                _previousPlayerControlEnabled = _activeLocomotionInput.IsControlledByPlayer;
                _hadControlSnapshot = true;
                _activeLocomotionInput.IsControlledByPlayer = false;
                _activeLocomotionInput.MovementInput = Vector2.zero;
                _activeLocomotionInput.LookInput = Vector2.zero;
                _activeLocomotionInput.InteractPressed = false;
                _activeLocomotionInput.JumpPressed = false;
                _activeLocomotionInput.SprintPressed = false;
            }
            else
            {
                _hadControlSnapshot = false;
            }
        }

        private void HandleSleepCancelled()
        {
            CleanupSleepSession();
        }

        private void HandleSleepConfirmed(int selectedHours)
        {
            int hours = Mathf.Clamp(selectedHours, _minHours, _maxHours);
            if (hours <= 0)
            {
                CleanupSleepSession();
                return;
            }

            if (_sleepRoutine != null)
                StopCoroutine(_sleepRoutine);

            _sleepRoutine = StartCoroutine(CompleteSleepRoutine(hours));
        }

        private IEnumerator CompleteSleepRoutine(int hours)
        {
            UIStateOwnership.SetUiCapture(true);
            yield return FadeSleepOverlay(targetAlpha: 1f);

            ApplySleepEffects(hours);

            yield return null;
            yield return FadeSleepOverlay(targetAlpha: 0f);

            _sleepRoutine = null;
            UIStateOwnership.SetUiCapture(false);
            CleanupSleepSession();
        }

        private void ApplySleepEffects(int hours)
        {
            TimeOfDay timeOfDay = FindFirstObjectByType<TimeOfDay>();
            if (timeOfDay != null)
                timeOfDay.AdvanceHours(hours);
            else
                Debug.LogWarning("[BedSleepInteractable] TimeOfDay not found; sleep could not advance time.", this);

            float healthDelta = 0f;
            if (_healthRecoveryPercentPerHour > 0f)
            {
                if (_activeInteractor?.PlayerSoul != null)
                {
                    healthDelta = _activeInteractor.PlayerSoul.MaxHealth * (_healthRecoveryPercentPerHour * 0.01f) * hours;
                    _activeInteractor.PlayerSoul.Heal(healthDelta);
                }
                else if (_activeInteractor?.NpcSoul != null)
                {
                    healthDelta = _activeInteractor.NpcSoul.MaxHealth * (_healthRecoveryPercentPerHour * 0.01f) * hours;
                    _activeInteractor.NpcSoul.Heal(healthDelta);
                }
            }
        }

        private string BuildRecoveryPreview(int hours)
        {
            if (_healthRecoveryPercentPerHour <= 0f)
                return $"ADVANCE +{hours:0}H";

            float percent = _healthRecoveryPercentPerHour * hours;
            return $"HEAL +{percent:0.#}%";
        }

        private IEnumerator FadeSleepOverlay(float targetAlpha)
        {
            CanvasGroup fadeGroup = EnsureSleepFadeOverlay();
            if (fadeGroup == null)
                yield break;

            float duration = Mathf.Max(0f, _sleepFadeDuration);
            if (duration <= 0f)
            {
                fadeGroup.alpha = targetAlpha;
                yield break;
            }

            float startAlpha = fadeGroup.alpha;
            float elapsed = 0f;
            while (elapsed < duration)
            {
                elapsed += Time.unscaledDeltaTime;
                float t = Mathf.Clamp01(elapsed / duration);
                fadeGroup.alpha = Mathf.Lerp(startAlpha, targetAlpha, t);
                yield return null;
            }

            fadeGroup.alpha = targetAlpha;
        }

        private CanvasGroup EnsureSleepFadeOverlay()
        {
            if (_sleepFadeCanvasGroup != null)
                return _sleepFadeCanvasGroup;

            _sleepFadeCanvasObject = new GameObject(
                "SleepFadeOverlay",
                typeof(RectTransform),
                typeof(Canvas),
                typeof(CanvasGroup));
            int uiLayer = LayerMask.NameToLayer("UI");
            if (uiLayer >= 0)
                _sleepFadeCanvasObject.layer = uiLayer;

            Canvas canvas = _sleepFadeCanvasObject.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = short.MaxValue;

            _sleepFadeCanvasGroup = _sleepFadeCanvasObject.GetComponent<CanvasGroup>();
            _sleepFadeCanvasGroup.alpha = 0f;
            _sleepFadeCanvasGroup.interactable = false;
            _sleepFadeCanvasGroup.blocksRaycasts = false;

            GameObject imageObject = new GameObject("Fade", typeof(RectTransform), typeof(Image));
            imageObject.transform.SetParent(_sleepFadeCanvasObject.transform, false);
            Image image = imageObject.GetComponent<Image>();
            image.color = Color.black;
            image.raycastTarget = false;

            RectTransform rect = imageObject.GetComponent<RectTransform>();
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;

            return _sleepFadeCanvasGroup;
        }

        private void CleanupSleepSession()
        {
            if (_isCleaningUp)
                return;

            _isCleaningUp = true;
            try
            {
                RadialMenuSystem radialMenu = RadialMenuSystem.Instance;
                if (_sleepSessionActive && _sleepRoutine == null && radialMenu != null && radialMenu.IsOpen)
                {
                    radialMenu.Close();
                    radialMenu = RadialMenuSystem.Instance;
                }

                if (_sleepRoutine != null)
                {
                    StopCoroutine(_sleepRoutine);
                    _sleepRoutine = null;
                }

                if (radialMenu != null)
                {
                    radialMenu.SetHourRange(1, 24);
                    radialMenu.ResetSecondaryPreview();
                }

                if (_sleepFadeCanvasGroup != null)
                    _sleepFadeCanvasGroup.alpha = 0f;

                if (_activeLocomotionInput != null && _hadControlSnapshot)
                {
                    _activeLocomotionInput.MovementInput = Vector2.zero;
                    _activeLocomotionInput.LookInput = Vector2.zero;
                    _activeLocomotionInput.IsControlledByPlayer = _previousPlayerControlEnabled;
                }

                _activeInteractor = null;
                _activeLocomotionInput = null;
                _hadControlSnapshot = false;
                _sleepSessionActive = false;
                UIStateOwnership.SetUiCapture(false);
            }
            finally
            {
                _isCleaningUp = false;
            }
        }

        private void OnDisable()
        {
            CleanupSleepSession();
        }

        private void OnDestroy()
        {
            CleanupSleepSession();
            if (_sleepFadeCanvasObject != null)
                Destroy(_sleepFadeCanvasObject);
        }
    }
}
