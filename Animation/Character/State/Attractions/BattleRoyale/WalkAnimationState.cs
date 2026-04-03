using UnityEngine;

namespace REIW.Animations.Character.BR
{
    public class WalkAnimationState : Character.WalkAnimationState
    {
        [AnimationType(eStateType.WALK)]
        public enum eAnimationType : uint
        {
            TYPE_START = Animations.Character.eAnimationType.BR_WALK_TYPE_START,
            WALK = eMoveAnimationType.MOVE + TYPE_START,
            WALK_TURN_LEFT,
            WALK_TURN_RIGHT,
            WALK_STAND_STOP = eMoveAnimationType.STAND_STOP + TYPE_START,
            WALK_MOVE_STOP,
            TYPE_END
        }

        public override eStateType StateType => eStateType.BR_WALK;

        protected override void InitializeDefineStateType()
        {
            _defineStateType = new DefineStateType();
        }
    }
}
