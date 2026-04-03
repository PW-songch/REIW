using System;
using Animancer.Units;
using Animancer;
using UnityEngine;

namespace REIW.Animations.Character
{
    public class IdleAnimationState : CharacterAnimationState
    {
        [AnimationType(eStateType.IDLE)]
        public enum eAnimationType : uint
        {
            TYPE_START = Animations.Character.eAnimationType.IDLE_TYPE_START,
            IDLE,
            TYPE_END
        }

        public override eStateType StateType => eStateType.IDLE;

        [SerializeField] private ClipTransition _mainAnimation;
        [SerializeField] private ClipTransition[] _randomAnimations;

        [SerializeField, Seconds] private float _firstRandomizeDelay = 5;
        [SerializeField, Seconds] private float _minRandomizeInterval = 0;
        [SerializeField, Seconds] private float _maxRandomizeInterval = 20;

        private float _randomizeTime;

        public override (bool IsChange, eStateType Next) NextStateType
        {
            get
            {
                var nextState = base.NextStateType;
                if (nextState.IsChange)
                    return nextState;

                if (Movement.IsMoveInput || AnimationParameters.IsValidForwardSpeed)
                {
                    if (Movement.IsWalkInput)
                        return (true, GetStateType(eStateType.WALK));
                    return (false, GetStateType(eStateType.RUN));
                }

                return (false, StateType);
            }
        }

        public override bool CanEnterState => Movement.IsGrounded;
        public override bool CanExitState => true;

        protected override void OnEnable()
        {
            base.OnEnable();

            ChangeStaminaActionType(EnumCategory.LocomotionStateIdle);
            PlayMainAnimation();

            _randomizeTime += _firstRandomizeDelay;
            Movement.CurrentMoveType = eMoveType.STAND;
        }

        public override bool LateUpdateState()
        {
            if (!base.LateUpdateState())
                return false;

            var state = Animancer.States.Current;
            if (state == _mainAnimation.State && state.Time >= _randomizeTime)
            {
                if (!PlayRandomAnimation())
                    PlayMainAnimation();
            }

            return true;
        }

        private void PlayMainAnimation()
        {
            _randomizeTime = UnityEngine.Random.Range(_minRandomizeInterval, _maxRandomizeInterval);
            InternalPlayAnimation((Character.eAnimationType)eAnimationType.IDLE);
        }

        private bool PlayRandomAnimation()
        {
            if (_randomAnimations.Length == 0)
                return false;

            var currentAni = Animancer.States.Current.GetTransition();
            var index = UnityEngine.Random.Range(0, _randomAnimations.Length);
            var ani = GetRandomAnimation(index);
            while (ani == currentAni)
            {
                index = UnityEngine.Random.Range(0, _randomAnimations.Length);
                ani = GetRandomAnimation(index);
            }

            return InternalPlayAnimation((index + eAnimationType.IDLE.ToInt() + 1).ToEnum<Character.eAnimationType>()) != null;
        }

        private ClipTransition GetRandomAnimation(in int InIndex)
        {
            return InIndex >= _randomAnimations.Length ? _mainAnimation : _randomAnimations[InIndex];
        }

        private ClipTransition GetRandomAnimation(in Character.eAnimationType InAnimationType)
        {
            if (InAnimationType == Animations.Character.eAnimationType.IDLE)
                return _mainAnimation;

            var index = InAnimationType.ToInt() - eAnimationType.IDLE.ToInt() + 1;
            return GetRandomAnimation(index);
        }

        protected override AnimancerState InternalPlayAnimation(in Character.eAnimationType animationType,
            in float animationSpeed = 1f, in Func<AnimancerState, float> calculateSpeedFunc = null, in eLayerType layerType = eLayerType.BASE)
        {
            AnimancerState state = null;
            switch (animationType)
            {
                case (Character.eAnimationType)eAnimationType.IDLE:
                    Movement.CurrentMoveType = eMoveType.STAND;
                    state = Animation.PlayAnimation(animationType, _mainAnimation, animationSpeed, calculateSpeedFunc, layerType);
                    ExecuteMixerRecalculateWeights(state);
                    break;
                default:
                    var ani = GetRandomAnimation(animationType);
                    if (ani != null)
                    {
                        state = Animation.PlayAnimation(animationType, ani, animationSpeed, calculateSpeedFunc, layerType);
                        SetAnimationEndEvent(state, PlayMainAnimation);
                    }
                    break;
            }

            SetUseRootMotion(state);
            return state;
        }
    }
}