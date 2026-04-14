using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using Sol.SaveLoad;

namespace Sol.HUD
{
    public sealed class SaveMenuSystem : MonoBehaviour
    {
        [SerializeField] private ScrollRect _scrollRect;
        [SerializeField] private Transform _contentParent;
        [SerializeField] private Button _backButton;
        [SerializeField] private Button _newSaveButton;
        [SerializeField] private TMP_InputField _saveNameInput;

        public static SaveMenuSystem Instance { get; private set; }
        public bool IsOpen => gameObject.activeSelf;

        private readonly List<GameObject> _spawnedEntries = new();
        private CanvasGroup _canvasGroup;
        private bool _returnToPause;

        private void Awake()
        {
            if (Instance != null && Instance != this) { Destroy(gameObject); return; }
            Instance = this;
            _canvasGroup = MenuUiUtility.EnsureCanvasGroup(gameObject);
            AutoWire();
            SetOpen(false);
        }

        private void OnDestroy()
        {
            if (Instance == this)
                Instance = null;
        }

        public static SaveMenuSystem ResolveInstance(bool activateIfInactive = true)
        {
            SaveMenuSystem resolved = Instance ?? UIStateOwnership.Resolve<SaveMenuSystem>(activateIfInactive);
            if (resolved != null)
                resolved.AutoWire();
            return resolved;
        }

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
                PauseMenuSystem.ResolveInstance()?.Open();
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

            for (int slotIndex = 0; slotIndex < SaveManager.MaxSlots; slotIndex++)
                CreateSlotEntry(manager, slotIndex);

            LayoutRebuilder.ForceRebuildLayoutImmediate(_contentParent as RectTransform);
        }

        private void CreateSlotEntry(SaveManager manager, int slotIndex)
        {
            Button row = CreateEntryButton(slotIndex);
            if (row == null)
                return;

            SaveMetadata metadata = manager.GetSlotMetadata(slotIndex);
            TMP_Text label = row.GetComponentInChildren<TMP_Text>(true);
            if (label != null)
                label.text = BuildSaveSlotLabel(slotIndex, metadata);

            row.onClick.RemoveAllListeners();
            row.onClick.AddListener(() => SaveToSlot(slotIndex));
            row.interactable = true;
            _spawnedEntries.Add(row.gameObject);
        }

        private Button CreateEntryButton(int slotIndex)
        {
            if (_contentParent == null)
                return null;

            Button template = _newSaveButton ?? _backButton;
            Button row;
            if (template != null)
            {
                row = Object.Instantiate(template, _contentParent);
                row.gameObject.SetActive(true);
            }
            else
            {
                GameObject rowObject = new GameObject($"SaveSlot_{slotIndex:00}", typeof(RectTransform), typeof(Image), typeof(Button));
                rowObject.transform.SetParent(_contentParent, false);
                row = rowObject.GetComponent<Button>();

                GameObject labelObject = new GameObject("Label", typeof(RectTransform), typeof(TextMeshProUGUI));
                labelObject.transform.SetParent(row.transform, false);
                RectTransform labelRect = labelObject.GetComponent<RectTransform>();
                labelRect.anchorMin = Vector2.zero;
                labelRect.anchorMax = Vector2.one;
                labelRect.offsetMin = new Vector2(24f, 8f);
                labelRect.offsetMax = new Vector2(-24f, -8f);
                TextMeshProUGUI text = labelObject.GetComponent<TextMeshProUGUI>();
                text.fontSize = 24f;
                text.alignment = TextAlignmentOptions.Left;
            }

            row.name = $"SaveSlot_{slotIndex:00}";
            return row;
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

            MenuUiUtility.WireButton(_backButton, Close);
            MenuUiUtility.WireButton(_newSaveButton, CreateNewSave);
        }

        private void SetOpen(bool open)
        {
            MenuUiUtility.SetVisible(gameObject, open);
            _canvasGroup ??= MenuUiUtility.EnsureCanvasGroup(gameObject);
            MenuUiUtility.SetCanvasGroupVisible(_canvasGroup, open);
        }

        private void ClearEntries()
        {
            for (int i = _spawnedEntries.Count - 1; i >= 0; i--)
            {
                if (_spawnedEntries[i] != null)
                    Object.Destroy(_spawnedEntries[i]);
            }

            _spawnedEntries.Clear();
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
