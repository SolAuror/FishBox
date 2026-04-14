using UnityEngine;

namespace Sol
{
    /// <summary>
    /// Lightweight scene lookup for ownership IDs.
    /// </summary>
    public static class OwnerRegistry
    {
        public static GameObject Resolve(string ownerId)
        {
            string normalizedId = NormalizeOwnerId(ownerId);
            if (string.IsNullOrEmpty(normalizedId))
                return null;

            if (string.Equals(normalizedId, EntityCodeUtility.DefaultPlayerOwnerId, System.StringComparison.OrdinalIgnoreCase))
                return GameObject.FindGameObjectWithTag("Player");

            OwnerIdentity[] identities = Object.FindObjectsByType<OwnerIdentity>(
                FindObjectsInactive.Include,
                FindObjectsSortMode.None);
            for (int i = 0; i < identities.Length; i++)
            {
                OwnerIdentity identity = identities[i];
                if (identity == null)
                    continue;

                if (string.Equals(identity.OwnerId, normalizedId, System.StringComparison.OrdinalIgnoreCase))
                    return identity.gameObject;
            }

            return null;
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
