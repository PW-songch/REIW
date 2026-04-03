using UnityEngine;

namespace REIW.Animations.Character.BR
{
    public class SprintAnimationState : Character.SprintAnimationState
    {
        [AnimationType(eStateType.BR_SPRINT)]
        public enum eAnimationType : uint
        {
            TYPE_START = Animations.Character.eAnimationType.BR_SPRINT_TYPE_START,
            SPRINT = eMoveAnimationType.MOVE + TYPE_START,
            SPRINT_QUICK_TURN_LEFT = eMoveAnimationType.QUICK_TURN_LEFT + TYPE_START,
            SPRINT_QUICK_TURN_RIGHT,
            SPRINT_STAND_STOP,
            SPRINT_MOVE_STOP,
            TYPE_END
        }

        public override eStateType StateType => eStateType.BR_SPRINT;

        protected override void InitializeDefineStateType()
        {
            _defineStateType = new DefineStateType();
        }
    }
}
