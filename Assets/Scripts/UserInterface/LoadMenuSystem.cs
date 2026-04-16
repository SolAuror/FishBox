using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using Sol.SaveLoad;

namespace Sol.HUD
{
    public sealed class LoadMenuSystem : MenuSystemBase<LoadMenuSystem>
    {
        [SerializeField] private ScrollRect _scrollRect;
        [SerializeField] private Transform _contentParent;
        [SerializeField] private Button _backButton;

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
            UIStateOwnership.CloseConflictingUi(nameof(LoadMenuSystem));
            SetOpen(true);
            MenuUiUtility.BringToFront(transform.parent);
            MenuUiUtility.BringToFront(transform);
            MenuUiUtility.SetBackgroundUiRaycasts(transform, false);
            UIStateOwnership.SetUiCapture(true);
            RebuildSlots();
            MenuUiUtility.SelectButton(_backButton);
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

        private void RebuildSlots()
        {
            ClearEntries();

            SaveManager manager = ResolveSaveManager();
            if (_contentParent == null || manager == null)
                return;

            for (int slotIndex = 0; slotIndex < SaveManager.MaxSlots; slotIndex++)
            {
                if (manager.SlotExists(slotIndex))
                    CreateSlotEntry(manager, slotIndex);
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
            if (metadata == null)
                return;

            TMP_Text slotNameText = MenuUiUtility.FindTextByNames(entry.transform, "SlotName");
            TMP_Text dateTimeText = MenuUiUtility.FindTextByNames(entry.transform, "DateTime");
            TMP_Text playtimeText = MenuUiUtility.FindTextByNames(entry.transform, "Playtime");
            RawImage thumbnailImage = MenuUiUtility.FindDeepComponent<RawImage>(entry.transform, "Thumbnail");

            if (slotNameText != null)
                slotNameText.text = string.IsNullOrWhiteSpace(metadata.SaveName) ? BuildSlotName(slotIndex) : metadata.SaveName.Trim();

            if (dateTimeText != null)
                dateTimeText.text = $"Real: {metadata.Timestamp}";

            if (playtimeText != null)
                playtimeText.text = $"World: {manager.GetSlotInGameDate(slotIndex)}\nPlaytime: {SaveManager.FormatPlaytime(metadata.PlaytimeSeconds)}";

            if (thumbnailImage != null)
            {
                Texture2D screenshot = manager.LoadScreenshot(slotIndex);
                thumbnailImage.texture = screenshot;
                thumbnailImage.color = screenshot != null ? Color.white : new Color(0.2f, 0.2f, 0.2f, 0.5f);
                if (screenshot != null)
                    _loadedThumbnails.Add(screenshot);
            }

            Button loadButton = MenuUiUtility.FindButtonByNames(entry.transform, "SelectButton", "Load", "LoadButton")
                ?? entry.GetComponent<Button>()
                ?? entry.GetComponentInChildren<Button>(true);
            if (loadButton != null)
            {
                loadButton.onClick.RemoveAllListeners();
                loadButton.onClick.AddListener(() => RequestLoadSlot(slotIndex));
                loadButton.interactable = true;
                SetButtonLabel(loadButton, "Load");
            }

            Button deleteButton = MenuUiUtility.FindButtonByNames(entry.transform, "Delete", "Delete...", "DeleteButton");
            if (deleteButton != null)
            {
                deleteButton.onClick.RemoveAllListeners();
                deleteButton.gameObject.SetActive(true);
                deleteButton.onClick.AddListener(() => RequestDeleteSlot(slotIndex));
            }

            entry.SetActive(true);
            _spawnedEntries.Add(entry);
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
                    cancelLabel: "Cancel");
                return;
            }

            LoadSlot(slotIndex);
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
                    cancelLabel: "Cancel");
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
                Debug.LogWarning("[LoadMenuSystem] No authored load slot template was found. Assign a template entry under the content root.", this);
                return null;
            }

            GameObject clone = UnityEngine.Object.Instantiate(source, _contentParent);
            clone.name = $"LoadSlot_{slotIndex:00}";
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

            if (_slotEntryTemplate == null && _contentParent != null && _contentParent.childCount > 0)
            {
                _slotEntryTemplate = _contentParent.GetChild(0).gameObject;
                _slotEntryTemplate.SetActive(false);
            }

            MenuUiUtility.WireButton(_backButton, Close);
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
