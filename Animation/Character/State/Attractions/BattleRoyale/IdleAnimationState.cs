using UnityEngine;

namespace REIW.Animations.Character.BR
{
    public class IdleAnimationState : Character.IdleAnimationState
    {
        [AnimationType(eStateType.BR_IDLE)]
        public enum eAnimationType : uint
        {
            TYPE_START = Animations.Character.eAnimationType.BR_IDLE_TYPE_START,
            IDLE,
            TYPE_END
        }

        public override eStateType StateType => eStateType.BR_IDLE;

        protected override void InitializeDefineStateType()
        {
            _defineStateType = new DefineStateType();
        }
    }
}
