using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using Sol.SaveLoad;

namespace Sol.HUD
{
    public sealed partial class SaveLoadMenuSystem
    {
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
    }
}
