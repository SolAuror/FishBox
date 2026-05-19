using UnityEngine;
using Sol.Rpg;

namespace Sol
{
    public partial class InteractionPoint
    {
        public virtual bool TryReserve(Interactor interactor, float reservationDuration = ReservationGraceDuration)
        {
            if (interactor == null || interactor.Owner == null)
                return false;

            if (!EffectiveSingleOccupancy)
                return true;

            ClearExpiredReservationIfNeeded();
            if (_inUse)
                return false;

            if (_reservedInteractor != null && !IsSameInteractor(_reservedInteractor, interactor))
                return false;

            _reservedInteractor = interactor;
            _reservationExpiresAt = Time.time + Mathf.Max(0.1f, reservationDuration);
            _tags ??= new GameplayTagSet();
            _tags.AddRuntimeTagPath(GameplayCapabilityTags.StateReserved);
            return true;
        }

        public virtual void ReleaseReservation(Interactor interactor = null)
        {
            if (_reservedInteractor == null)
                return;

            if (interactor == null || IsSameInteractor(_reservedInteractor, interactor))
            {
                _reservedInteractor = null;
                _reservationExpiresAt = 0f;
                _tags ??= new GameplayTagSet();
                _tags.RemoveRuntimeTagPath(GameplayCapabilityTags.StateReserved);
            }
        }

        public virtual bool IsReservedBy(Interactor interactor)
        {
            ClearExpiredReservationIfNeeded();
            if (_reservedInteractor == null || interactor == null)
                return false;

            return IsSameInteractor(_reservedInteractor, interactor);
        }

        private bool IsReservedByAnother(Interactor interactor)
        {
            if (_reservedInteractor == null)
                return false;

            return !IsSameInteractor(_reservedInteractor, interactor);
        }

        private static bool IsSameInteractor(Interactor a, Interactor b)
        {
            return a != null && b != null && a.Owner == b.Owner;
        }

        private void ClearExpiredReservationIfNeeded()
        {
            if (_reservedInteractor == null || _inUse || Time.time < _reservationExpiresAt)
                return;

            _reservedInteractor = null;
            _reservationExpiresAt = 0f;
            _tags ??= new GameplayTagSet();
            _tags.RemoveRuntimeTagPath(GameplayCapabilityTags.StateReserved);
        }
    }
}
