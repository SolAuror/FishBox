using UnityEngine;
#if UNITY_EDITOR
using UnityEditor;
#endif
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
        #region Inspector Settings
        [Header("Identity")]
        [Tooltip("Inspector: tunes container id.")]
        [SerializeField] private string _containerId = string.Empty;

        [Tooltip("Inspector: tunes open prompt.")]
        [SerializeField] private string _openPrompt = "Open";
        [SerializeField] private string _lockedPrompt = "Locked";
        [Tooltip("Inspector: tunes pick snap sound cue.")]
        [SerializeField] private AudioClip _pickSnapSoundCue;
        [Tooltip("Inspector: tunes pick snap volume.")]
        [SerializeField] [Range(0f, 1f)] private float _pickSnapVolume = 0.85f;
        #endregion

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

            // Runtime-spawned crates (no editor OnValidate run) need a fallback ID so
            // save/load can distinguish them when hierarchy path isn't unique.
            if (Application.isPlaying && string.IsNullOrWhiteSpace(_containerId))
                _containerId = $"{EntityCodeUtility.ContainerPrefix}-RT-{GetInstanceID():X}";
        }

        private void OnValidate()
        {
            _containerId = EntityCodeUtility.NormalizeOrEmpty(_containerId, EntityCodeUtility.ContainerPrefix);
#if UNITY_EDITOR
 // Prefab assets must not carry a ContainerId - otherwise every scene instance
            // inherits the same code and collides on save/load. Instances get a unique
            // code; a collision with another instance is treated as "unassigned" and
            // reassigned to the next free code.
            if (PrefabUtility.IsPartOfPrefabAsset(this))
            {
                if (!string.IsNullOrEmpty(_containerId))
                    _containerId = string.Empty;
                return;
            }

            if (HasContainerIdCollision())
                _containerId = string.Empty;

            _containerId = EntityCodeUtility.EnsureAssignedCode(
                this,
                _containerId,
                EntityCodeUtility.ContainerPrefix,
                static container => container._containerId);
#endif
        }

#if UNITY_EDITOR
        private bool HasContainerIdCollision()
        {
            if (string.IsNullOrEmpty(_containerId))
                return false;

            ContainerInteractable[] all = Resources.FindObjectsOfTypeAll<ContainerInteractable>();
            for (int i = 0; i < all.Length; i++)
            {
                ContainerInteractable other = all[i];
                if (other == null || other == this)
                    continue;
                if (PrefabUtility.IsPartOfPrefabAsset(other))
                    continue;
                if (string.Equals(other._containerId, _containerId, System.StringComparison.Ordinal))
                    return true;
            }
            return false;
        }
#endif

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
