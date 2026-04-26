using System;
using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using Sol.SaveLoad;

namespace Sol.HUD
{
    public enum SaveLoadMode { Save, Load }

    public sealed class SaveLoadMenuSystem : MenuSystemBase<SaveLoadMenuSystem>
    {
        #region Inspector Settings
        [Tooltip("Inspector: tunes title text.")]
        [SerializeField] private TextMeshProUGUI _TitleText;
        [SerializeField] private TextMeshProUGUI _hintText1;
        [Tooltip("Inspector: tunes hint text2.")]
        [SerializeField] private TextMeshProUGUI _hintText2;
        [SerializeField] private ScrollRect _scrollRect;
        [Tooltip("Inspector: tunes content parent.")]
        [SerializeField] private Transform _contentParent;
        [SerializeField] private Button _backButton;
        [Tooltip("Inspector: tunes new save button.")]
        [SerializeField] private Button _newSaveButton;
        [SerializeField] private TMP_InputField _saveNameInput;
        [Tooltip("Inspector: tunes slot entry template.")]
        [SerializeField] private GameObject _slotEntryTemplate;
        #endregion

        private sealed class SlotRow
        {
            public int SlotIndex;
            public GameObject Root;
            public TMP_Text SlotNameText;
            public TMP_Text DateTimeText;
            public TMP_Text PlaytimeText;
            public RawImage ThumbnailImage;
            public Button ActionButton;
            public TMP_Text ActionButtonText;
            public Button DeleteButton;
        }

        private const int ThumbnailLoadsPerFrame = 1;
        private static readonly Color FilledThumbnailColor = Color.white;
        private static readonly Color EmptyThumbnailColor = new Color(0.2f, 0.2f, 0.2f, 0.5f);

        public bool ReturnsToPause => _returnToPause;
        public SaveLoadMode Mode => _mode;

        private readonly Dictionary<int, SlotRow> _slotRows = new();
        private readonly List<int> _visibleSlots = new();
        private readonly Queue<int> _pendingThumbnailSlots = new();
        private readonly HashSet<int> _pendingThumbnailSet = new();
        private readonly Dictionary<int, Texture2D> _thumbnailCache = new();

        private bool _returnToPause;
        private SaveLoadMode _mode;
        private int _firstEmptyManualSlot = -1;
        private Coroutine _thumbnailLoadCoroutine;

        protected override void Awake()
        {
            base.Awake();
            if (_instance != this)
                return;

            CacheReferences();
            SetOpen(false);
        }

        protected override void PostResolve() => CacheReferences();

        protected override void OnDestroy()
        {
            StopThumbnailLoading();
            DestroyAllCachedThumbnails();
            base.OnDestroy();
        }

        public void Open(SaveLoadMode mode, bool returnToPause = false)
        {
            _mode = mode;
            _returnToPause = returnToPause;
            CacheReferences();
            UIStateOwnership.CloseConflictingUi(nameof(SaveLoadMenuSystem));
            SetOpen(true);
            MenuUiUtility.BringToFront(transform.parent);
            MenuUiUtility.BringToFront(transform);
            MenuUiUtility.SetBackgroundUiRaycasts(transform, false);
            UIStateOwnership.SetUiCapture(true);

            bool isSave = mode == SaveLoadMode.Save;
            if (_newSaveButton != null) _newSaveButton.gameObject.SetActive(isSave);
            if (_saveNameInput != null) _saveNameInput.gameObject.SetActive(isSave);
            if (isSave) SetDefaultSaveName();

            RefreshVisibleSlots(resetScrollPosition: true);
            MenuUiUtility.SelectButton(isSave && _newSaveButton != null ? _newSaveButton : _backButton);
        }

        public void Close() => Close(false);

        public void Close(bool reopenPause)
        {
            if (!IsOpen)
                return;

            bool wasPauseSubmenu = _returnToPause;
            _returnToPause = false;
            StopThumbnailLoading();

            if (_newSaveButton != null) _newSaveButton.gameObject.SetActive(false);
            if (_saveNameInput != null) _saveNameInput.gameObject.SetActive(false);

            SetOpen(false);
            MenuUiUtility.SetBackgroundUiRaycasts(transform, true);

            if (reopenPause)
            {
                PauseMenuSystem.ResolveInstance()?.Show();
                return;
            }

            if (wasPauseSubmenu)
            {
                PauseMenuSystem pauseMenu = PauseMenuSystem.ResolveInstance(activateIfInactive: false);
                if (pauseMenu != null)
                {
                    pauseMenu.EndPauseSessionFromSubmenu();
                    return;
                }
            }

            UIStateOwnership.SetUiCapture(false);
        }

        private void SetDefaultSaveName()
        {
            if (_saveNameInput != null)
                _saveNameInput.text = $"Save - {DateTime.Now:dd MMM HH:mm}";
        }

        private void CreateNewSave()
        {
            SaveManager manager = ResolveSaveManager();
            if (manager == null)
                return;

            int emptySlot = FindPreferredSaveSlot(manager);
            if (emptySlot >= 0)
            {
                SaveToSlot(emptySlot);
                return;
            }

            int overwriteSlot = FindOldestManualSlot(manager);
            if (overwriteSlot < 0)
            {
                SaveToSlot(SaveManager.AutoSaveSlot);
                return;
            }

            DialoguePromptSystem prompt = DialoguePromptSystem.ResolveInstance(activateIfInactive: true);
            if (prompt != null)
            {
                int slot = overwriteSlot;
                prompt.Show(
                    "All Slots Full",
                    $"Overwrite the oldest save in {BuildSlotName(slot)}?",
                    () => SaveToSlot(slot),
                    () => { },
                    confirmLabel: "Overwrite",
                    cancelLabel: "Cancel",
                    closeConflictingUi: false);
                return;
            }

            SaveToSlot(overwriteSlot);
        }

        private void SaveToSlot(int slotIndex)
        {
            SaveManager manager = ResolveSaveManager();
            if (manager == null)
                return;

            string saveName = _saveNameInput != null ? _saveNameInput.text : null;
            if (!manager.SaveGame(slotIndex, saveName))
                return;

            StopAllCoroutines();
            if (isActiveAndEnabled)
                StartCoroutine(CaptureScreenshotAndRefresh(slotIndex));
        }

        private IEnumerator CaptureScreenshotAndRefresh(int slotIndex)
        {
            float prevAlpha = _canvasGroup != null ? _canvasGroup.alpha : 1f;
            bool prevBlocksRaycasts = _canvasGroup != null && _canvasGroup.blocksRaycasts;

            if (_canvasGroup != null)
            {
                _canvasGroup.alpha = 0f;
                _canvasGroup.blocksRaycasts = false;
            }

            Canvas.ForceUpdateCanvases();
            yield return new WaitForEndOfFrame();

            ResolveSaveManager()?.CaptureScreenshotForSlot(slotIndex);

            yield return null;

            if (_canvasGroup != null)
            {
                _canvasGroup.alpha = prevAlpha;
                _canvasGroup.blocksRaycasts = prevBlocksRaycasts;
            }

            if (this != null && isActiveAndEnabled)
            {
                InvalidateThumbnail(slotIndex);
                RefreshAfterSlotMutation(slotIndex);
            }
        }

        private void LoadSlot(int slotIndex)
        {
            SaveManager manager = ResolveSaveManager();
            if (manager == null || !manager.LoadGame(slotIndex))
                return;

            PauseMenuSystem.ResolveInstance(false)?.Close();
            Close(reopenPause: false);
            Time.timeScale = 1f;
        }

        private void RequestLoadSlot(int slotIndex)
        {
            DialoguePromptSystem prompt = DialoguePromptSystem.ResolveInstance(activateIfInactive: true);
            if (prompt != null)
            {
                prompt.Show(
                    "Load Save?",
                    "Unsaved progress will be lost.",
                    () => LoadSlot(slotIndex),
                    () => { },
                    confirmLabel: "Load",
                    cancelLabel: "Cancel",
                    closeConflictingUi: false);
                return;
            }

            LoadSlot(slotIndex);
        }

        private void RequestDeleteSlot(int slotIndex)
        {
            DialoguePromptSystem prompt = DialoguePromptSystem.ResolveInstance(activateIfInactive: true);
            if (prompt != null)
            {
                prompt.Show(
                    "Delete Save?",
                    "This save data will be permanently deleted.",
                    () => DeleteSlot(slotIndex),
                    () => { },
                    confirmLabel: "Delete",
                    cancelLabel: "Cancel",
                    closeConflictingUi: false);
                return;
            }

            DeleteSlot(slotIndex);
        }

        private void DeleteSlot(int slotIndex)
        {
            SaveManager manager = ResolveSaveManager();
            if (manager == null || !manager.DeleteSave(slotIndex))
                return;

            InvalidateThumbnail(slotIndex);
            RefreshAfterSlotMutation(slotIndex);
        }

        private void HandleSaveSlotSelected(int slotIndex, bool hasSave)
        {
            if (!hasSave)
            {
                SaveToSlot(slotIndex);
                return;
            }

            DialoguePromptSystem prompt = DialoguePromptSystem.ResolveInstance(activateIfInactive: true);
            if (prompt != null)
            {
                prompt.Show(
                    "Overwrite Save?",
                    "This will replace the existing save data.",
                    () => SaveToSlot(slotIndex),
                    () => { },
                    confirmLabel: "Overwrite",
                    cancelLabel: "Cancel",
                    closeConflictingUi: false);
                return;
            }

            SaveToSlot(slotIndex);
        }

        private void RefreshVisibleSlots(bool resetScrollPosition)
        {
            SaveManager manager = ResolveSaveManager();
            if (_contentParent == null || manager == null)
                return;

            SaveMetadata[] metadataBySlot = manager.GetAllSlotMetadata();
            List<int> desiredSlots = BuildDesiredSlotList(metadataBySlot, _mode, out int firstEmptyManualSlot);
            List<int> addedSlots = ApplyVisibleSlotSet(desiredSlots);
            _firstEmptyManualSlot = firstEmptyManualSlot;

            for (int i = 0; i < desiredSlots.Count; i++)
            {
                int slotIndex = desiredSlots[i];
                RefreshSlotRow(slotIndex, metadataBySlot[slotIndex]);
            }

            for (int i = 0; i < addedSlots.Count; i++)
                QueueThumbnailLoad(addedSlots[i]);

            if (resetScrollPosition && _scrollRect != null)
                _scrollRect.verticalNormalizedPosition = 1f;

            LayoutRebuilder.ForceRebuildLayoutImmediate(_contentParent as RectTransform);
            StartThumbnailLoaderIfNeeded();
        }

        private void RefreshAfterSlotMutation(int changedSlotIndex)
        {
            SaveManager manager = ResolveSaveManager();
            if (_contentParent == null || manager == null)
                return;

            SaveMetadata[] metadataBySlot = manager.GetAllSlotMetadata();
            int previousFirstEmpty = _firstEmptyManualSlot;

            List<int> desiredSlots = BuildDesiredSlotList(metadataBySlot, _mode, out int firstEmptyManualSlot);
            List<int> addedSlots = ApplyVisibleSlotSet(desiredSlots);
            _firstEmptyManualSlot = firstEmptyManualSlot;

            HashSet<int> refreshTargets = new HashSet<int>(addedSlots);
            refreshTargets.Add(changedSlotIndex);
            if (previousFirstEmpty >= 0)
                refreshTargets.Add(previousFirstEmpty);
            if (_firstEmptyManualSlot >= 0)
                refreshTargets.Add(_firstEmptyManualSlot);

            foreach (int slotIndex in refreshTargets)
            {
                if (!_visibleSlots.Contains(slotIndex))
                    continue;

                RefreshSlotRow(slotIndex, metadataBySlot[slotIndex]);
                QueueThumbnailLoad(slotIndex);
            }

            LayoutRebuilder.ForceRebuildLayoutImmediate(_contentParent as RectTransform);
            StartThumbnailLoaderIfNeeded();
        }

        private List<int> ApplyVisibleSlotSet(List<int> desiredSlots)
        {
            HashSet<int> desiredSet = new HashSet<int>(desiredSlots);
            for (int i = 0; i < _visibleSlots.Count; i++)
            {
                int slotIndex = _visibleSlots[i];
                if (!desiredSet.Contains(slotIndex) && _slotRows.TryGetValue(slotIndex, out SlotRow row) && row.Root != null)
                    row.Root.SetActive(false);
            }

            _visibleSlots.Clear();
            _visibleSlots.AddRange(desiredSlots);

            List<int> addedSlots = new List<int>();
            for (int i = 0; i < desiredSlots.Count; i++)
            {
                int slotIndex = desiredSlots[i];
                SlotRow row = EnsureSlotRow(slotIndex);
                if (row == null || row.Root == null)
                    continue;

                if (!row.Root.activeSelf)
                    addedSlots.Add(slotIndex);

                row.Root.SetActive(true);
                row.Root.transform.SetSiblingIndex(i);
            }

            return addedSlots;
        }

        private SlotRow EnsureSlotRow(int slotIndex)
        {
            if (_slotRows.TryGetValue(slotIndex, out SlotRow existing) && existing != null && existing.Root != null)
                return existing;

            if (_contentParent == null)
                return null;

            if (_slotEntryTemplate == null)
            {
                Debug.LogWarning("[SaveLoadMenuSystem] No slot template found. Assign a template entry under the content root.", this);
                return null;
            }

            GameObject clone = UnityEngine.Object.Instantiate(_slotEntryTemplate, _contentParent);
            clone.name = $"Slot_{slotIndex:00}";
            clone.SetActive(false);

            SlotRow row = new SlotRow
            {
                SlotIndex = slotIndex,
                Root = clone,
                SlotNameText = MenuUiUtility.FindTextByNames(clone.transform, "SlotName"),
                DateTimeText = MenuUiUtility.FindTextByNames(clone.transform, "DateTime"),
                PlaytimeText = MenuUiUtility.FindTextByNames(clone.transform, "Playtime"),
                ThumbnailImage = MenuUiUtility.FindDeepComponent<RawImage>(clone.transform, "Thumbnail"),
                ActionButton = MenuUiUtility.FindButtonByNames(clone.transform, "SelectButton", "Save", "Load", "Save...", "LoadButton")
                    ?? clone.GetComponent<Button>()
                    ?? clone.GetComponentInChildren<Button>(true),
                DeleteButton = MenuUiUtility.FindButtonByNames(clone.transform, "Delete", "Delete...", "DeleteButton")
            };

            row.ActionButtonText = row.ActionButton != null ? row.ActionButton.GetComponentInChildren<TMP_Text>(true) : null;
            _slotRows[slotIndex] = row;
            return row;
        }

        private void RefreshSlotRow(int slotIndex, SaveMetadata metadata)
        {
            if (!_slotRows.TryGetValue(slotIndex, out SlotRow row) || row.Root == null)
                return;

            bool hasSave = metadata != null;
            bool isSaveMode = _mode == SaveLoadMode.Save;

            if (row.SlotNameText != null)
            {
                row.SlotNameText.text = hasSave
                    ? string.IsNullOrWhiteSpace(metadata.SaveName) ? BuildSlotName(slotIndex) : metadata.SaveName.Trim()
                    : $"{BuildSlotName(slotIndex)} - Empty";
            }

            if (row.DateTimeText != null)
                row.DateTimeText.text = hasSave ? BuildInGameDateText(metadata) : "Empty";

            if (row.PlaytimeText != null)
            {
                row.PlaytimeText.text = hasSave
                    ? $"{metadata.Timestamp} | {SaveManager.FormatPlaytime(metadata.PlaytimeSeconds)}"
                    : "-- | 0m";
            }

            if (row.ActionButton != null)
            {
                row.ActionButton.onClick.RemoveAllListeners();
                row.ActionButton.interactable = true;
                if (isSaveMode)
                {
                    row.ActionButton.onClick.AddListener(() => HandleSaveSlotSelected(slotIndex, hasSave));
                    SetButtonLabel(row, hasSave ? "Overwrite" : "Save");
                }
                else
                {
                    row.ActionButton.onClick.AddListener(() => RequestLoadSlot(slotIndex));
                    SetButtonLabel(row, "Load");
                }
            }

            if (row.DeleteButton != null)
            {
                row.DeleteButton.onClick.RemoveAllListeners();
                row.DeleteButton.gameObject.SetActive(hasSave);
                if (hasSave)
                    row.DeleteButton.onClick.AddListener(() => RequestDeleteSlot(slotIndex));
            }

            ApplySlotThumbnail(row, slotIndex, hasSave);
        }

        private void ApplySlotThumbnail(SlotRow row, int slotIndex, bool hasSave)
        {
            if (row.ThumbnailImage == null)
                return;

            if (!hasSave)
            {
                row.ThumbnailImage.texture = null;
                row.ThumbnailImage.color = EmptyThumbnailColor;
                return;
            }

            if (_thumbnailCache.TryGetValue(slotIndex, out Texture2D cached) && cached != null)
            {
                row.ThumbnailImage.texture = cached;
                row.ThumbnailImage.color = FilledThumbnailColor;
                return;
            }

            row.ThumbnailImage.texture = null;
            row.ThumbnailImage.color = EmptyThumbnailColor;
            QueueThumbnailLoad(slotIndex);
        }

        private void QueueThumbnailLoad(int slotIndex)
        {
            if (_pendingThumbnailSet.Add(slotIndex))
                _pendingThumbnailSlots.Enqueue(slotIndex);
        }

        private void StartThumbnailLoaderIfNeeded()
        {
            if (!isActiveAndEnabled)
                return;
            if (_thumbnailLoadCoroutine != null)
                return;
            if (_pendingThumbnailSlots.Count == 0)
                return;

            _thumbnailLoadCoroutine = StartCoroutine(LoadThumbnailsProgressively());
        }

        private void StopThumbnailLoading()
        {
            if (_thumbnailLoadCoroutine != null)
            {
                StopCoroutine(_thumbnailLoadCoroutine);
                _thumbnailLoadCoroutine = null;
            }

            _pendingThumbnailSlots.Clear();
            _pendingThumbnailSet.Clear();
        }

        private IEnumerator LoadThumbnailsProgressively()
        {
            while (_pendingThumbnailSlots.Count > 0)
            {
                int loadedThisFrame = 0;
                while (loadedThisFrame < ThumbnailLoadsPerFrame && _pendingThumbnailSlots.Count > 0)
                {
                    int slotIndex = _pendingThumbnailSlots.Dequeue();
                    _pendingThumbnailSet.Remove(slotIndex);

                    if (_thumbnailCache.ContainsKey(slotIndex))
                    {
                        loadedThisFrame++;
                        continue;
                    }

                    SaveManager manager = ResolveSaveManager();
                    if (manager != null)
                    {
                        Texture2D texture = manager.LoadScreenshot(slotIndex);
                        if (texture != null)
                            _thumbnailCache[slotIndex] = texture;
                    }

                    if (_slotRows.TryGetValue(slotIndex, out SlotRow row) && row.Root != null && row.Root.activeSelf)
                    {
                        if (_thumbnailCache.TryGetValue(slotIndex, out Texture2D cached) && cached != null)
                        {
                            row.ThumbnailImage.texture = cached;
                            row.ThumbnailImage.color = FilledThumbnailColor;
                        }
                    }

                    loadedThisFrame++;
                }

                yield return null;
            }

            _thumbnailLoadCoroutine = null;
        }

        private void InvalidateThumbnail(int slotIndex)
        {
            if (_thumbnailCache.TryGetValue(slotIndex, out Texture2D existing) && existing != null)
                Destroy(existing);
            _thumbnailCache.Remove(slotIndex);
        }

        private void DestroyAllCachedThumbnails()
        {
            foreach (Texture2D texture in _thumbnailCache.Values)
            {
                if (texture != null)
                    Destroy(texture);
            }
            _thumbnailCache.Clear();
        }

        private void CacheReferences()
        {
            _scrollRect ??= GetComponentInChildren<ScrollRect>(true);
            _contentParent ??= MenuUiUtility.FindDeep(transform, "Content")
                ?? MenuUiUtility.FindDeep(transform, "ContentArea")
                ?? (_scrollRect != null ? _scrollRect.content : null);
            _backButton ??= MenuUiUtility.FindButtonByNames(transform, "Back", "CloseButton");
            _newSaveButton ??= MenuUiUtility.FindButtonByNames(transform, "NewSave", "newSaveButton", "NewSaveButton");
            _saveNameInput ??= MenuUiUtility.FindDeepComponent<TMP_InputField>(transform, "SaveNameInput")
                ?? GetComponentInChildren<TMP_InputField>(true);

            if (_slotEntryTemplate != null)
            {
                _slotEntryTemplate.SetActive(false);
            }
            else
            {
                Debug.LogWarning("[SaveLoadMenuSystem] Slot entry template is not assigned.", this);
            }

            MenuUiUtility.WireButton(_backButton, () => Close(_returnToPause));
            MenuUiUtility.WireButton(_newSaveButton, CreateNewSave);
        }

        private static List<int> BuildDesiredSlotList(SaveMetadata[] metadataBySlot, SaveLoadMode mode, out int firstEmptyManualSlot)
        {
            List<int> desiredSlots = new List<int>(SaveManager.MaxSlots);
            firstEmptyManualSlot = -1;

            if (mode == SaveLoadMode.Save)
            {
                for (int i = 0; i < SaveManager.MaxSlots; i++)
                {
                    bool hasSave = metadataBySlot[i] != null;
                    if (hasSave)
                    {
                        desiredSlots.Add(i);
                    }
                    else if (i != SaveManager.AutoSaveSlot && firstEmptyManualSlot < 0)
                    {
                        firstEmptyManualSlot = i;
                    }
                }

                if (firstEmptyManualSlot >= 0)
                    desiredSlots.Add(firstEmptyManualSlot);
            }
            else
            {
                for (int i = 0; i < SaveManager.MaxSlots; i++)
                {
                    if (metadataBySlot[i] != null)
                        desiredSlots.Add(i);
                }
            }

            return desiredSlots;
        }

        private static string BuildInGameDateText(SaveMetadata metadata)
        {
            if (metadata == null)
                return string.Empty;

            if (!string.IsNullOrWhiteSpace(metadata.InGameDate))
                return metadata.InGameDate.Trim();

            return string.IsNullOrWhiteSpace(metadata.Timestamp) ? "Unknown" : metadata.Timestamp;
        }

        private static int FindPreferredSaveSlot(SaveManager manager)
        {
            for (int i = SaveManager.AutoSaveSlot + 1; i < SaveManager.MaxSlots; i++)
            {
                if (!manager.SlotExists(i))
                    return i;
            }
            return -1;
        }

        private static int FindOldestManualSlot(SaveManager manager)
        {
            int oldestSlot = -1;
            DateTime oldestTimestamp = DateTime.MaxValue;

            for (int i = SaveManager.AutoSaveSlot + 1; i < SaveManager.MaxSlots; i++)
            {
                SaveMetadata metadata = manager.GetSlotMetadata(i);
                if (metadata == null)
                    continue;

                DateTime parsed;

                if (metadata.TimestampTicks > 0)
                {
                    parsed = new DateTime(metadata.TimestampTicks, DateTimeKind.Utc);
                }
                else if (!DateTime.TryParse(
                            metadata.Timestamp,
                            System.Globalization.CultureInfo.InvariantCulture,
                            System.Globalization.DateTimeStyles.AssumeUniversal | System.Globalization.DateTimeStyles.AdjustToUniversal,
                            out parsed))
                {
                    parsed = DateTime.MinValue;
                }

                if (oldestSlot < 0 || parsed < oldestTimestamp)
                {
                    oldestSlot = i;
                    oldestTimestamp = parsed;
                }
            }

            return oldestSlot;
        }

        private static SaveManager ResolveSaveManager()
        {
            if (SaveManager.Instance != null)
                return SaveManager.Instance;

            SaveManager manager = UnityEngine.Object.FindFirstObjectByType<SaveManager>();
            if (manager != null)
                return manager;

            GameObject host = new GameObject("SaveManager");
            UnityEngine.Object.DontDestroyOnLoad(host);
            return host.AddComponent<SaveManager>();
        }

        private static string BuildSlotName(int slotIndex)
            => slotIndex == SaveManager.AutoSaveSlot ? "Autosave" : $"Slot {slotIndex}";

        private static void SetButtonLabel(SlotRow row, string label)
        {
            if (row == null || string.IsNullOrWhiteSpace(label))
                return;

            if (row.ActionButtonText != null)
                row.ActionButtonText.text = label;
        }
    }
}
