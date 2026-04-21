using UnityEngine;
using System.Collections.Generic;

namespace Sol
{
    /// <summary>
    /// Central owner-id cross-reference lookup.
    /// </summary>
    public static class OwnerRegistry
    {
        private static readonly Dictionary<string, GameObject> _ownersById =
            new Dictionary<string, GameObject>(System.StringComparer.OrdinalIgnoreCase);

        public struct OwnerEntry
        {
            public string OwnerId;
            public GameObject Owner;
        }

        public static void Register(string ownerId, GameObject owner)
        {
            string normalized = NormalizeOwnerId(ownerId);
            if (string.IsNullOrEmpty(normalized) || owner == null)
                return;

            _ownersById[normalized] = owner;
        }

        public static void Unregister(string ownerId, GameObject owner)
        {
            string normalized = NormalizeOwnerId(ownerId);
            if (string.IsNullOrEmpty(normalized))
                return;

            if (!_ownersById.TryGetValue(normalized, out GameObject registered))
                return;

            if (registered == null || registered == owner)
                _ownersById.Remove(normalized);
        }

        public static string ResolveOwnerId(GameObject owner)
        {
            if (owner == null)
                return string.Empty;

            if (owner.CompareTag("Player"))
                return EntityCodeUtility.DefaultPlayerOwnerId;

            if (owner.TryGetComponent(out Sol.Player.PlayerSoul playerSoul))
                return playerSoul.OwnerId;

            if (owner.TryGetComponent(out Sol.AI.NPCSoul npcSoul))
                return npcSoul.OwnerId;

            if (owner.TryGetComponent(out OwnerIdentity identity))
                return identity.OwnerId;

            return string.Empty;
        }

        public static List<OwnerEntry> GetEntries()
        {
            // Cleanup stale refs before exposing entries.
            List<string> stale = null;
            foreach (var kvp in _ownersById)
            {
                if (kvp.Value != null)
                    continue;

                stale ??= new List<string>();
                stale.Add(kvp.Key);
            }

            if (stale != null)
            {
                for (int i = 0; i < stale.Count; i++)
                    _ownersById.Remove(stale[i]);
            }

            List<OwnerEntry> entries = new List<OwnerEntry>(_ownersById.Count);
            foreach (var kvp in _ownersById)
            {
                entries.Add(new OwnerEntry
                {
                    OwnerId = kvp.Key,
                    Owner = kvp.Value
                });
            }

            return entries;
        }

        public static GameObject Resolve(string ownerId)
        {
            string normalizedId = NormalizeOwnerId(ownerId);
            if (string.IsNullOrEmpty(normalizedId))
                return null;

            if (string.Equals(normalizedId, EntityCodeUtility.DefaultPlayerOwnerId, System.StringComparison.OrdinalIgnoreCase))
                return GameObject.FindGameObjectWithTag("Player");

            if (_ownersById.TryGetValue(normalizedId, out GameObject direct) && direct != null)
                return direct;

            Sol.AI.NPCSoul[] npcs = Object.FindObjectsByType<Sol.AI.NPCSoul>(
                FindObjectsInactive.Include,
                FindObjectsSortMode.None);
            for (int i = 0; i < npcs.Length; i++)
            {
                Sol.AI.NPCSoul npc = npcs[i];
                if (npc == null)
                    continue;

                if (string.Equals(npc.OwnerId, normalizedId, System.StringComparison.OrdinalIgnoreCase))
                {
                    Register(npc.OwnerId, npc.gameObject);
                    return npc.gameObject;
                }
            }

            Sol.Player.PlayerSoul[] players = Object.FindObjectsByType<Sol.Player.PlayerSoul>(
                FindObjectsInactive.Include,
                FindObjectsSortMode.None);
            for (int i = 0; i < players.Length; i++)
            {
                Sol.Player.PlayerSoul player = players[i];
                if (player == null)
                    continue;

                if (string.Equals(player.OwnerId, normalizedId, System.StringComparison.OrdinalIgnoreCase))
                {
                    Register(player.OwnerId, player.gameObject);
                    return player.gameObject;
                }
            }

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
