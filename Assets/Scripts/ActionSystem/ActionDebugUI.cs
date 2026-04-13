using UnityEngine;
using TMPro;
using Sol.Actions;

namespace Sol.HUD
{
    /// <summary>
    /// Read-only debug overlay for ActionSystem + State Systems.
    /// Attach to a UI Canvas with a TextMeshProUGUI target.
    /// Reads snapshots every frame — no logic, only display.
    /// </summary>
    public class ActionDebugUI : MonoBehaviour
    {
        [Header("Target")]
        [Tooltip("The actor whose action/state data to display.")]
        [SerializeField] private GameObject _actor;

        [Header("UI")]
        [SerializeField] private TextMeshProUGUI _debugText;

        [Header("Options")]
        [Tooltip("Maximum history entries to show.")]
        [SerializeField] private int _maxHistory = 8;

        private readonly System.Text.StringBuilder _sb = new();
        private bool _subscribed;
        private string _lastActionEvent;

        private void OnEnable()
        {
            TrySubscribe();
        }

        private void OnDisable()
        {
            Unsubscribe();
        }

        private void Update()
        {
            TrySubscribe();

            if (_debugText == null || _actor == null) return;
            if (ActionSystem.Instance == null) { _debugText.text = string.Empty; return; }

            var snap = ActionSystem.Instance.GetDebugSnapshot(_actor);

            _sb.Clear();

            // --- Current Action ---
            _sb.Append("<b>Action:</b> ");
            if (snap.CurrentAction != null)
                _sb.Append(snap.CurrentAction).Append(" [").Append(snap.CurrentPriority).Append(']');
            else
                _sb.Append("<i>none</i>");
            _sb.AppendLine();

            // --- Queue ---
            _sb.Append("<b>Queue:</b> ");
            if (snap.QueuedActions != null && snap.QueuedActions.Length > 0)
            {
                for (int i = 0; i < snap.QueuedActions.Length; i++)
                {
                    if (i > 0) _sb.Append(" ? ");
                    _sb.Append(snap.QueuedActions[i]);
                }
            }
            else
            {
                _sb.Append("<i>empty</i>");
            }
            _sb.AppendLine();

            // --- Active States ---
            _sb.Append("<b>States:</b> ");
            if (snap.ActiveStates != null && snap.ActiveStates.Count > 0)
            {
                for (int i = 0; i < snap.ActiveStates.Count; i++)
                {
                    if (i > 0) _sb.Append(", ");
                    _sb.Append(snap.ActiveStates[i]);
                }
            }
            else
            {
                _sb.Append("<i>none</i>");
            }
            _sb.AppendLine();

            // --- History ---
            if (snap.History != null && snap.History.Length > 0)
            {
                _sb.AppendLine("<b>History:</b>");
                int start = Mathf.Max(0, snap.History.Length - _maxHistory);
                for (int i = snap.History.Length - 1; i >= start; i--)
                    _sb.Append("  ").AppendLine(snap.History[i].ToString());
            }

            if (!string.IsNullOrEmpty(_lastActionEvent))
            {
                _sb.AppendLine();
                _sb.Append("<b>Last Event:</b> ").Append(_lastActionEvent);
            }

            _debugText.text = _sb.ToString();
        }

        private void TrySubscribe()
        {
            if (_subscribed || ActionSystem.Instance == null)
                return;

            ActionSystem.Instance.OnActionStarted += HandleActionStarted;
            ActionSystem.Instance.OnActionCompleted += HandleActionCompleted;
            ActionSystem.Instance.OnActionCancelled += HandleActionCancelled;
            _subscribed = true;
        }

        private void Unsubscribe()
        {
            if (!_subscribed || ActionSystem.Instance == null)
                return;

            ActionSystem.Instance.OnActionStarted -= HandleActionStarted;
            ActionSystem.Instance.OnActionCompleted -= HandleActionCompleted;
            ActionSystem.Instance.OnActionCancelled -= HandleActionCancelled;
            _subscribed = false;
        }

        private void HandleActionStarted(GameObject actor, GameAction action)
        {
            if (actor != _actor || action == null) return;
            _lastActionEvent = $"Started {action.GetType().Name}";
        }

        private void HandleActionCompleted(GameObject actor, GameAction action)
        {
            if (actor != _actor || action == null) return;
            _lastActionEvent = $"Completed {action.GetType().Name}";
        }

        private void HandleActionCancelled(GameObject actor, GameAction action)
        {
            if (actor != _actor || action == null) return;
            _lastActionEvent = $"Cancelled {action.GetType().Name}";
        }
    }
}
