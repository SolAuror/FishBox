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

        private readonly List<ItemComponent> _items = new();
        private readonly List<ItemRow> _rows = new();
        private readonly List<ItemRow> _filteredRows = new();
        private readonly Dictionary<ItemComponent, List<ItemAuthoringWarning>> _warningCache = new();

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
        private ItemComponent _selectedItem;
        private SerializedObject _selectedSerializedObject;

        public override SolDatabaseTab Tab => SolDatabaseTab.Items;
        public override string DisplayName => "Items";

        public override void DrawToolbar()
        {
            if (GUILayout.Button("New", EditorStyles.toolbarButton, GUILayout.Width(48f)))
                CreateNewItem();
            _newTemplate = (ItemAuthoringTemplate)EditorGUILayout.EnumPopup(_newTemplate, EditorStyles.toolbarPopup, GUILayout.Width(120f));
            using (new EditorGUI.DisabledScope(_selectedItem == null))
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
            _items.Clear();
            _rows.Clear();
            InvalidateWarningCache();
            ItemRegistry registry = ItemRegistry.Get();
            if (registry?.Entries != null)
            {
                HashSet<ItemComponent> seen = new();
                for (int i = 0; i < registry.Entries.Count; i++)
                {
                    ItemComponent item = registry.Entries[i]?.Prefab;
                    if (item == null || !seen.Add(item))
                        continue;

                    _items.Add(item);
                    _rows.Add(BuildRow(item));
                }
            }

            _filteredRowsDirty = true;
            RebuildFilteredRowsIfNeeded();
            if (_selectedItem != null && !_items.Contains(_selectedItem))
                SelectItem(null);
        }

        public override bool SelectById(string id)
        {
            if (string.IsNullOrWhiteSpace(id))
                return false;

            RefreshIndex();
            for (int i = 0; i < _items.Count; i++)
            {
                ItemComponent item = _items[i];
                if (item == null)
                    continue;
                if (string.Equals(item.ItemId, id, System.StringComparison.OrdinalIgnoreCase))
                {
                    SelectItem(item);
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

            for (int i = 0; i < _items.Count; i++)
            {
                ItemComponent item = _items[i];
                if (item == null)
                    continue;

                List<ItemAuthoringWarning> warnings = GetWarnings(item);
                for (int j = 0; j < warnings.Count; j++)
                {
                    ItemAuthoringWarning warning = warnings[j];
                    issues.Add(new SolDatabaseIssue(
                        MapIssueSeverity(warning.Severity),
                        Tab,
                        item.ItemId,
                        string.IsNullOrWhiteSpace(item.ItemName) ? item.name : item.ItemName,
                        warning.Message,
                        item));
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
            DrawRowChrome(rowRect, row.Item == _selectedItem, () => SelectItem(row.Item));
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
            if (_selectedItem == null)
            {
                EditorGUILayout.HelpBox("Select an item prefab from the database list.", MessageType.Info);
                EditorGUILayout.EndVertical();
                return;
            }

            if (_selectedSerializedObject == null || _selectedSerializedObject.targetObject != _selectedItem)
                _selectedSerializedObject = new SerializedObject(_selectedItem);

            DetailScroll = EditorGUILayout.BeginScrollView(DetailScroll);
            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.LabelField(_selectedItem.ItemName, EditorStyles.largeLabel);
            GUILayout.FlexibleSpace();
            using (new EditorGUI.DisabledScope(_selectedItem.AuthoringTemplate == ItemAuthoringTemplate.None))
            {
                if (GUILayout.Button("Fill Missing Defaults", GUILayout.Width(140f)))
                    FillMissingDefaultsForSelectedItem();
                if (GUILayout.Button("Reapply...", GUILayout.Width(84f)))
                    ReapplyTemplateForSelectedItem();
            }
            EditorGUILayout.EndHorizontal();

            EditorGUILayout.LabelField(ItemAuthoringEditorUtility.GetPrefabPath(_selectedItem), EditorStyles.miniLabel);
            EditorGUILayout.Space(4f);

            _selectedSerializedObject.Update();
            ItemComponentEditorUtility.DrawItemInspector(_selectedSerializedObject, _selectedItem);
            if (_selectedSerializedObject.ApplyModifiedProperties())
            {
                EditorUtility.SetDirty(_selectedItem);
                ItemRegistry.ScheduleEditorSync();
                RefreshRow(_selectedItem);
            }

            EditorGUILayout.EndScrollView();
            EditorGUILayout.EndVertical();
        }

        private ItemRow BuildRow(ItemComponent item)
        {
            string path = ItemAuthoringEditorUtility.GetPrefabPath(item);
            List<ItemAuthoringWarning> warnings = GetWarnings(item);
            Texture icon = item.Icon != null
                ? AssetPreview.GetAssetPreview(item.Icon)
                : AssetDatabase.GetCachedIcon(path);
            string itemName = string.IsNullOrWhiteSpace(item.ItemName) ? item.name : item.ItemName.Trim();
            string itemId = string.IsNullOrWhiteSpace(item.ItemId) ? "<missing>" : item.ItemId.Trim();
            string typeName = item.TypeDisplayName;

            return new ItemRow
            {
                Item = item,
                ItemId = itemId,
                ItemName = itemName,
                TypeName = typeName,
                ItemType = item.Type,
                Value = item.Value,
                PrefabPath = path,
                SearchText = $"{itemName} {itemId} {typeName} {path}".ToLowerInvariant(),
                Icon = icon,
                WarningCount = warnings.Count,
                IsConsumable = item.IsConsumable,
                IsEquipable = ItemTypeRules.IsEquipableType(item.Type),
                IsStackable = item.IsStackable,
                MissingIcon = item.Icon == null
            };
        }

        private void RefreshRow(ItemComponent item)
        {
            if (item == null)
                return;

            InvalidateWarningCache(item);
            ItemRow row = BuildRow(item);
            for (int i = 0; i < _rows.Count; i++)
            {
                if (_rows[i].Item != item)
                    continue;

                _rows[i] = row;
                _filteredRowsDirty = true;
                return;
            }

            _items.Add(item);
            _rows.Add(row);
            _filteredRowsDirty = true;
        }

        internal static bool RowMatchesFilters(ItemRow row, string search, ItemFilter filter, bool useTypeFilter, ItemType typeFilter)
        {
            if (row == null || row.Item == null)
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

        private void SelectItem(ItemComponent item)
        {
            _selectedItem = item;
            _selectedSerializedObject = _selectedItem != null ? new SerializedObject(_selectedItem) : null;
            Window?.Repaint();
        }

        private void CreateNewItem()
        {
            ItemComponent created = ItemAuthoringEditorUtility.CreateItemPrefab(_newTemplate);
            RefreshIndex();
            SelectItem(created);
            SelectCurrentPrefab();
        }

        private void DuplicateSelectedItem()
        {
            ItemComponent duplicated = ItemAuthoringEditorUtility.DuplicateItemPrefab(_selectedItem);
            RefreshIndex();
            SelectItem(duplicated);
            SelectCurrentPrefab();
        }

        private void RevealSelectedPrefab()
        {
            string path = ItemAuthoringEditorUtility.GetPrefabPath(_selectedItem);
            if (!string.IsNullOrWhiteSpace(path))
                EditorUtility.RevealInFinder(path);
        }

        private void SelectCurrentPrefab()
        {
            if (_selectedItem == null)
                return;

            Selection.activeObject = _selectedItem.gameObject;
            EditorGUIUtility.PingObject(_selectedItem.gameObject);
        }

        private void RebuildRegistry()
        {
            ItemRegistry.ForceEditorSyncNow();
            RefreshIndex();
        }

        private List<ItemAuthoringWarning> GetWarnings(ItemComponent item)
        {
            if (item == null)
                return new List<ItemAuthoringWarning>();

            if (!_warningCache.TryGetValue(item, out List<ItemAuthoringWarning> warnings))
            {
                warnings = ItemAuthoringValidator.Validate(item);
                _warningCache[item] = warnings;
            }

            return warnings;
        }

        private void InvalidateWarningCache()
        {
            _warningCache.Clear();
        }

        private void InvalidateWarningCache(ItemComponent item)
        {
            if (item != null)
                _warningCache.Remove(item);
        }

        private void FillMissingDefaultsForSelectedItem()
        {
            if (_selectedItem == null || _selectedItem.AuthoringTemplate == ItemAuthoringTemplate.None)
                return;

            ItemAuthoringEditorUtility.FillMissingTemplateDefaults(_selectedItem, _selectedItem.AuthoringTemplate, _selectedItem.ItemName);
            RefreshRow(_selectedItem);
            _selectedSerializedObject = new SerializedObject(_selectedItem);
            ItemRegistry.ScheduleEditorSync();
        }

        private void ReapplyTemplateForSelectedItem()
        {
            if (_selectedItem == null || _selectedItem.AuthoringTemplate == ItemAuthoringTemplate.None)
                return;

            List<string> changes = ItemAuthoringEditorUtility.BuildTemplateOverwritePreview(_selectedItem.AuthoringTemplate);
            string message = "This will overwrite authored item fields:\n\n- "
                + string.Join("\n- ", changes)
                + "\n\nUse Fill Missing Defaults for the non-destructive path.";

            bool confirmed = EditorUtility.DisplayDialog("Reapply Item Template", message, "Overwrite Fields", "Cancel");
            if (!confirmed)
                return;

            ItemAuthoringEditorUtility.ApplyTemplate(_selectedItem, _selectedItem.AuthoringTemplate, _selectedItem.ItemName);
            RefreshRow(_selectedItem);
            _selectedSerializedObject = new SerializedObject(_selectedItem);
            ItemRegistry.ScheduleEditorSync();
        }
    }
}
