using UnityEngine;
using Sol.Actions;
using Sol.HUD;

namespace Sol
{
    /// <summary>
    /// World container interaction (crate/chest/barrel).
    /// Uses existing loot-mode trade UI to transfer items.
    /// </summary>
    [RequireComponent(typeof(Inventory))]
    public class ContainerInteractable : MonoBehaviour, IInteractable
    {
        [Header("Identity")]
        [SerializeField] private string _containerId = string.Empty;

        [SerializeField] private string _openPrompt = "Open";
        [SerializeField] private string _lockedPrompt = "Locked";
        [SerializeField] private AudioClip _pickSnapSoundCue;
        [SerializeField] [Range(0f, 1f)] private float _pickSnapVolume = 0.85f;

        private Inventory _inventory;
        private bool _lastInteractorHasKey;
        private string _lastInteractorKeyLabel;
        private bool _lastInteractorHasLockpick;
        public string ContainerId => _containerId;

        public string InteractionPrompt
        {
            get
            {
                if (_inventory == null)
                    return _openPrompt;

                if (ShouldShowLockedPrompt())
                    return BuildLockedPrompt();

                return _openPrompt;
            }
        }

        private void Reset()
        {
            var inventory = GetComponent<Inventory>();
            inventory?.SetContainerType(InventoryContainerType.Container);
        }

        private void Awake()
        {
            _inventory = GetComponent<Inventory>();
            _inventory?.SetContainerType(InventoryContainerType.Container);
        }

        private void OnValidate()
        {
            _containerId = EntityCodeUtility.NormalizeOrEmpty(_containerId, EntityCodeUtility.ContainerPrefix);
#if UNITY_EDITOR
            _containerId = EntityCodeUtility.EnsureAssignedCode(
                this,
                _containerId,
                EntityCodeUtility.ContainerPrefix,
                static container => container._containerId);
#endif
        }

        public bool CanInteract(Interactor interactor)
        {
            bool isValid = interactor != null
                && interactor.IsPlayer
                && interactor.Inventory != null
                && _inventory != null;

            if (!isValid)
            {
                _lastInteractorHasKey = false;
                _lastInteractorKeyLabel = string.Empty;
                _lastInteractorHasLockpick = false;
                return false;
            }

            _lastInteractorKeyLabel = _inventory.GetUsableKeyLabel(interactor);
            _lastInteractorHasKey = !string.IsNullOrEmpty(_lastInteractorKeyLabel);
            _lastInteractorHasLockpick = interactor.Inventory.HasLockpick();
            return true;
        }

        public GameAction GetInteraction(Interactor interactor)
        {
            if (!CanInteract(interactor))
                return null;

            if (_inventory.IsLocked && !TryHandleLockedInteraction(interactor))
                return null;

            // Reuse loot flow: no gold exchange, direct transfer.
            return new OpenTradeAction(interactor.Inventory, _inventory, lootMode: true);
        }

        private bool ShouldShowLockedPrompt()
        {
            return _inventory != null && _inventory.IsLocked;
        }

        private string BuildLockedPrompt()
        {
            if (_inventory == null)
                return _lockedPrompt;

            if (_lastInteractorHasKey)
                return $"{_lockedPrompt} (Use {_lastInteractorKeyLabel})";

            if (_inventory.IsLockpickable && _lastInteractorHasLockpick)
                return "Pick Lock";

            return _lockedPrompt;
        }

        private bool TryHandleLockedInteraction(Interactor interactor)
        {
            if (_inventory == null || interactor?.Inventory == null)
                return false;

            if (_inventory.HasRequiredKey(interactor))
            {
                _inventory.Unlock();
                return true;
            }

            if (!_inventory.IsLockpickable || !interactor.Inventory.HasLockpick())
                return false;

            float successChance = GetLockpickSuccessChance(_inventory.LockLevel);
            bool unlocked = Random.value <= successChance;
            if (unlocked)
            {
                _inventory.Unlock();
                ShowLockpickFeedback(unlocked: true);
                return true;
            }

            interactor.Inventory.TryConsumeLockpick();
            PlayPickSnapCue(interactor.Transform != null ? interactor.Transform.position : transform.position);
            ShowLockpickFeedback(unlocked: false);

            return false;
        }

        private static float GetLockpickSuccessChance(int lockLevel)
        {
            return Mathf.Clamp(lockLevel, 1, 5) switch
            {
                1 => 0.85f,
                2 => 0.65f,
                3 => 0.45f,
                4 => 0.30f,
                5 => 0.15f,
                _ => 0.15f
            };
        }

        private void ShowLockpickFeedback(bool unlocked)
        {
        }

        private void PlayPickSnapCue(Vector3 worldPosition)
        {
            if (_pickSnapSoundCue == null)
                return;

            AudioSource.PlayClipAtPoint(_pickSnapSoundCue, worldPosition, _pickSnapVolume);
        }
    }
}
