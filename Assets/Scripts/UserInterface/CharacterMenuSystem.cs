using TMPro;
using UnityEngine;
using UnityEngine.UI;
using Sol.AI;
using Sol.Player;

namespace Sol.HUD
{
    public sealed class CharacterMenuSystem : MonoBehaviour
    {
        [SerializeField] private RawImage _characterPreview;
        [SerializeField] private TMP_Text _characterName;
        [SerializeField] private Transform _playerRoot;
        [SerializeField] private CanvasGroup _canvasGroup;

        public static CharacterMenuSystem Instance { get; private set; }
        public bool IsOpen => gameObject.activeSelf;

        private Button _closeButton;

        private void Awake()
        {
            if (Instance != null && Instance != this) { Destroy(gameObject); return; }
            Instance = this;
            _canvasGroup ??= MenuUiUtility.EnsureCanvasGroup(gameObject);
            AutoWire();
            RefreshPlayerData();
            SetOpen(false);
        }

        private void OnDestroy()
        {
            if (Instance == this)
                Instance = null;
        }

        public static CharacterMenuSystem ResolveInstance(bool activateIfInactive = true)
        {
            CharacterMenuSystem resolved = Instance ?? UIStateOwnership.Resolve<CharacterMenuSystem>(activateIfInactive);
            if (resolved != null)
                resolved.AutoWire();
            return resolved;
        }

        public void Open()
        {
            AutoWire();
            RefreshPlayerData();
            UIStateOwnership.CloseConflictingUi(nameof(CharacterMenuSystem));
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
            _characterName ??= MenuUiUtility.FindTextByNames(transform, "CharacterName", "PlayerName", "Name");
            _closeButton ??= MenuUiUtility.FindButtonByNames(transform, "Back", "CloseButton");
            MenuUiUtility.WireButton(_closeButton, Close);
        }

        private void RefreshPlayerData()
        {
            if (_playerRoot == null)
            {
                GameObject player = GameObject.FindGameObjectWithTag("Player");
                _playerRoot = player != null ? player.transform : null;
            }

            if (_characterName == null)
                return;

            string fallback = "Player";
            if (_playerRoot != null)
            {
                NPCSoul soul = _playerRoot.GetComponent<NPCSoul>();
                PlayerSoul playerSoul = _playerRoot.GetComponent<PlayerSoul>();
                if (playerSoul != null && !string.IsNullOrWhiteSpace(playerSoul.CharacterName))
                    fallback = playerSoul.CharacterName.Trim();
                else if (soul != null && !string.IsNullOrWhiteSpace(soul.CharacterName))
                    fallback = soul.CharacterName.Trim();
                else
                    fallback = _playerRoot.name;
            }

            _characterName.text = fallback;
            if (_characterPreview != null)
                _characterPreview.gameObject.SetActive(_characterPreview.texture != null);
        }

        private void SetOpen(bool open)
        {
            MenuUiUtility.SetVisible(gameObject, open);
            _canvasGroup ??= MenuUiUtility.EnsureCanvasGroup(gameObject);
            MenuUiUtility.SetCanvasGroupVisible(_canvasGroup, open);
        }
    }
}
