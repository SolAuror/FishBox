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
        private GameObject _slotEntryTemplate;
        private bool _returnToPause;

        protected override void Awake()
        {
            base.Awake();
            if (_instance != this) return;
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

        private void CreateNewSave()
        {
            SaveToSlot(FindPreferredSaveSlot());
        }

        private void SaveToSlot(int slotIndex)
        {
            SaveManager manager = ResolveSaveManager();
            if (manager == null)
                return;

            string saveName = _saveNameInput != null ? _saveNameInput.text : null;
            manager.SaveGame(slotIndex, saveName);
            RebuildSlots();
        }

        private void RebuildSlots()
        {
            ClearEntries();

            SaveManager manager = ResolveSaveManager();
            if (_contentParent == null || manager == null)
                return;

            bool emptySlotShown = false;
            for (int slotIndex = 0; slotIndex < SaveManager.MaxSlots; slotIndex++)
            {
                if (manager.SlotExists(slotIndex))
                {
                    CreateSlotEntry(manager, slotIndex);
                }
                else if (!emptySlotShown)
                {
                    CreateSlotEntry(manager, slotIndex);
                    emptySlotShown = true;
                }
            }

            LayoutRebuilder.ForceRebuildLayoutImmediate(_contentParent as RectTransform);
        }

        private void CreateSlotEntry(SaveManager manager, int slotIndex)
        {
            GameObject entry = CreateEntry(slotIndex);
            if (entry == null)
                return;

            SaveMetadata metadata = manager.GetSlotMetadata(slotIndex);
            TMP_Text label = entry.GetComponentInChildren<TMP_Text>(true);
            if (label != null)
                label.text = BuildSaveSlotLabel(slotIndex, metadata);

            Button saveBtn = MenuUiUtility.FindButtonByNames(entry.transform, "Save", "Save...")
                ?? entry.GetComponent<Button>()
                ?? entry.GetComponentInChildren<Button>(true);
            if (saveBtn != null)
            {
                saveBtn.onClick.RemoveAllListeners();
                saveBtn.onClick.AddListener(() => SaveToSlot(slotIndex));
                saveBtn.interactable = true;
            }

            Button deleteBtn = MenuUiUtility.FindButtonByNames(entry.transform, "Delete", "Delete...", "DeleteButton");
            if (deleteBtn != null)
            {
                deleteBtn.onClick.RemoveAllListeners();
                deleteBtn.onClick.AddListener(() => DeleteSlot(slotIndex));
            }

            entry.SetActive(true);
            _spawnedEntries.Add(entry);
        }

        private void DeleteSlot(int slotIndex)
        {
            SaveManager manager = ResolveSaveManager();
            if (manager == null)
                return;

            manager.DeleteSave(slotIndex);
            RebuildSlots();
        }

        private GameObject CreateEntry(int slotIndex)
        {
            if (_contentParent == null)
                return null;

            GameObject source = _slotEntryTemplate != null ? _slotEntryTemplate
                : _newSaveButton != null ? _newSaveButton.gameObject
                : _backButton != null ? _backButton.gameObject
                : null;

            if (source != null)
            {
                GameObject clone = Object.Instantiate(source, _contentParent);
                clone.name = $"SaveSlot_{slotIndex:00}";
                clone.SetActive(false);
                return clone;
            }

            Debug.LogWarning("[SaveMenuSystem] No authored save slot template was found. Assign a prefab/template entry under the content root.", this);
            return null;
        }

        private void AutoWire()
        {
            _scrollRect ??= GetComponentInChildren<ScrollRect>(true);
            _contentParent ??= MenuUiUtility.FindDeep(transform, "Content")
                ?? MenuUiUtility.FindDeep(transform, "ContentArea")
                ?? (_scrollRect != null ? _scrollRect.content : null);
            _backButton ??= MenuUiUtility.FindButtonByNames(transform, "Back", "CloseButton");
            _newSaveButton ??= MenuUiUtility.FindButtonByNames(transform, "newSaveButton", "NewSaveButton", "Save");
            _saveNameInput ??= GetComponentInChildren<TMP_InputField>(true);

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
            for (int i = _spawnedEntries.Count - 1; i >= 0; i--)
            {
                if (_spawnedEntries[i] != null)
                    Object.Destroy(_spawnedEntries[i]);
            }

            _spawnedEntries.Clear();

            if (_contentParent != null)
            {
                for (int i = _contentParent.childCount - 1; i >= 0; i--)
                {
                    Transform child = _contentParent.GetChild(i);
                    if (child != null && child.gameObject != _slotEntryTemplate)
                        Object.Destroy(child.gameObject);
                }
            }
        }

        private int FindPreferredSaveSlot()
        {
            SaveManager manager = ResolveSaveManager();
            if (manager == null)
                return 1;

            for (int i = 1; i < SaveManager.MaxSlots; i++)
            {
                if (!manager.SlotExists(i))
                    return i;
            }

            return 1;
        }

        private static SaveManager ResolveSaveManager()
        {
            if (SaveManager.Instance != null)
                return SaveManager.Instance;

            SaveManager manager = Object.FindFirstObjectByType<SaveManager>();
            if (manager != null)
                return manager;

            GameObject host = new GameObject("SaveManager");
            Object.DontDestroyOnLoad(host);
            return host.AddComponent<SaveManager>();
        }

        private static string BuildSaveSlotLabel(int slotIndex, SaveMetadata metadata)
        {
            string slotName = $"Slot {slotIndex}";
            if (metadata == null)
                return $"{slotName}\nEMPTY";

            string name = string.IsNullOrWhiteSpace(metadata.SaveName) ? slotName : metadata.SaveName.Trim();
            string timestamp = string.IsNullOrWhiteSpace(metadata.Timestamp) ? "Unknown Time" : metadata.Timestamp.Trim();
            return $"{slotName}  {name}\n{timestamp}";
        }
    }
}
