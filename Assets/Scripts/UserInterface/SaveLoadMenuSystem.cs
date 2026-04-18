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
        [SerializeField] private ScrollRect _scrollRect;
        [SerializeField] private Transform _contentParent;
        [SerializeField] private Button _backButton;
        [SerializeField] private Button _newSaveButton;
        [SerializeField] private TMP_InputField _saveNameInput;

        public bool ReturnsToPause => _returnToPause;
        public SaveLoadMode Mode => _mode;

        private readonly List<GameObject> _spawnedEntries = new();
        private readonly List<Texture2D> _loadedThumbnails = new();
        private GameObject _slotEntryTemplate;
        private bool _returnToPause;
        private SaveLoadMode _mode;

        protected override void Awake()
        {
            base.Awake();
            if (_instance != this)
                return;

            AutoWire();
            SetOpen(false);
        }

        protected override void PostResolve() => AutoWire();

        public void Open(SaveLoadMode mode, bool returnToPause = false)
        {
            _mode = mode;
            _returnToPause = returnToPause;
            AutoWire();
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

            RebuildSlots();
            MenuUiUtility.SelectButton(isSave && _newSaveButton != null ? _newSaveButton : _backButton);
        }

        public void Close() => Close(false);

        public void Close(bool reopenPause)
        {
            if (!IsOpen)
                return;

            bool wasPauseSubmenu = _returnToPause;
            _returnToPause = false;

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

            yield return new WaitForEndOfFrame();

            ResolveSaveManager()?.CaptureScreenshotForSlot(slotIndex);

            yield return null;

            if (_canvasGroup != null)
            {
                _canvasGroup.alpha = prevAlpha;
                _canvasGroup.blocksRaycasts = prevBlocksRaycasts;
            }

            if (this != null && isActiveAndEnabled)
                RebuildSlots();
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

        private void RebuildSlots()
        {
            ClearEntries();

            SaveManager manager = ResolveSaveManager();
            if (_contentParent == null || manager == null)
                return;

            if (_mode == SaveLoadMode.Save)
            {
                bool emptyManualSlotShown = false;
                for (int i = 0; i < SaveManager.MaxSlots; i++)
                {
                    bool hasSave = manager.SlotExists(i);
                    bool shouldShowEmpty = i != SaveManager.AutoSaveSlot && !emptyManualSlotShown;

                    if (hasSave)
                        CreateSlotEntry(manager, i);
                    else if (shouldShowEmpty)
                    {
                        CreateSlotEntry(manager, i);
                        emptyManualSlotShown = true;
                    }
                }
            }
            else
            {
                for (int i = 0; i < SaveManager.MaxSlots; i++)
                {
                    if (manager.SlotExists(i))
                        CreateSlotEntry(manager, i);
                }
            }

            if (_scrollRect != null)
                _scrollRect.verticalNormalizedPosition = 1f;

            LayoutRebuilder.ForceRebuildLayoutImmediate(_contentParent as RectTransform);
        }

        private void CreateSlotEntry(SaveManager manager, int slotIndex)
        {
            GameObject entry = CreateEntry(slotIndex);
            if (entry == null)
                return;

            SaveMetadata metadata = manager.GetSlotMetadata(slotIndex);
            bool hasSave = metadata != null;
            bool isSave = _mode == SaveLoadMode.Save;

            TMP_Text slotNameText = MenuUiUtility.FindTextByNames(entry.transform, "SlotName");
            TMP_Text dateTimeText = MenuUiUtility.FindTextByNames(entry.transform, "DateTime");
            TMP_Text playtimeText = MenuUiUtility.FindTextByNames(entry.transform, "Playtime");
            RawImage thumbnailImage = MenuUiUtility.FindDeepComponent<RawImage>(entry.transform, "Thumbnail");

            if (slotNameText != null)
                slotNameText.text = hasSave
                    ? string.IsNullOrWhiteSpace(metadata.SaveName) ? BuildSlotName(slotIndex) : metadata.SaveName.Trim()
                    : $"{BuildSlotName(slotIndex)} - Empty";

            if (dateTimeText != null)
                dateTimeText.text = hasSave ? manager.GetSlotInGameDate(slotIndex) : "Empty";

            if (playtimeText != null)
            {
                playtimeText.text = hasSave
                    ? $"{metadata.Timestamp} | {SaveManager.FormatPlaytime(metadata.PlaytimeSeconds)}"
                    : "-- | 0m";
            }

            if (thumbnailImage != null)
            {
                Texture2D screenshot = hasSave ? manager.LoadScreenshot(slotIndex) : null;
                thumbnailImage.texture = screenshot;
                thumbnailImage.color = screenshot != null ? Color.white : new Color(0.2f, 0.2f, 0.2f, 0.5f);
                if (screenshot != null)
                    _loadedThumbnails.Add(screenshot);
            }

            Button actionButton = MenuUiUtility.FindButtonByNames(entry.transform, "SelectButton", "Save", "Load", "Save...", "LoadButton")
                ?? entry.GetComponent<Button>()
                ?? entry.GetComponentInChildren<Button>(true);
            if (actionButton != null)
            {
                actionButton.onClick.RemoveAllListeners();
                actionButton.interactable = true;

                if (isSave)
                {
                    actionButton.onClick.AddListener(() => HandleSaveSlotSelected(slotIndex, hasSave));
                    SetButtonLabel(actionButton, hasSave ? "Overwrite" : "Save");
                }
                else
                {
                    actionButton.onClick.AddListener(() => RequestLoadSlot(slotIndex));
                    SetButtonLabel(actionButton, "Load");
                }
            }

            Button deleteButton = MenuUiUtility.FindButtonByNames(entry.transform, "Delete", "Delete...", "DeleteButton");
            if (deleteButton != null)
            {
                deleteButton.onClick.RemoveAllListeners();
                deleteButton.gameObject.SetActive(hasSave);
                if (hasSave)
                    deleteButton.onClick.AddListener(() => RequestDeleteSlot(slotIndex));
            }

            entry.SetActive(true);
            _spawnedEntries.Add(entry);
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

            RebuildSlots();
        }

        private GameObject CreateEntry(int slotIndex)
        {
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
            return clone;
        }

        private void AutoWire()
        {
            _scrollRect ??= GetComponentInChildren<ScrollRect>(true);
            _contentParent ??= MenuUiUtility.FindDeep(transform, "Content")
                ?? MenuUiUtility.FindDeep(transform, "ContentArea")
                ?? (_scrollRect != null ? _scrollRect.content : null);
            _backButton ??= MenuUiUtility.FindButtonByNames(transform, "Back", "CloseButton");
            _newSaveButton ??= MenuUiUtility.FindButtonByNames(transform, "NewSave", "newSaveButton", "NewSaveButton");
            _saveNameInput ??= MenuUiUtility.FindDeepComponent<TMP_InputField>(transform, "SaveNameInput")
                ?? GetComponentInChildren<TMP_InputField>(true);

            if (_slotEntryTemplate == null && _contentParent != null && _contentParent.childCount > 0)
            {
                _slotEntryTemplate = _contentParent.GetChild(0).gameObject;
                _slotEntryTemplate.SetActive(false);
            }

            MenuUiUtility.WireButton(_backButton, () => Close(_returnToPause));
            MenuUiUtility.WireButton(_newSaveButton, CreateNewSave);
        }

        private void ClearEntries()
        {
            for (int i = 0; i < _loadedThumbnails.Count; i++)
            {
                if (_loadedThumbnails[i] != null)
                    UnityEngine.Object.Destroy(_loadedThumbnails[i]);
            }
            _loadedThumbnails.Clear();

            for (int i = _spawnedEntries.Count - 1; i >= 0; i--)
            {
                if (_spawnedEntries[i] != null)
                    UnityEngine.Object.Destroy(_spawnedEntries[i]);
            }
            _spawnedEntries.Clear();

            if (_contentParent == null)
                return;

            for (int i = _contentParent.childCount - 1; i >= 0; i--)
            {
                Transform child = _contentParent.GetChild(i);
                if (child != null && child.gameObject != _slotEntryTemplate)
                    UnityEngine.Object.Destroy(child.gameObject);
            }
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

                if (!DateTime.TryParse(metadata.Timestamp, out DateTime parsed))
                    parsed = DateTime.MinValue;

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

        private static void SetButtonLabel(Button button, string label)
        {
            if (button == null || string.IsNullOrWhiteSpace(label))
                return;

            TMP_Text text = button.GetComponentInChildren<TMP_Text>(true);
            if (text != null)
                text.text = label;
        }
    }
}
