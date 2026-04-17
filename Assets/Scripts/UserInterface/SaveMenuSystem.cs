using System;
using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using Sol.SaveLoad;

namespace Sol.HUD
{
    public sealed class SaveMenuSystem : MenuSystemBase<SaveMenuSystem>
    {
        [SerializeField] private ScrollRect _scrollRect;
        [SerializeField] private Transform _contentParent;
        [SerializeField] private Button _backButton;
        [SerializeField] private Button _newSaveButton;
        [SerializeField] private TMP_InputField _saveNameInput;

        public bool ReturnsToPause => _returnToPause;

        private readonly List<GameObject> _spawnedEntries = new();
        private readonly List<Texture2D> _loadedThumbnails = new();
        private GameObject _slotEntryTemplate;
        private bool _returnToPause;

        protected override void Awake()
        {
            base.Awake();
            if (_instance != this)
                return;

            AutoWire();
            SetOpen(false);
        }

        protected override void PostResolve() => AutoWire();

        public void Open(bool returnToPause = false)
        {
            _returnToPause = returnToPause;
            AutoWire();
            UIStateOwnership.CloseConflictingUi(nameof(SaveMenuSystem));
            SetOpen(true);
            MenuUiUtility.BringToFront(transform.parent);
            MenuUiUtility.BringToFront(transform);
            MenuUiUtility.SetBackgroundUiRaycasts(transform, false);
            UIStateOwnership.SetUiCapture(true);
            SetDefaultSaveName();
            RebuildSlots();
            MenuUiUtility.SelectButton(_newSaveButton != null ? _newSaveButton : _backButton);
        }

        public void Close()
        {
            Close(_returnToPause);
        }

        public void Close(bool reopenPause)
        {
            if (!IsOpen)
                return;

            SetOpen(false);
            MenuUiUtility.SetBackgroundUiRaycasts(transform, true);
            UIStateOwnership.SetUiCapture(false);

            if (reopenPause)
                PauseMenuSystem.ResolveInstance()?.Show();
        }

        private void SetDefaultSaveName()
        {
            if (_saveNameInput == null)
                return;

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
                StartCoroutine(RefreshAfterScreenshotCapture());
        }

        private IEnumerator RefreshAfterScreenshotCapture()
        {
            yield return new WaitForEndOfFrame();
            yield return null;

            if (this != null && isActiveAndEnabled)
                RebuildSlots();
        }

        private void RebuildSlots()
        {
            ClearEntries();

            SaveManager manager = ResolveSaveManager();
            if (_contentParent == null || manager == null)
                return;

            bool emptyManualSlotShown = false;
            for (int slotIndex = 0; slotIndex < SaveManager.MaxSlots; slotIndex++)
            {
                bool hasSave = manager.SlotExists(slotIndex);
                bool shouldShowEmptyManualSlot = slotIndex != SaveManager.AutoSaveSlot && !emptyManualSlotShown;

                if (hasSave)
                {
                    CreateSlotEntry(manager, slotIndex);
                }
                else if (shouldShowEmptyManualSlot)
                {
                    CreateSlotEntry(manager, slotIndex);
                    emptyManualSlotShown = true;
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

            TMP_Text slotNameText = MenuUiUtility.FindTextByNames(entry.transform, "SlotName");
            TMP_Text dateTimeText = MenuUiUtility.FindTextByNames(entry.transform, "DateTime");
            TMP_Text playtimeText = MenuUiUtility.FindTextByNames(entry.transform, "Playtime");
            RawImage thumbnailImage = MenuUiUtility.FindDeepComponent<RawImage>(entry.transform, "Thumbnail");

            if (slotNameText != null)
                slotNameText.text = hasSave
                    ? string.IsNullOrWhiteSpace(metadata.SaveName) ? BuildSlotName(slotIndex) : metadata.SaveName.Trim()
                    : $"{BuildSlotName(slotIndex)} - Empty";

            if (dateTimeText != null)
                dateTimeText.text = hasSave
                    ? manager.GetSlotInGameDate(slotIndex)
                    : "Empty";

            if (playtimeText != null)
            {
                string realDate = hasSave ? metadata.Timestamp : string.Empty;
                string playtime = hasSave ? SaveManager.FormatPlaytime(metadata.PlaytimeSeconds) : "0m";
                playtimeText.text = hasSave
                    ? $"{realDate} | {playtime}"
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

            Button selectButton = MenuUiUtility.FindButtonByNames(entry.transform, "SelectButton", "Save", "Save...")
                ?? entry.GetComponent<Button>()
                ?? entry.GetComponentInChildren<Button>(true);
            if (selectButton != null)
            {
                selectButton.onClick.RemoveAllListeners();
                selectButton.onClick.AddListener(() => HandleSlotSelected(slotIndex, hasSave));
                selectButton.interactable = true;
                SetButtonLabel(selectButton, hasSave ? "Overwrite" : "Save");
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

        private void HandleSlotSelected(int slotIndex, bool hasSave)
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
            if (manager == null)
                return;

            if (!manager.DeleteSave(slotIndex))
                return;

            RebuildSlots();
        }

        private GameObject CreateEntry(int slotIndex)
        {
            if (_contentParent == null)
                return null;

            GameObject source = _slotEntryTemplate != null ? _slotEntryTemplate : null;
            if (source == null)
            {
                Debug.LogWarning("[SaveMenuSystem] No authored save slot template was found. Assign a template entry under the content root.", this);
                return null;
            }

            GameObject clone = UnityEngine.Object.Instantiate(source, _contentParent);
            clone.name = $"SaveSlot_{slotIndex:00}";
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
            _newSaveButton ??= MenuUiUtility.FindButtonByNames(transform, "NewSave", "newSaveButton", "NewSaveButton", "Save");
            _saveNameInput ??= MenuUiUtility.FindDeepComponent<TMP_InputField>(transform, "SaveNameInput")
                ?? GetComponentInChildren<TMP_InputField>(true);

            if (_slotEntryTemplate == null && _contentParent != null && _contentParent.childCount > 0)
            {
                _slotEntryTemplate = _contentParent.GetChild(0).gameObject;
                _slotEntryTemplate.SetActive(false);
            }

            MenuUiUtility.WireButton(_backButton, Close);
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
            if (manager == null)
                return -1;

            for (int i = SaveManager.AutoSaveSlot + 1; i < SaveManager.MaxSlots; i++)
            {
                if (!manager.SlotExists(i))
                    return i;
            }

            return -1;
        }

        private static int FindOldestManualSlot(SaveManager manager)
        {
            if (manager == null)
                return -1;

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
        {
            return slotIndex == SaveManager.AutoSaveSlot ? "Autosave" : $"Slot {slotIndex}";
        }

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
