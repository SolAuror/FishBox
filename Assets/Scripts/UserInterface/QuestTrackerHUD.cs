using TMPro;
using UnityEngine;
using UnityEngine.UI;
using Sol.Quests;

namespace Sol.HUD
{
    /// <summary>
    /// Lightweight HUD tracker for the currently tracked quest.
    /// </summary>
    public class QuestTrackerHUD : MonoBehaviour
    {
#region Inspector Settings
        [SerializeField] private RectTransform _root;
        [SerializeField] private TMP_Text _titleText;
        [SerializeField] private TMP_Text _objectiveText;
        [SerializeField] private TMP_Text _timerText;
#endregion

        private QuestManager _manager;
        private CanvasGroup _canvasGroup;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void EnsureInstance()
        {
            if (Object.FindFirstObjectByType<QuestTrackerHUD>() != null)
                return;

            Canvas[] canvases = Object.FindObjectsByType<Canvas>(FindObjectsInactive.Include, FindObjectsSortMode.None);
            if (canvases == null || canvases.Length == 0)
                return;

            Canvas target = null;
            for (int i = 0; i < canvases.Length; i++)
            {
                if (canvases[i] != null && canvases[i].name == "MasterCanvas")
                {
                    target = canvases[i];
                    break;
                }
            }

            if (target == null)
                target = canvases[0];

            GameObject panel = new("QuestTrackerHUD", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image), typeof(QuestTrackerHUD));
            panel.transform.SetParent(target.transform, false);
            RectTransform panelRect = panel.GetComponent<RectTransform>();
            panelRect.anchorMin = new Vector2(0f, 1f);
            panelRect.anchorMax = new Vector2(0f, 1f);
            panelRect.pivot = new Vector2(0f, 1f);
            panelRect.anchoredPosition = new Vector2(24f, -24f);
            panelRect.sizeDelta = new Vector2(380f, 120f);

            Image bg = panel.GetComponent<Image>();
            bg.color = new Color(0f, 0f, 0f, 0.6f);
            bg.raycastTarget = false;

            CreateText(panel.transform, "Title", new Vector2(12f, -12f), 24f, 18f);
            CreateText(panel.transform, "Objective", new Vector2(12f, -44f), 24f, 18f);
            CreateText(panel.transform, "Timer", new Vector2(12f, -76f), 24f, 18f);
        }

        private static void CreateText(Transform parent, string name, Vector2 anchoredPosition, float left, float size)
        {
            GameObject go = new(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(TextMeshProUGUI));
            go.transform.SetParent(parent, false);
            RectTransform rect = go.GetComponent<RectTransform>();
            rect.anchorMin = new Vector2(0f, 1f);
            rect.anchorMax = new Vector2(1f, 1f);
            rect.pivot = new Vector2(0f, 1f);
            rect.anchoredPosition = anchoredPosition;
            rect.sizeDelta = new Vector2(-left, size + 10f);

            TextMeshProUGUI text = go.GetComponent<TextMeshProUGUI>();
            text.text = string.Empty;
            if (TMP_Settings.defaultFontAsset != null)
                text.font = TMP_Settings.defaultFontAsset;
            text.fontSize = size;
            text.raycastTarget = false;
            text.alignment = TextAlignmentOptions.Left;
            text.color = new Color(0.95f, 0.95f, 0.95f, 1f);
        }

        private void Awake()
        {
            AutoWire();
            _canvasGroup = (_root != null ? _root : (RectTransform)transform).GetComponent<CanvasGroup>();
            if (_canvasGroup == null)
                _canvasGroup = (_root != null ? _root : (RectTransform)transform).gameObject.AddComponent<CanvasGroup>();
            SetVisible(false);
        }

        private void OnEnable()
        {
            TryBindManager();
            Refresh();
        }

        private void OnDisable()
        {
            UnbindManager();
        }

        private void Update()
        {
            if (_manager == null)
                TryBindManager();

            if (_manager != null)
                Refresh();
        }

        private void TryBindManager()
        {
            if (_manager != null)
                return;

            _manager = QuestManager.Instance;
            if (_manager == null)
                return;

            _manager.OnQuestAccepted += HandleQuestEvent;
            _manager.OnQuestUpdated += HandleQuestEvent;
            _manager.OnObjectiveAdvanced += HandleQuestEvent;
            _manager.OnQuestCompleted += HandleQuestEvent;
            _manager.OnQuestFailed += HandleQuestEvent;
        }

        private void UnbindManager()
        {
            if (_manager == null)
                return;

            _manager.OnQuestAccepted -= HandleQuestEvent;
            _manager.OnQuestUpdated -= HandleQuestEvent;
            _manager.OnObjectiveAdvanced -= HandleQuestEvent;
            _manager.OnQuestCompleted -= HandleQuestEvent;
            _manager.OnQuestFailed -= HandleQuestEvent;
            _manager = null;
        }

        private void HandleQuestEvent(QuestSaveData _) => Refresh();

        private void Refresh()
        {
            if (_manager == null)
            {
                SetVisible(false);
                return;
            }

            QuestSaveData tracked = _manager.GetTrackedQuest();
            if (tracked == null)
            {
                SetVisible(false);
                return;
            }

            QuestDefinition def = QuestRegistry.Get()?.Find(tracked.QuestId);
            if (def == null)
            {
                SetVisible(false);
                return;
            }

            SetVisible(true);
            if (_titleText != null)
                _titleText.text = def.Title;

            if (_objectiveText != null)
                _objectiveText.text = BuildObjectiveLine(tracked, def);

            if (_timerText != null)
            {
                if (def.IsTimed && tracked.State == QuestState.Active)
                {
                    int seconds = Mathf.CeilToInt(Mathf.Max(0f, tracked.RemainingSeconds));
                    _timerText.text = $"Time: {seconds / 60:00}:{seconds % 60:00}";
                }
                else
                {
                    _timerText.text = string.Empty;
                }
            }
        }

        private static string BuildObjectiveLine(QuestSaveData data, QuestDefinition def)
        {
            if (data.State == QuestState.ReadyToTurnIn)
                return "Return to turn in.";

            if (data.CurrentObjectiveIndex < 0 || data.CurrentObjectiveIndex >= def.Objectives.Count)
                return "Objective complete.";

            QuestObjective obj = def.Objectives[data.CurrentObjectiveIndex];
            string label = string.IsNullOrWhiteSpace(obj.DisplayText)
                ? obj.Type.ToString()
                : obj.DisplayText;

            return obj.Type switch
            {
                QuestObjectiveType.CatchTotalValue => $"{label}: {Mathf.Max(0, data.CurrentObjectiveProgress)}/{Mathf.Max(0, obj.GoldAmount)}",
                QuestObjectiveType.CatchByPrefix => $"{label}: {BuildPrefixProgress(data, obj)}",
                QuestObjectiveType.EquipItem or QuestObjectiveType.TalkToNpc => $"{label}: {(data.CurrentObjectiveProgress > 0 ? "Done" : "Pending")}",
                QuestObjectiveType.DeliverItem when obj.GetRequiredGoldPaymentAmount() > 0 => $"{label}: {Mathf.Max(0, data.CurrentObjectiveProgress)}/{obj.GetRequiredGoldPaymentAmount()}",
                _ => $"{label}: {Mathf.Max(0, data.CurrentObjectiveProgress)}/{Mathf.Max(1, obj.Count)}",
            };
        }

        private static string BuildPrefixProgress(QuestSaveData data, QuestObjective obj)
        {
            if (obj.PrefixRequirements == null || obj.PrefixRequirements.Count == 0)
                return "Done";

            System.Text.StringBuilder sb = new();
            for (int i = 0; i < obj.PrefixRequirements.Count; i++)
            {
                PrefixRequirement req = obj.PrefixRequirements[i];
                if (i > 0)
                    sb.Append(" + ");

                int have = i < data.PrefixProgress.Count ? data.PrefixProgress[i] : 0;
                int need = Mathf.Max(1, req.Count);
                sb.Append($"{req.Prefix} {have}/{need}");
            }

            return sb.ToString();
        }

        private void AutoWire()
        {
            _root ??= transform as RectTransform;
            _titleText ??= MenuUiUtility.FindTextByNames(transform, "Title");
            _objectiveText ??= MenuUiUtility.FindTextByNames(transform, "Objective");
            _timerText ??= MenuUiUtility.FindTextByNames(transform, "Timer");
        }

        private void SetVisible(bool visible)
        {
            if (_canvasGroup == null)
                return;

            _canvasGroup.alpha = visible ? 1f : 0f;
            _canvasGroup.interactable = false;
            _canvasGroup.blocksRaycasts = false;
        }
    }
}
