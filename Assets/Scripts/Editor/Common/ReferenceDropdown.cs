#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using Sol;
using Sol.AI;
using Sol.Grab;
using Sol.Quests;
using Sol.Rpg;
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
            Quest,
            Shop,
            Stat,
            GameplayTag
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

        public static void DrawShopLayout(GUIContent label, SerializedProperty idProp, bool allowEmpty = true)
            => DrawLayout(ReferenceKind.Shop, label, idProp, allowEmpty);

        public static void DrawStatLayout(GUIContent label, SerializedProperty idProp, bool allowEmpty = true)
            => DrawLayout(ReferenceKind.Stat, label, idProp, allowEmpty);

        public static void DrawGameplayTagLayout(GUIContent label, SerializedProperty idProp, bool allowEmpty = true)
            => DrawLayout(ReferenceKind.GameplayTag, label, idProp, allowEmpty);

        // Rect helpers
        public static void DrawItem(Rect rect, GUIContent label, SerializedProperty idProp, bool allowEmpty = true)
            => Draw(ReferenceKind.Item, rect, label, idProp, allowEmpty);

        public static void DrawNpc(Rect rect, GUIContent label, SerializedProperty idProp, bool allowEmpty = true)
            => Draw(ReferenceKind.Npc, rect, label, idProp, allowEmpty);

        public static void DrawQuest(Rect rect, GUIContent label, SerializedProperty idProp, bool allowEmpty = true)
            => Draw(ReferenceKind.Quest, rect, label, idProp, allowEmpty);

        public static void DrawShop(Rect rect, GUIContent label, SerializedProperty idProp, bool allowEmpty = true)
            => Draw(ReferenceKind.Shop, rect, label, idProp, allowEmpty);

        public static void DrawStat(Rect rect, GUIContent label, SerializedProperty idProp, bool allowEmpty = true)
            => Draw(ReferenceKind.Stat, rect, label, idProp, allowEmpty);

        public static void DrawGameplayTag(Rect rect, GUIContent label, SerializedProperty idProp, bool allowEmpty = true)
            => Draw(ReferenceKind.GameplayTag, rect, label, idProp, allowEmpty);

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
                // Capture the owning SerializedObject too. Database windows keep cached
                // SerializedObjects, so writing through that object first prevents a stale
                // cache from overwriting the delayed AdvancedDropdown selection.
                SerializedObject ownerSO = idProp.serializedObject;
                UnityEngine.Object target = ownerSO != null
                    ? ownerSO.targetObject
                    : null;
                string path = idProp.propertyPath;

                ShowDropdown(buttonRect, kind, allowEmpty, current, selectedId =>
                {
                    ApplyReferenceSelection(kind, ownerSO, target, path, selectedId);
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

        internal static bool ApplyReferenceSelection(
            ReferenceKind kind,
            SerializedObject preferredSO,
            UnityEngine.Object target,
            string propertyPath,
            string selectedId)
        {
            if (target == null || string.IsNullOrEmpty(propertyPath))
                return false;

            string newValue = selectedId ?? string.Empty;
            SerializedObject writeSO = preferredSO != null && preferredSO.targetObject == target
                ? preferredSO
                : null;

            if (TryApplyReferenceSelection(kind, writeSO, target, propertyPath, newValue))
                return true;

            return TryApplyReferenceSelection(kind, new SerializedObject(target), target, propertyPath, newValue);
        }

        private static bool TryApplyReferenceSelection(
            ReferenceKind kind,
            SerializedObject serializedObject,
            UnityEngine.Object target,
            string propertyPath,
            string newValue)
        {
            if (serializedObject == null)
                return false;

            serializedObject.Update();
            SerializedProperty property = serializedObject.FindProperty(propertyPath);
            if (property == null || property.propertyType != SerializedPropertyType.String)
                return false;

            if (string.Equals(property.stringValue, newValue, StringComparison.Ordinal))
                return false;

            Undo.SetCurrentGroupName("Set Reference Id");
            property.stringValue = newValue;
            bool applied = serializedObject.ApplyModifiedProperties();
            if (!applied)
                return false;

            EditorUtility.SetDirty(target);
            PrefabUtility.RecordPrefabInstancePropertyModifications(target);
            NotifyReferenceChanged(kind, target);
            return true;
        }

        private static void NotifyReferenceChanged(ReferenceKind kind, UnityEngine.Object target)
        {
            EditorApplication.delayCall += () =>
            {
                if (target != null)
                    EditorUtility.SetDirty(target);

                foreach (SolDatabaseWindow window in Resources.FindObjectsOfTypeAll<SolDatabaseWindow>())
                    window.Repaint();

                switch (kind)
                {
                    case ReferenceKind.Item:
                        ItemRegistry.ScheduleEditorSync();
                        break;
                    case ReferenceKind.Npc:
                        NPCRegistry.ScheduleEditorSync();
                        break;
                    case ReferenceKind.Quest:
                        QuestRegistry.ScheduleEditorSync();
                        break;
                    case ReferenceKind.Shop:
                    case ReferenceKind.Stat:
                    case ReferenceKind.GameplayTag:
                        RpgDefinitionRegistry.ScheduleEditorSync();
                        break;
                }
            };
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
                ReferenceKind.Shop => BuildShopEntries(),
                ReferenceKind.Stat => BuildStatEntries(),
                ReferenceKind.GameplayTag => BuildGameplayTagEntries(),
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

                string itemName = !string.IsNullOrWhiteSpace(e.DisplayName)
                    ? e.DisplayName.Trim()
                    : id;
                string itemType = e.ItemType == ItemType.Miscellaneous ? "Miscellaneous" : e.ItemType.ToString();
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

        private static List<Entry> BuildShopEntries()
        {
            List<Entry> entries = new List<Entry>();
            HashSet<string> seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            RpgDefinitionRegistry registry = RpgDefinitionRegistry.Get();
            if (registry?.Shops != null)
            {
                for (int i = 0; i < registry.Shops.Count; i++)
                {
                    RpgShopDefinition shop = registry.Shops[i];
                    if (shop == null || string.IsNullOrWhiteSpace(shop.Id))
                        continue;

                    string id = shop.Id.Trim();
                    if (!seen.Add(id))
                        continue;

                    string title = string.IsNullOrWhiteSpace(shop.DisplayName) ? id : shop.DisplayName.Trim();
                    entries.Add(new Entry(id, $"{title} ({id})", $"{title} ({id})"));
                }
            }

            entries.Sort((a, b) => StringComparer.OrdinalIgnoreCase.Compare(a.MenuPath, b.MenuPath));
            return entries;
        }

        private static List<Entry> BuildStatEntries()
        {
            List<Entry> entries = new List<Entry>();
            HashSet<string> seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            AddStatEntry(entries, seen, GameplayStatIds.MaxHealth, "Max Health", "Built-In/Vitals");
            AddStatEntry(entries, seen, GameplayStatIds.MaxStamina, "Max Stamina", "Built-In/Vitals");
            AddStatEntry(entries, seen, GameplayStatIds.ArmorRating, "Armor Rating", "Built-In/Combat");
            AddStatEntry(entries, seen, GameplayStatIds.StaminaCost, "Stamina Cost", "Built-In/Combat");
            AddStatEntry(entries, seen, GameplayStatIds.StaminaRegen, "Stamina Regen", "Built-In/Combat");
            AddStatEntry(entries, seen, GameplayStatIds.OutgoingDamage, "Outgoing Damage", "Built-In/Combat");
            AddStatEntry(entries, seen, GameplayStatIds.IncomingDamage, "Incoming Damage", "Built-In/Combat");

            RpgDefinitionRegistry registry = RpgDefinitionRegistry.Get();
            if (registry?.Stats != null)
            {
                for (int i = 0; i < registry.Stats.Count; i++)
                {
                    RpgStatDefinition stat = registry.Stats[i];
                    if (stat == null || string.IsNullOrWhiteSpace(stat.Id))
                        continue;

                    string id = stat.Id.Trim();
                    if (!seen.Add(id))
                        continue;

                    string title = string.IsNullOrWhiteSpace(stat.DisplayName) ? id : stat.DisplayName.Trim();
                    string category = stat.Category.ToString();
                    entries.Add(new Entry(id, $"{title} ({id})", $"RPG/{category}/{title} ({id})"));
                }
            }

            entries.Sort((a, b) => StringComparer.OrdinalIgnoreCase.Compare(a.MenuPath, b.MenuPath));
            return entries;
        }

        private static void AddStatEntry(List<Entry> entries, HashSet<string> seen, string id, string title, string folder)
        {
            if (string.IsNullOrWhiteSpace(id) || !seen.Add(id))
                return;

            entries.Add(new Entry(id, $"{title} ({id})", $"{folder}/{title} ({id})"));
        }

        private static List<Entry> BuildGameplayTagEntries()
        {
            List<Entry> entries = new List<Entry>();
            HashSet<string> seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            RpgDefinitionRegistry registry = RpgDefinitionRegistry.Get();
            if (registry?.Tags != null)
            {
                for (int i = 0; i < registry.Tags.Count; i++)
                {
                    GameplayTagDefinition tag = registry.Tags[i];
                    if (tag == null || string.IsNullOrWhiteSpace(tag.Id))
                        continue;

                    string id = tag.Id.Trim();
                    if (!seen.Add(id))
                        continue;

                    string title = string.IsNullOrWhiteSpace(tag.TagPath) ? id : tag.TagPath.Trim();
                    entries.Add(new Entry(id, $"{title} ({id})", title.Replace('.', '/') + $" ({id})"));
                }
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
            private readonly Dictionary<string, string> _idsByLabel = new Dictionary<string, string>(StringComparer.Ordinal);
            private int _nextItemId = 1;

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
                    AdvancedDropdownItem none = CreateSelectableItem(NoneLabel, string.Empty);
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

                    AdvancedDropdownItem child = CreateSelectableItem(leafLabel, entry.Id);
                    parent.AddChild(child);
                }

                return root;
            }

            private ReferenceDropdownItem CreateSelectableItem(string label, string referenceId)
            {
                ReferenceDropdownItem item = new ReferenceDropdownItem(label, referenceId);
                item.id = _nextItemId++;
                _idsByItemId[item.id] = item.ReferenceId;
                _idsByLabel[label] = item.ReferenceId;
                return item;
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
                if (item == null)
                    return;

                string id;
                if (item is ReferenceDropdownItem referenceItem)
                {
                    id = referenceItem.ReferenceId;
                }
                else if (!_idsByItemId.TryGetValue(item.id, out id))
                {
                    if (string.IsNullOrEmpty(item.name) || !_idsByLabel.TryGetValue(item.name, out id))
                        return;
                }

                _onPicked?.Invoke(id);
            }
        }

        private sealed class ReferenceDropdownItem : AdvancedDropdownItem
        {
            public readonly string ReferenceId;

            public ReferenceDropdownItem(string name, string referenceId)
                : base(name)
            {
                ReferenceId = referenceId ?? string.Empty;
            }
        }
    }
}
#endif
