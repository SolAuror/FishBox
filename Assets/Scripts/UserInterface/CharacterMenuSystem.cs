using TMPro;
using UnityEngine;
using UnityEngine.UI;
using Sol.AI;
using Sol.Player;

namespace Sol.HUD
{
    public sealed class CharacterMenuSystem : MenuSystemBase<CharacterMenuSystem>
    {
        [SerializeField] private RawImage _characterPreview;
        [SerializeField] private TMP_Text _characterName;
        [SerializeField] private Transform _playerRoot;

        private Button _closeButton;

        protected override void Awake()
        {
            base.Awake();
            if (_instance != this) return;
            AutoWire();
            RefreshPlayerData();
            SetOpen(false);
        }

        protected override void PostResolve() => AutoWire();

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
    }
}
