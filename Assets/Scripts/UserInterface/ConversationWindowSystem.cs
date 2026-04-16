using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.InputSystem;
using UnityEngine.UI;
using Sol.AI;

namespace Sol.HUD
{
    public class ConversationWindowSystem : MonoBehaviour
    {
        [SerializeField] private Image _backdropImage;
        [SerializeField] private RectTransform _panelRoot;
        [SerializeField] private Image _icon;
        [SerializeField] private TextMeshProUGUI _titleText;
        [SerializeField] private TextMeshProUGUI _descriptionText;
        [SerializeField] private RectTransform _optionsRoot;
        [SerializeField] private Button _optionTemplateButton;
        [SerializeField] private Button _cancelButton;
        [SerializeField] private TextMeshProUGUI _topicHintText;
        [SerializeField] private TextMeshProUGUI _speakerRoleText;
        [SerializeField] private string _topicHint = "Topics";
        [SerializeField] private int _maxVisibleOptions = 8;
        [SerializeField] private string _speakerRoleLine = "Conversation";

        public static ConversationWindowSystem Instance { get; private set; }
        public bool IsVisible => gameObject.activeInHierarchy && _panelRoot != null && _panelRoot.gameObject.activeSelf;

        private readonly List<Button> _spawnedOptionButtons = new();
        private Action<int> _onOptionSelected;
        private Action _onClosed;
        private Transform _speakerTransform;
        private Transform _listenerTransform;
        private AI_NPC _speakerNpc;
        private NavMeshAgent _speakerAgent;
        private bool _speakerAgentStopLockApplied;
        private bool _speakerAgentWasStopped;

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }

            Instance = this;
            UIStateOwnership.Register<ConversationWindowSystem>(this);
            AutoWire();
            SetWindowVisible(false);
        }

        private void OnDestroy()
        {
            if (_cancelButton != null)
                _cancelButton.onClick.RemoveListener(Hide);

            ReleaseConversationSpeakerLock();

            UIStateOwnership.Unregister<ConversationWindowSystem>();

            if (Instance == this)
                Instance = null;
        }

        private void Update()
        {
            if (!IsVisible)
                return;

            if (Keyboard.current != null && Keyboard.current.escapeKey.wasPressedThisFrame)
                Hide();

            TickConversationSpeakerLock();
        }

        public static ConversationWindowSystem ResolveInstance(bool activateIfInactive = true)
        {
            if (Instance != null)
                return Instance;

            ConversationWindowSystem[] found = UnityEngine.Object.FindObjectsByType<ConversationWindowSystem>(FindObjectsInactive.Include, FindObjectsSortMode.None);
            if (found == null || found.Length == 0)
                return null;

            ConversationWindowSystem resolved = found[0];
            if (activateIfInactive && resolved != null && !resolved.gameObject.activeSelf)
                resolved.gameObject.SetActive(true);

            if (resolved != null)
                resolved.AutoWire();

            return Instance ?? resolved;
        }

        public void ShowConversation(
            string speakerName,
            string dialogueLine,
            IReadOnlyList<string> options,
            Action<int> onOptionSelected,
            Action onClosed = null,
            Sprite speakerIcon = null,
            Transform speakerTransform = null,
            Transform listenerTransform = null)
        {
            AutoWire();
            UIStateOwnership.CloseConflictingUi(nameof(ConversationWindowSystem));

            _onOptionSelected = onOptionSelected;
            _onClosed = onClosed;

            if (_titleText != null)
                _titleText.text = string.IsNullOrWhiteSpace(speakerName) ? "Conversation" : speakerName;

            if (_descriptionText != null)
                _descriptionText.text = string.IsNullOrWhiteSpace(dialogueLine) ? string.Empty : dialogueLine;

            if (_icon != null)
            {
                bool showIcon = speakerIcon != null;
                _icon.gameObject.SetActive(showIcon);
                if (showIcon)
                    _icon.sprite = speakerIcon;
            }

            if (_topicHintText != null)
                _topicHintText.text = _topicHint;

            if (_speakerRoleText != null)
                _speakerRoleText.text = _speakerRoleLine;

            RebuildOptions(options);
            _speakerTransform = speakerTransform;
            _listenerTransform = listenerTransform;
            ApplyConversationSpeakerLock();

            if (transform is RectTransform hostRect)
                hostRect.SetAsLastSibling();

            SetWindowVisible(true);
            MenuUiUtility.BringToFront(transform.parent);
            MenuUiUtility.BringToFront(transform);
            MenuUiUtility.SetBackgroundUiRaycasts(transform, false);
            UIStateOwnership.SetUiCapture(true);

            if (_spawnedOptionButtons.Count > 0)
                MenuUiUtility.SelectButton(_spawnedOptionButtons[0]);
            else
                MenuUiUtility.SelectButton(_cancelButton);
        }

        public void Hide()
        {
            if (!IsVisible)
                return;

            if (_cancelButton != null)
                _cancelButton.onClick.RemoveListener(Hide);

            SetWindowVisible(false);
            ClearSpawnedOptions();
            MenuUiUtility.SetBackgroundUiRaycasts(transform, true);
            UIStateOwnership.SetUiCapture(false);
            ReleaseConversationSpeakerLock();
            _speakerTransform = null;
            _listenerTransform = null;

            Action closed = _onClosed;
            _onOptionSelected = null;
            _onClosed = null;
            closed?.Invoke();
        }

        private void RebuildOptions(IReadOnlyList<string> options)
        {
            ClearSpawnedOptions();
            if (_optionTemplateButton == null || _optionsRoot == null)
                return;

            _optionTemplateButton.gameObject.SetActive(false);

            int optionCount = options != null ? options.Count : 0;
            if (optionCount <= 0)
            {
                SpawnOption(0, "Continue");
                return;
            }

            int clampedCount = Mathf.Clamp(optionCount, 1, Mathf.Max(1, _maxVisibleOptions));
            for (int i = 0; i < clampedCount; i++)
            {
                string label = options[i];
                if (string.IsNullOrWhiteSpace(label))
                    label = "...";

                SpawnOption(i, label);
            }
        }

        private void SpawnOption(int optionIndex, string label)
        {
            Button optionButton = Instantiate(_optionTemplateButton, _optionsRoot);
            optionButton.gameObject.SetActive(true);
            optionButton.name = $"Option_{optionIndex + 1:00}";
            optionButton.onClick.RemoveAllListeners();
            optionButton.onClick.AddListener(() => SelectOption(optionIndex));

            TextMeshProUGUI labelText = optionButton.GetComponentInChildren<TextMeshProUGUI>(true);
            if (labelText != null)
                labelText.text = $"> {label}";

            _spawnedOptionButtons.Add(optionButton);
        }

        private void SelectOption(int optionIndex)
        {
            Action<int> callback = _onOptionSelected;
            callback?.Invoke(optionIndex);
        }

        private void ClearSpawnedOptions()
        {
            for (int i = 0; i < _spawnedOptionButtons.Count; i++)
            {
                Button button = _spawnedOptionButtons[i];
                if (button != null)
                    Destroy(button.gameObject);
            }

            _spawnedOptionButtons.Clear();
        }

        private void AutoWire()
        {
            if (_backdropImage == null)
                _backdropImage = FindDeep(transform, "Backdrop")?.GetComponent<Image>();
            if (_panelRoot == null)
                _panelRoot = FindDeep(transform, "Panel") as RectTransform;
            if (_icon == null)
                _icon = FindDeep(transform, "Icon")?.GetComponent<Image>();
            if (_titleText == null)
                _titleText = FindDeep(transform, "Title")?.GetComponent<TextMeshProUGUI>();
            if (_descriptionText == null)
                _descriptionText = FindDeep(transform, "Description")?.GetComponent<TextMeshProUGUI>();
            if (_optionsRoot == null)
                _optionsRoot = FindDeep(transform, "OptionsList") as RectTransform;
            if (_optionTemplateButton == null)
                _optionTemplateButton = FindDeep(transform, "OptionTemplate")?.GetComponent<Button>();
            if (_cancelButton == null)
                _cancelButton = FindDeep(transform, "Cancel")?.GetComponent<Button>();
            if (_topicHintText == null)
                _topicHintText = FindDeep(transform, "TopicHint")?.GetComponent<TextMeshProUGUI>();
            if (_speakerRoleText == null)
                _speakerRoleText = FindDeep(transform, "SpeakerRole")?.GetComponent<TextMeshProUGUI>();

            if (_cancelButton != null)
            {
                _cancelButton.onClick.RemoveListener(Hide);
                _cancelButton.onClick.AddListener(Hide);
            }

            if (_optionTemplateButton != null && _optionTemplateButton.transform.parent == _optionsRoot)
                _optionTemplateButton.gameObject.SetActive(false);
        }

        private void SetWindowVisible(bool visible)
        {
            if (gameObject.activeSelf != visible)
                gameObject.SetActive(visible);

            if (_backdropImage != null)
                _backdropImage.gameObject.SetActive(visible);
            if (_panelRoot != null)
                _panelRoot.gameObject.SetActive(visible);
        }

        private void ApplyConversationSpeakerLock()
        {
            ReleaseConversationSpeakerLock();

            if (_speakerTransform == null)
                return;

            _speakerNpc = _speakerTransform.GetComponent<AI_NPC>();
            if (_speakerNpc != null && _speakerNpc.CurrentState == AI_NPC.State.Dead)
                return;

            _speakerAgent = _speakerNpc != null
                ? _speakerNpc.Agent
                : _speakerTransform.GetComponent<NavMeshAgent>();

            if (_speakerAgent != null && _speakerAgent.isOnNavMesh)
            {
                _speakerAgentWasStopped = _speakerAgent.isStopped;
                _speakerAgent.ResetPath();
                _speakerAgent.isStopped = true;
                _speakerAgentStopLockApplied = true;
            }
        }

        private void TickConversationSpeakerLock()
        {
            if (_speakerTransform == null)
                return;

            if (_speakerAgent != null && _speakerAgent.isOnNavMesh)
            {
                _speakerAgent.isStopped = true;
                if (_speakerAgent.hasPath)
                    _speakerAgent.ResetPath();
            }

            if (_listenerTransform == null)
                return;

            Vector3 toListener = _listenerTransform.position - _speakerTransform.position;
            toListener.y = 0f;
            if (toListener.sqrMagnitude <= 0.0001f)
                return;

            Quaternion targetRotation = Quaternion.LookRotation(toListener.normalized, Vector3.up);
            _speakerTransform.rotation = Quaternion.Slerp(
                _speakerTransform.rotation,
                targetRotation,
                Time.unscaledDeltaTime * 14f);
        }

        private void ReleaseConversationSpeakerLock()
        {
            if (_speakerAgentStopLockApplied && _speakerAgent != null && _speakerAgent.isOnNavMesh)
                _speakerAgent.isStopped = _speakerAgentWasStopped;

            _speakerNpc = null;
            _speakerAgent = null;
            _speakerAgentStopLockApplied = false;
            _speakerAgentWasStopped = false;
        }

        private static Transform FindDeep(Transform parent, string name)
        {
            if (parent == null)
                return null;

            if (parent.name == name)
                return parent;

            for (int i = 0; i < parent.childCount; i++)
            {
                Transform result = FindDeep(parent.GetChild(i), name);
                if (result != null)
                    return result;
            }

            return null;
        }
    }
}
