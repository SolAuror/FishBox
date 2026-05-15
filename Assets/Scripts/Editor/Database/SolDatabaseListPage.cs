using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace Sol.Editor
{
    /// <summary>
    /// Shared list-detail page scaffold. Owns search/filter/sort state, virtualized list
    /// rendering, selection, persistence across domain reloads, multi-select, and pending-edit
    /// commit. Subclasses describe what rows mean, how to draw them, and how to mutate
    /// underlying assets.
    /// </summary>
    internal abstract class SolDatabaseListPage<TRow, TFilter, TSort> : SolDatabasePageBase
        where TRow : class
        where TFilter : struct, Enum
        where TSort : struct, Enum
    {
        protected readonly List<TRow> Rows = new();
        protected readonly List<TRow> FilteredRows = new();

        private string _lastFilterSearch;
        private TFilter _filter;
        private TFilter _lastFilter;
        private TSort _sort;
        private TSort _lastSort;
        private bool _lastIssuesOnly;
        private bool _filterCacheInitialized;
        private bool _filteredRowsDirty = true;
        private bool _issuesOnly;
        private bool _prefsLoaded;

        private readonly HashSet<string> _selectedIds = new(StringComparer.OrdinalIgnoreCase);
        private string _anchorRowId;
        private readonly List<string> _recentIds = new();
        private const int RecentCapacity = 5;
        private const char RecentSeparator = (char)0x1F;
        private string _searchControlName;
        private bool _focusSearchOnNextDraw;

        protected TRow SelectedRow;
        protected SerializedObject SelectedSerializedObject;

        protected TFilter Filter
        {
            get => _filter;
            set
            {
                if (!EqualityComparer<TFilter>.Default.Equals(_filter, value))
                {
                    _filter = value;
                    _filteredRowsDirty = true;
                }
            }
        }

        protected TSort Sort
        {
            get => _sort;
            set
            {
                if (!EqualityComparer<TSort>.Default.Equals(_sort, value))
                {
                    _sort = value;
                    _filteredRowsDirty = true;
                }
            }
        }

        protected bool IssuesOnly
        {
            get => _issuesOnly;
            set
            {
                if (_issuesOnly != value)
                {
                    _issuesOnly = value;
                    _filteredRowsDirty = true;
                }
            }
        }

        protected IReadOnlyCollection<string> SelectedIds => _selectedIds;

        // -- Row contract -------------------------------------------------------------------

        protected abstract UnityEngine.Object GetRowAsset(TRow row);
        protected abstract string GetRowId(TRow row);
        protected abstract string GetRowSearchText(TRow row);
        protected abstract int GetRowWarningCount(TRow row);
        protected abstract bool MatchesCustomFilter(TRow row, TFilter filter);
        protected abstract void SortRows(List<TRow> rows, TSort sort);
        protected abstract void DrawRow(Rect rect, TRow row);
        protected abstract void DrawDetail(TRow row);

        // -- Lifecycle ----------------------------------------------------------------------

        public override void Bind(SolDatabaseWindow window)
        {
            base.Bind(window);
            LoadPrefs();
        }

        // -- Refresh / load -----------------------------------------------------------------

        /// <summary>
        /// Populate <see cref="Rows"/> with current asset state. Subclasses clear and refill.
        /// </summary>
        protected abstract void LoadRows();

        public override void RefreshIndex()
        {
            string previouslySelectedId = SelectedRow != null ? GetRowId(SelectedRow) : null;

            Rows.Clear();
            OnBeforeLoadRows();
            LoadRows();

            _filteredRowsDirty = true;
            RebuildFilteredRowsIfNeeded();

            PruneSelectedIds();
            if (!string.IsNullOrWhiteSpace(_anchorRowId) && FindRowById(_anchorRowId) == null)
                _anchorRowId = null;

            if (!string.IsNullOrWhiteSpace(previouslySelectedId))
            {
                TRow rebound = FindRowById(previouslySelectedId);
                if (rebound != null)
                {
                    SetPrimaryWithoutClearingMulti(rebound);
                }
                else
                {
                    SelectedRow = null;
                    SelectedSerializedObject = null;
                }
            }
        }

        protected virtual void OnBeforeLoadRows()
        {
        }

        // -- Persistence --------------------------------------------------------------------

        /// <summary>
        /// EditorPrefs key prefix for this tab. Falls back to the tab enum name. Override to
        /// pin a stable string if the tab enum may be renumbered.
        /// </summary>
        protected virtual string PrefsPrefix => $"SolDatabase.{Tab}";

        private string SearchPrefsKey => $"{PrefsPrefix}.Search";
        private string FilterPrefsKey => $"{PrefsPrefix}.Filter";
        private string SortPrefsKey => $"{PrefsPrefix}.Sort";
        private string IssuesOnlyPrefsKey => $"{PrefsPrefix}.IssuesOnly";
        private string LastSelectedIdPrefsKey => $"{PrefsPrefix}.LastSelectedId";
        private string RecentIdsPrefsKey => $"{PrefsPrefix}.RecentIds";

        private void LoadPrefs()
        {
            if (_prefsLoaded)
                return;
            _prefsLoaded = true;

            Search = EditorPrefs.GetString(SearchPrefsKey, string.Empty);

            int filterValue = EditorPrefs.GetInt(FilterPrefsKey, -1);
            if (filterValue >= 0 && Enum.IsDefined(typeof(TFilter), filterValue))
                _filter = (TFilter)Enum.ToObject(typeof(TFilter), filterValue);

            int sortValue = EditorPrefs.GetInt(SortPrefsKey, -1);
            if (sortValue >= 0 && Enum.IsDefined(typeof(TSort), sortValue))
                _sort = (TSort)Enum.ToObject(typeof(TSort), sortValue);

            _issuesOnly = EditorPrefs.GetBool(IssuesOnlyPrefsKey, false);

            string recentRaw = EditorPrefs.GetString(RecentIdsPrefsKey, string.Empty);
            _recentIds.Clear();
            if (!string.IsNullOrEmpty(recentRaw))
            {
                string[] parts = recentRaw.Split(RecentSeparator);
                for (int i = 0; i < parts.Length && _recentIds.Count < RecentCapacity; i++)
                {
                    if (!string.IsNullOrWhiteSpace(parts[i]))
                        _recentIds.Add(parts[i]);
                }
            }

            _filteredRowsDirty = true;
        }

        private void SaveRecentPrefs()
        {
            if (!_prefsLoaded)
                return;
            EditorPrefs.SetString(RecentIdsPrefsKey, string.Join(RecentSeparator.ToString(), _recentIds));
        }

        private void PushRecent(string id)
        {
            if (string.IsNullOrWhiteSpace(id))
                return;
            _recentIds.RemoveAll(existing => string.Equals(existing, id, StringComparison.OrdinalIgnoreCase));
            _recentIds.Insert(0, id);
            while (_recentIds.Count > RecentCapacity)
                _recentIds.RemoveAt(_recentIds.Count - 1);
            SaveRecentPrefs();
        }

        private void SavePrefs()
        {
            if (!_prefsLoaded)
                return;

            EditorPrefs.SetString(SearchPrefsKey, Search ?? string.Empty);
            EditorPrefs.SetInt(FilterPrefsKey, Convert.ToInt32(_filter));
            EditorPrefs.SetInt(SortPrefsKey, Convert.ToInt32(_sort));
            EditorPrefs.SetBool(IssuesOnlyPrefsKey, _issuesOnly);
        }

        // -- Toolbar ------------------------------------------------------------------------

        public override void DrawToolbar()
        {
            DrawToolbarLeading();

            if (ShowDefaultToolbarOps)
            {
                DrawNewButton();
                DrawToolbarBeforeRowOps();

                using (new EditorGUI.DisabledScope(SelectedRow == null))
                {
                    DrawDuplicateButton();
                    DrawRevealButton();
                    DrawSelectButton();
                    DrawToolbarSelectionExtras();
                }

                DrawBulkButton();
                DrawRecentDropdown();
            }
            else
            {
                DrawToolbarBeforeRowOps();
                DrawToolbarSelectionExtras();
            }

            DrawToolbarTrailing();
        }

        protected virtual bool ShowDefaultToolbarOps => true;
        protected virtual bool SupportsBulkOps => ShowDefaultToolbarOps;

        protected virtual string NewButtonLabel => "New";
        protected virtual string DuplicateButtonLabel => "Duplicate";
        protected virtual string RevealButtonLabel => "Reveal Asset";
        protected virtual string SelectButtonLabel => "Select";
        protected virtual float NewButtonWidth => SolDatabaseStyles.ButtonXS;
        protected virtual float DuplicateButtonWidth => SolDatabaseStyles.ButtonM;
        protected virtual float RevealButtonWidth => SolDatabaseStyles.ButtonL;
        protected virtual float SelectButtonWidth => SolDatabaseStyles.ButtonS;

        protected virtual void DrawToolbarLeading() { }
        protected virtual void DrawToolbarBeforeRowOps() { }
        protected virtual void DrawToolbarSelectionExtras() { }
        protected virtual void DrawToolbarTrailing() { }

        private void DrawNewButton()
        {
            if (GUILayout.Button(NewButtonLabel, EditorStyles.toolbarButton, GUILayout.Width(NewButtonWidth)))
                OnNewClicked();
        }

        private void DrawDuplicateButton()
        {
            if (GUILayout.Button(DuplicateButtonLabel, EditorStyles.toolbarButton, GUILayout.Width(DuplicateButtonWidth)))
                OnDuplicateClicked();
        }

        private void DrawRevealButton()
        {
            if (GUILayout.Button(RevealButtonLabel, EditorStyles.toolbarButton, GUILayout.Width(RevealButtonWidth)))
                OnRevealClicked();
        }

        private void DrawSelectButton()
        {
            if (GUILayout.Button(SelectButtonLabel, EditorStyles.toolbarButton, GUILayout.Width(SelectButtonWidth)))
                OnSelectAssetClicked();
        }

        private void DrawBulkButton()
        {
            if (!SupportsBulkOps || _selectedIds.Count <= 1)
                return;

            string label = $"Bulk ({_selectedIds.Count}) ▾";
            if (!GUILayout.Button(label, EditorStyles.toolbarDropDown, GUILayout.Width(96f)))
                return;

            GenericMenu menu = new();
            menu.AddItem(new GUIContent($"Duplicate {_selectedIds.Count} selected"), false, ConfirmAndBulkDuplicate);
            menu.AddItem(new GUIContent($"Delete {_selectedIds.Count} selected..."), false, ConfirmAndBulkDelete);
            BuildBulkMenuExtras(menu);
            menu.ShowAsContext();
        }

        protected virtual void BuildBulkMenuExtras(GenericMenu menu) { }

        protected virtual void OnNewClicked() { }
        protected virtual void OnDuplicateClicked() { }

        protected virtual void OnRevealClicked()
        {
            UnityEngine.Object asset = SelectedRow != null ? GetRowAsset(SelectedRow) : null;
            if (asset == null)
                return;

            string path = AssetDatabase.GetAssetPath(asset);
            if (!string.IsNullOrWhiteSpace(path))
                EditorUtility.RevealInFinder(path);
        }

        protected virtual void OnSelectAssetClicked()
        {
            UnityEngine.Object asset = SelectedRow != null ? GetRowAsset(SelectedRow) : null;
            if (asset == null)
                return;

            Selection.activeObject = asset;
            EditorGUIUtility.PingObject(asset);
        }

        // -- Page layout --------------------------------------------------------------------

        public override void DrawPage()
        {
            HandleKeyboardShortcuts();

            EditorGUILayout.BeginHorizontal();
            DrawLeftPane();
            DrawRightPane();
            EditorGUILayout.EndHorizontal();
        }

        protected virtual float LeftPaneWidth => SolDatabaseStyles.DefaultLeftPaneWidth;

        protected virtual void DrawLeftPane()
        {
            EditorGUILayout.BeginVertical(GUILayout.Width(LeftPaneWidth));

            string searchBefore = Search;
            DrawNamedSearchField();
            if (!string.Equals(searchBefore, Search, StringComparison.Ordinal))
                SavePrefs();

            EditorGUI.BeginChangeCheck();
            DrawFilterControls();
            DrawIssuesOnlyRow();
            if (EditorGUI.EndChangeCheck())
            {
                _filteredRowsDirty = true;
                SavePrefs();
            }

            RebuildFilteredRowsIfNeeded();
            DrawVirtualizedRows(FilteredRows.Count, EmptyListMessage, (rect, index) => DrawRow(rect, FilteredRows[index]));
            EditorGUILayout.EndVertical();
        }

        protected virtual void DrawFilterControls()
        {
            EditorGUILayout.BeginHorizontal();
            _filter = (TFilter)(object)EditorGUILayout.EnumPopup((Enum)(object)_filter);
            EditorGUILayout.EndHorizontal();

            EditorGUILayout.BeginHorizontal();
            GUILayout.Label("Sort", GUILayout.Width(32f));
            _sort = (TSort)(object)EditorGUILayout.EnumPopup((Enum)(object)_sort);
            EditorGUILayout.EndHorizontal();
        }

        protected virtual void DrawIssuesOnlyRow()
        {
            EditorGUILayout.BeginHorizontal();
            _issuesOnly = EditorGUILayout.ToggleLeft(IssuesOnlyLabel, _issuesOnly);
            EditorGUILayout.EndHorizontal();
        }

        protected virtual string IssuesOnlyLabel => "Only show entries with warnings";

        protected virtual string EmptyListMessage
        {
            get
            {
                string baseMessage = $"No {DisplayName.ToLowerInvariant()} match the current filters.";
                return _issuesOnly ? baseMessage + " (issues-only filter is on)" : baseMessage;
            }
        }

        protected virtual void DrawRightPane()
        {
            EditorGUILayout.BeginVertical();
            if (SelectedRow == null)
            {
                EditorGUILayout.HelpBox(EmptyDetailMessage, MessageType.Info);
                EditorGUILayout.EndVertical();
                return;
            }

            EnsureSelectedSerializedObject();

            DetailScroll = EditorGUILayout.BeginScrollView(DetailScroll);
            DrawDetail(SelectedRow);
            EditorGUILayout.EndScrollView();
            EditorGUILayout.EndVertical();
        }

        protected virtual string EmptyDetailMessage => $"Select an entry from the {DisplayName.ToLowerInvariant()} list.";

        // -- Filtering ----------------------------------------------------------------------

        private void RebuildFilteredRowsIfNeeded()
        {
            if (_filterCacheInitialized
                && !_filteredRowsDirty
                && string.Equals(_lastFilterSearch, Search, StringComparison.Ordinal)
                && EqualityComparer<TFilter>.Default.Equals(_lastFilter, _filter)
                && EqualityComparer<TSort>.Default.Equals(_lastSort, _sort)
                && _lastIssuesOnly == _issuesOnly)
            {
                return;
            }

            string[] tokens = TokenizeSearch(Search);

            FilteredRows.Clear();
            for (int i = 0; i < Rows.Count; i++)
            {
                TRow row = Rows[i];
                if (row == null)
                    continue;
                if (_issuesOnly && GetRowWarningCount(row) <= 0)
                    continue;
                if (!MatchesSearch(row, tokens))
                    continue;
                if (!MatchesCustomFilter(row, _filter))
                    continue;
                FilteredRows.Add(row);
            }

            SortRows(FilteredRows, _sort);

            _lastFilterSearch = Search;
            _lastFilter = _filter;
            _lastSort = _sort;
            _lastIssuesOnly = _issuesOnly;
            _filteredRowsDirty = false;
            _filterCacheInitialized = true;
        }

        private static string[] TokenizeSearch(string search)
        {
            if (string.IsNullOrWhiteSpace(search))
                return Array.Empty<string>();
            return search.Trim().ToLowerInvariant().Split(new[] { ' ', '\t' }, StringSplitOptions.RemoveEmptyEntries);
        }

        private bool MatchesSearch(TRow row, string[] tokens)
        {
            if (tokens.Length == 0)
                return true;
            string searchText = GetRowSearchText(row);
            if (string.IsNullOrEmpty(searchText))
                return false;
            for (int i = 0; i < tokens.Length; i++)
            {
                if (searchText.IndexOf(tokens[i], StringComparison.Ordinal) < 0)
                    return false;
            }
            return true;
        }

        protected void MarkFilteredRowsDirty()
        {
            _filteredRowsDirty = true;
        }

        // -- Selection ----------------------------------------------------------------------

        public override bool SelectById(string id)
        {
            if (string.IsNullOrWhiteSpace(id))
                return false;

            RefreshIndex();
            for (int i = 0; i < Rows.Count; i++)
            {
                TRow row = Rows[i];
                if (row == null)
                    continue;
                if (string.Equals(GetRowId(row), id, StringComparison.OrdinalIgnoreCase))
                {
                    SetSelectedRow(row);
                    return true;
                }
            }

            return false;
        }

        protected void SetSelectedRow(TRow row, bool repaint = true)
        {
            bool changed = !ReferenceEquals(SelectedRow, row);
            if (changed)
            {
                CommitPendingEdits();
                ClearEditorTextFocus();
            }

            SelectedRow = row;
            UnityEngine.Object soTarget = row != null ? GetSerializedObjectTarget(row) : null;
            SelectedSerializedObject = soTarget != null ? new SerializedObject(soTarget) : null;

            string id = row != null ? GetRowId(row) : null;
            _anchorRowId = id;
            _selectedIds.Clear();
            if (!string.IsNullOrWhiteSpace(id))
            {
                _selectedIds.Add(id);
                PushRecent(id);
            }

            if (changed)
                OnSelectionChanged(row);
            if (repaint)
                Window?.Repaint();
        }

        protected virtual void OnSelectionChanged(TRow newRow)
        {
        }

        /// <summary>
        /// Handle a click on a row in the list, honouring shift (range) and ctrl/cmd (toggle).
        /// Replaces a direct call to <see cref="SetSelectedRow"/> in row click handlers.
        /// </summary>
        protected void HandleRowClick(TRow clickedRow)
        {
            if (clickedRow == null)
                return;

            Event evt = Event.current;
            bool shift = evt != null && evt.shift;
            bool toggle = evt != null && (evt.control || evt.command);

            if (shift && !string.IsNullOrWhiteSpace(_anchorRowId))
            {
                SelectRange(_anchorRowId, GetRowId(clickedRow));
                SetPrimaryWithoutClearingMulti(clickedRow);
                return;
            }

            if (toggle)
            {
                string clickedId = GetRowId(clickedRow);
                if (!string.IsNullOrWhiteSpace(clickedId))
                {
                    if (_selectedIds.Contains(clickedId))
                    {
                        _selectedIds.Remove(clickedId);
                        if (ReferenceEquals(SelectedRow, clickedRow))
                        {
                            TRow next = FindFirstSelectedRow();
                            if (next != null)
                                SetPrimaryWithoutClearingMulti(next);
                            else
                                SetSelectedRow(null);
                        }
                    }
                    else
                    {
                        _selectedIds.Add(clickedId);
                        _anchorRowId = clickedId;
                        SetPrimaryWithoutClearingMulti(clickedRow);
                    }
                }
                Window?.Repaint();
                return;
            }

            SetSelectedRow(clickedRow);
        }

        private void SelectRange(string fromId, string toId)
        {
            if (string.IsNullOrWhiteSpace(fromId) || string.IsNullOrWhiteSpace(toId))
                return;

            int fromIndex = -1;
            int toIndex = -1;
            for (int i = 0; i < FilteredRows.Count; i++)
            {
                string rowId = GetRowId(FilteredRows[i]);
                if (fromIndex < 0 && string.Equals(rowId, fromId, StringComparison.OrdinalIgnoreCase))
                    fromIndex = i;
                if (toIndex < 0 && string.Equals(rowId, toId, StringComparison.OrdinalIgnoreCase))
                    toIndex = i;
            }

            if (fromIndex < 0 || toIndex < 0)
                return;

            int min = Mathf.Min(fromIndex, toIndex);
            int max = Mathf.Max(fromIndex, toIndex);
            _selectedIds.Clear();
            for (int i = min; i <= max; i++)
            {
                string id = GetRowId(FilteredRows[i]);
                if (!string.IsNullOrWhiteSpace(id))
                    _selectedIds.Add(id);
            }
        }

        private TRow FindFirstSelectedRow()
        {
            foreach (string id in _selectedIds)
            {
                TRow row = FindRowById(id);
                if (row != null)
                    return row;
            }
            return null;
        }

        private void SetPrimaryWithoutClearingMulti(TRow row)
        {
            bool changed = !ReferenceEquals(SelectedRow, row);
            if (changed)
            {
                CommitPendingEdits();
                ClearEditorTextFocus();
            }

            SelectedRow = row;
            UnityEngine.Object soTarget = row != null ? GetSerializedObjectTarget(row) : null;
            SelectedSerializedObject = soTarget != null ? new SerializedObject(soTarget) : null;

            string id = row != null ? GetRowId(row) : null;
            if (!string.IsNullOrWhiteSpace(id))
                PushRecent(id);

            if (changed)
                OnSelectionChanged(row);
            Window?.Repaint();
        }

        private void PruneSelectedIds()
        {
            if (_selectedIds.Count == 0)
                return;

            List<string> toRemove = null;
            foreach (string id in _selectedIds)
            {
                if (FindRowById(id) == null)
                {
                    toRemove ??= new List<string>();
                    toRemove.Add(id);
                }
            }

            if (toRemove == null)
                return;
            for (int i = 0; i < toRemove.Count; i++)
                _selectedIds.Remove(toRemove[i]);
        }

        protected void EnsureSelectedSerializedObject()
        {
            UnityEngine.Object asset = SelectedRow != null ? GetSerializedObjectTarget(SelectedRow) : null;
            if (asset == null)
            {
                SelectedSerializedObject = null;
                return;
            }

            if (SelectedSerializedObject == null || SelectedSerializedObject.targetObject != asset)
                SelectedSerializedObject = new SerializedObject(asset);
        }

        /// <summary>
        /// Override when the inspected SerializedObject is something other than the row's
        /// own asset (e.g. ItemRegistry rather than each individual ItemComponent).
        /// </summary>
        protected virtual UnityEngine.Object GetSerializedObjectTarget(TRow row) => GetRowAsset(row);

        protected TRow FindRowByAsset(UnityEngine.Object asset)
        {
            if (asset == null)
                return null;
            for (int i = 0; i < Rows.Count; i++)
            {
                TRow row = Rows[i];
                if (row != null && GetRowAsset(row) == asset)
                    return row;
            }

            return null;
        }

        protected bool SetSelectedRowByAsset(UnityEngine.Object asset)
        {
            TRow row = FindRowByAsset(asset);
            if (row == null)
                return false;
            SetSelectedRow(row);
            return true;
        }

        protected TRow FindRowById(string id)
        {
            if (string.IsNullOrWhiteSpace(id))
                return null;
            for (int i = 0; i < Rows.Count; i++)
            {
                TRow row = Rows[i];
                if (row != null && string.Equals(GetRowId(row), id, StringComparison.OrdinalIgnoreCase))
                    return row;
            }

            return null;
        }

        protected void RefreshRow(TRow row)
        {
            if (row == null)
                return;

            int index = Rows.IndexOf(row);
            TRow rebuilt = RebuildRow(row);
            if (rebuilt == null)
                return;

            if (index >= 0)
                Rows[index] = rebuilt;
            else
                Rows.Add(rebuilt);

            if (ReferenceEquals(row, SelectedRow))
                SelectedRow = rebuilt;

            _filteredRowsDirty = true;
        }

        /// <summary>
        /// Override to rebuild a single row in place (used after edits). Default returns the
        /// row unchanged — override when row metadata needs recomputing from the asset.
        /// </summary>
        protected virtual TRow RebuildRow(TRow row) => row;

        // -- Shared row drawing helpers -----------------------------------------------------

        protected bool IsSelected(TRow row)
        {
            if (ReferenceEquals(row, SelectedRow))
                return true;
            string id = GetRowId(row);
            return !string.IsNullOrWhiteSpace(id) && _selectedIds.Contains(id);
        }

        protected void DrawDefaultSerializedInspector()
        {
            EnsureSelectedSerializedObject();
            if (SelectedSerializedObject == null)
                return;

            SelectedSerializedObject.Update();
            SerializedProperty iterator = SelectedSerializedObject.GetIterator();
            bool enterChildren = true;
            while (iterator.NextVisible(enterChildren))
            {
                using (new EditorGUI.DisabledScope(iterator.propertyPath == "m_Script"))
                    EditorGUILayout.PropertyField(iterator, true);
                enterChildren = false;
            }
        }

        // -- Keyboard / focus --------------------------------------------------------------

        private void DrawNamedSearchField()
        {
            if (_searchControlName == null)
                _searchControlName = $"SolDatabaseSearch.{Tab}";

            EditorGUILayout.BeginHorizontal(EditorStyles.toolbar);
            GUI.SetNextControlName(_searchControlName);
            EditorGUI.BeginChangeCheck();
            Search = GUILayout.TextField(Search, GUI.skin.FindStyle("ToolbarSearchTextField"), GUILayout.MinWidth(120f));
            bool changed = EditorGUI.EndChangeCheck();
            if (GUILayout.Button(GUIContent.none, GUI.skin.FindStyle("ToolbarSearchCancelButton")))
            {
                Search = string.Empty;
                GUI.FocusControl(null);
                changed = true;
            }
            EditorGUILayout.EndHorizontal();
            if (changed)
                _filteredRowsDirty = true;

            if (_focusSearchOnNextDraw && Event.current.type == EventType.Repaint)
            {
                _focusSearchOnNextDraw = false;
                EditorGUI.FocusTextInControl(_searchControlName);
            }
        }

        private void FocusSearchField()
        {
            _focusSearchOnNextDraw = true;
            Window?.Repaint();
        }

        private void HandleKeyboardShortcuts()
        {
            Event e = Event.current;
            if (e.type != EventType.KeyDown)
                return;

            bool searchFocused = GUI.GetNameOfFocusedControl() == _searchControlName;

            // Ctrl/Cmd+F always reaches us — even from the search field — to toggle focus.
            if (e.keyCode == KeyCode.F && (e.control || e.command))
            {
                FocusSearchField();
                e.Use();
                return;
            }

            if (searchFocused || EditorGUIUtility.editingTextField)
                return;

            switch (e.keyCode)
            {
                case KeyCode.UpArrow:
                    NavigateRow(-1);
                    e.Use();
                    break;
                case KeyCode.DownArrow:
                    NavigateRow(+1);
                    e.Use();
                    break;
                case KeyCode.F2:
                    if (SelectedRow != null)
                    {
                        OnSelectAssetClicked();
                        e.Use();
                    }
                    break;
                case KeyCode.Delete:
                case KeyCode.Backspace:
                    if (SupportsBulkOps && _selectedIds.Count > 0)
                    {
                        ConfirmAndBulkDelete();
                        e.Use();
                    }
                    break;
                case KeyCode.N:
                    if ((e.control || e.command) && ShowDefaultToolbarOps)
                    {
                        OnNewClicked();
                        e.Use();
                    }
                    break;
                case KeyCode.D:
                    if ((e.control || e.command) && SelectedRow != null && ShowDefaultToolbarOps)
                    {
                        OnDuplicateClicked();
                        e.Use();
                    }
                    break;
            }
        }

        private void NavigateRow(int delta)
        {
            if (FilteredRows.Count == 0)
                return;

            int currentIndex = -1;
            if (SelectedRow != null)
            {
                for (int i = 0; i < FilteredRows.Count; i++)
                {
                    if (ReferenceEquals(FilteredRows[i], SelectedRow))
                    {
                        currentIndex = i;
                        break;
                    }
                }
            }

            int next = Mathf.Clamp(currentIndex + delta, 0, FilteredRows.Count - 1);
            if (next == currentIndex && currentIndex >= 0)
                return;
            SetSelectedRow(FilteredRows[next]);
        }

        // -- Recent dropdown ---------------------------------------------------------------

        protected virtual bool ShowRecentDropdown => ShowDefaultToolbarOps;

        protected void DrawRecentDropdown()
        {
            if (!ShowRecentDropdown || _recentIds.Count == 0)
                return;

            if (!GUILayout.Button("Recent ▾", EditorStyles.toolbarDropDown, GUILayout.Width(72f)))
                return;

            GenericMenu menu = new();
            for (int i = 0; i < _recentIds.Count; i++)
            {
                string id = _recentIds[i];
                TRow row = FindRowById(id);
                string label = row != null ? FormatRecentLabel(row) : $"{id} (not found)";
                if (row != null)
                {
                    string captured = id;
                    menu.AddItem(new GUIContent(label), false, () => SelectById(captured));
                }
                else
                {
                    menu.AddDisabledItem(new GUIContent(label));
                }
            }

            menu.ShowAsContext();
        }

        /// <summary>Label shown in the Recent dropdown. Defaults to the row id; override for richer formatting.</summary>
        protected virtual string FormatRecentLabel(TRow row) => GetRowId(row) ?? string.Empty;

        // -- Bulk actions -------------------------------------------------------------------

        /// <summary>Currently-selected rows, resolved from <see cref="SelectedIds"/>.</summary>
        protected List<TRow> GetBulkSelectedRows()
        {
            List<TRow> result = new();
            foreach (string id in _selectedIds)
            {
                TRow row = FindRowById(id);
                if (row != null)
                    result.Add(row);
            }
            return result;
        }

        private void ConfirmAndBulkDelete()
        {
            List<TRow> rows = GetBulkSelectedRows();
            if (rows.Count == 0)
                return;

            string preview = BuildBulkPreview(rows, max: 10);
            string entriesWord = rows.Count == 1 ? "entry" : "entries";
            string message = $"Delete {rows.Count} {entriesWord}? This cannot be undone.\n\n{preview}";
            if (!EditorUtility.DisplayDialog($"Delete {rows.Count} {DisplayName}", message, "Delete", "Cancel"))
                return;

            try
            {
                AssetDatabase.StartAssetEditing();
                DoBulkDelete(rows);
            }
            finally
            {
                AssetDatabase.StopAssetEditing();
                AssetDatabase.SaveAssets();
            }

            _selectedIds.Clear();
            _anchorRowId = null;
            SelectedRow = null;
            SelectedSerializedObject = null;
            RefreshIndex();
        }

        private void ConfirmAndBulkDuplicate()
        {
            List<TRow> rows = GetBulkSelectedRows();
            if (rows.Count == 0)
                return;

            try
            {
                AssetDatabase.StartAssetEditing();
                DoBulkDuplicate(rows);
            }
            finally
            {
                AssetDatabase.StopAssetEditing();
                AssetDatabase.SaveAssets();
            }

            RefreshIndex();
        }

        protected virtual void DoBulkDelete(IReadOnlyList<TRow> rows)
        {
            Debug.LogWarning($"Bulk delete is not implemented for {GetType().Name}.");
        }

        protected virtual void DoBulkDuplicate(IReadOnlyList<TRow> rows)
        {
            Debug.LogWarning($"Bulk duplicate is not implemented for {GetType().Name}.");
        }

        private string BuildBulkPreview(IReadOnlyList<TRow> rows, int max)
        {
            int shown = Mathf.Min(rows.Count, max);
            System.Text.StringBuilder sb = new();
            for (int i = 0; i < shown; i++)
                sb.Append("• ").Append(GetRowId(rows[i]) ?? "<missing>").Append('\n');
            if (rows.Count > shown)
                sb.Append("• … and ").Append(rows.Count - shown).Append(" more");
            return sb.ToString();
        }
    }
}
