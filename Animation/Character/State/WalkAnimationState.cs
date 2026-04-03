using System;
using Animancer;
using UnityEngine;

namespace REIW.Animations.Character
{
    public class WalkAnimationState : LocomotionAnimationState
    {
        [AnimationType(eStateType.WALK)]
        public enum eAnimationType : uint
        {
            TYPE_START = Animations.Character.eAnimationType.WALK_TYPE_START,
            WALK = eMoveAnimationType.MOVE + TYPE_START,
            WALK_TURN_LEFT,
            WALK_TURN_RIGHT,
            WALK_STAND_STOP = eMoveAnimationType.STAND_STOP + TYPE_START,
            WALK_MOVE_STOP,
            TYPE_END
        }

        public override eStateType StateType => eStateType.WALK;

        [SerializeField] private ClipTransition _moveStopRight;

        protected override int AnimationStartTypeIndex => (int)eAnimationType.TYPE_START;

        public override (bool IsChange, eStateType Next) NextStateType
        {
            get
            {
                var nextState = base.NextStateType;
                if (nextState.IsChange)
                    return nextState;

                if (Movement.IsMoveInput || AnimationParameters.IsValidForwardSpeed)
                {
                    if (!Movement.IsWalkInput)
                        return (false, GetStateType(eStateType.RUN));
                    return (true, StateType);
                }

                return (false, DefaultStateType);
            }
        }

        public bool IsWalking => Animancer.IsPlaying(_moveMixer);

        protected override void OnEnable()
        {
            base.OnEnable();

            if (!ApplyReservedMoveType())
                PlayMoveAnimation(true);
        }

        protected override void SetState(in eMovementType type)
        {
            if (StateType == GetStateType(eStateType.WALK) && type == eMovementType.MOVE)
                Movement.CurrentMoveType = eMoveType.WALK;

            base.SetState(type);
        }

        protected override void UpdateMove()
        {
            if (AnimationParameters.ForwardSpeed > _currentForwardSpeed)
            {
                if (_currentMovementType is eMovementType.STOP)
                    PlayMoveAnimation();
            }
        }

        protected override void PlayMoveAnimation(in bool checkFoot = false)
        {
            if (checkFoot)
                CheckFrontFootOnMoveAnimation();

            if (Movement.IsMoveInput)
            {
                if (_moveMixer.State is { IsCurrent: true })
                {
                    SetState(eMovementType.MOVE);
                    return;
                }

                InternalPlayAnimation((Character.eAnimationType)eAnimationType.WALK);
            }
            else
            {
                SetState(eMovementType.MOVE);
            }

            //ChangeStaminaActionType(eStaminaActionType.Normal);
        }

        public void SetMoveMixer(in LinearMixerTransition moveMixer)
        {
            _moveMixer = moveMixer;
        }

        protected override AnimancerState PlayAnimation(in eMoveAnimationType moveAnimationType,
            in float animationSpeed = 1, in Func<AnimancerState, float> calculateSpeedFunc = null, in eLayerType layerType = eLayerType.BASE)
        {
            AnimancerState state = null;

            switch (moveAnimationType)
            {
                case eMoveAnimationType.MOVE_STOP:
                    SetState(eMovementType.STOP);
                    state = Animation.PlayAnimation(ConvertAnimationType(moveAnimationType),
                        Movement.FrontFoot == AvatarIKGoal.LeftFoot ? _moveStop : _moveStopRight, animationSpeed,
                        calculateSpeedFunc, layerType);
                    break;
                default:
                    state = base.PlayAnimation(moveAnimationType, animationSpeed, calculateSpeedFunc, layerType);
                    break;
            }

            ExecuteMixerRecalculateWeights(state);
            SetUseRootMotion(state);
            return state;
        }

        protected override AnimancerState InternalPlayAnimation(in Character.eAnimationType animationType,
            in float animationSpeed = 1f, in Func<AnimancerState, float> calculateSpeedFunc = null, in eLayerType layerType = eLayerType.BASE)
        {
            AnimancerState state = null;

            switch (animationType)
            {
                case Animations.Character.eAnimationType.RUN:
                case (Character.eAnimationType)eAnimationType.WALK:
                    if (!IsLocal)
                        CheckFrontFootOnMoveAnimation();
                    SetState(eMovementType.MOVE);
                    state = Animation.PlayAnimation((Character.eAnimationType)eAnimationType.WALK, _moveMixer, animationSpeed, calculateSpeedFunc, layerType);
                    break;
                default:
                    var moveAniType = ConvertAnimationType(animationType);
                    if (moveAniType != eMoveAnimationType.NONE)
                        state = PlayAnimation(moveAniType, animationSpeed, calculateSpeedFunc, layerType);
                    break;
            }

            ExecuteMixerRecalculateWeights(state);
            SetUseRootMotion(state);
            return state;
        }
    }
}