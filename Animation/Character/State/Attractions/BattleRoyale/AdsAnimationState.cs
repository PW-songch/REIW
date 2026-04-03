using System;
using Animancer;
using UnityEngine;

namespace REIW.Animations.Character.BR
{
    public class AdsAnimationState : CharacterAnimationState
    {
        [AnimationType(eStateType.BR_ADS)]
        public enum eAnimationType : uint
        {
            TYPE_START = REIW.Animations.Character.eAnimationType.BR_ADS_TYPE_START,
            LOCOMOTION,
            TYPE_END
        }

        [SerializeField] private MixerTransition2D _adsMixer;
        [SerializeField] private float _adsSpeed;
        [SerializeField] private float _adsAcceleration;
        [SerializeField] private float _adsDeceleration;

        public override eStateType StateType => eStateType.BR_ADS;

        public override (bool IsChange, eStateType Next) NextStateType
        {
            get
            {
                var nextState = base.NextStateType;
                if (nextState.IsChange)
                    return nextState;

                return (false, DefaultStateType);
            }
        }

        public override bool CanEnterState => Movement.IsGrounded;
        public override bool CanExitState => base.CanExitState || Movement.IsAnyActionInput || Movement.IsAirborne;

        protected override void OnEnable()
        {
            base.OnEnable();
            PlayAdsAnimation();
        }

        protected override void OnDisable()
        {
            base.OnDisable();
            Movement.EnableAimIKController(false);
        }

        protected override void InitializeDefineStateType()
        {
            _defineStateType = new DefineStateType();
        }

        protected override IStateChangeModule<eStateType>[] CreateStateChangeModules(
            CharacterAnimation animation, in EnumAttraction attractionType)
        {
            return new IStateChangeModule<eStateType>[]
            {
                new AdsStateChangeModule(animation)
            };
        }

        protected override void UpdateAnimationParameters()
        {
            base.UpdateAnimationParameters();
            UpdateAdsParameter();
        }

        private void UpdateAdsParameter()
        {
            if (!_adsMixer.State.IsValid())
                return;

            var dir = LocalCharacter.CharacterTransform.InverseTransformDirection(Movement.MovementDirection);
            var planarDir = new Vector2(dir.x, dir.z);
            var deltaSpeed = planarDir != Vector2.zero ? _adsAcceleration : _adsDeceleration;
            _adsMixer.State.Parameter = Vector2.MoveTowards(
                _adsMixer.State.Parameter, planarDir, deltaSpeed * Time.deltaTime);
        }

        public override bool IsPlayingAnimation(in float normalizedTime)
        {
            if (Animancer.States.TryGet(_adsMixer, out var state) &&
                state.IsPlaying && state.NormalizedTime < normalizedTime)
                return true;
            return false;
        }

        protected override AnimancerState InternalPlayAnimation(in REIW.Animations.Character.eAnimationType animationType,
            in float animationSpeed = 1f, in Func<AnimancerState, float> calculateSpeedFunc = null, in eLayerType layerType = eLayerType.BASE)
        {
            AnimancerState state = null;

            switch (animationType)
            {
                case (REIW.Animations.Character.eAnimationType)eAnimationType.LOCOMOTION:
                {
                    state = Animation.PlayAnimation(animationType, _adsMixer, animationSpeed, calculateSpeedFunc, layerType);
                    break;
                }
            }

            ExecuteMixerRecalculateWeights(state);
            SetUseRootMotion(state);
            return state;
        }

        private void PlayAdsAnimation()
        {
            Movement.CurrentMoveType = eMoveType.ADS;
            Movement.EnableAimIKController(true);
            LocalCharacter?.SetMoveSpeed((int)Movement.CurrentMoveType);
            _playingAniState = InternalPlayAnimation((REIW.Animations.Character.eAnimationType)eAnimationType.LOCOMOTION);
        }
    }
}
