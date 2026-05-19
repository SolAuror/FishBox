using System;
using System.Collections.Generic;
using Sol.Grab;
using Sol.Rpg;
using UnityEditor;
using UnityEngine;

namespace Sol.Editor
{
    internal sealed class SolDatabaseItemPage : SolDatabaseListPage<SolDatabaseItemPage.ItemRow, SolDatabaseItemPage.ItemFilter, SolDatabaseItemPage.ItemSort>
    {
        private const float PreviewHeight = 220f;
        private const float PreviewRotationSpeed = 20f;

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
            public string WarningTooltip;
            public bool IsConsumable;
            public bool IsEquipable;
            public bool IsStackable;
            public bool MissingIcon;
        }

        private readonly Dictionary<ItemRegistry.Entry, List<ItemAuthoringWarning>> _warningCache = new();
        private readonly SolDatabaseItemPreview _preview = new();

        private ItemType _typeFilter = ItemType.Material;
        private bool _useTypeFilter;
        private ItemAuthoringTemplate _newTemplate = ItemAuthoringTemplate.None;
        private bool _previewAutoRotate = true;
        private double _lastPreviewTime;

        public override SolDatabaseTab Tab => SolDatabaseTab.Items;
        public override string DisplayName => "Items";

        protected override string EmptyDetailMessage => "Select an item definition from the database list.";
        protected override string RevealButtonLabel => "Reveal Prefab";

        public override void Dispose()
        {
            _preview.Dispose();
        }

        public override void CommitPendingEdits()
        {
            if (SelectedSerializedObject == null || SelectedRow?.Entry == null)
                return;

            if (SelectedSerializedObject.ApplyModifiedProperties())
            {
                SelectedRow.Entry.Normalize();
                EditorUtility.SetDirty(SelectedSerializedObject.targetObject);
                RefreshRow(SelectedRow);
                ItemRegistry.NotifyEditorDefinitionsChanged();
            }
        }

        // -- Row contract --------------------------------------------------------------

        protected override UnityEngine.Object GetRowAsset(ItemRow row) => row?.Item;
        protected override UnityEngine.Object GetSerializedObjectTarget(ItemRow row) => ItemRegistry.Get();
        protected override string GetRowId(ItemRow row) => row?.Entry?.ItemId;
        protected override string GetRowSearchText(ItemRow row) => row?.SearchText;
        protected override int GetRowWarningCount(ItemRow row) => row?.WarningCount ?? 0;

        protected override void OnBeforeLoadRows()
        {
            _warningCache.Clear();
        }

        protected override void LoadRows()
        {
            ItemRegistry registry = ItemRegistry.Get();
            if (registry?.Entries == null)
                return;

            for (int i = 0; i < registry.Entries.Count; i++)
            {
                ItemRegistry.Entry entry = registry.Entries[i];
                if (entry == null)
                    continue;

                entry.Normalize();
                Rows.Add(BuildRow(entry, i));
            }
        }

        protected override ItemRow RebuildRow(ItemRow row)
        {
            if (row?.Entry == null)
                return row;
            _warningCache.Remove(row.Entry);
            int entryIndex = FindRegistryEntryIndex(row.Entry);
            return BuildRow(row.Entry, entryIndex);
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
                WarningTooltip = JoinWarningMessages(warnings),
                IsConsumable = entry != null && entry.HasConsumableTag,
                IsEquipable = entry != null && ItemTypeRules.IsEquipableType(entry.ItemType),
                IsStackable = entry != null && entry.IsStackable,
                MissingIcon = entry?.Icon == null
            };
        }

        protected override bool MatchesCustomFilter(ItemRow row, ItemFilter filter)
        {
            if (row == null || (row.Entry == null && row.Item == null))
                return false;

            if (_useTypeFilter && row.ItemType != _typeFilter)
                return false;

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

        protected override void SortRows(List<ItemRow> rows, ItemSort sort)
        {
            switch (sort)
            {
                case ItemSort.Id:
                    rows.Sort((a, b) => StringComparer.OrdinalIgnoreCase.Compare(a.ItemId, b.ItemId));
                    break;
                case ItemSort.Value:
                    rows.Sort((a, b) =>
                    {
                        int cmp = b.Value.CompareTo(a.Value);
                        return cmp != 0 ? cmp : StringComparer.OrdinalIgnoreCase.Compare(a.ItemName, b.ItemName);
                    });
                    break;
                default:
                    rows.Sort((a, b) => StringComparer.OrdinalIgnoreCase.Compare(a.ItemName, b.ItemName));
                    break;
            }
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
                if (row.SearchText == null || row.SearchText.IndexOf(needle, StringComparison.Ordinal) < 0)
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

        protected override void DrawFilterControls()
        {
            EditorGUILayout.BeginHorizontal();
            Filter = (ItemFilter)EditorGUILayout.EnumPopup(Filter);
            _useTypeFilter = EditorGUILayout.ToggleLeft("Type", _useTypeFilter, GUILayout.Width(48f));
            using (new EditorGUI.DisabledScope(!_useTypeFilter))
                _typeFilter = (ItemType)EditorGUILayout.EnumPopup(_typeFilter);
            EditorGUILayout.EndHorizontal();

            EditorGUILayout.BeginHorizontal();
            GUILayout.Label("Sort", GUILayout.Width(32f));
            Sort = (ItemSort)EditorGUILayout.EnumPopup(Sort);
            EditorGUILayout.EndHorizontal();
        }

        // -- Toolbar -------------------------------------------------------------------

        protected override void DrawToolbarBeforeRowOps()
        {
            _newTemplate = (ItemAuthoringTemplate)EditorGUILayout.EnumPopup(_newTemplate, EditorStyles.toolbarPopup, GUILayout.Width(SolDatabaseStyles.ButtonXXL));
        }

        protected override void DrawToolbarTrailing()
        {
            if (GUILayout.Button("Rebuild Registry", EditorStyles.toolbarButton, GUILayout.Width(SolDatabaseStyles.ButtonXL)))
                RebuildRegistry();
        }

        protected override void OnNewClicked()
        {
            ItemComponent created = ItemAuthoringEditorUtility.CreateItemPrefab(_newTemplate);
            RefreshIndex();
            if (created != null)
                SelectById(created.ItemId);
            OnSelectAssetClicked();
        }

        protected override void OnDuplicateClicked()
        {
            ItemComponent duplicated = ItemAuthoringEditorUtility.DuplicateItemPrefab(SelectedRow?.Item);
            RefreshIndex();
            if (duplicated != null)
                SelectById(duplicated.ItemId);
            OnSelectAssetClicked();
        }

        protected override void OnRevealClicked()
        {
            string path = ItemAuthoringEditorUtility.GetPrefabPath(SelectedRow?.Item);
            if (!string.IsNullOrWhiteSpace(path))
                EditorUtility.RevealInFinder(path);
        }

        protected override void OnSelectAssetClicked()
        {
            ItemComponent item = SelectedRow?.Item;
            if (item == null)
                return;

            Selection.activeObject = item.gameObject;
            EditorGUIUtility.PingObject(item.gameObject);
        }

        protected override void OnSelectionChanged(ItemRow newRow)
        {
            _preview.SetItem(newRow?.Item);
            _lastPreviewTime = 0d;
        }

        private void RebuildRegistry()
        {
            ItemRegistry.ForceEditorSyncNow();
            RefreshIndex();
        }

        protected override void DoBulkDelete(IReadOnlyList<ItemRow> rows)
        {
            for (int i = 0; i < rows.Count; i++)
            {
                ItemComponent item = rows[i]?.Item;
                if (item == null)
                    continue;
                string path = AssetDatabase.GetAssetPath(item.gameObject);
                if (!string.IsNullOrWhiteSpace(path))
                    AssetDatabase.DeleteAsset(path);
            }
            ItemRegistry.ScheduleEditorSync();
        }

        protected override void DoBulkDuplicate(IReadOnlyList<ItemRow> rows)
        {
            for (int i = 0; i < rows.Count; i++)
            {
                ItemComponent item = rows[i]?.Item;
                if (item == null)
                    continue;
                ItemAuthoringEditorUtility.DuplicateItemPrefab(item);
            }
            ItemRegistry.ScheduleEditorSync();
        }

        protected override void BuildBulkMenuExtras(GenericMenu menu)
        {
            List<ItemRow> rows = GetBulkSelectedRows();
            int templatable = 0;
            for (int i = 0; i < rows.Count; i++)
            {
                ItemRegistry.Entry entry = rows[i]?.Entry;
                if (entry?.Prefab != null && entry.AuthoringTemplate != ItemAuthoringTemplate.None)
                    templatable++;
            }

            string label = $"Apply template to {templatable} selected";
            if (templatable == 0)
                menu.AddDisabledItem(new GUIContent(label));
            else
                menu.AddItem(new GUIContent(label), false, () => BulkApplyTemplate(rows));
        }

        private void BulkApplyTemplate(List<ItemRow> rows)
        {
            if (!EditorUtility.DisplayDialog(
                "Reapply Templates",
                $"Reapply each entry's AuthoringTemplate to its prefab? Visual prefab setup fields will be overwritten on {rows.Count} item(s) (entries without a template are skipped).",
                "Overwrite", "Cancel"))
                return;

            try
            {
                AssetDatabase.StartAssetEditing();
                for (int i = 0; i < rows.Count; i++)
                {
                    ItemRegistry.Entry entry = rows[i]?.Entry;
                    if (entry?.Prefab == null || entry.AuthoringTemplate == ItemAuthoringTemplate.None)
                        continue;
                    ItemAuthoringEditorUtility.ApplyTemplate(entry.Prefab, entry.AuthoringTemplate, entry.NameOrId);
                }
            }
            finally
            {
                AssetDatabase.StopAssetEditing();
                AssetDatabase.SaveAssets();
            }

            ItemRegistry.ScheduleEditorSync();
            RefreshIndex();
        }

        // -- Row drawing ---------------------------------------------------------------

        protected override void DrawRow(Rect rowRect, ItemRow row)
        {
            DrawRowChrome(rowRect, IsSelected(row), () => HandleRowClick(row));
            DrawIcon(rowRect, row.Icon);

            Rect textRect = TextColumnRect(rowRect);
            Rect titleRect = new(textRect.x, rowRect.y + 5f, textRect.width, EditorGUIUtility.singleLineHeight);
            Rect metaRect = new(textRect.x, titleRect.yMax + 1f, textRect.width, EditorGUIUtility.singleLineHeight);
            Rect pathRect = new(textRect.x, metaRect.yMax + 1f, textRect.width, EditorGUIUtility.singleLineHeight);

            GUI.Label(titleRect, $"{row.ItemName} ({row.ItemId})", EditorStyles.boldLabel);
            GUI.Label(metaRect, $"{row.TypeName}  Value {row.Value}", EditorStyles.miniLabel);
            GUI.Label(pathRect, row.PrefabPath, EditorStyles.miniLabel);
            DrawWarningBadge(rowRect, row.WarningCount, row.WarningTooltip);
        }

        // -- Detail --------------------------------------------------------------------

        protected override void DrawRightPane()
        {
            EditorGUILayout.BeginVertical();
            if (SelectedRow == null)
            {
                EditorGUILayout.HelpBox(EmptyDetailMessage, MessageType.Info);
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

            EnsureSelectedSerializedObject();

            DetailScroll = EditorGUILayout.BeginScrollView(DetailScroll);
            DrawDetail(SelectedRow);
            EditorGUILayout.EndScrollView();
            EditorGUILayout.EndVertical();
        }

        protected override void DrawDetail(ItemRow row)
        {
            ItemRegistry.Entry entry = row.Entry;
            if (entry == null)
                return;

            ItemRegistry registry = (ItemRegistry)SelectedSerializedObject.targetObject;

            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.LabelField(entry.NameOrId, EditorStyles.largeLabel);
            GUILayout.FlexibleSpace();
            using (new EditorGUI.DisabledScope(entry.Prefab == null || entry.AuthoringTemplate == ItemAuthoringTemplate.None))
            {
                if (GUILayout.Button("Fill Missing Defaults", GUILayout.Width(140f)))
                    FillMissingDefaultsForSelectedItem();
                if (GUILayout.Button("Reapply...", GUILayout.Width(84f)))
                    ReapplyTemplateForSelectedItem();
            }
            EditorGUILayout.EndHorizontal();

            EditorGUILayout.LabelField(entry.Prefab != null ? ItemAuthoringEditorUtility.GetPrefabPath(entry.Prefab) : "No visual/world prefab", EditorStyles.miniLabel);
            EditorGUILayout.Space(4f);

            DrawSelectedPreview(entry);
            EditorGUILayout.Space(4f);

            SelectedSerializedObject.Update();
            DrawSelectedRegistryEntry(SelectedSerializedObject, row.EntryIndex);
            if (SelectedSerializedObject.ApplyModifiedProperties())
            {
                entry.Normalize();
                EditorUtility.SetDirty(registry);
                RefreshRow(row);
                ItemRegistry.NotifyEditorDefinitionsChanged();
            }

            DrawWarnings(entry);
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

        // -- Preview -------------------------------------------------------------------

        private void DrawSelectedPreview(ItemRegistry.Entry entry)
        {
            _preview.SetItem(entry?.Prefab);

            EditorGUILayout.BeginVertical(EditorStyles.helpBox);
            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.LabelField("Preview", EditorStyles.boldLabel);
            GUILayout.FlexibleSpace();
            _previewAutoRotate = GUILayout.Toggle(_previewAutoRotate, "Auto Rotate", GUILayout.Width(92f));
            if (GUILayout.Button("Reset", GUILayout.Width(58f)))
            {
                _preview.ResetRotation();
                _lastPreviewTime = 0d;
            }
            EditorGUILayout.EndHorizontal();

            Rect previewRect = GUILayoutUtility.GetRect(1f, PreviewHeight, GUILayout.ExpandWidth(true), GUILayout.Height(PreviewHeight));
            if (Event.current.type == EventType.Repaint)
            {
                DrawPreviewBackground(previewRect);
                if (_preview.HasRenderablePreview)
                {
                    RotatePreviewIfNeeded();
                    Texture texture = _preview.Render(previewRect);
                    if (texture != null)
                        GUI.DrawTexture(previewRect, texture, ScaleMode.StretchToFill, alphaBlend: false);
                    else
                        DrawPreviewFallback(previewRect, "Preview render failed.");
                }
                else
                {
                    DrawPreviewFallback(previewRect, entry?.Prefab == null
                        ? "No visual/world prefab assigned."
                        : "Selected prefab has no renderers to preview.");
                }
            }

            EditorGUILayout.EndVertical();

            if (_previewAutoRotate && _preview.HasRenderablePreview)
                Window?.Repaint();
        }

        private void RotatePreviewIfNeeded()
        {
            if (!_previewAutoRotate)
            {
                _lastPreviewTime = 0d;
                return;
            }

            double now = EditorApplication.timeSinceStartup;
            if (_lastPreviewTime <= 0d)
            {
                _lastPreviewTime = now;
                return;
            }

            float delta = Mathf.Clamp((float)(now - _lastPreviewTime), 0f, 0.1f);
            _lastPreviewTime = now;
            _preview.Rotate(delta * PreviewRotationSpeed);
        }

        private static void DrawPreviewBackground(Rect rect)
        {
            EditorGUI.DrawRect(rect, SolDatabaseStyles.PreviewBg);
            Rect border = new(rect.x, rect.y, rect.width, 1f);
            EditorGUI.DrawRect(border, new Color(1f, 1f, 1f, 0.12f));
        }

        private static void DrawPreviewFallback(Rect rect, string message)
        {
            GUIStyle style = new(EditorStyles.centeredGreyMiniLabel)
            {
                alignment = TextAnchor.MiddleCenter,
                wordWrap = true
            };
            GUI.Label(rect, message, style);
        }

        // -- Issues --------------------------------------------------------------------

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

            for (int i = 0; i < Rows.Count; i++)
            {
                ItemRegistry.Entry entry = Rows[i]?.Entry;
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

        // -- Helpers -------------------------------------------------------------------

        private static string JoinWarningMessages(List<ItemAuthoringWarning> warnings)
        {
            if (warnings == null || warnings.Count == 0)
                return null;
            System.Text.StringBuilder sb = new();
            for (int i = 0; i < warnings.Count; i++)
            {
                if (i > 0) sb.Append('\n');
                sb.Append(warnings[i].Message);
            }
            return sb.ToString();
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

        private void FillMissingDefaultsForSelectedItem()
        {
            ItemRegistry.Entry entry = SelectedRow?.Entry;
            if (entry?.Prefab == null || entry.AuthoringTemplate == ItemAuthoringTemplate.None)
                return;

            ItemAuthoringEditorUtility.FillMissingTemplateDefaults(entry.Prefab, entry.AuthoringTemplate, entry.NameOrId);
            RefreshRow(SelectedRow);
            SelectedSerializedObject = new SerializedObject(ItemRegistry.Get());
            ItemRegistry.ScheduleEditorSync();
        }

        private void ReapplyTemplateForSelectedItem()
        {
            ItemRegistry.Entry entry = SelectedRow?.Entry;
            if (entry?.Prefab == null || entry.AuthoringTemplate == ItemAuthoringTemplate.None)
                return;

            List<string> changes = ItemAuthoringEditorUtility.BuildTemplateOverwritePreview(entry.AuthoringTemplate);
            string message = "This will overwrite visual prefab setup fields:\n\n- "
                + string.Join("\n- ", changes)
                + "\n\nItem design values remain authoritative in ItemRegistry.";

            bool confirmed = EditorUtility.DisplayDialog("Reapply Item Template", message, "Overwrite Fields", "Cancel");
            if (!confirmed)
                return;

            ItemAuthoringEditorUtility.ApplyTemplate(entry.Prefab, entry.AuthoringTemplate, entry.NameOrId);
            RefreshRow(SelectedRow);
            SelectedSerializedObject = new SerializedObject(ItemRegistry.Get());
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

        private static void DrawSelectedRegistryEntry(SerializedObject registryObject, int selectedEntryIndex)
        {
            SerializedProperty entries = registryObject.FindProperty("_entries");
            if (entries == null || selectedEntryIndex < 0 || selectedEntryIndex >= entries.arraySize)
            {
                EditorGUILayout.HelpBox("Could not find the selected registry entry.", MessageType.Warning);
                return;
            }

            SerializedProperty entry = entries.GetArrayElementAtIndex(selectedEntryIndex);
            DrawDefinitionSection(entry);
            DrawGameplayTagsSection(entry);
            DrawInventoryRulesSection(entry);
            DrawUseDefinitionSection(entry);
            DrawEquipmentDefinitionSection(entry);
            DrawVisualPrefabSection(entry);
            DrawDefinitionAuthoringSection(entry);
        }

        internal static void DrawDefinitionSection(SerializedProperty entry)
        {
            EditorGUILayout.LabelField("Definition", EditorStyles.boldLabel);
            using (new EditorGUI.DisabledScope(true))
                EditorGUILayout.PropertyField(entry.FindPropertyRelative("ItemId"));
            EditorGUILayout.PropertyField(entry.FindPropertyRelative("DisplayName"));
            EditorGUILayout.PropertyField(entry.FindPropertyRelative("ItemType"));
            EditorGUILayout.PropertyField(entry.FindPropertyRelative("Value"));
            EditorGUILayout.PropertyField(entry.FindPropertyRelative("Weight"));
            EditorGUILayout.PropertyField(entry.FindPropertyRelative("Icon"));
            EditorGUILayout.PropertyField(entry.FindPropertyRelative("FlavourText"));
            EditorGUILayout.Space(4f);
        }

        internal static void DrawInventoryRulesSection(SerializedProperty entry)
        {
            EditorGUILayout.LabelField("Inventory Rules", EditorStyles.boldLabel);
            EditorGUILayout.PropertyField(entry.FindPropertyRelative("IsStackable"));
            if (entry.FindPropertyRelative("IsStackable")?.boolValue == true)
                EditorGUILayout.PropertyField(entry.FindPropertyRelative("MaxStackSize"));
            EditorGUILayout.HelpBox("Consumable and tradeable capability are controlled by Gameplay Tags: Item.Consumable and Item.Tradeable.", MessageType.None);
            EditorGUILayout.Space(4f);
        }

        internal static void DrawGameplayTagsSection(SerializedProperty entry)
        {
            EditorGUILayout.LabelField("Gameplay Tags", EditorStyles.boldLabel);
            SerializedProperty tags = entry.FindPropertyRelative("Tags");
            if (tags != null)
                EditorGUILayout.PropertyField(tags, includeChildren: true);
            SerializedProperty modifiers = entry.FindPropertyRelative("StatModifiers");
            if (modifiers != null)
                EditorGUILayout.PropertyField(modifiers, includeChildren: true);
            EditorGUILayout.Space(4f);
        }

        internal static void DrawUseDefinitionSection(SerializedProperty entry)
        {
            SerializedProperty effects = entry.FindPropertyRelative("UseEffects");
            if (effects == null || (!EntryHasTag(entry, GameplayCapabilityTags.ItemConsumable) && effects.arraySize == 0))
                return;

            EditorGUILayout.LabelField("Use", EditorStyles.boldLabel);
            EditorGUILayout.PropertyField(entry.FindPropertyRelative("UseOccasion"));
            EditorGUILayout.PropertyField(effects, includeChildren: true);
            EditorGUILayout.Space(4f);
        }

        private static bool EntryHasTag(SerializedProperty entry, string tagPath)
        {
            ItemRegistry registry = ItemRegistry.Get();
            if (registry?.Entries == null || entry == null)
                return false;

            SerializedProperty idProperty = entry.FindPropertyRelative("ItemId");
            ItemRegistry.Entry definition = registry.GetDefinition(idProperty?.stringValue);
            return definition != null && definition.Tags.HasTagOrChild(tagPath);
        }

        internal static void DrawEquipmentDefinitionSection(SerializedProperty entry)
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

        internal static void DrawVisualPrefabSection(SerializedProperty entry)
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

        internal static void DrawDefinitionAuthoringSection(SerializedProperty entry)
        {
            EditorGUILayout.LabelField("Authoring", EditorStyles.boldLabel);
            EditorGUILayout.PropertyField(entry.FindPropertyRelative("AuthoringTemplate"));
            EditorGUILayout.PropertyField(entry.FindPropertyRelative("AuthoringNotes"));
        }
    }
}
