using System;
using Animancer;
using UnityEngine;

namespace REIW.Animations.Character
{
    public class InteractionAnimationState : PlayTargetAnimationState
    {
        public override eStateType StateType => eStateType.INTERACTION;

        [SerializeField] protected LinearMixerTransition _animationMixer;

        public override eAnimationType PlayAnimationType
        {
            set
            {
                base.PlayAnimationType = value;
                if (!enabled)
                    Movement.IsInteractionInput = PlayAnimationType != eAnimationType.NONE;
            }
        }

        protected override void PlayAnimationPostProcess(in AnimancerState animationState)
        {
            base.PlayAnimationPostProcess(animationState);
            Movement.IsInteractionInput = false;
        }

        protected override AnimancerState InternalPlayAnimation(in eAnimationType animationType,
            in float animationSpeed = 1f, in Func<AnimancerState, float> calculateSpeedFunc = null, in eLayerType layerType = eLayerType.BASE)
        {
            var state = Animation.PlayAnimation(animationType, _animationMixer, animationSpeed, calculateSpeedFunc, layerType);
            if (state.IsValid() && _animationMixer.State != null)
            {
                _animationMixer.State.Parameter = GetAnimationParameter(animationType);
                PlayAnimationPostProcess(state);
            }

            return state;
        }

        protected virtual float GetAnimationParameter(in eAnimationType animationType)
        {
            return animationType - eAnimationType.INTERACTION_TYPE_START;
        }
    }
}
