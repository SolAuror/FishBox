using System;
using System.Collections;
using Sol.Actions;
using Sol.HUD;
using Sol.Locomotion;
using Sol.ToD;
using UnityEngine;
using UnityEngine.UI;

namespace Sol
{
    [DisallowMultipleComponent]
    public sealed class SleepInteractable : MonoBehaviour, IInteractable
    {
        [Header("Prompt")]
        [SerializeField] private string _prompt = "Sleep";
        [SerializeField] private string _displayName = "Bed";

        [Header("Interaction")]
        [SerializeField] private InteractionPoint _interactionPoint;

        [Header("Sleep")]
        [SerializeField] [Min(1)] private int _defaultHours = 8;
        [SerializeField] [Min(1)] private int _minHours = 1;
        [SerializeField] [Min(1)] private int _maxHours = 24;
        [SerializeField] [Range(0f, 100f)] private float _healthRecoveryPercentPerHour = 12.5f;
        [SerializeField] [Range(0f, 100f)] private float _staminaRecoveryPercentPerHour = 12.5f;
        [SerializeField] [Min(0f)] private float _sleepFadeDuration = 0.5f;

        private bool _sleepSessionActive;
        private bool _sleepMenuOpened;
        private bool _sleepCompleting;
        private bool _sleepFinished;
        private bool _sleepSucceeded;
        private Interactor _activeInteractor;
        private Interactor _lastPromptInteractor;
        private InteractionSession _activeSession;
        private Coroutine _sleepRoutine;
        private GameObject _sleepFadeCanvasObject;
        private CanvasGroup _sleepFadeCanvasGroup;

        public string DisplayName => _displayName;
        public string InteractionPrompt => _sleepSessionActive ? "Sleeping..." : BuildPrompt();
        public bool SleepInteractionFinished => _sleepFinished;
        public bool SleepInteractionSucceeded => _sleepSucceeded;
        public InteractionPoint InteractionPoint => ResolveInteractionPoint();

        public bool CanInteract(Interactor interactor)
        {
            _lastPromptInteractor = interactor;

            if (_sleepSessionActive || interactor == null || !interactor.IsPlayer || interactor.Owner == null)
                return false;

            InteractionPoint point = ResolveInteractionPoint();
            return point != null && point.CanInteract(interactor);
        }

        public GameAction GetInteraction(Interactor interactor)
        {
            if (!CanInteract(interactor))
                return null;

            return new OpenSleepMenuAction(this, interactor);
        }

        public bool BeginSleepInteraction(Interactor interactor)
        {
            if (_sleepSessionActive || interactor == null || !interactor.IsPlayer)
                return false;

            InteractionPoint point = ResolveInteractionPoint();
            if (point == null)
            {
                Debug.LogWarning($"[{nameof(SleepInteractable)}] '{name}' cannot sleep because no InteractionPoint is assigned or present on the bed.", this);
                return false;
            }

            SleepMenuSystem sleepMenu = SleepMenuSystem.ResolveInstance();
            if (sleepMenu == null)
            {
                Debug.LogWarning($"[{nameof(SleepInteractable)}] SleepMenuSystem is unavailable.", this);
                return false;
            }

            if (!point.TryBeginSession(interactor, out InteractionSession session))
                return false;

            _sleepSessionActive = true;
            _sleepMenuOpened = false;
            _sleepCompleting = false;
            _sleepFinished = false;
            _sleepSucceeded = false;
            _activeInteractor = interactor;
            _activeSession = session;
            _activeSession.Ready += HandleInteractionReady;

            if (_activeSession.IsReady)
                OpenSleepMenuAtReady();

            return true;
        }

        public void TickSleepInteraction()
        {
            if (!_sleepSessionActive || _sleepFinished)
                return;

            if (_activeSession == null || _activeInteractor == null || _activeInteractor.Owner == null)
            {
                CancelSleepInteraction();
                return;
            }

            if (_activeSession.IsReady && !_sleepMenuOpened && !_sleepCompleting)
                OpenSleepMenuAtReady();
        }

        public void CancelSleepInteraction()
        {
            if (!_sleepSessionActive || _sleepFinished)
                return;

            if (_sleepRoutine != null)
            {
                StopCoroutine(_sleepRoutine);
                _sleepRoutine = null;
            }

            SleepMenuSystem sleepMenu = SleepMenuSystem.Instance;
            if (sleepMenu != null && sleepMenu.IsOpen)
                sleepMenu.Close();

            InteractionPoint point = ResolveInteractionPoint();
            if (point != null && point.IsInUseBy(_activeInteractor))
                point.EndUse(completed: false);

            FinishSleepSession(succeeded: false);
        }

        private void HandleInteractionReady(InteractionSession session)
        {
            if (session == _activeSession)
                OpenSleepMenuAtReady();
        }

        private void OpenSleepMenuAtReady()
        {
            if (!_sleepSessionActive || _sleepMenuOpened || _sleepCompleting)
                return;

            SleepMenuSystem sleepMenu = SleepMenuSystem.ResolveInstance();
            if (sleepMenu == null)
            {
                CancelSleepInteraction();
                return;
            }

            _sleepMenuOpened = true;
            sleepMenu.SetHourRange(_minHours, _maxHours);
            sleepMenu.ConfigureSecondaryPreview(BuildRecoveryPreview);
            sleepMenu.Open(HandleSleepConfirmed, HandleSleepCancelled, _defaultHours);

            if (!sleepMenu.IsOpen)
                CancelSleepInteraction();
        }

        private void HandleSleepCancelled()
        {
            if (_sleepCompleting || _sleepFinished)
                return;

            InteractionPoint point = ResolveInteractionPoint();
            if (point != null && point.IsInUseBy(_activeInteractor))
                point.EndUse(completed: false);

            FinishSleepSession(succeeded: false);
        }

        private void HandleSleepConfirmed(int selectedHours)
        {
            if (!_sleepSessionActive || _sleepCompleting)
                return;

            int hours = Mathf.Clamp(selectedHours, _minHours, _maxHours);
            if (hours <= 0)
            {
                HandleSleepCancelled();
                return;
            }

            _sleepCompleting = true;
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

            InteractionPoint point = ResolveInteractionPoint();
            if (point != null && point.IsInUseBy(_activeInteractor))
                point.EndUse(completed: true);

            FinishSleepSession(succeeded: true);
        }

        private void ApplySleepEffects(int hours)
        {
            TimeOfDay timeOfDay = FindFirstObjectByType<TimeOfDay>();
            if (timeOfDay != null)
                timeOfDay.AdvanceHours(hours);
            else
                Debug.LogWarning($"[{nameof(SleepInteractable)}] TimeOfDay not found; sleep could not advance time.", this);

            if (_healthRecoveryPercentPerHour > 0f)
            {
                if (_activeInteractor?.PlayerSoul != null)
                    _activeInteractor.PlayerSoul.Heal(_activeInteractor.PlayerSoul.MaxHealth * (_healthRecoveryPercentPerHour * 0.01f) * hours);
                else if (_activeInteractor?.NpcSoul != null)
                    _activeInteractor.NpcSoul.Heal(_activeInteractor.NpcSoul.MaxHealth * (_healthRecoveryPercentPerHour * 0.01f) * hours);
            }

            if (_staminaRecoveryPercentPerHour > 0f)
            {
                if (_activeInteractor?.PlayerSoul != null)
                    _activeInteractor.PlayerSoul.RestoreStamina(_activeInteractor.PlayerSoul.MaxStamina * (_staminaRecoveryPercentPerHour * 0.01f) * hours);
                else if (_activeInteractor?.NpcSoul != null)
                    _activeInteractor.NpcSoul.RestoreStamina(_activeInteractor.NpcSoul.MaxStamina * (_staminaRecoveryPercentPerHour * 0.01f) * hours);
            }
        }

        private string BuildRecoveryPreview(int hours)
        {
            if (_healthRecoveryPercentPerHour <= 0f && _staminaRecoveryPercentPerHour <= 0f)
                return $"ADVANCE +{hours:0}H";

            float healthPercent = _healthRecoveryPercentPerHour * hours;
            float staminaPercent = _staminaRecoveryPercentPerHour * hours;

            if (_healthRecoveryPercentPerHour <= 0f)
                return $"STAMINA +{staminaPercent:0.#}%";

            if (_staminaRecoveryPercentPerHour <= 0f)
                return $"HEAL +{healthPercent:0.#}%";

            return $"HEAL +{healthPercent:0.#}% / STAMINA +{staminaPercent:0.#}%";
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

            _sleepFadeCanvasObject = new GameObject("SleepFadeOverlay", typeof(RectTransform), typeof(Canvas), typeof(CanvasGroup));
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

        private void FinishSleepSession(bool succeeded)
        {
            if (_activeSession != null)
                _activeSession.Ready -= HandleInteractionReady;

            SleepMenuSystem sleepMenu = SleepMenuSystem.Instance;
            if (sleepMenu != null)
            {
                sleepMenu.SetHourRange(1, 24);
                sleepMenu.ResetSecondaryPreview();
            }

            if (_sleepFadeCanvasGroup != null)
                _sleepFadeCanvasGroup.alpha = 0f;

            UIStateOwnership.SetUiCapture(false);
            _sleepSucceeded = succeeded;
            _sleepFinished = true;
            _sleepSessionActive = false;
            _sleepMenuOpened = false;
            _sleepCompleting = false;
            _activeInteractor = null;
            _activeSession = null;
        }

        private string BuildPrompt()
        {
            InteractionPoint point = ResolveInteractionPoint();
            if (point != null && point.IsOwned && _lastPromptInteractor != null && !point.CanOwnerUse(_lastPromptInteractor))
                return point.InteractionPrompt;

            if (string.IsNullOrWhiteSpace(_displayName))
                return _prompt;
            return $"{_prompt} {_displayName}";
        }

        private InteractionPoint ResolveInteractionPoint()
        {
            if (_interactionPoint != null)
                return _interactionPoint;

            _interactionPoint = GetComponent<InteractionPoint>()
                ?? GetComponentInChildren<InteractionPoint>(true)
                ?? GetComponentInParent<InteractionPoint>();

            return _interactionPoint;
        }

        private void Reset()
        {
            _interactionPoint = GetComponent<InteractionPoint>() ?? GetComponentInChildren<InteractionPoint>(true);
        }

        private void OnValidate()
        {
            _prompt = string.IsNullOrWhiteSpace(_prompt) ? "Sleep" : _prompt.Trim();
            _displayName = _displayName?.Trim() ?? string.Empty;
            _defaultHours = Mathf.Max(1, _defaultHours);
            _minHours = Mathf.Max(1, _minHours);
            _maxHours = Mathf.Max(_minHours, _maxHours);
            _sleepFadeDuration = Mathf.Max(0f, _sleepFadeDuration);
        }

        private void OnDisable()
        {
            CancelSleepInteraction();
        }

        private void OnDestroy()
        {
            CancelSleepInteraction();
            if (_sleepFadeCanvasObject != null)
                Destroy(_sleepFadeCanvasObject);
        }
    }
}
