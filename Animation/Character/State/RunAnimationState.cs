using System;
using Animancer;
using UnityEngine;

namespace REIW.Animations.Character
{
    public class RunAnimationState : LocomotionAnimationState
    {
        [AnimationType(eStateType.RUN)]
        public enum eAnimationType : uint
        {
            TYPE_START = Animations.Character.eAnimationType.RUN_TYPE_START,
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

        public override eStateType StateType => eStateType.RUN;

        protected override int AnimationStartTypeIndex => (int)eAnimationType.TYPE_START;

        public override (bool IsChange, eStateType Next) NextStateType
        {
            get
            {
                var nextState = base.NextStateType;
                if (nextState.IsChange)
                    return nextState;

                if (Movement.IsMoveInput)
                {
                    if (Movement.IsSprintInput)
                    {
                        if (Movement.IsSprint)
                            return (true, GetStateType(eStateType.SPRINT));
                    }
                    else if (Movement.IsWalkInput)
                    {
                        if (AnimationParameters.ForwardSpeed <= Mathf.Max(Movement.WalkSpeed, Movement.RunSpeed * 0.5f))
                            return (true, GetStateType(eStateType.WALK));
                    }

                    return (false, StateType);
                }
                else if (AnimationParameters.IsValidForwardSpeed)
                {
                    return (false, StateType);
                }

                return (false, DefaultStateType);
            }
        }

        protected override bool CanExitStopState
        {
            get
            {
                if (base.CanExitStopState)
                    return true;
                if (Movement.IsMoveInput && Movement.IsWalkInput)
                    return true;
                return false;
            }
        }

        public LinearMixerTransition MoveMixer => _moveMixer;

        protected override void OnEnable()
        {
            base.OnEnable();

            SetState(eMovementType.IDLE);

            if (!ApplyReservedMoveType())
            {
                if (Movement.IsMoveInput)
                {
                    if (!PlayQuickTurnAnimation())
                    {
                        Movement.IsCorrectionRootMotion = true;
                        PlayMoveAnimation(true);
                    }
                }
            }
        }

        protected override void OnDisable()
        {
            base.OnDisable();

            if (Character is LocalCharacter localCharacter && Animation.CurrentStateType == GetStateType(eStateType.WALK))
                localCharacter.ResetRootMotionVelocityQueue();
        }

        protected override void SetState(in eMovementType type)
        {
            switch (type)
            {
                case eMovementType.MOVE:
                    if (StateType == GetStateType(eStateType.RUN))
                        Movement.CurrentMoveType = eMoveType.RUN;
                    break;
                case eMovementType.STOP:
                    Movement.IsSprintInput = false;
                    break;
            }

            base.SetState(type);
        }

        protected override void PlayIdleAnimation()
        {
            if (_moveMixer.State is { IsCurrent: false })
                InternalPlayAnimation(Animations.Character.eAnimationType.IDLE);
            else
                base.PlayIdleAnimation();
        }

        public override bool IsPlayingAnimation(in float normalizedTime)
        {
            if (base.IsPlayingAnimation(normalizedTime))
                return true;
            if (Animancer.States.TryGet(_moveStart, out var state) && state.IsPlaying && state.NormalizedTime < normalizedTime)
                return true;
            return false;
        }

        protected override AnimancerState InternalPlayAnimation(in Character.eAnimationType animationType,
            in float animationSpeed = 1f, in Func<AnimancerState, float> calculateSpeedFunc = null, in eLayerType layerType = eLayerType.BASE)
        {
            AnimancerState state = null;

            switch (animationType)
            {
                case Animations.Character.eAnimationType.IDLE:
                    SetState(eMovementType.IDLE);
                    state = Animation.PlayAnimation(animationType, _moveMixer, animationSpeed, calculateSpeedFunc, layerType);
                    break;
                case (Character.eAnimationType)eAnimationType.RUN:
                    if (!IsLocal)
                        CheckFrontFootOnMoveAnimation();
                    state = PlayAnimation(eMoveAnimationType.MOVE, animationSpeed, calculateSpeedFunc, layerType);
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

        public void OnAnimation_FinishCorrectionRootMotion()
        {
            Movement.IsCorrectionRootMotion = false;
        }
    }
}