using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace Sol.Editor
{
    /// <summary>
    /// Shared list-detail page scaffold. Owns search/filter/sort state, virtualized list
    /// rendering, selection, and pending-edit commit. Subclasses describe what rows mean,
    /// how to draw them, and how to mutate underlying assets.
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
        private bool _filterCacheInitialized;
        private bool _filteredRowsDirty = true;

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

        // -- Row contract -------------------------------------------------------------------

        protected abstract UnityEngine.Object GetRowAsset(TRow row);
        protected abstract string GetRowId(TRow row);
        protected abstract string GetRowSearchText(TRow row);
        protected abstract int GetRowWarningCount(TRow row);
        protected abstract bool MatchesCustomFilter(TRow row, TFilter filter);
        protected abstract void SortRows(List<TRow> rows, TSort sort);
        protected abstract void DrawRow(Rect rect, TRow row);
        protected abstract void DrawDetail(TRow row);

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

            if (!string.IsNullOrWhiteSpace(previouslySelectedId))
            {
                TRow rebound = FindRowById(previouslySelectedId);
                SetSelectedRow(rebound, repaint: false);
            }
        }

        protected virtual void OnBeforeLoadRows()
        {
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
            }
            else
            {
                DrawToolbarBeforeRowOps();
                DrawToolbarSelectionExtras();
            }

            DrawToolbarTrailing();
        }

        protected virtual bool ShowDefaultToolbarOps => true;

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
            EditorGUILayout.BeginHorizontal();
            DrawLeftPane();
            DrawRightPane();
            EditorGUILayout.EndHorizontal();
        }

        protected virtual float LeftPaneWidth => SolDatabaseStyles.DefaultLeftPaneWidth;

        protected virtual void DrawLeftPane()
        {
            EditorGUILayout.BeginVertical(GUILayout.Width(LeftPaneWidth));

            DrawSearchField(() => _filteredRowsDirty = true);

            EditorGUI.BeginChangeCheck();
            DrawFilterControls();
            if (EditorGUI.EndChangeCheck())
                _filteredRowsDirty = true;

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

        protected virtual string EmptyListMessage => $"No {DisplayName.ToLowerInvariant()} match the current filters.";

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
                && EqualityComparer<TSort>.Default.Equals(_lastSort, _sort))
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
            if (changed)
                OnSelectionChanged(row);
            if (repaint)
                Window?.Repaint();
        }

        protected virtual void OnSelectionChanged(TRow newRow)
        {
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

        protected bool IsSelected(TRow row) => ReferenceEquals(row, SelectedRow);

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
    }
}
