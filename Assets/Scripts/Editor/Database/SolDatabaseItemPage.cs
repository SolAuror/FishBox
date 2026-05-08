using System.Collections.Generic;
using Sol.Grab;
using UnityEditor;
using UnityEngine;

namespace Sol.Editor
{
    internal sealed class SolDatabaseItemPage : SolDatabasePageBase
    {
        internal enum ItemFilter
        {
            All,
            Consumable,
            Equipable,
            Stackable,
            MissingIcon,
            HasWarnings
        }

        internal enum ItemSort
        {
            Name,
            Id,
            Value
        }

        internal sealed class ItemRow
        {
            public ItemRegistry.Entry Entry;
            public int EntryIndex;
            public ItemComponent Item;
            public string ItemId;
            public string ItemName;
            public string TypeName;
            public ItemType ItemType;
            public int Value;
            public string PrefabPath;
            public string SearchText;
            public Texture Icon;
            public int WarningCount;
            public bool IsConsumable;
            public bool IsEquipable;
            public bool IsStackable;
            public bool MissingIcon;
        }

        private readonly List<ItemRegistry.Entry> _entries = new();
        private readonly List<ItemRow> _rows = new();
        private readonly List<ItemRow> _filteredRows = new();
        private readonly Dictionary<ItemRegistry.Entry, List<ItemAuthoringWarning>> _warningCache = new();

        private string _lastFilterSearch = null;
        private ItemFilter _filter = ItemFilter.All;
        private ItemFilter _lastFilter = (ItemFilter)(-1);
        private ItemType _typeFilter = ItemType.Material;
        private ItemType _lastTypeFilter = (ItemType)(-1);
        private bool _useTypeFilter;
        private bool _lastUseTypeFilter;
        private ItemSort _sort = ItemSort.Name;
        private ItemSort _lastSort = (ItemSort)(-1);
        private bool _filteredRowsDirty = true;
        private ItemAuthoringTemplate _newTemplate = ItemAuthoringTemplate.None;
        private ItemRegistry.Entry _selectedEntry;
        private int _selectedEntryIndex = -1;
        private SerializedObject _registrySerializedObject;

        public override SolDatabaseTab Tab => SolDatabaseTab.Items;
        public override string DisplayName => "Items";

        public override void DrawToolbar()
        {
            if (GUILayout.Button("New", EditorStyles.toolbarButton, GUILayout.Width(48f)))
                CreateNewItem();
            _newTemplate = (ItemAuthoringTemplate)EditorGUILayout.EnumPopup(_newTemplate, EditorStyles.toolbarPopup, GUILayout.Width(120f));
            using (new EditorGUI.DisabledScope(_selectedEntry == null))
            {
                if (GUILayout.Button("Duplicate", EditorStyles.toolbarButton, GUILayout.Width(76f)))
                    DuplicateSelectedItem();
                if (GUILayout.Button("Reveal Prefab", EditorStyles.toolbarButton, GUILayout.Width(92f)))
                    RevealSelectedPrefab();
                if (GUILayout.Button("Select", EditorStyles.toolbarButton, GUILayout.Width(54f)))
                    SelectCurrentPrefab();
            }
            if (GUILayout.Button("Rebuild Registry", EditorStyles.toolbarButton, GUILayout.Width(108f)))
                RebuildRegistry();
        }

        public override void DrawPage()
        {
            EditorGUILayout.BeginHorizontal();
            DrawLeftPane();
            DrawRightPane();
            EditorGUILayout.EndHorizontal();
        }

        public override void RefreshIndex()
        {
            _entries.Clear();
            _rows.Clear();
            InvalidateWarningCache();

            ItemRegistry registry = ItemRegistry.Get();
            if (registry?.Entries != null)
            {
                for (int i = 0; i < registry.Entries.Count; i++)
                {
                    ItemRegistry.Entry entry = registry.Entries[i];
                    if (entry == null)
                        continue;

                    entry.Normalize();
                    _entries.Add(entry);
                    _rows.Add(BuildRow(entry, i));
                }
            }

            _filteredRowsDirty = true;
            RebuildFilteredRowsIfNeeded();
            if (_selectedEntry != null && !_entries.Contains(_selectedEntry))
                SelectEntry(null, -1);
        }

        public override bool SelectById(string id)
        {
            if (string.IsNullOrWhiteSpace(id))
                return false;

            RefreshIndex();
            for (int i = 0; i < _entries.Count; i++)
            {
                ItemRegistry.Entry entry = _entries[i];
                if (entry == null)
                    continue;
                if (string.Equals(entry.ItemId, id, System.StringComparison.OrdinalIgnoreCase))
                {
                    SelectEntry(entry, FindRegistryEntryIndex(entry));
                    return true;
                }
            }

            return false;
        }

        public override List<SolDatabaseIssue> CollectIssues()
        {
            List<SolDatabaseIssue> issues = new();
            ItemRegistry registry = ItemRegistry.Get();
            List<ItemAuthoringWarning> registryWarnings = ItemAuthoringValidator.ValidateRegistry(registry);
            for (int i = 0; i < registryWarnings.Count; i++)
            {
                ItemAuthoringWarning warning = registryWarnings[i];
                issues.Add(new SolDatabaseIssue(MapIssueSeverity(warning.Severity), Tab, string.Empty, "Item Registry", warning.Message, registry));
            }

            for (int i = 0; i < _entries.Count; i++)
            {
                ItemRegistry.Entry entry = _entries[i];
                if (entry == null)
                    continue;

                List<ItemAuthoringWarning> warnings = GetWarnings(entry);
                for (int j = 0; j < warnings.Count; j++)
                {
                    ItemAuthoringWarning warning = warnings[j];
                    issues.Add(new SolDatabaseIssue(
                        MapIssueSeverity(warning.Severity),
                        Tab,
                        entry.ItemId,
                        string.IsNullOrWhiteSpace(entry.DisplayName) ? entry.ItemId : entry.DisplayName,
                        warning.Message,
                        registry));
                }
            }

            return issues;
        }

        private void DrawLeftPane()
        {
            EditorGUILayout.BeginVertical(GUILayout.Width(DefaultLeftPaneWidth));

            DrawSearchField(() => _filteredRowsDirty = true);

            EditorGUI.BeginChangeCheck();
            EditorGUILayout.BeginHorizontal();
            _filter = (ItemFilter)EditorGUILayout.EnumPopup(_filter);
            _useTypeFilter = EditorGUILayout.ToggleLeft("Type", _useTypeFilter, GUILayout.Width(48f));
            using (new EditorGUI.DisabledScope(!_useTypeFilter))
                _typeFilter = (ItemType)EditorGUILayout.EnumPopup(_typeFilter);
            EditorGUILayout.EndHorizontal();

            EditorGUILayout.BeginHorizontal();
            GUILayout.Label("Sort", GUILayout.Width(32f));
            _sort = (ItemSort)EditorGUILayout.EnumPopup(_sort);
            EditorGUILayout.EndHorizontal();
            if (EditorGUI.EndChangeCheck())
                _filteredRowsDirty = true;

            RebuildFilteredRowsIfNeeded();
            DrawVirtualizedRows(_filteredRows.Count, "No items match the current filters.", (rect, index) => DrawItemRow(rect, _filteredRows[index]));
            EditorGUILayout.EndVertical();
        }

        private void DrawItemRow(Rect rowRect, ItemRow row)
        {
            DrawRowChrome(rowRect, row.Entry == _selectedEntry, () => SelectEntry(row.Entry, row.EntryIndex));
            DrawIcon(rowRect, row.Icon);

            Rect textRect = TextColumnRect(rowRect);
            Rect titleRect = new(textRect.x, rowRect.y + 5f, textRect.width, EditorGUIUtility.singleLineHeight);
            Rect metaRect = new(textRect.x, titleRect.yMax + 1f, textRect.width, EditorGUIUtility.singleLineHeight);
            Rect pathRect = new(textRect.x, metaRect.yMax + 1f, textRect.width, EditorGUIUtility.singleLineHeight);

            GUI.Label(titleRect, $"{row.ItemName} ({row.ItemId})", EditorStyles.boldLabel);
            GUI.Label(metaRect, $"{row.TypeName}  Value {row.Value}", EditorStyles.miniLabel);
            GUI.Label(pathRect, row.PrefabPath, EditorStyles.miniLabel);
            DrawWarningBadge(rowRect, row.WarningCount);
        }

        private void DrawRightPane()
        {
            EditorGUILayout.BeginVertical();
            if (_selectedEntry == null)
            {
                EditorGUILayout.HelpBox("Select an item definition from the database list.", MessageType.Info);
                EditorGUILayout.EndVertical();
                return;
            }

            ItemRegistry registry = ItemRegistry.Get();
            if (registry == null)
            {
                EditorGUILayout.HelpBox("ItemRegistry asset is missing.", MessageType.Error);
                EditorGUILayout.EndVertical();
                return;
            }

            if (_registrySerializedObject == null || _registrySerializedObject.targetObject != registry)
                _registrySerializedObject = new SerializedObject(registry);

            DetailScroll = EditorGUILayout.BeginScrollView(DetailScroll);
            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.LabelField(_selectedEntry.NameOrId, EditorStyles.largeLabel);
            GUILayout.FlexibleSpace();
            using (new EditorGUI.DisabledScope(_selectedEntry.Prefab == null || _selectedEntry.AuthoringTemplate == ItemAuthoringTemplate.None))
            {
                if (GUILayout.Button("Fill Missing Defaults", GUILayout.Width(140f)))
                    FillMissingDefaultsForSelectedItem();
                if (GUILayout.Button("Reapply...", GUILayout.Width(84f)))
                    ReapplyTemplateForSelectedItem();
            }
            EditorGUILayout.EndHorizontal();

            EditorGUILayout.LabelField(_selectedEntry.Prefab != null ? ItemAuthoringEditorUtility.GetPrefabPath(_selectedEntry.Prefab) : "No visual/world prefab", EditorStyles.miniLabel);
            EditorGUILayout.Space(4f);

            DrawWarnings(_selectedEntry);

            _registrySerializedObject.Update();
            DrawSelectedRegistryEntry(_registrySerializedObject);
            if (_registrySerializedObject.ApplyModifiedProperties())
            {
                _selectedEntry.Normalize();
                EditorUtility.SetDirty(registry);
                RefreshRow(_selectedEntry);
            }

            EditorGUILayout.EndScrollView();
            EditorGUILayout.EndVertical();
        }

        private void DrawWarnings(ItemRegistry.Entry entry)
        {
            List<ItemAuthoringWarning> warnings = GetWarnings(entry);
            for (int i = 0; i < warnings.Count; i++)
            {
                MessageType type = warnings[i].Severity switch
                {
                    ItemAuthoringWarningSeverity.Error => MessageType.Error,
                    ItemAuthoringWarningSeverity.Warning => MessageType.Warning,
                    _ => MessageType.Info
                };
                EditorGUILayout.HelpBox(warnings[i].Message, type);
            }

            if (warnings.Count > 0)
                EditorGUILayout.Space(4f);
        }

        private ItemRow BuildRow(ItemRegistry.Entry entry, int entryIndex)
        {
            ItemComponent item = entry?.Prefab;
            string path = ItemAuthoringEditorUtility.GetPrefabPath(item);
            List<ItemAuthoringWarning> warnings = GetWarnings(entry);
            Texture icon = entry?.Icon != null
                ? AssetPreview.GetAssetPreview(entry.Icon)
                : AssetDatabase.GetCachedIcon(path);
            string itemName = string.IsNullOrWhiteSpace(entry?.DisplayName) ? item != null ? item.name : "<missing>" : entry.DisplayName.Trim();
            string itemId = string.IsNullOrWhiteSpace(entry?.ItemId) ? "<missing>" : entry.ItemId.Trim();
            string typeName = entry != null ? entry.ItemType == ItemType.Miscellaneous ? "Miscellaneous" : entry.ItemType.ToString() : "Missing";

            return new ItemRow
            {
                Entry = entry,
                EntryIndex = entryIndex,
                Item = item,
                ItemId = itemId,
                ItemName = itemName,
                TypeName = typeName,
                ItemType = entry != null ? entry.ItemType : ItemType.Material,
                Value = entry != null ? entry.Value : 0,
                PrefabPath = path,
                SearchText = $"{itemName} {itemId} {typeName} {path}".ToLowerInvariant(),
                Icon = icon,
                WarningCount = warnings.Count,
                IsConsumable = entry != null && (entry.IsConsumable || ItemTypeRules.IsConsumableType(entry.ItemType)),
                IsEquipable = entry != null && ItemTypeRules.IsEquipableType(entry.ItemType),
                IsStackable = entry != null && entry.IsStackable,
                MissingIcon = entry?.Icon == null
            };
        }

        private void RefreshRow(ItemRegistry.Entry entry)
        {
            if (entry == null)
                return;

            InvalidateWarningCache(entry);
            int entryIndex = FindRegistryEntryIndex(entry);
            ItemRow row = BuildRow(entry, entryIndex);
            for (int i = 0; i < _rows.Count; i++)
            {
                if (_rows[i].Entry != entry)
                    continue;

                _rows[i] = row;
                _filteredRowsDirty = true;
                return;
            }

            _entries.Add(entry);
            _rows.Add(row);
            _filteredRowsDirty = true;
        }

        internal static bool RowMatchesFilters(ItemRow row, string search, ItemFilter filter, bool useTypeFilter, ItemType typeFilter)
        {
            if (row == null || (row.Entry == null && row.Item == null))
                return false;

            if (useTypeFilter && row.ItemType != typeFilter)
                return false;

            if (!string.IsNullOrWhiteSpace(search))
            {
                string needle = search.Trim().ToLowerInvariant();
                if (row.SearchText == null || row.SearchText.IndexOf(needle, System.StringComparison.Ordinal) < 0)
                    return false;
            }

            return filter switch
            {
                ItemFilter.Consumable => row.IsConsumable,
                ItemFilter.Equipable => row.IsEquipable,
                ItemFilter.Stackable => row.IsStackable,
                ItemFilter.MissingIcon => row.MissingIcon,
                ItemFilter.HasWarnings => row.WarningCount > 0,
                _ => true
            };
        }

        private void RebuildFilteredRowsIfNeeded()
        {
            if (!_filteredRowsDirty
                && string.Equals(_lastFilterSearch, Search, System.StringComparison.Ordinal)
                && _lastFilter == _filter
                && _lastUseTypeFilter == _useTypeFilter
                && _lastTypeFilter == _typeFilter
                && _lastSort == _sort)
            {
                return;
            }

            _filteredRows.Clear();
            for (int i = 0; i < _rows.Count; i++)
            {
                ItemRow row = _rows[i];
                if (RowMatchesFilters(row, Search, _filter, _useTypeFilter, _typeFilter))
                    _filteredRows.Add(row);
            }

            SortFilteredRows(_sort);

            _lastFilterSearch = Search;
            _lastFilter = _filter;
            _lastUseTypeFilter = _useTypeFilter;
            _lastTypeFilter = _typeFilter;
            _lastSort = _sort;
            _filteredRowsDirty = false;
        }

        private void SortFilteredRows(ItemSort sort)
        {
            switch (sort)
            {
                case ItemSort.Id:
                    _filteredRows.Sort((a, b) => System.StringComparer.OrdinalIgnoreCase.Compare(a.ItemId, b.ItemId));
                    break;
                case ItemSort.Value:
                    _filteredRows.Sort((a, b) =>
                    {
                        int cmp = b.Value.CompareTo(a.Value);
                        return cmp != 0 ? cmp : System.StringComparer.OrdinalIgnoreCase.Compare(a.ItemName, b.ItemName);
                    });
                    break;
                default:
                    _filteredRows.Sort((a, b) => System.StringComparer.OrdinalIgnoreCase.Compare(a.ItemName, b.ItemName));
                    break;
            }
        }

        private void SelectEntry(ItemRegistry.Entry entry, int entryIndex)
        {
            _selectedEntry = entry;
            _selectedEntryIndex = entryIndex;
            _registrySerializedObject = ItemRegistry.Get() != null ? new SerializedObject(ItemRegistry.Get()) : null;
            Window?.Repaint();
        }

        private void CreateNewItem()
        {
            ItemComponent created = ItemAuthoringEditorUtility.CreateItemPrefab(_newTemplate);
            RefreshIndex();
            SelectById(created != null ? created.ItemId : string.Empty);
            SelectCurrentPrefab();
        }

        private void DuplicateSelectedItem()
        {
            ItemComponent duplicated = ItemAuthoringEditorUtility.DuplicateItemPrefab(_selectedEntry?.Prefab);
            RefreshIndex();
            SelectById(duplicated != null ? duplicated.ItemId : string.Empty);
            SelectCurrentPrefab();
        }

        private void RevealSelectedPrefab()
        {
            string path = ItemAuthoringEditorUtility.GetPrefabPath(_selectedEntry?.Prefab);
            if (!string.IsNullOrWhiteSpace(path))
                EditorUtility.RevealInFinder(path);
        }

        private void SelectCurrentPrefab()
        {
            if (_selectedEntry?.Prefab == null)
                return;

            Selection.activeObject = _selectedEntry.Prefab.gameObject;
            EditorGUIUtility.PingObject(_selectedEntry.Prefab.gameObject);
        }

        private void RebuildRegistry()
        {
            ItemRegistry.ForceEditorSyncNow();
            RefreshIndex();
        }

        private List<ItemAuthoringWarning> GetWarnings(ItemRegistry.Entry entry)
        {
            if (entry == null)
                return new List<ItemAuthoringWarning>();

            if (!_warningCache.TryGetValue(entry, out List<ItemAuthoringWarning> warnings))
            {
                warnings = ItemAuthoringValidator.ValidateDefinition(entry);
                if (entry.Prefab != null)
                    warnings.AddRange(ItemAuthoringValidator.Validate(entry.Prefab));
                _warningCache[entry] = warnings;
            }

            return warnings;
        }

        private void InvalidateWarningCache()
        {
            _warningCache.Clear();
        }

        private void InvalidateWarningCache(ItemRegistry.Entry entry)
        {
            if (entry != null)
                _warningCache.Remove(entry);
        }

        private void FillMissingDefaultsForSelectedItem()
        {
            if (_selectedEntry?.Prefab == null || _selectedEntry.AuthoringTemplate == ItemAuthoringTemplate.None)
                return;

            ItemAuthoringEditorUtility.FillMissingTemplateDefaults(_selectedEntry.Prefab, _selectedEntry.AuthoringTemplate, _selectedEntry.NameOrId);
            RefreshRow(_selectedEntry);
            _registrySerializedObject = new SerializedObject(ItemRegistry.Get());
            ItemRegistry.ScheduleEditorSync();
        }

        private void ReapplyTemplateForSelectedItem()
        {
            if (_selectedEntry?.Prefab == null || _selectedEntry.AuthoringTemplate == ItemAuthoringTemplate.None)
                return;

            List<string> changes = ItemAuthoringEditorUtility.BuildTemplateOverwritePreview(_selectedEntry.AuthoringTemplate);
            string message = "This will overwrite visual prefab setup fields:\n\n- "
                + string.Join("\n- ", changes)
                + "\n\nItem design values remain authoritative in ItemRegistry.";

            bool confirmed = EditorUtility.DisplayDialog("Reapply Item Template", message, "Overwrite Fields", "Cancel");
            if (!confirmed)
                return;

            ItemAuthoringEditorUtility.ApplyTemplate(_selectedEntry.Prefab, _selectedEntry.AuthoringTemplate, _selectedEntry.NameOrId);
            RefreshRow(_selectedEntry);
            _registrySerializedObject = new SerializedObject(ItemRegistry.Get());
            ItemRegistry.ScheduleEditorSync();
        }

        private int FindRegistryEntryIndex(ItemRegistry.Entry entry)
        {
            ItemRegistry registry = ItemRegistry.Get();
            if (registry?.Entries == null || entry == null)
                return -1;

            for (int i = 0; i < registry.Entries.Count; i++)
            {
                if (registry.Entries[i] == entry)
                    return i;
            }

            return -1;
        }

        private void DrawSelectedRegistryEntry(SerializedObject registryObject)
        {
            SerializedProperty entries = registryObject.FindProperty("_entries");
            if (entries == null || _selectedEntryIndex < 0 || _selectedEntryIndex >= entries.arraySize)
            {
                EditorGUILayout.HelpBox("Could not find the selected registry entry.", MessageType.Warning);
                return;
            }

            SerializedProperty entry = entries.GetArrayElementAtIndex(_selectedEntryIndex);
            DrawDefinitionSection(entry);
            DrawInventoryRulesSection(entry);
            DrawUseDefinitionSection(entry);
            DrawEquipmentDefinitionSection(entry);
            DrawVisualPrefabSection(entry);
            DrawDefinitionAuthoringSection(entry);
        }

        private static void DrawDefinitionSection(SerializedProperty entry)
        {
            EditorGUILayout.LabelField("Definition", EditorStyles.boldLabel);
            using (new EditorGUI.DisabledScope(true))
                EditorGUILayout.PropertyField(entry.FindPropertyRelative("ItemId"));
            EditorGUILayout.PropertyField(entry.FindPropertyRelative("DisplayName"));
            EditorGUILayout.PropertyField(entry.FindPropertyRelative("ItemType"));
            EditorGUILayout.PropertyField(entry.FindPropertyRelative("Value"));
            EditorGUILayout.PropertyField(entry.FindPropertyRelative("Icon"));
            EditorGUILayout.PropertyField(entry.FindPropertyRelative("FlavourText"));
            EditorGUILayout.Space(4f);
        }

        private static void DrawInventoryRulesSection(SerializedProperty entry)
        {
            EditorGUILayout.LabelField("Inventory Rules", EditorStyles.boldLabel);
            EditorGUILayout.PropertyField(entry.FindPropertyRelative("IsStackable"));
            if (entry.FindPropertyRelative("IsStackable")?.boolValue == true)
                EditorGUILayout.PropertyField(entry.FindPropertyRelative("MaxStackSize"));
            EditorGUILayout.PropertyField(entry.FindPropertyRelative("IsConsumable"));
            EditorGUILayout.PropertyField(entry.FindPropertyRelative("IsTradeable"));
            EditorGUILayout.Space(4f);
        }

        private static void DrawUseDefinitionSection(SerializedProperty entry)
        {
            SerializedProperty effects = entry.FindPropertyRelative("UseEffects");
            SerializedProperty consumable = entry.FindPropertyRelative("IsConsumable");
            if (consumable == null || effects == null || (!consumable.boolValue && effects.arraySize == 0))
                return;

            EditorGUILayout.LabelField("Use", EditorStyles.boldLabel);
            EditorGUILayout.PropertyField(entry.FindPropertyRelative("UseOccasion"));
            EditorGUILayout.PropertyField(effects, includeChildren: true);
            EditorGUILayout.Space(4f);
        }

        private static void DrawEquipmentDefinitionSection(SerializedProperty entry)
        {
            SerializedProperty itemType = entry.FindPropertyRelative("ItemType");
            if (itemType == null || !ItemTypeRules.ShowEquipmentSection((ItemType)itemType.enumValueIndex))
                return;

            EditorGUILayout.LabelField("Equipment", EditorStyles.boldLabel);
            if (ItemTypeRules.UsesWeaponStats((ItemType)itemType.enumValueIndex))
                EditorGUILayout.PropertyField(entry.FindPropertyRelative("Damage"));
            if (ItemTypeRules.UsesArmorStats((ItemType)itemType.enumValueIndex))
                EditorGUILayout.PropertyField(entry.FindPropertyRelative("Defense"));
            EditorGUILayout.PropertyField(entry.FindPropertyRelative("EquipBone"));
            EditorGUILayout.PropertyField(entry.FindPropertyRelative("EquipOffset"));
            EditorGUILayout.PropertyField(entry.FindPropertyRelative("EquipRotation"));
            EditorGUILayout.PropertyField(entry.FindPropertyRelative("EquipDomain"));
            EditorGUILayout.PropertyField(entry.FindPropertyRelative("WeaponHanding"));
            EditorGUILayout.PropertyField(entry.FindPropertyRelative("AllowedEquipSlots"), includeChildren: true);
            EditorGUILayout.Space(4f);
        }

        private static void DrawVisualPrefabSection(SerializedProperty entry)
        {
            EditorGUILayout.LabelField("Visual / World Prefab", EditorStyles.boldLabel);
            EditorGUILayout.PropertyField(entry.FindPropertyRelative("Prefab"));
            ItemComponent prefab = entry.FindPropertyRelative("Prefab")?.objectReferenceValue as ItemComponent;
            if (prefab != null)
            {
                SerializedObject prefabObject = new(prefab);
                prefabObject.Update();
                EditorGUILayout.PropertyField(prefabObject.FindProperty("_pickupGrip"));

                MeshFilter filter = prefab.GetComponent<MeshFilter>();
                if (filter != null)
                {
                    SerializedObject filterObject = new(filter);
                    filterObject.Update();
                    EditorGUILayout.PropertyField(filterObject.FindProperty("m_Mesh"), new GUIContent("Mesh"));
                    filterObject.ApplyModifiedProperties();
                }

                MeshRenderer renderer = prefab.GetComponent<MeshRenderer>();
                if (renderer != null)
                {
                    SerializedObject rendererObject = new(renderer);
                    rendererObject.Update();
                    EditorGUILayout.PropertyField(rendererObject.FindProperty("m_Materials"), new GUIContent("Materials"), true);
                    rendererObject.ApplyModifiedProperties();
                }

                prefabObject.ApplyModifiedProperties();
            }
            EditorGUILayout.Space(4f);
        }

        private static void DrawDefinitionAuthoringSection(SerializedProperty entry)
        {
            EditorGUILayout.LabelField("Authoring", EditorStyles.boldLabel);
            EditorGUILayout.PropertyField(entry.FindPropertyRelative("AuthoringTemplate"));
            EditorGUILayout.PropertyField(entry.FindPropertyRelative("AuthoringNotes"));
        }

    }
}
