using System.Collections.Generic;
using UnityEngine;
using Sol.Actions;
using Sol.HUD;

namespace Sol.Quests
{
    /// <summary>
    /// Lightweight quest board source that reuses conversation + dialogue prompt UI.
    /// </summary>
    public class QuestBoardInteractable : MonoBehaviour, IInteractable
    {
        private enum OptionId
        {
            Offer = 0,
            TurnIn = 1,
            Goodbye = 2
        }

#region Inspector Settings
        [SerializeField] private string _prompt = "Quest Board";
        [SerializeField] private string _speakerName = "Quest Board";
        [SerializeField] private string _greeting = "Need work?";
        [SerializeField] private Sprite _speakerIcon;
#endregion

        public string InteractionPrompt => _prompt;

        public bool CanInteract(Interactor interactor)
        {
            return interactor != null && interactor.IsPlayer;
        }

        public GameAction GetInteraction(Interactor interactor)
        {
            if (!CanInteract(interactor))
                return null;

            List<OptionId> ids = new();
            List<string> labels = new();
            QuestManager qm = QuestManager.Instance;
            if (qm != null)
            {
                if (qm.GetOfferableQuests(null).Count > 0)
                {
                    ids.Add(OptionId.Offer);
                    labels.Add("Browse jobs");
                }

                if (HasTurnIn(qm))
                {
                    ids.Add(OptionId.TurnIn);
                    labels.Add("Turn in");
                }
            }

            ids.Add(OptionId.Goodbye);
            labels.Add("Goodbye");

            return new OpenConversationAction(
                _speakerName,
                _greeting,
                labels,
                optionIndex => HandleOptionSelected(ids, optionIndex),
                speakerIcon: _speakerIcon);
        }

        private static bool HasTurnIn(QuestManager qm)
        {
            IReadOnlyList<QuestSaveData> active = qm.Active;
            for (int i = 0; i < active.Count; i++)
            {
                QuestSaveData q = active[i];
                if (q.State != QuestState.ReadyToTurnIn)
                    continue;

                QuestDefinition def = QuestRegistry.Get()?.Find(q.QuestId);
                if (def == null)
                    continue;

                if (string.IsNullOrWhiteSpace(def.GiverNpcName))
                    return true;
            }

            return false;
        }

        private bool HandleOptionSelected(IReadOnlyList<OptionId> ids, int optionIndex)
        {
            if (ids == null || optionIndex < 0 || optionIndex >= ids.Count)
                return true;

            switch (ids[optionIndex])
            {
                case OptionId.Offer:
                    OfferNext();
                    break;
                case OptionId.TurnIn:
                    TurnInReady();
                    break;
            }

            return true;
        }

        private void OfferNext()
        {
            QuestManager qm = QuestManager.Instance;
            if (qm == null)
                return;

            List<QuestDefinition> offerable = qm.GetOfferableQuests(null);
            if (offerable.Count == 0)
                return;

            QuestDefinition def = offerable[0];
            ConfirmationPromptSystem prompt = ConfirmationPromptSystem.ResolveInstance();
            if (prompt == null)
            {
                qm.TryAccept(def.QuestId, string.Empty);
                return;
            }

            prompt.Show(
                def.Title,
                def.Summary,
                confirmAction: () => qm.TryAccept(def.QuestId, string.Empty),
                cancelAction: null,
                confirmLabel: "Accept",
                cancelLabel: "Decline",
                icon: _speakerIcon);
        }

        private void TurnInReady()
        {
            QuestManager qm = QuestManager.Instance;
            if (qm == null)
                return;

            QuestSaveData target = null;
            QuestDefinition targetDef = null;
            IReadOnlyList<QuestSaveData> active = qm.Active;
            for (int i = 0; i < active.Count; i++)
            {
                QuestSaveData q = active[i];
                if (q.State != QuestState.ReadyToTurnIn)
                    continue;

                QuestDefinition def = QuestRegistry.Get()?.Find(q.QuestId);
                if (def == null || !string.IsNullOrWhiteSpace(def.GiverNpcName))
                    continue;

                target = q;
                targetDef = def;
                break;
            }

            if (target == null || targetDef == null)
                return;

            ConfirmationPromptSystem prompt = ConfirmationPromptSystem.ResolveInstance();
            if (prompt == null)
            {
                qm.TryTurnIn(target.QuestId, string.Empty);
                return;
            }

            prompt.Show(
                targetDef.Title,
                $"Turn in \"{targetDef.Title}\"?",
                confirmAction: () => qm.TryTurnIn(target.QuestId, string.Empty),
                cancelAction: null,
                confirmLabel: "Turn in",
                cancelLabel: "Not yet",
                icon: _speakerIcon);
        }
    }
}
