using System.Collections.Generic;
using Sol.Grab;
using UnityEditor;
using UnityEngine;

namespace Sol.Editor
{
    public sealed class ItemDatabaseWindow : EditorWindow
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

        internal sealed class ItemDatabaseRow
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

        private const float LeftPaneWidth = 380f;
        private const float RowHeight = 58f;
        private const float RowPadding = 4f;
        private const float IconSize = 32f;

        private readonly List<ItemComponent> _items = new();
        private readonly List<ItemDatabaseRow> _rows = new();
        private readonly List<ItemDatabaseRow> _filteredRows = new();
        private readonly Dictionary<ItemComponent, List<ItemAuthoringWarning>> _warningCache = new();
        private Vector2 _listScroll;
        private Vector2 _detailScroll;
        private string _search = string.Empty;
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

        [MenuItem("Window/Sol/Item Database")]
        public static void Open()
        {
            ItemDatabaseWindow window = GetWindow<ItemDatabaseWindow>("Sol Item Database");
            window.minSize = new Vector2(900f, 520f);
            window.RefreshIndex();
        }

        private void OnEnable()
        {
            RefreshIndex();
        }

        private void OnProjectChange()
        {
            RefreshIndex();
            Repaint();
        }

        private void OnGUI()
        {
            EditorGUILayout.BeginHorizontal(EditorStyles.toolbar);
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
            if (GUILayout.Button("Refresh Index", EditorStyles.toolbarButton, GUILayout.Width(94f)))
                RefreshIndex();
            if (GUILayout.Button("Rebuild Registry", EditorStyles.toolbarButton, GUILayout.Width(108f)))
                RebuildRegistry();
            if (GUILayout.Button("Validate All", EditorStyles.toolbarButton, GUILayout.Width(82f)))
                ValidateAll();
            GUILayout.FlexibleSpace();
            EditorGUILayout.EndHorizontal();

            EditorGUILayout.BeginHorizontal();
            DrawLeftPane();
            DrawRightPane();
            EditorGUILayout.EndHorizontal();
        }

        private void DrawLeftPane()
        {
            EditorGUILayout.BeginVertical(GUILayout.Width(LeftPaneWidth));

            EditorGUI.BeginChangeCheck();
            EditorGUILayout.BeginHorizontal(EditorStyles.toolbar);
            _search = GUILayout.TextField(_search, GUI.skin.FindStyle("ToolbarSearchTextField"), GUILayout.MinWidth(120f));
            if (GUILayout.Button(GUIContent.none, GUI.skin.FindStyle("ToolbarSearchCancelButton")))
            {
                _search = string.Empty;
                GUI.FocusControl(null);
            }
            EditorGUILayout.EndHorizontal();

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
            DrawVirtualizedRows();
            EditorGUILayout.EndVertical();
        }

        private void DrawVirtualizedRows()
        {
            float viewportHeight = Mathf.Max(120f, position.height - 78f);
            _listScroll = EditorGUILayout.BeginScrollView(_listScroll);

            if (_filteredRows.Count == 0)
            {
                EditorGUILayout.HelpBox("No items match the current filters.", MessageType.Info);
                EditorGUILayout.EndScrollView();
                return;
            }

            int firstRow = Mathf.Clamp(Mathf.FloorToInt(_listScroll.y / RowHeight), 0, Mathf.Max(0, _filteredRows.Count - 1));
            int visibleCount = Mathf.CeilToInt(viewportHeight / RowHeight) + 2;
            int lastRowExclusive = Mathf.Min(_filteredRows.Count, firstRow + visibleCount);

            float topSpace = firstRow * RowHeight;
            float bottomSpace = (_filteredRows.Count - lastRowExclusive) * RowHeight;
            if (topSpace > 0f)
                GUILayout.Space(topSpace);

            for (int i = firstRow; i < lastRowExclusive; i++)
            {
                Rect rowRect = EditorGUILayout.GetControlRect(false, RowHeight);
                DrawItemRow(rowRect, _filteredRows[i]);
            }

            if (bottomSpace > 0f)
                GUILayout.Space(bottomSpace);

            EditorGUILayout.EndScrollView();
        }

        private void DrawItemRow(Rect rowRect, ItemDatabaseRow row)
        {
            Event evt = Event.current;
            bool selected = row.Item == _selectedItem;
            bool hover = rowRect.Contains(evt.mousePosition);
            if (selected)
                EditorGUI.DrawRect(rowRect, new Color(0.22f, 0.44f, 0.68f, 0.28f));
            else if (hover)
                EditorGUI.DrawRect(rowRect, new Color(1f, 1f, 1f, 0.06f));

            if (evt.type == EventType.MouseDown && evt.button == 0 && rowRect.Contains(evt.mousePosition))
            {
                SelectItem(row.Item);
                evt.Use();
            }

            Rect iconRect = new(rowRect.x + RowPadding, rowRect.y + (RowHeight - IconSize) * 0.5f, IconSize, IconSize);
            if (row.Icon != null)
                GUI.DrawTexture(iconRect, row.Icon, ScaleMode.ScaleToFit);
            else
                EditorGUI.DrawRect(iconRect, new Color(0f, 0f, 0f, 0.18f));

            float textX = iconRect.xMax + RowPadding;
            Rect titleRect = new(textX, rowRect.y + 5f, rowRect.width - textX - 44f, EditorGUIUtility.singleLineHeight);
            Rect metaRect = new(textX, titleRect.yMax + 1f, titleRect.width, EditorGUIUtility.singleLineHeight);
            Rect pathRect = new(textX, metaRect.yMax + 1f, titleRect.width, EditorGUIUtility.singleLineHeight);

            GUI.Label(titleRect, $"{row.ItemName} ({row.ItemId})", EditorStyles.boldLabel);
            GUI.Label(metaRect, $"{row.TypeName}  Value {row.Value}", EditorStyles.miniLabel);
            GUI.Label(pathRect, row.PrefabPath, EditorStyles.miniLabel);

            if (row.WarningCount > 0)
            {
                Rect warningRect = new(rowRect.xMax - 38f, rowRect.y + 6f, 34f, EditorGUIUtility.singleLineHeight);
                GUI.Label(warningRect, $"! {row.WarningCount}", EditorStyles.miniBoldLabel);
            }
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

            _detailScroll = EditorGUILayout.BeginScrollView(_detailScroll);
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

        private void RefreshIndex()
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

        private ItemDatabaseRow BuildRow(ItemComponent item)
        {
            string path = ItemAuthoringEditorUtility.GetPrefabPath(item);
            List<ItemAuthoringWarning> warnings = GetWarnings(item);
            Texture icon = item.Icon != null
                ? AssetPreview.GetAssetPreview(item.Icon)
                : AssetDatabase.GetCachedIcon(path);
            string itemName = string.IsNullOrWhiteSpace(item.ItemName) ? item.name : item.ItemName.Trim();
            string itemId = string.IsNullOrWhiteSpace(item.ItemId) ? "<missing>" : item.ItemId.Trim();
            string typeName = item.TypeDisplayName;

            return new ItemDatabaseRow
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
            ItemDatabaseRow row = BuildRow(item);
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

        internal static bool RowMatchesFilters(
            ItemDatabaseRow row,
            string search,
            ItemFilter filter,
            bool useTypeFilter,
            ItemType typeFilter)
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
                && string.Equals(_lastFilterSearch, _search, System.StringComparison.Ordinal)
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
                ItemDatabaseRow row = _rows[i];
                if (RowMatchesFilters(row, _search, _filter, _useTypeFilter, _typeFilter))
                    _filteredRows.Add(row);
            }

            SortFilteredRows(_sort);

            _lastFilterSearch = _search;
            _lastFilter = _filter;
            _lastUseTypeFilter = _useTypeFilter;
            _lastTypeFilter = _typeFilter;
            _lastSort = _sort;
            _filteredRowsDirty = false;
            float maxScrollY = Mathf.Max(0f, _filteredRows.Count * RowHeight - Mathf.Max(120f, position.height - 78f));
            _listScroll.y = Mathf.Min(_listScroll.y, maxScrollY);
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
            RefreshSelectedSerializedObject();
            Repaint();
        }

        private void RefreshSelectedSerializedObject()
        {
            _selectedSerializedObject = _selectedItem != null ? new SerializedObject(_selectedItem) : null;
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

        private void ValidateAll()
        {
            InvalidateWarningCache();
            int warningCount = 0;
            ItemRegistry registry = ItemRegistry.Get();
            List<ItemAuthoringWarning> registryWarnings = ItemAuthoringValidator.ValidateRegistry(registry);
            for (int i = 0; i < registryWarnings.Count; i++)
            {
                Debug.LogWarning($"[Item Database] {registryWarnings[i].Message}");
                warningCount++;
            }

            for (int i = 0; i < _items.Count; i++)
            {
                ItemComponent item = _items[i];
                List<ItemAuthoringWarning> warnings = GetWarnings(item);
                for (int j = 0; j < warnings.Count; j++)
                {
                    Debug.LogWarning($"[Item Database] {item.ItemName} ({item.ItemId}): {warnings[j].Message}", item);
                    warningCount++;
                }
            }

            RefreshIndex();
            string message = warningCount == 0
                ? "No item authoring warnings found."
                : $"Found {warningCount} item authoring warning(s). See Console for details.";
            EditorUtility.DisplayDialog("Validate Item Database", message, "OK");
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

            ItemAuthoringEditorUtility.FillMissingTemplateDefaults(
                _selectedItem,
                _selectedItem.AuthoringTemplate,
                _selectedItem.ItemName);
            RefreshRow(_selectedItem);
            RefreshSelectedSerializedObject();
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

            bool confirmed = EditorUtility.DisplayDialog(
                "Reapply Item Template",
                message,
                "Overwrite Fields",
                "Cancel");
            if (!confirmed)
                return;

            ItemAuthoringEditorUtility.ApplyTemplate(_selectedItem, _selectedItem.AuthoringTemplate, _selectedItem.ItemName);
            RefreshRow(_selectedItem);
            RefreshSelectedSerializedObject();
            ItemRegistry.ScheduleEditorSync();
        }
    }
}
