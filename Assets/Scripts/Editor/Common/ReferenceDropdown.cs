#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using Sol;
using Sol.AI;
using Sol.Grab;
using Sol.Quests;
using UnityEditor;
using UnityEditor.IMGUI.Controls;
using UnityEngine;

namespace Sol.Editor
{
    /// <summary>
    /// Reusable searchable id-dropdown widget backed by Item / NPC / Quest registries.
    /// Replaces the duplicated popup logic across the project's id-dropdown attribute drawers.
    /// </summary>
    public static class ReferenceDropdown
    {
        public enum ReferenceKind
        {
            Item,
            Npc,
            Quest
        }

        public readonly struct Entry
        {
            public readonly string Id;
            public readonly string Display;
            public readonly string MenuPath;

            public Entry(string id, string display, string menuPath)
            {
                Id = id;
                Display = display;
                MenuPath = menuPath;
            }
        }

        private const string NoneLabel = "<None>";
        private const string MissingLabelPrefix = "<Missing>";

        // Layout helpers
        public static void DrawItemLayout(GUIContent label, SerializedProperty idProp, bool allowEmpty = true)
            => DrawLayout(ReferenceKind.Item, label, idProp, allowEmpty);

        public static void DrawNpcLayout(GUIContent label, SerializedProperty idProp, bool allowEmpty = true)
            => DrawLayout(ReferenceKind.Npc, label, idProp, allowEmpty);

        public static void DrawQuestLayout(GUIContent label, SerializedProperty idProp, bool allowEmpty = true)
            => DrawLayout(ReferenceKind.Quest, label, idProp, allowEmpty);

        // Rect helpers
        public static void DrawItem(Rect rect, GUIContent label, SerializedProperty idProp, bool allowEmpty = true)
            => Draw(ReferenceKind.Item, rect, label, idProp, allowEmpty);

        public static void DrawNpc(Rect rect, GUIContent label, SerializedProperty idProp, bool allowEmpty = true)
            => Draw(ReferenceKind.Npc, rect, label, idProp, allowEmpty);

        public static void DrawQuest(Rect rect, GUIContent label, SerializedProperty idProp, bool allowEmpty = true)
            => Draw(ReferenceKind.Quest, rect, label, idProp, allowEmpty);

        public static void DrawLayout(ReferenceKind kind, GUIContent label, SerializedProperty idProp, bool allowEmpty)
        {
            Rect rect = EditorGUILayout.GetControlRect(true, EditorGUIUtility.singleLineHeight);
            Draw(kind, rect, label, idProp, allowEmpty);
        }

        public static void Draw(ReferenceKind kind, Rect rect, GUIContent label, SerializedProperty idProp, bool allowEmpty)
        {
            if (idProp == null)
                return;

            if (idProp.propertyType != SerializedPropertyType.String)
            {
                EditorGUI.PropertyField(rect, idProp, label, true);
                return;
            }

            EditorGUI.BeginProperty(rect, label, idProp);

            Rect labelRect = rect;
            Rect fieldRect = rect;
            if (label != null && !string.IsNullOrEmpty(label.text))
            {
                fieldRect = EditorGUI.PrefixLabel(rect, label);
            }

            string current = idProp.stringValue ?? string.Empty;
            List<Entry> entries = GetEntries(kind);
            Entry? matched = FindEntry(entries, current);

            string buttonText;
            bool missing = false;
            if (string.IsNullOrEmpty(current))
            {
                buttonText = NoneLabel;
            }
            else if (matched.HasValue)
            {
                buttonText = matched.Value.Display;
            }
            else
            {
                buttonText = $"{MissingLabelPrefix} {current}";
                missing = true;
            }

            // Reserve room for warning glyph if missing.
            const float warnWidth = 18f;
            Rect buttonRect = fieldRect;
            Rect warnRect = Rect.zero;
            if (missing)
            {
                buttonRect.width -= warnWidth + 2f;
                warnRect = new Rect(buttonRect.xMax + 2f, fieldRect.y, warnWidth, fieldRect.height);
            }

            if (EditorGUI.DropdownButton(buttonRect, new GUIContent(buttonText), FocusType.Keyboard))
            {
                ShowDropdown(buttonRect, kind, allowEmpty, current, selectedId =>
                {
                    idProp.stringValue = selectedId ?? string.Empty;
                    idProp.serializedObject.ApplyModifiedProperties();
                });
            }

            if (missing)
            {
                GUIContent warn = EditorGUIUtility.IconContent("console.warnicon.sml");
                warn.tooltip = $"Id '{current}' is not in the {kind} registry.";
                GUI.Label(warnRect, warn);
            }

            EditorGUI.EndProperty();
        }

        private static void ShowDropdown(Rect activator, ReferenceKind kind, bool allowEmpty, string current, Action<string> onPicked)
        {
            ReferenceAdvancedDropdown dropdown = new ReferenceAdvancedDropdown(
                new AdvancedDropdownState(),
                kind,
                allowEmpty,
                current,
                onPicked);
            dropdown.Show(activator);
        }

        private static Entry? FindEntry(List<Entry> entries, string id)
        {
            if (string.IsNullOrEmpty(id) || entries == null)
                return null;

            for (int i = 0; i < entries.Count; i++)
            {
                if (string.Equals(entries[i].Id, id, StringComparison.OrdinalIgnoreCase))
                    return entries[i];
            }
            return null;
        }

        // ----- Entry sources -----

        public static List<Entry> GetEntries(ReferenceKind kind)
        {
            return kind switch
            {
                ReferenceKind.Item => BuildItemEntries(),
                ReferenceKind.Npc => BuildNpcEntries(),
                ReferenceKind.Quest => BuildQuestEntries(),
                _ => new List<Entry>()
            };
        }

        private static List<Entry> BuildItemEntries()
        {
            List<Entry> entries = new List<Entry>();
            ItemRegistry registry = ItemRegistry.Get();
            if (registry?.Entries == null)
                return entries;

            HashSet<string> seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            for (int i = 0; i < registry.Entries.Count; i++)
            {
                ItemRegistry.Entry e = registry.Entries[i];
                if (e == null || string.IsNullOrWhiteSpace(e.ItemId))
                    continue;

                string id = e.ItemId.Trim();
                if (!seen.Add(id))
                    continue;

                string itemName = e.Prefab != null && !string.IsNullOrWhiteSpace(e.Prefab.ItemName)
                    ? e.Prefab.ItemName.Trim()
                    : id;
                string itemType = e.Prefab != null ? e.Prefab.TypeDisplayName : "Missing";
                string display = $"{itemName} ({id})";
                string menuPath = string.IsNullOrEmpty(itemType)
                    ? itemName
                    : $"{itemType}/{itemName} ({id})";

                entries.Add(new Entry(id, display, menuPath));
            }

            entries.Sort((a, b) => StringComparer.OrdinalIgnoreCase.Compare(a.MenuPath, b.MenuPath));
            return entries;
        }

        private static List<Entry> BuildNpcEntries()
        {
            List<Entry> entries = new List<Entry>();
            HashSet<string> seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            // Pin Player.
            entries.Add(new Entry(EntityCodeUtility.DefaultPlayerOwnerId, "Player (PLY00001)", "Player (PLY00001)"));
            seen.Add(EntityCodeUtility.DefaultPlayerOwnerId);

            NPCRegistry registry = NPCRegistry.Get();
            if (registry?.Entries != null)
            {
                for (int i = 0; i < registry.Entries.Count; i++)
                {
                    NPCRegistry.Entry e = registry.Entries[i];
                    if (e?.Prefab == null)
                        continue;

                    string ownerId = string.IsNullOrWhiteSpace(e.OwnerId) ? e.Prefab.OwnerId : e.OwnerId;
                    if (string.IsNullOrWhiteSpace(ownerId) || !seen.Add(ownerId))
                        continue;

                    string displayName = string.IsNullOrWhiteSpace(e.Prefab.CharacterName)
                        ? e.Prefab.gameObject.name
                        : e.Prefab.CharacterName;
                    entries.Add(new Entry(ownerId, $"{displayName} ({ownerId})", $"{displayName} ({ownerId})"));
                }
            }

            NPCSoul[] sceneSouls = UnityEngine.Object.FindObjectsByType<NPCSoul>(FindObjectsInactive.Include, FindObjectsSortMode.None);
            for (int i = 0; i < sceneSouls.Length; i++)
            {
                NPCSoul soul = sceneSouls[i];
                if (soul == null || string.IsNullOrWhiteSpace(soul.OwnerId) || !seen.Add(soul.OwnerId))
                    continue;

                string displayName = string.IsNullOrWhiteSpace(soul.CharacterName) ? soul.gameObject.name : soul.CharacterName;
                entries.Add(new Entry(soul.OwnerId, $"{displayName} ({soul.OwnerId})", $"Scene/{displayName} ({soul.OwnerId})"));
            }

            return entries;
        }

        private static List<Entry> BuildQuestEntries()
        {
            List<Entry> entries = new List<Entry>();
            HashSet<string> seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            QuestRegistry registry = QuestRegistry.Get();
            if (registry?.Quests != null)
            {
                for (int i = 0; i < registry.Quests.Count; i++)
                {
                    QuestDefinition def = registry.Quests[i];
                    if (def == null || string.IsNullOrWhiteSpace(def.QuestId))
                        continue;

                    string id = def.QuestId.Trim();
                    if (!seen.Add(id))
                        continue;

                    string title = string.IsNullOrWhiteSpace(def.Title) ? id : def.Title.Trim();
                    entries.Add(new Entry(id, $"{title} ({id})", $"{title} ({id})"));
                }
            }

            string[] guids = AssetDatabase.FindAssets("t:QuestDefinition");
            for (int i = 0; i < guids.Length; i++)
            {
                string path = AssetDatabase.GUIDToAssetPath(guids[i]);
                QuestDefinition def = AssetDatabase.LoadAssetAtPath<QuestDefinition>(path);
                if (def == null || string.IsNullOrWhiteSpace(def.QuestId))
                    continue;

                string id = def.QuestId.Trim();
                if (!seen.Add(id))
                    continue;

                string title = string.IsNullOrWhiteSpace(def.Title) ? id : def.Title.Trim();
                entries.Add(new Entry(id, $"{title} ({id})", $"{title} ({id})"));
            }

            entries.Sort((a, b) => StringComparer.OrdinalIgnoreCase.Compare(a.MenuPath, b.MenuPath));
            return entries;
        }

        // ----- AdvancedDropdown implementation -----

        private sealed class ReferenceAdvancedDropdown : AdvancedDropdown
        {
            private readonly ReferenceKind _kind;
            private readonly bool _allowEmpty;
            private readonly string _current;
            private readonly Action<string> _onPicked;
            private readonly Dictionary<int, string> _idsByItemId = new Dictionary<int, string>();

            public ReferenceAdvancedDropdown(AdvancedDropdownState state, ReferenceKind kind, bool allowEmpty, string current, Action<string> onPicked)
                : base(state)
            {
                _kind = kind;
                _allowEmpty = allowEmpty;
                _current = current ?? string.Empty;
                _onPicked = onPicked;
                minimumSize = new Vector2(320f, 320f);
            }

            protected override AdvancedDropdownItem BuildRoot()
            {
                AdvancedDropdownItem root = new AdvancedDropdownItem($"{_kind} References");

                if (_allowEmpty)
                {
                    AdvancedDropdownItem none = new AdvancedDropdownItem(NoneLabel);
                    _idsByItemId[none.id] = string.Empty;
                    root.AddChild(none);
                    root.AddSeparator();
                }

                List<Entry> entries = GetEntries(_kind);
                Dictionary<string, AdvancedDropdownItem> folders = new Dictionary<string, AdvancedDropdownItem>(StringComparer.OrdinalIgnoreCase);

                for (int i = 0; i < entries.Count; i++)
                {
                    Entry entry = entries[i];
                    string menuPath = entry.MenuPath ?? entry.Display;
                    AdvancedDropdownItem parent = root;

                    int lastSlash = menuPath.LastIndexOf('/');
                    string leafLabel;
                    if (lastSlash >= 0)
                    {
                        string folderPath = menuPath.Substring(0, lastSlash);
                        leafLabel = menuPath.Substring(lastSlash + 1);
                        parent = GetOrCreateFolder(root, folderPath, folders);
                    }
                    else
                    {
                        leafLabel = menuPath;
                    }

                    AdvancedDropdownItem child = new AdvancedDropdownItem(leafLabel);
                    _idsByItemId[child.id] = entry.Id;
                    parent.AddChild(child);
                }

                return root;
            }

            private static AdvancedDropdownItem GetOrCreateFolder(AdvancedDropdownItem root, string folderPath, Dictionary<string, AdvancedDropdownItem> cache)
            {
                if (cache.TryGetValue(folderPath, out AdvancedDropdownItem existing))
                    return existing;

                string[] segments = folderPath.Split('/');
                AdvancedDropdownItem cursor = root;
                string accumulated = string.Empty;

                for (int i = 0; i < segments.Length; i++)
                {
                    accumulated = i == 0 ? segments[i] : $"{accumulated}/{segments[i]}";
                    if (cache.TryGetValue(accumulated, out AdvancedDropdownItem cached))
                    {
                        cursor = cached;
                        continue;
                    }

                    AdvancedDropdownItem folder = new AdvancedDropdownItem(segments[i]);
                    cursor.AddChild(folder);
                    cache[accumulated] = folder;
                    cursor = folder;
                }

                return cursor;
            }

            protected override void ItemSelected(AdvancedDropdownItem item)
            {
                if (item == null || !_idsByItemId.TryGetValue(item.id, out string id))
                    return;

                _onPicked?.Invoke(id);
            }
        }
    }
}
#endif
