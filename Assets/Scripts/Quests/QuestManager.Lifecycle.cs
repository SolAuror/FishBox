using System;
using System.Collections.Generic;
using UnityEngine;
using Sol.AI;
using Sol.Actions;
using Sol.Grab;
using Sol.HUD;
using Sol.ToD;
using System.Collections;

namespace Sol.Quests
{
    public partial class QuestManager : MonoBehaviour
    {

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }

            Instance = this;
        }


        private void OnDestroy()
        {
            UnsubscribeFromPlayer();
            UnsubscribeFromActionSystem();
            if (Instance == this) Instance = null;
        }


        private void Start()
        {
            ResolvePlayerRefs();
            SubscribeToPlayer();
            TrySubscribeToActionSystem();
            TryBeginAutoOfferRoutine();
        }


        private void Update()
        {
            TrySubscribeToActionSystem();

            if (_active.Count == 0 || Time.timeScale <= 0f || UIStateOwnership.IsBlockingUiOpen())
                return;

            float dt = Time.unscaledDeltaTime;
            for (int i = _active.Count - 1; i >= 0; i--)
            {
                QuestSaveData q = _active[i];
                if (q.State != QuestState.Active)
                    continue;

                QuestDefinition def = QuestRegistry.Get()?.Find(q.QuestId);
                if (def == null || !def.IsTimed)
                    continue;

                q.RemainingSeconds -= dt;
                if (q.RemainingSeconds <= 0f)
                {
                    q.RemainingSeconds = 0f;
                    FailQuest(q);
                }
                else
                {
                    OnQuestUpdated?.Invoke(q);
                }
            }
        }


        private void TrySubscribeToActionSystem()
        {
            if (_actionEventsSubscribed || ActionSystem.Instance == null)
                return;

            ActionSystem.Instance.OnActionCompleted += HandleActionCompleted;
            _actionEventsSubscribed = true;
        }


        private void UnsubscribeFromActionSystem()
        {
            if (!_actionEventsSubscribed || ActionSystem.Instance == null)
                return;

            ActionSystem.Instance.OnActionCompleted -= HandleActionCompleted;
            _actionEventsSubscribed = false;
        }


        private void TryBeginAutoOfferRoutine()
        {
            if (_autoOfferRoutineRunning)
                return;

            _autoOfferRoutineRunning = true;
            StartCoroutine(AutoOfferWhenUiReady());
        }


        private IEnumerator AutoOfferWhenUiReady()
        {
            // Let scene UI systems finish Awake/Start before we attempt to prompt.
            yield return null;
            yield return null;

            const float maxWaitSeconds = 10f;
            float elapsed = 0f;

            while (elapsed < maxWaitSeconds)
            {
                if (TryAutoOfferNow())
                    break;

                elapsed += Time.unscaledDeltaTime;
                yield return null;
            }

            _autoOfferRoutineRunning = false;
        }
    }
}
