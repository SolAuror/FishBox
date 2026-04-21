using System.Collections.Generic;
using UnityEngine;

namespace Sol.Quests
{
    /// <summary>
    /// Central registry of every <see cref="QuestDefinition"/> in the project.
 /// Mirror of ItemRegistry - single asset at Resources/QuestRegistry.asset, lookup by questId.
    /// </summary>
    [CreateAssetMenu(fileName = "QuestRegistry", menuName = "Sol/Quests/Quest Registry")]
    public class QuestRegistry : ScriptableObject
    {
        private const string ResourcePath = "QuestRegistry";
#region Inspector Settings

        [Tooltip("Inspector: tunes quests.")]
        [SerializeField] private List<QuestDefinition> _quests = new();
#endregion

        private Dictionary<string, QuestDefinition> _lookup;
        private static QuestRegistry _instance;

        public IReadOnlyList<QuestDefinition> Quests => _quests;

        public static QuestRegistry Get()
        {
            if (_instance == null)
                _instance = Resources.Load<QuestRegistry>(ResourcePath);
            return _instance;
        }

        public QuestDefinition Find(string questId)
        {
            if (string.IsNullOrWhiteSpace(questId)) return null;
            EnsureLookup();
            _lookup.TryGetValue(questId, out QuestDefinition def);
            return def;
        }

        private void EnsureLookup()
        {
            if (_lookup != null) return;
            _lookup = new Dictionary<string, QuestDefinition>(System.StringComparer.OrdinalIgnoreCase);
            for (int i = 0; i < _quests.Count; i++)
            {
                QuestDefinition q = _quests[i];
                if (q == null || string.IsNullOrWhiteSpace(q.QuestId)) continue;
                _lookup[q.QuestId] = q;
            }
        }

        private void OnEnable() { _lookup = null; }
        private void OnValidate() { _lookup = null; }
    }
}
