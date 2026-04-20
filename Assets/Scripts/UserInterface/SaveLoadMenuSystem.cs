using System;
using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using Sol.SaveLoad;

namespace Sol.HUD
{
    public enum SaveLoadMode { Save, Load } // Mode for the SaveLoadMenuSystem, determining whether it is being used to save or load games. This affects which slots are displayed and what actions are available.

    public sealed class SaveLoadMenuSystem : MenuSystemBase<SaveLoadMenuSystem> 
    {
        [SerializeField] private TextMeshProUGUI _TitleText;
        [SerializeField] private TextMeshProUGUI _hintText1;
        [SerializeField] private TextMeshProUGUI _hintText2;
        [SerializeField] private ScrollRect _scrollRect;
        [SerializeField] private Transform _contentParent;
        [SerializeField] private Button _backButton;
        [SerializeField] private Button _newSaveButton;
        [SerializeField] private TMP_InputField _saveNameInput;
        [SerializeField] private GameObject _slotEntryTemplate;

        public bool ReturnsToPause => _returnToPause;
        public SaveLoadMode Mode => _mode;

        private readonly List<GameObject> _spawnedEntries = new();

        private readonly List<Texture2D> _loadedThumbnails = new();

        private bool _returnToPause;
        private SaveLoadMode _mode;

        protected override void Awake()
        {
            base.Awake();
            if (_instance != this)
                return;

            CacheReferences();
            SetOpen(false);
        }

        protected override void PostResolve() => CacheReferences(); // Ensure references are cached after resolving instance, in case of dynamic UI setup.

        public void Open(SaveLoadMode mode, bool returnToPause = false) // Opens the save/load menu in the specified mode (save or load). If returnToPause is true, closing the menu will return to the pause menu instead of resuming the game. This is used when accessing the save/load menu from within the pause menu.
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

            RebuildSlots();
            MenuUiUtility.SelectButton(isSave && _newSaveButton != null ? _newSaveButton : _backButton);
        }

        public void Close() => Close(false); // Closes the save/load menu. If the menu was opened with returnToPause = true, this will return to the pause menu instead of resuming the game. Otherwise, it will simply close the menu and resume the game if it was paused.

        public void Close(bool reopenPause) // Closes the save/load menu. If reopenPause is true, it will open the pause menu after closing this menu. This is used when the save/load menu is accessed from outside the pause menu but should return to it afterward.
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

        private void SetDefaultSaveName()// Sets the default save name in the input field to something like "Save - 12 Mar 14:30" based on the current date and time. This is called when opening the menu in save mode to provide a default name for new saves.
        {
            if (_saveNameInput != null)
                _saveNameInput.text = $"Save - {DateTime.Now:dd MMM HH:mm}";
        }

        private void CreateNewSave() // Handles the logic for creating a new save when the "New Save" button is clicked. It checks for an empty slot to save in, and if all slots are full, it prompts the user to overwrite the oldest manual save slot. If the user confirms, it saves to that slot. If there are no manual slots to overwrite, it saves to the autosave slot.
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

        private void SaveToSlot(int slotIndex) // Saves the game to the specified slot index. After saving, it captures a screenshot for that slot to use as a thumbnail in the UI. This is called when the user confirms saving to a slot, either through the "New Save" button or by selecting an existing slot to overwrite.
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

        private IEnumerator CaptureScreenshotAndRefresh(int slotIndex) // Coroutine that captures a screenshot for the specified slot index and then refreshes the UI to show the updated thumbnail. It temporarily hides the UI to ensure the screenshot captures the game view without the menu, and then restores the UI after capturing.
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
                RebuildSlots();
        }

        private void LoadSlot(int slotIndex) // Loads the game from the specified slot index. This is called when the user selects a slot to load and confirms the action. After loading, it closes the save/load menu and, if it was accessed from the pause menu, it will return to the pause menu instead of resuming the game immediately.
        {
            SaveManager manager = ResolveSaveManager();
            if (manager == null || !manager.LoadGame(slotIndex))
                return;

            PauseMenuSystem.ResolveInstance(false)?.Close();
            Close(reopenPause: false);
            Time.timeScale = 1f;
        }

        private void RebuildSlots() // Rebuilds the list of save/load slots displayed in the menu based on the current state of the save manager. It clears any existing entries and creates new ones for each valid save slot. This is called when opening the menu and after saving or deleting a slot to ensure the UI reflects the current saves.
        {
            ClearEntries();

            SaveManager manager = ResolveSaveManager();
            if (_contentParent == null || manager == null)
                return;

            if (_mode == SaveLoadMode.Save)
            {
                int firstEmptyManualSlot = -1;

                for (int i = 0; i < SaveManager.MaxSlots; i++)
                {
                    bool hasSave = manager.SlotExists(i);

                    if (hasSave)
                    {
                        CreateSlotEntry(manager, i);
                    }
                    else if (i != SaveManager.AutoSaveSlot && firstEmptyManualSlot < 0)
                    {
                        firstEmptyManualSlot = i;
                    }
                }

                if (firstEmptyManualSlot >= 0)
                    CreateSlotEntry(manager, firstEmptyManualSlot);
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

        private void CreateSlotEntry(SaveManager manager, int slotIndex) // Creates a UI entry for the specified save slot index, populating it with the save metadata and thumbnail. It also sets up the buttons for loading, saving, or deleting the slot based on whether it currently has a save and the current mode of the menu (save or load). This is called for each slot that should be displayed in the menu when rebuilding the slots.
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
                if (screenshot != null)
                    _loadedThumbnails.Add(screenshot);
                thumbnailImage.texture = screenshot;
                thumbnailImage.color = screenshot != null 
                                     ? Color.white 
                                     : new Color(0.2f, 0.2f, 0.2f, 0.5f);
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

        private void HandleSaveSlotSelected(int slotIndex, bool hasSave) // Handles the logic when a save slot is selected in save mode. If the slot is empty, it saves to that slot immediately. If the slot already has a save, it prompts the user to confirm overwriting that slot before saving.
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

        private void RequestLoadSlot(int slotIndex) // Handles the logic when a save slot is selected in load mode. It prompts the user to confirm loading that slot, warning them that unsaved progress will be lost. If the user confirms, it loads that slot.
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

        private void RequestDeleteSlot(int slotIndex) // Handles the logic when the delete button for a save slot is clicked. It prompts the user to confirm deleting that slot, warning them that the save data will be permanently deleted. If the user confirms, it deletes that slot.
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

        private void DeleteSlot(int slotIndex) // Deletes the save data for the specified slot index. After deleting, it refreshes the UI to reflect the change. This is called when the user confirms deleting a save slot.
        {
            SaveManager manager = ResolveSaveManager();
            if (manager == null || !manager.DeleteSave(slotIndex))
                return;

            RebuildSlots();
        }

        private GameObject CreateEntry(int slotIndex) // Creates a new UI entry for the specified slot index by instantiating the slot entry template. The new entry is parented under the content parent and is initially inactive. This is called when rebuilding the slots to create an entry for each save slot that should be displayed.
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

        private void CacheReferences() // Caches references to UI components such as the scroll rect, content parent, buttons, and input field. This is called in Awake and PostResolve to ensure that the references are set up correctly, especially if the UI is dynamically constructed or if the instance is resolved after Awake.
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

        private void ClearEntries() // Clears all currently spawned slot entries and loaded thumbnails from the UI. This is called before rebuilding the slots to remove any existing entries and free up resources used by the thumbnails.
        {
            for (int i = 0; i < _loadedThumbnails.Count; i++)
            {
                if (_loadedThumbnails[i] != null)
                    Destroy(_loadedThumbnails[i]);
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

        private static int FindPreferredSaveSlot(SaveManager manager) // Finds the preferred save slot index to save a new game. It first looks for an empty manual slot (not the autosave slot) and returns that if found. If all manual slots are full, it returns -1 to indicate that there are no empty slots available. This is used when creating a new save to determine where to save it.
        {
            for (int i = SaveManager.AutoSaveSlot + 1; i < SaveManager.MaxSlots; i++)
            {
                if (!manager.SlotExists(i))
                    return i;
            }
            return -1;
        }

        private static int FindOldestManualSlot(SaveManager manager) // Finds the index of the oldest manual save slot (not the autosave slot) that can be overwritten. It iterates through all manual slots and checks their timestamps to find the one with the oldest save. If it finds at least one manual slot, it returns the index of the oldest one. If there are no manual slots (all are empty or only the autosave slot exists), it returns -1 to indicate that there are no slots available for overwriting.
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

        private static SaveManager ResolveSaveManager() // Resolves and returns the instance of the SaveManager. It first checks if the instance is already set, then tries to find an existing SaveManager in the scene. If it still can't find one, it creates a new GameObject and adds a SaveManager component to it. This ensures that there is always a SaveManager available when needed.
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

        private static string BuildSlotName(int slotIndex) // Builds a display name for the specified slot index. For the autosave slot, it returns "Autosave". For regular slots, it returns "Slot X" where X is the slot index. This is used in the UI when a save slot does not have a custom name set by the user.
            => slotIndex == SaveManager.AutoSaveSlot ? "Autosave" : $"Slot {slotIndex}";

        private static void SetButtonLabel(Button button, string label) // Sets the text of the specified button to the given label. It looks for a TextMeshProUGUI component in the button's children to set the text. This is used to update the button labels dynamically based on the context (e.g., changing "Save" to "Overwrite" if the slot already has a save).
        {
            if (button == null || string.IsNullOrWhiteSpace(label))
                return;

            TMP_Text text = button.GetComponentInChildren<TMP_Text>(true);
            if (text != null)
                text.text = label;
        }
    }
}
