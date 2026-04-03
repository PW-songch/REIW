using System;
using Animancer.Units;
using Animancer;
using UnityEngine;

namespace REIW.Animations.Npc
{
    public class IdleAnimationState : NpcAnimationState
    {
        [AnimationType(eStateType.IDLE)]
        public enum eAnimationType : uint
        {
            TYPE_START = Npc.eAnimationType.IDLE_TYPE_START,
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

        public override bool CanExitState => _playingAniState == null;

        protected override void OnEnable()
        {
            base.OnEnable();

            PlayMainAnimation();

            _randomizeTime += _firstRandomizeDelay;
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
            InternalPlayAnimation((Npc.eAnimationType)eAnimationType.IDLE);
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

            return InternalPlayAnimation((index + eAnimationType.IDLE.ToInt() + 1).ToEnum<Npc.eAnimationType>()) != null;
        }

        private ClipTransition GetRandomAnimation(in int InIndex)
        {
            return InIndex >= _randomAnimations.Length ? _mainAnimation : _randomAnimations[InIndex];
        }

        private ClipTransition GetRandomAnimation(in Npc.eAnimationType InAnimationType)
        {
            if (InAnimationType == Npc.eAnimationType.IDLE)
                return _mainAnimation;

            var index = InAnimationType.ToInt() - eAnimationType.IDLE.ToInt() + 1;
            return GetRandomAnimation(index);
        }

        protected override AnimancerState InternalPlayAnimation(in Npc.eAnimationType animationType,
            in float animationSpeed = 1f, in Func<AnimancerState, float> calculateSpeedFunc = null, in eLayerType layerType = eLayerType.BASE)
        {
            AnimancerState state = null;
            switch (animationType)
            {
                case Npc.eAnimationType.IDLE:
                    state = Animation.PlayAnimation(animationType, _mainAnimation, animationSpeed, calculateSpeedFunc, layerType);
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

            return state;
        }
    }
}