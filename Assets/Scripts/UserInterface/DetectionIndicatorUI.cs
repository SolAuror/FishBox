using TMPro;
using UnityEngine;
using UnityEngine.UI;
using Sol.Locomotion;

namespace Sol.HUD
{
    public sealed class DetectionIndicatorUI : MonoBehaviour
    {
        [SerializeField] private CanvasGroup _canvasGroup;
        [SerializeField] private RectTransform _indicatorRoot;
        [SerializeField] private Image _detectionFillImage;
        [SerializeField] private Image _coreDiscImage;
        [SerializeField] private TMP_Text _stateText;
        [SerializeField] private Transform _playerTransform;
        [SerializeField] private LocomotionState _playerLocomotionState;
        [SerializeField] private string _playerTag = "Player";
        [SerializeField] private float _fadeInSpeed = 8f;
        [SerializeField] private float _fadeOutSpeed = 10f;
        [SerializeField] private float _suspiciousThreshold = 0.4f;
        [SerializeField] private Color _hiddenColor = new(0.8f, 0.76f, 0.66f, 0.95f);
        [SerializeField] private Color _suspiciousColor = new(0.92f, 0.72f, 0.34f, 1f);
        [SerializeField] private Color _detectedColor = new(0.84f, 0.24f, 0.18f, 1f);
        [SerializeField] private string _hiddenText = "HIDDEN";
        [SerializeField] private string _suspiciousText = "SEEN";
        [SerializeField] private string _detectedText = "DETECTED";
        [SerializeField] private Vector3 _hiddenScale = new(0.96f, 0.96f, 1f);
        [SerializeField] private Vector3 _detectedScale = new(1.08f, 1.08f, 1f);
        [SerializeField] private float _scaleLerpSpeed = 9f;

        private float _targetSuspicion;
        private float _displayedSuspicion;

        private void Awake()
        {
            _canvasGroup ??= MenuUiUtility.EnsureCanvasGroup(gameObject);
            if (_canvasGroup == null)
                Debug.LogWarning($"[DetectionIndicatorUI] CanvasGroup is not assigned on '{name}'. Author it in the prefab instead of relying on runtime creation.", this);
            ResolvePlayer();
            ApplyVisuals(immediate: true);
        }

        private void Update()
        {
            ResolvePlayer();
            _displayedSuspicion = Mathf.MoveTowards(_displayedSuspicion, _targetSuspicion, Time.unscaledDeltaTime);
            ApplyVisuals(immediate: false);
        }

        public void SetSuspicion(float suspicion)
        {
            _targetSuspicion = Mathf.Clamp01(suspicion);
        }

        private void ResolvePlayer()
        {
            if (_playerTransform != null)
                return;

            GameObject player = GameObject.FindGameObjectWithTag(_playerTag);
            if (player == null)
                return;

            _playerTransform = player.transform;
            _playerLocomotionState = player.GetComponent<LocomotionState>();
        }

        private void ApplyVisuals(bool immediate)
        {
            bool detected = _displayedSuspicion >= 1f;
            bool suspicious = !detected && _displayedSuspicion >= _suspiciousThreshold;
            Color targetColor = detected ? _detectedColor : suspicious ? _suspiciousColor : _hiddenColor;
            string targetText = detected ? _detectedText : suspicious ? _suspiciousText : _hiddenText;
            Vector3 targetScale = detected ? _detectedScale : _hiddenScale;
            float targetAlpha = _playerTransform != null ? 1f : 0f;
            float fadeSpeed = targetAlpha > _canvasGroup.alpha ? _fadeInSpeed : _fadeOutSpeed;

            if (_detectionFillImage != null)
            {
                _detectionFillImage.color = targetColor;
                _detectionFillImage.fillAmount = Mathf.Clamp01(_displayedSuspicion);
            }

            if (_coreDiscImage != null)
                _coreDiscImage.color = targetColor;

            if (_stateText != null)
                _stateText.text = targetText;

            if (_indicatorRoot != null)
                _indicatorRoot.localScale = Vector3.Lerp(_indicatorRoot.localScale, targetScale, immediate ? 1f : Time.unscaledDeltaTime * _scaleLerpSpeed);

            if (_canvasGroup != null)
                _canvasGroup.alpha = immediate ? targetAlpha : Mathf.MoveTowards(_canvasGroup.alpha, targetAlpha, Time.unscaledDeltaTime * fadeSpeed);
        }
    }
}
