using UnityEngine;

namespace REIW.Animations.Character.BR
{
    public class LocomotionStateChangeModule : StateChangeModule
    {
        public override (bool Valid, eStateType Next) NextStateType
        {
            get
            {
                var next = base.NextStateType;
                if (next.Valid)
                    return next;

                return (false, eStateType.NONE);
            }
        }

        public LocomotionStateChangeModule(MonoBehaviour animation) : base(animation)
        {
        }

        public override bool CanChangeNextState(eStateType nextState)
        {
            if (base.CanChangeNextState(nextState))
                return true;
            return true;
        }
    }
}
