using System;
using System.Collections.Generic;
using UnityEngine;

namespace Sol.Rpg
{
    [Serializable]
    public sealed class GameplayTagReference
    {
        [GameplayTagIdDropdown]
        [SerializeField] private string _tagId = string.Empty;

        public string TagId => _tagId;

        public GameplayTagReference()
        {
        }

        public GameplayTagReference(string tagId)
        {
            _tagId = tagId;
            Normalize();
        }

        public void Normalize()
        {
            _tagId = EntityCodeUtility.NormalizeOrEmpty(_tagId, RpgDefinitionIds.TagPrefix);
        }
    }

    [Serializable]
    public sealed class GameplayTagSet
    {
        public static readonly GameplayTagSet Empty = new();

        [SerializeField] private List<GameplayTagReference> _tags = new();

        [NonSerialized] private HashSet<string> _ids;
        [NonSerialized] private HashSet<string> _paths;
        [NonSerialized] private HashSet<string> _runtimeIds;
        [NonSerialized] private HashSet<string> _runtimePaths;

        public IReadOnlyList<GameplayTagReference> Tags => _tags;

        public void Normalize()
        {
            _tags ??= new List<GameplayTagReference>();
            HashSet<string> seen = new(StringComparer.OrdinalIgnoreCase);
            for (int i = _tags.Count - 1; i >= 0; i--)
            {
                GameplayTagReference reference = _tags[i];
                if (reference == null)
                {
                    _tags.RemoveAt(i);
                    continue;
                }

                reference.Normalize();
                if (string.IsNullOrWhiteSpace(reference.TagId) || !seen.Add(reference.TagId))
                    _tags.RemoveAt(i);
            }

            ClearCache();
        }

        public void AddSerializedTagId(string tagId)
        {
            if (ReferenceEquals(this, Empty))
                return;

            string normalizedId = EntityCodeUtility.NormalizeOrEmpty(tagId, RpgDefinitionIds.TagPrefix);
            if (string.IsNullOrEmpty(normalizedId))
                return;

            _tags ??= new List<GameplayTagReference>();
            for (int i = 0; i < _tags.Count; i++)
            {
                if (string.Equals(_tags[i]?.TagId, normalizedId, StringComparison.OrdinalIgnoreCase))
                    return;
            }

            _tags.Add(new GameplayTagReference(normalizedId));
            ClearCache();
        }

        public void AddSerializedTagPath(string tagPath)
        {
            if (ReferenceEquals(this, Empty))
                return;

            string normalizedPath = GameplayTagUtility.NormalizePathOrEmpty(tagPath);
            if (string.IsNullOrEmpty(normalizedPath))
                return;

            GameplayTagDefinition definition = RpgDefinitionRegistry.Get()?.GetTagByPath(normalizedPath);
            if (definition != null)
            {
                AddSerializedTagId(definition.Id);
                return;
            }

            AddRuntimeTagPath(normalizedPath);
        }

        public void RemoveSerializedTagId(string tagId)
        {
            if (ReferenceEquals(this, Empty) || _tags == null)
                return;

            string normalizedId = EntityCodeUtility.NormalizeOrEmpty(tagId, RpgDefinitionIds.TagPrefix);
            if (string.IsNullOrEmpty(normalizedId))
                return;

            for (int i = _tags.Count - 1; i >= 0; i--)
            {
                if (string.Equals(_tags[i]?.TagId, normalizedId, StringComparison.OrdinalIgnoreCase))
                    _tags.RemoveAt(i);
            }

            ClearCache();
        }

        public void RemoveSerializedTagPath(string tagPath)
        {
            if (ReferenceEquals(this, Empty) || _tags == null)
                return;

            string normalizedPath = GameplayTagUtility.NormalizePathOrEmpty(tagPath);
            if (string.IsNullOrEmpty(normalizedPath))
                return;

            GameplayTagDefinition definition = RpgDefinitionRegistry.Get()?.GetTagByPath(normalizedPath);
            if (definition != null)
                RemoveSerializedTagId(definition.Id);
        }

        public bool HasExact(string tagIdOrPath)
        {
            if (string.IsNullOrWhiteSpace(tagIdOrPath))
                return false;

            EnsureCache();
            string normalizedId = EntityCodeUtility.NormalizeOrEmpty(tagIdOrPath, RpgDefinitionIds.TagPrefix);
            if (!string.IsNullOrEmpty(normalizedId) && _ids.Contains(normalizedId))
                return true;

            string normalizedPath = GameplayTagUtility.NormalizePathOrEmpty(tagIdOrPath);
            return !string.IsNullOrEmpty(normalizedPath) && _paths.Contains(normalizedPath);
        }

        public bool HasAny(params string[] tagIdsOrPaths)
        {
            if (tagIdsOrPaths == null)
                return false;

            for (int i = 0; i < tagIdsOrPaths.Length; i++)
            {
                if (HasExact(tagIdsOrPaths[i]))
                    return true;
            }

            return false;
        }

        public bool HasAll(params string[] tagIdsOrPaths)
        {
            if (tagIdsOrPaths == null || tagIdsOrPaths.Length == 0)
                return true;

            for (int i = 0; i < tagIdsOrPaths.Length; i++)
            {
                if (!HasExact(tagIdsOrPaths[i]))
                    return false;
            }

            return true;
        }

        public bool HasNone(params string[] tagIdsOrPaths)
        {
            return !HasAny(tagIdsOrPaths);
        }

        public bool HasTagOrChild(string tagPath)
        {
            string normalizedPath = GameplayTagUtility.NormalizePathOrEmpty(tagPath);
            if (string.IsNullOrEmpty(normalizedPath))
                return false;

            EnsureCache();
            foreach (string candidate in _paths)
            {
                if (GameplayTagUtility.IsChildOfOrEqual(candidate, normalizedPath))
                    return true;
            }

            return false;
        }

        public bool HasAllTagsOrChildren(GameplayTagSet requiredTags)
        {
            if (requiredTags == null)
                return true;

            foreach (string path in requiredTags.EnumerateTagPaths())
            {
                if (!HasTagOrChild(path))
                    return false;
            }

            return true;
        }

        public bool HasNoTagsOrChildren(GameplayTagSet forbiddenTags)
        {
            if (forbiddenTags == null)
                return true;

            foreach (string path in forbiddenTags.EnumerateTagPaths())
            {
                if (HasTagOrChild(path))
                    return false;
            }

            return true;
        }

        public bool HasAnyTagOrChild(GameplayTagSet candidateTags)
        {
            if (candidateTags == null)
                return false;

            foreach (string path in candidateTags.EnumerateTagPaths())
            {
                if (HasTagOrChild(path))
                    return true;
            }

            return false;
        }

        public bool IsEmpty()
        {
            EnsureCache();
            return _ids.Count == 0 && _paths.Count == 0;
        }

        public IEnumerable<string> EnumerateTagIds()
        {
            if (_tags == null)
                yield break;

            for (int i = 0; i < _tags.Count; i++)
            {
                string tagId = _tags[i]?.TagId;
                if (!string.IsNullOrWhiteSpace(tagId))
                    yield return tagId.Trim();
            }
        }

        public IEnumerable<string> EnumerateTagPaths()
        {
            EnsureCache();
            foreach (string path in _paths)
                yield return path;
        }

        public void AddRuntimeTagPath(string tagPath)
        {
            if (ReferenceEquals(this, Empty))
                return;

            string normalizedPath = GameplayTagUtility.NormalizePathOrEmpty(tagPath);
            if (string.IsNullOrEmpty(normalizedPath))
                return;

            _runtimePaths ??= new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            _runtimePaths.Add(normalizedPath);
            ClearCache();
        }

        public void AddRuntimeTagPaths(IEnumerable<string> tagPaths)
        {
            if (tagPaths == null)
                return;

            foreach (string path in tagPaths)
                AddRuntimeTagPath(path);
        }

        public void AddRuntimeTagId(string tagId)
        {
            if (ReferenceEquals(this, Empty))
                return;

            string normalizedId = EntityCodeUtility.NormalizeOrEmpty(tagId, RpgDefinitionIds.TagPrefix);
            if (string.IsNullOrEmpty(normalizedId))
                return;

            _runtimeIds ??= new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            _runtimeIds.Add(normalizedId);
            ClearCache();
        }

        public void RemoveRuntimeTagPath(string tagPath)
        {
            if (ReferenceEquals(this, Empty))
                return;

            string normalizedPath = GameplayTagUtility.NormalizePathOrEmpty(tagPath);
            if (string.IsNullOrEmpty(normalizedPath) || _runtimePaths == null)
                return;

            _runtimePaths.Remove(normalizedPath);
            ClearCache();
        }

        public void RemoveRuntimeTagId(string tagId)
        {
            if (ReferenceEquals(this, Empty))
                return;

            string normalizedId = EntityCodeUtility.NormalizeOrEmpty(tagId, RpgDefinitionIds.TagPrefix);
            if (string.IsNullOrEmpty(normalizedId) || _runtimeIds == null)
                return;

            _runtimeIds.Remove(normalizedId);
            ClearCache();
        }

        public void ClearRuntimeTagsWithPrefix(string tagPathPrefix)
        {
            if (ReferenceEquals(this, Empty))
                return;

            if (_runtimePaths == null || _runtimePaths.Count == 0)
                return;

            string prefix = GameplayTagUtility.NormalizePathOrEmpty(tagPathPrefix);
            if (string.IsNullOrEmpty(prefix))
                return;

            _runtimePaths.RemoveWhere(path => GameplayTagUtility.IsChildOfOrEqual(path, prefix));
            ClearCache();
        }

        public List<string> CaptureTagPaths()
        {
            List<string> result = new();
            foreach (string path in EnumerateTagPaths())
            {
                if (!string.IsNullOrWhiteSpace(path) && !result.Contains(path))
                    result.Add(path);
            }

            result.Sort(StringComparer.OrdinalIgnoreCase);
            return result;
        }

        public void ClearCache()
        {
            _ids = null;
            _paths = null;
        }

        private void EnsureCache()
        {
            if (_ids != null && _paths != null)
                return;

            _ids = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            _paths = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            RpgDefinitionRegistry registry = RpgDefinitionRegistry.Get();
            if (_tags == null)
                return;

            for (int i = 0; i < _tags.Count; i++)
            {
                string tagId = _tags[i]?.TagId;
                if (string.IsNullOrWhiteSpace(tagId))
                    continue;

                string normalizedId = EntityCodeUtility.NormalizeOrEmpty(tagId, RpgDefinitionIds.TagPrefix);
                if (string.IsNullOrEmpty(normalizedId))
                    continue;

                _ids.Add(normalizedId);
                GameplayTagDefinition definition = registry != null ? registry.GetTag(normalizedId) : null;
                if (definition != null && !string.IsNullOrWhiteSpace(definition.TagPath))
                    _paths.Add(definition.TagPath);
            }

            if (_runtimeIds != null)
            {
                foreach (string runtimeId in _runtimeIds)
                {
                    if (string.IsNullOrWhiteSpace(runtimeId))
                        continue;

                    _ids.Add(runtimeId);
                    GameplayTagDefinition definition = registry != null ? registry.GetTag(runtimeId) : null;
                    if (definition != null && !string.IsNullOrWhiteSpace(definition.TagPath))
                        _paths.Add(definition.TagPath);
                }
            }

            if (_runtimePaths == null)
                return;

            foreach (string runtimePath in _runtimePaths)
            {
                if (!string.IsNullOrWhiteSpace(runtimePath))
                    _paths.Add(runtimePath);
            }
        }
    }
}
