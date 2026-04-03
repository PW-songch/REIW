using System;
using Animancer;
using UnityEngine;

namespace REIW.Animations.Npc
{
    public class CollisionAnimationState : PlayTargetAnimationState
    {
        [AnimationType(eStateType.COLLISION)]
        public enum eAnimationType : uint
        {
            TYPE_START = Npc.eAnimationType.COLLISION_TYPE_START,
            COLLISION_BACK,
            COLLISION_FRONT,
            COLLISION_LEFT,
            COLLISION_RIGHT,
            TYPE_END
        }

        public override eStateType StateType => eStateType.COLLISION;

        [SerializeField] protected LinearMixerTransition _animationMixer;

        protected override AnimancerState InternalPlayAnimation(in Npc.eAnimationType animationType,
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

        protected virtual float GetAnimationParameter(Npc.eAnimationType InAnimationType)
        {
            return InAnimationType - Npc.eAnimationType.COLLISION_TYPE_START;
        }
    }
}
