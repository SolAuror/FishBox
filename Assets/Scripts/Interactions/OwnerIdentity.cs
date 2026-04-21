using UnityEngine;

namespace Sol
{
    /// <summary>
    /// Stable ownership marker used by theft, trade, and save/load systems.
    /// Add this to actors or authored owner roots that should be referenceable by ID.
    /// </summary>
    public sealed class OwnerIdentity : MonoBehaviour
    {
        #region Inspector Settings
        [Tooltip("Inspector: tunes owner id.")]
        [SerializeField] private string _ownerId = string.Empty;
        #endregion

        public string OwnerId => NormalizeOwnerId(_ownerId);

        public static string ResolveOwnerId(GameObject owner)
        {
            return OwnerRegistry.ResolveOwnerId(owner);
        }

        private void Reset()
        {
            EnsureOwnerId();
        }

        private void Awake()
        {
            EnsureOwnerId();
        }

        private void OnValidate()
        {
            EnsureOwnerId();
        }

        private void EnsureOwnerId()
        {
            if (gameObject.CompareTag("Player"))
            {
                _ownerId = EntityCodeUtility.DefaultPlayerOwnerId;
                return;
            }

            _ownerId = EntityCodeUtility.NormalizeOrEmpty(_ownerId, EntityCodeUtility.OwnerPrefix);
#if UNITY_EDITOR
            _ownerId = EntityCodeUtility.EnsureAssignedCode(
                this,
                _ownerId,
                EntityCodeUtility.OwnerPrefix,
                static identity => identity._ownerId);
#endif
        }

        private static string NormalizeOwnerId(string rawOwnerId)
        {
            if (string.IsNullOrWhiteSpace(rawOwnerId))
                return string.Empty;

            string trimmed = rawOwnerId.Trim();
            if (string.Equals(trimmed, EntityCodeUtility.DefaultPlayerOwnerId, System.StringComparison.OrdinalIgnoreCase))
                return EntityCodeUtility.DefaultPlayerOwnerId;

            return EntityCodeUtility.NormalizeOrEmpty(trimmed, EntityCodeUtility.OwnerPrefix);
        }
    }
}
