using UnityEngine;

namespace REIW.Animations.Character.BR
{
    public class DashAnimationState : Character.DashAnimationState
    {
        [AnimationType(eStateType.BR_DASH)]
        public enum eAnimationType : uint
        {
            TYPE_START = Animations.Character.eAnimationType.BR_DASH_TYPE_START,
            DASH = eMoveAnimationType.MOVE + TYPE_START,
            DASH_STOP = eMoveAnimationType.MOVE_STOP + TYPE_START,
            TYPE_END
        }

        public override eStateType StateType => eStateType.BR_DASH;

        protected override void InitializeDefineStateType()
        {
            _defineStateType = new DefineStateType();
        }
    }
}
