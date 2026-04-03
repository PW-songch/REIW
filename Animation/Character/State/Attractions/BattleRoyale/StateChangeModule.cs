using REIW.BR;
using UnityEngine;

namespace REIW.Animations.Character.BR
{
    public class StateChangeModule : Character.StateChangeModule
    {
        protected eMovementState _movementState = eMovementState.None;

        public override (bool Valid, eStateType Next) NextStateType
        {
            get
            {
                var next = base.NextStateType;
                if (next.Valid)
                    return next;

                if (_movementState != eMovementState.None)
                {
                    if (_movementState.HasFlagUnsafe(eMovementState.Unarmed))
                        next = (true, eStateType.IDLE);
                    else if (_movementState.HasFlagUnsafe(eMovementState.Ads))
                        next = (true, eStateType.BR_ADS);
                    else if (_movementState.HasFlagUnsafe(eMovementState.Armed))
                        next = (true, eStateType.BR_IDLE);
                    return next;
                }

                return (false, eStateType.NONE);
            }
        }

        public StateChangeModule(MonoBehaviour animation) : base(animation)
        {
        }

        public override bool CanChangeNextState(eStateType nextState)
        {
            if (base.CanChangeNextState(nextState))
                return true;
            return true;
        }

        public override void ChangedState()
        {
            _movementState = eMovementState.None;
        }

        public void SetMovementState(eMovementState state)
        {
            _movementState = state;
        }
    }
}
