using System;
using Animancer;
using UnityEngine;

namespace REIW.Animations.Npc
{
    public class WalkAnimationState : NpcAnimationState
    {
        [AnimationType(eStateType.WALK)]
        public enum eAnimationType : uint
        {
            TYPE_START = Npc.eAnimationType.WALK_TYPE_START,
            WALK,
            TYPE_END
        }

        public override eStateType StateType => eStateType.WALK;
        
        [SerializeField] private ClipTransition _walkClip;
        
        protected override AnimancerState InternalPlayAnimation(in Npc.eAnimationType animationType,
            in float animationSpeed = 1f, in Func<AnimancerState, float> calculateSpeedFunc = null, in eLayerType layerType = eLayerType.BASE)
        {
            var state = Animation.PlayAnimation(animationType, _walkClip, animationSpeed, calculateSpeedFunc, layerType);
            
            _playingAniState = state;
            _animation.TryForceSetAnimationState(StateType);
            
            return state;
        }
    }
}