using UnityEngine;

namespace REIW.Animations.Character.BR
{
    public class RunAnimationState : Character.RunAnimationState
    {
        [AnimationType(eStateType.BR_RUN)]
        public enum eAnimationType : uint
        {
            TYPE_START = Animations.Character.eAnimationType.BR_RUN_TYPE_START,
            RUN_START,
            RUN,
            RUN_TURN_LEFT,
            RUN_TURN_RIGHT,
            RUN_QUICK_TURN_LEFT,
            RUN_QUICK_TURN_RIGHT,
            RUN_STAND_STOP,
            RUN_MOVE_STOP,
            TYPE_END
        }

        public override eStateType StateType => eStateType.BR_RUN;

        protected override int AnimationStartTypeIndex => (int)eAnimationType.TYPE_START;

        protected override void InitializeDefineStateType()
        {
            _defineStateType = new DefineStateType();
        }
    }
}
