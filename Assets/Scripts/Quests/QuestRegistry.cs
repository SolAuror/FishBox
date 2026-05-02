using System.Collections.Generic;
using UnityEngine;
#if UNITY_EDITOR
using UnityEditor;
#endif

namespace Sol.Quests
{
    /// <summary>
    /// Central registry of every <see cref="QuestDefinition"/> in the project.
    /// Mirror of ItemRegistry / NPCRegistry — single asset at Resources/QuestRegistry.asset,
    /// auto-synced from the project in the editor, lookup by questId.
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

#if UNITY_EDITOR
        private const string DefaultAssetPath = "Assets/Resources/QuestRegistry.asset";
        private static bool _editorSyncScheduled;
        private static bool _isEditorSynchronizing;
#endif

        public static QuestRegistry Get()
        {
            if (_instance != null)
                return _instance;

            _instance = Resources.Load<QuestRegistry>(ResourcePath);
#if UNITY_EDITOR
            if (_instance == null)
                _instance = GetOrCreateEditorAsset();
#endif
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
        private void OnValidate()
        {
            _lookup = null;
#if UNITY_EDITOR
            if (_isEditorSynchronizing)
                return;
            ScheduleEditorSync();
#endif
        }

#if UNITY_EDITOR
        internal static bool IsEditorSyncInProgress => _isEditorSynchronizing;

        [InitializeOnLoadMethod]
        private static void InitializeEditorHooks()
        {
            EditorApplication.projectChanged -= ScheduleEditorSync;
            EditorApplication.projectChanged += ScheduleEditorSync;
            ScheduleEditorSync();
        }

        public static void ScheduleEditorSync()
        {
            if (Application.isPlaying || _editorSyncScheduled)
                return;

            _editorSyncScheduled = true;
            EditorApplication.delayCall += ExecuteScheduledEditorSync;
        }

        public static void ForceEditorSyncNow()
        {
            ForceEditorSync();
        }

        [ContextMenu("Rebuild From Project")]
        private void RebuildFromProject()
        {
            ForceEditorSync();
        }

        private static void ExecuteScheduledEditorSync()
        {
            _editorSyncScheduled = false;
            if (Application.isPlaying || _isEditorSynchronizing)
                return;

            ForceEditorSync();
        }

        private static void ForceEditorSync()
        {
            QuestRegistry registry = GetOrCreateEditorAsset();
            if (registry == null)
                return;

            _isEditorSynchronizing = true;
            try
            {
                registry.SyncFromProject();
            }
            finally
            {
                _isEditorSynchronizing = false;
            }
        }

        private static QuestRegistry GetOrCreateEditorAsset()
        {
            QuestRegistry loaded = Resources.Load<QuestRegistry>(ResourcePath);
            if (loaded != null)
                return loaded;

            loaded = AssetDatabase.LoadAssetAtPath<QuestRegistry>(DefaultAssetPath);
            if (loaded != null)
                return loaded;

            const string resourcesFolder = "Assets/Resources";
            if (!AssetDatabase.IsValidFolder(resourcesFolder))
                AssetDatabase.CreateFolder("Assets", "Resources");

            QuestRegistry created = CreateInstance<QuestRegistry>();
            AssetDatabase.CreateAsset(created, DefaultAssetPath);
            AssetDatabase.SaveAssets();
            return created;
        }

        private void SyncFromProject()
        {
            List<QuestDefinition> discovered = DiscoverProjectQuestDefinitions();
            if (ReplaceQuestsIfDifferent(discovered))
            {
                _lookup = null;
                EditorUtility.SetDirty(this);
                AssetDatabase.SaveAssets();
            }
        }

        private static List<QuestDefinition> DiscoverProjectQuestDefinitions()
        {
            string[] guids = AssetDatabase.FindAssets("t:" + nameof(QuestDefinition));
            List<QuestDefinition> discovered = new(guids.Length);
            HashSet<QuestDefinition> seen = new();

            for (int i = 0; i < guids.Length; i++)
            {
                string path = AssetDatabase.GUIDToAssetPath(guids[i]);
                QuestDefinition def = AssetDatabase.LoadAssetAtPath<QuestDefinition>(path);
                if (def == null || !seen.Add(def))
                    continue;

                discovered.Add(def);
            }

            discovered.Sort(static (a, b) =>
                System.StringComparer.OrdinalIgnoreCase.Compare(
                    a != null ? a.QuestId : string.Empty,
                    b != null ? b.QuestId : string.Empty));

            return discovered;
        }

        private bool ReplaceQuestsIfDifferent(List<QuestDefinition> rebuilt)
        {
            if (_quests.Count == rebuilt.Count)
            {
                bool identical = true;
                for (int i = 0; i < _quests.Count; i++)
                {
                    if (_quests[i] != rebuilt[i])
                    {
                        identical = false;
                        break;
                    }
                }
                if (identical)
                    return false;
            }

            _quests.Clear();
            _quests.AddRange(rebuilt);
            return true;
        }
#endif
    }
}
