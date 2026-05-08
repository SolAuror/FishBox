using System.Collections.Generic;
using System.IO;
using System.Text;
using Sol;
using Sol.Quests;
using UnityEditor;
using UnityEngine;

namespace Sol.Editor
{
    public static class QuestAuthoringEditorUtility
    {
        public const string DefaultQuestFolder = "Assets/Data/QuestData";

        public static QuestDefinition CreateQuestAsset(QuestAuthoringTemplate template, string title = null)
        {
            EnsureDefaultFolder();

            string resolvedTitle = string.IsNullOrWhiteSpace(title)
                ? DefaultTitleFor(template)
                : title.Trim();

            QuestDefinition quest = ScriptableObject.CreateInstance<QuestDefinition>();
            ApplyTemplate(quest, template, resolvedTitle, assignNewId: true);

            string fileName = BuildFileName(quest.QuestId, resolvedTitle);
            string path = AssetDatabase.GenerateUniqueAssetPath($"{DefaultQuestFolder}/{fileName}.asset");
            AssetDatabase.CreateAsset(quest, path);
            AssetDatabase.SaveAssets();
            AssetDatabase.ImportAsset(path);

            QuestRegistry.ForceEditorSyncNow();
            return AssetDatabase.LoadAssetAtPath<QuestDefinition>(path);
        }

        public static QuestDefinition DuplicateQuestAsset(QuestDefinition source)
        {
            if (source == null)
                return null;

            string sourcePath = AssetDatabase.GetAssetPath(source);
            if (string.IsNullOrWhiteSpace(sourcePath))
                return null;

            string folder = Path.GetDirectoryName(sourcePath)?.Replace("\\", "/") ?? DefaultQuestFolder;
            string newId = NextQuestId();
            string copyTitle = string.IsNullOrWhiteSpace(source.Title) ? "Quest" : $"{source.Title} Copy";
            string fileName = BuildFileName(newId, copyTitle);
            string targetPath = AssetDatabase.GenerateUniqueAssetPath($"{folder}/{fileName}.asset");
            if (!AssetDatabase.CopyAsset(sourcePath, targetPath))
                return null;

            AssetDatabase.ImportAsset(targetPath);
            QuestDefinition duplicated = AssetDatabase.LoadAssetAtPath<QuestDefinition>(targetPath);
            if (duplicated != null)
            {
                SerializedObject serializedObject = new(duplicated);
                serializedObject.Update();
                SetString(serializedObject, "_questId", newId);
                SetString(serializedObject, "_title", copyTitle);
                serializedObject.ApplyModifiedPropertiesWithoutUndo();
                EditorUtility.SetDirty(duplicated);
            }

            AssetDatabase.SaveAssets();
            QuestRegistry.ForceEditorSyncNow();
            return duplicated;
        }

        public static void ApplyTemplate(QuestDefinition quest, QuestAuthoringTemplate template, string title = null, bool assignNewId = false)
        {
            if (quest == null)
                return;

            Undo.RecordObject(quest, "Apply Quest Template");
            SerializedObject serializedObject = new(quest);
            serializedObject.Update();

            SerializedProperty questId = serializedObject.FindProperty("_questId");
            if (questId != null && (assignNewId || string.IsNullOrWhiteSpace(questId.stringValue)))
                questId.stringValue = NextQuestId();

            if (!string.IsNullOrWhiteSpace(title))
                SetString(serializedObject, "_title", title.Trim());

            SetEnum(serializedObject, "_authoringTemplate", (int)template);

            SerializedProperty objectives = serializedObject.FindProperty("_objectives");
            if (objectives != null && objectives.arraySize == 0)
                AddTemplateObjective(objectives, template);

            serializedObject.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(quest);
        }

        public static List<string> BuildTemplateOverwritePreview(QuestAuthoringTemplate template)
        {
            List<string> changes = new() { "Authoring template marker" };

            switch (template)
            {
                case QuestAuthoringTemplate.FishCatch:
                    changes.Add("First objective → CatchCount");
                    break;
                case QuestAuthoringTemplate.Delivery:
                    changes.Add("First objective → DeliverItem");
                    break;
                case QuestAuthoringTemplate.TalkTo:
                    changes.Add("First objective → TalkToNpc");
                    break;
                case QuestAuthoringTemplate.Collect:
                    changes.Add("First objective → CollectItem");
                    break;
            }

            return changes;
        }

        public static string NextQuestId()
        {
            QuestRegistry registry = QuestRegistry.Get();
            return NextQuestIdFromEntries(registry?.Quests);
        }

        internal static string NextQuestIdFromEntries(IReadOnlyList<QuestDefinition> entries)
        {
            HashSet<int> used = new();
            if (entries != null)
            {
                for (int i = 0; i < entries.Count; i++)
                {
                    string id = entries[i]?.QuestId;
                    if (EntityCodeUtility.TryParse(id, EntityCodeUtility.QuestPrefix, out int numeric))
                        used.Add(numeric);
                }
            }

            for (int next = 0; next <= 99999; next++)
            {
                if (!used.Contains(next))
                    return EntityCodeUtility.Format(EntityCodeUtility.QuestPrefix, next);
            }

            return string.Empty;
        }

        public static string GetAssetPath(QuestDefinition quest)
        {
            if (quest == null)
                return string.Empty;
            return AssetDatabase.GetAssetPath(quest);
        }

        private static void EnsureDefaultFolder()
        {
            EnsureFolderRecursive(DefaultQuestFolder);
        }

        private static void EnsureFolderRecursive(string folder)
        {
            if (string.IsNullOrWhiteSpace(folder) || AssetDatabase.IsValidFolder(folder))
                return;

            string parent = Path.GetDirectoryName(folder)?.Replace("\\", "/");
            string leaf = Path.GetFileName(folder);
            if (string.IsNullOrEmpty(parent) || string.IsNullOrEmpty(leaf))
                return;

            EnsureFolderRecursive(parent);
            if (!AssetDatabase.IsValidFolder(folder))
                AssetDatabase.CreateFolder(parent, leaf);
        }

        private static string DefaultTitleFor(QuestAuthoringTemplate template)
        {
            return template switch
            {
                QuestAuthoringTemplate.FishCatch => "New Fishing Quest",
                QuestAuthoringTemplate.Delivery => "New Delivery Quest",
                QuestAuthoringTemplate.TalkTo => "New Conversation Quest",
                QuestAuthoringTemplate.Collect => "New Collection Quest",
                _ => "New Quest"
            };
        }

        private static string BuildFileName(string questId, string title)
        {
            string slug = Slugify(title);
            if (string.IsNullOrWhiteSpace(questId))
                return slug;
            if (string.IsNullOrWhiteSpace(slug))
                return questId;
            return $"{questId}_{slug}";
        }

        private static string Slugify(string source)
        {
            if (string.IsNullOrWhiteSpace(source))
                return string.Empty;

            StringBuilder sb = new(source.Length);
            foreach (char c in source.Trim())
            {
                if (char.IsLetterOrDigit(c))
                    sb.Append(char.ToLowerInvariant(c));
            }

            return sb.ToString();
        }

        private static void AddTemplateObjective(SerializedProperty objectives, QuestAuthoringTemplate template)
        {
            QuestObjectiveType type;
            int count = 1;
            switch (template)
            {
                case QuestAuthoringTemplate.FishCatch:
                    type = QuestObjectiveType.CatchCount;
                    count = 3;
                    break;
                case QuestAuthoringTemplate.Delivery:
                    type = QuestObjectiveType.DeliverItem;
                    break;
                case QuestAuthoringTemplate.TalkTo:
                    type = QuestObjectiveType.TalkToNpc;
                    break;
                case QuestAuthoringTemplate.Collect:
                    type = QuestObjectiveType.CollectItem;
                    count = 3;
                    break;
                default:
                    return;
            }

            objectives.arraySize = 1;
            SerializedProperty objective = objectives.GetArrayElementAtIndex(0);
            objective.FindPropertyRelative(nameof(QuestObjective.Type)).enumValueIndex = (int)type;
            objective.FindPropertyRelative(nameof(QuestObjective.Count)).intValue = count;
            objective.FindPropertyRelative(nameof(QuestObjective.DisplayText)).stringValue = string.Empty;
        }

        private static void SetString(SerializedObject serializedObject, string propertyName, string value)
        {
            SerializedProperty property = serializedObject.FindProperty(propertyName);
            if (property != null)
                property.stringValue = value ?? string.Empty;
        }

        private static void SetEnum(SerializedObject serializedObject, string propertyName, int value)
        {
            SerializedProperty property = serializedObject.FindProperty(propertyName);
            if (property != null)
                property.enumValueIndex = value;
        }
    }
}
