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

    public sealed partial class SaveLoadMenuSystem : MenuSystemBase<SaveLoadMenuSystem>
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

            ConfirmationPromptSystem prompt = ConfirmationPromptSystem.ResolveInstance(activateIfInactive: true);
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
            ConfirmationPromptSystem prompt = ConfirmationPromptSystem.ResolveInstance(activateIfInactive: true);
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
            ConfirmationPromptSystem prompt = ConfirmationPromptSystem.ResolveInstance(activateIfInactive: true);
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

            ConfirmationPromptSystem prompt = ConfirmationPromptSystem.ResolveInstance(activateIfInactive: true);
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
