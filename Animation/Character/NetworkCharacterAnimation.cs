using System;
using Animancer;

namespace REIW.Animations.Character
{
    public class NetworkCharacterAnimation : CharacterAnimation
    {
        public override bool IsLocal => false;

        public override bool IsMoving => IsMovingState(true);

        protected override void Start()
        {
            base.Start();
            Movement.EnableIK(false);
        }

        protected override void OnEnable()
        {
            base.OnEnable();
            PlayAnimation(eAnimationType.IDLE, layerType: eLayerType.BASE);
        }

        protected override bool InitializeRootMotionSettings()
        {
            return true;
        }

        protected override bool CheckAnimationState(CharacterAnimationStateMachine stateMachine,
            ref eStateType currentStateType, ref eStateType prevStateType)
        {
            return false;
        }

        protected override AnimancerState InternalPlayAnimation(in eAnimationType animationType,
            in float animationSpeed = 1f, in Func<AnimancerState, float> calculateSpeedFunc = null, in uint layerIndex = 0)
        {
            var state = (CharacterAnimationState)GetAnimationState(animationType);
            if (!state)
                return null;

            var animancerState = state.PlayAnimation(animationType, animationSpeed, calculateSpeedFunc, layerIndex);

            if (state.StateType == CurrentStateType)
                return animancerState;

            StateMachine.CurrentState.DisableStateNetwork();
            _prevStateType = StateMachine.CurrentState.StateType;
            StateMachine.CurrentState = state;
            StateMachine.CurrentState.EnableStateNetwork();
            _currentStateType = state.StateType;
            return animancerState;
        }

        protected override bool ChangeAnimationNetObject(in eAnimationType animationType, in float animationSpeed)
        {
            return false;
        }
    }
}
