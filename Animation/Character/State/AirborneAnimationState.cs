using System;
using Animancer;
using UnityEngine;

namespace REIW.Animations.Character
{
    public class AirborneAnimationState : CharacterAnimationState
    {
        [AnimationType(eStateType.AIRBORNE)]
        public enum eAnimationType : uint
        {
            TYPE_START = Animations.Character.eAnimationType.AIRBORNE_TYPE_START,
            AIRBORNE_FALL,
            AIRBORNE_LANDIND,
            TYPE_END
        }

        public enum eLandingType
        {
            NONE = 0,
            STAND,
            LEFT_FOOT,
            RIGHT_FOOT,
            AIRBORNE,
        }

        public override eStateType StateType => eStateType.AIRBORNE;

        [SerializeField] protected ClipTransition _fall;
        [SerializeField] protected MixerTransition2D _landingMixer;

        protected eLandingType _landingType = eLandingType.NONE;
        protected bool _isLanding = false;
        protected bool _enableAnyMovement = false;

        public override (bool IsChange, eStateType Next) NextStateType
        {
            get
            {
                var nextState = base.NextStateType;
                if (nextState.IsChange)
                    return nextState;

                bool isMoveInput = Movement.IsMoveInput;
                if (isMoveInput || AnimationParameters.IsValidForwardSpeed)
                {
                    if (AnimationParameters.ForwardSpeed > 0.1f)
                    {
                        if (Movement.IsSprintInput)
                            return (true, GetStateType(eStateType.SPRINT));
                        if (Movement.IsWalkInput)
                            return (true, GetStateType(eStateType.WALK));
                        if (!isMoveInput)
                            AnimationParameters.ReservedMoveType = LocomotionAnimationState.eMovementType.STOP;
                        return (true, GetStateType(eStateType.RUN));
                    }
                }

                return (false, DefaultStateType);
            }
        }

        public override bool CanEnterState => Movement.IsAirborne;
        public override bool CanExitState => base.CanExitState || Movement.IsGrappleInput ||
                                             (_isLanding && _playingAniState == null);

        public override bool ApplyRawRootMotion => true;

        private bool updateGlidingLanding = false;

        #region Property name provider
        #if UNITY_EDITOR
        public const string FallName = nameof(_fall);
        #endif
        #endregion

        protected override void OnEnable()
        {
            base.OnEnable();

            ChangeStaminaActionType(EnumCategory.LocomotionStateIdle);
            PlayFallAnimation();

            Movement.IsLanding = false;

            updateGlidingLanding = _prevStateType == GetStateType(eStateType.GLIDING);
        }

        protected override void OnDisable()
        {
            base.OnDisable();

            Reset();

            Movement.IsCorrectionRootMotion = false;
            Movement.SetAirbornStateGrounderIKWeight(false);

            if (updateGlidingLanding)
            {
                updateGlidingLanding = false;
                PlayerController.Instance.OwnerPlayerNetObject.AddSnapShot_ActionCancel();
            }
        }

        protected virtual void Reset()
        {
            _isLanding = false;
            _enableAnyMovement = false;
        }

        public override bool LateUpdateState()
        {
            UpdateAnyMovement();

            if (!base.LateUpdateState())
                return false;

            UpdateLanding();

            return true;
        }

        protected virtual bool UpdateAnyMovement()
        {
            if (!_enableAnyMovement)
                return false;

            if (Movement.IsAnyMovementInput)
                _playingAniState = null;
            else
            {
                switch (_landingType)
                {
                    case eLandingType.LEFT_FOOT:
                    case eLandingType.RIGHT_FOOT:
                        if (!Movement.IsMoveInput)
                            _playingAniState = null;
                        break;
                }
            }

            return true;
        }

        private void PlayFallAnimation()
        {
            Reset();

            if (Movement.IsFalling)
                Movement.CurrentMoveType = eMoveType.AIRBORNE;

            _playingAniState = InternalPlayAnimation((Character.eAnimationType)eAnimationType.AIRBORNE_FALL);
            SetAnimationEndEvent(_playingAniState, OnAnimation_FallEndEvent);

            if (Character is LocalCharacter localCharacter)
                localCharacter.SetMoveSpeed((int)Movement.CurrentMoveType);
        }

        protected virtual void UpdateLanding()
        {
            if (UpdateGlidingLanding())
                return;

            if (_isLanding || !Movement.IsLanding)
                return;

            PlayLandingAnimation();
        }

        private bool UpdateGlidingLanding()
        {
            if (updateGlidingLanding == false)
                return false;
            if (_isLanding)
                return false;
            if (Movement.IsGrounded == false)
                return false;

            PlayLandingAnimation();

            return true;
        }


        protected virtual bool PlayLandingAnimation()
        {
            if (_landingMixer is { IsValid: true })
            {
                _isLanding = true;

                if (Movement.IsFalling || _prevStateType == GetStateType(eStateType.GLIDING))
                    Movement.CurrentMoveType = eMoveType.AIRBORNE;
                else if (!Movement.IsMoveInput)
                    Movement.CurrentMoveType = eMoveType.STAND;
                else if (Movement.CurrentMoveType == eMoveType.DASH)
                    Movement.CurrentMoveType = eMoveType.SPRINT;

                _playingAniState = InternalPlayAnimation(GetLandingAnimationType());
                SetAnimationEndEvent(_playingAniState, OnAnimation_LandingEndEvent);
                return true;
            }

            return false;
        }

        protected virtual Character.eAnimationType GetLandingAnimationType()
        {
            if (Movement.CurrentMoveType == eMoveType.AIRBORNE)
            {
                Character.AnimancerEvents.OnFxEventInt((int)eKnownEffect.FX_LAND);
                return (Character.eAnimationType)eAnimationType.AIRBORNE_LANDIND;
            }
            else if (!Movement.IsMoveInput ||
                     (!Movement.IsMoving && AnimationParameters.ForwardSpeed < Movement.WalkSpeed))
            {
                return (Character.eAnimationType)JumpAnimationState.eAnimationType.JUMP_STANDING_LANDIND;
            }
            else
            {
                AvatarIKGoal footType = Movement.FrontFoot;
                switch (Movement.CurrentMoveType)
                {
                    case eMoveType.WALK:
                        return footType == AvatarIKGoal.LeftFoot
                            ? (Character.eAnimationType)JumpAnimationState.eAnimationType.JUMP_WALK_LEFT_FOOT_LANDING
                            : (Character.eAnimationType)JumpAnimationState.eAnimationType.JUMP_WALK_RIGHT_FOOT_LANDING;
                    case eMoveType.RUN:
                        return footType == AvatarIKGoal.LeftFoot
                            ? (Character.eAnimationType)JumpAnimationState.eAnimationType.JUMP_RUN_LEFT_FOOT_LANDING
                            : (Character.eAnimationType)JumpAnimationState.eAnimationType.JUMP_RUN_RIGHT_FOOT_LANDING;
                    case eMoveType.SPRINT:
                        return footType == AvatarIKGoal.LeftFoot
                            ? (Character.eAnimationType)JumpAnimationState.eAnimationType.JUMP_SPRINT_LEFT_FOOT_LANDING
                            : (Character.eAnimationType)JumpAnimationState.eAnimationType.JUMP_SPRINT_RIGHT_FOOT_LANDING;
                }
            }

            return (Character.eAnimationType)eAnimationType.AIRBORNE_LANDIND;
        }

        public override bool IsPlayingAnimation(in float normalizedTime)
        {
            if (base.IsPlayingAnimation(normalizedTime))
                return true;
            if (Animancer.States.TryGet(_fall, out var state) && state.IsPlaying &&
                state.NormalizedTime < normalizedTime)
                return true;
            if (Animancer.States.TryGet(_landingMixer, out state) && state.IsPlaying &&
                state.NormalizedTime < normalizedTime)
                return true;
            return false;
        }

        protected override AnimancerState InternalPlayAnimation(in Character.eAnimationType animationType,
            in float animationSpeed = 1f, in Func<AnimancerState, float> calculateSpeedFunc = null, in eLayerType layerType = eLayerType.BASE)
        {
            AnimancerState state = null;

            if (animationType == (Character.eAnimationType)eAnimationType.AIRBORNE_FALL)
            {
                state = Animation.PlayAnimation(animationType, _fall, animationSpeed, calculateSpeedFunc, layerType);
            }
            else
            {
                _landingType = animationType switch
                {
                    (Character.eAnimationType)JumpAnimationState.eAnimationType.JUMP_STANDING_LANDIND => eLandingType.STAND,
                    (Character.eAnimationType)JumpAnimationState.eAnimationType.JUMP_WALK_LEFT_FOOT_LANDING or (Character.eAnimationType)JumpAnimationState.eAnimationType.JUMP_RUN_LEFT_FOOT_LANDING
                        or (Character.eAnimationType)JumpAnimationState.eAnimationType.JUMP_SPRINT_LEFT_FOOT_LANDING => eLandingType.LEFT_FOOT,
                    (Character.eAnimationType)JumpAnimationState.eAnimationType.JUMP_WALK_RIGHT_FOOT_LANDING or (Character.eAnimationType)JumpAnimationState.eAnimationType.JUMP_RUN_RIGHT_FOOT_LANDING
                        or (Character.eAnimationType)JumpAnimationState.eAnimationType.JUMP_SPRINT_RIGHT_FOOT_LANDING => eLandingType.RIGHT_FOOT,
                    (Character.eAnimationType)eAnimationType.AIRBORNE_LANDIND => eLandingType.AIRBORNE,
                    _ => eLandingType.STAND
                };

                switch (animationType)
                {
                    case (Character.eAnimationType)eAnimationType.AIRBORNE_LANDIND:
                    case (Character.eAnimationType)JumpAnimationState.eAnimationType.JUMP_STANDING_LANDIND:
                    case (Character.eAnimationType)JumpAnimationState.eAnimationType.JUMP_WALK_LEFT_FOOT_LANDING:
                    case (Character.eAnimationType)JumpAnimationState.eAnimationType.JUMP_WALK_RIGHT_FOOT_LANDING:
                    case (Character.eAnimationType)JumpAnimationState.eAnimationType.JUMP_RUN_LEFT_FOOT_LANDING:
                    case (Character.eAnimationType)JumpAnimationState.eAnimationType.JUMP_RUN_RIGHT_FOOT_LANDING:
                    case (Character.eAnimationType)JumpAnimationState.eAnimationType.JUMP_SPRINT_LEFT_FOOT_LANDING:
                    case (Character.eAnimationType)JumpAnimationState.eAnimationType.JUMP_SPRINT_RIGHT_FOOT_LANDING:
                    {
                        state = Animation.PlayAnimation(animationType, _landingMixer, animationSpeed, calculateSpeedFunc, layerType);
                        if (state.IsValid() && _landingMixer.State != null)
                            _landingMixer.State.Parameter = GetLandingAnimationParameter();
                        break;
                    }
                }
            }

            ExecuteMixerRecalculateWeights(state);
            SetUseRootMotion(state);
            return state;
        }

        public override AnimancerState PlayAnimation(in Character.eAnimationType animationType,
            in float animationSpeed = 1f, in Func<AnimancerState, float> calculateSpeedFunc = null, in eLayerType layerType = eLayerType.BASE)
        {
            switch (animationType)
            {
                case (Character.eAnimationType)eAnimationType.AIRBORNE_FALL:
                    Movement.CurrentMoveType = eMoveType.AIRBORNE;
                    return InternalPlayAnimation(animationType, animationSpeed, calculateSpeedFunc, layerType);
                default:
                    if (!Animation.IsLocal)
                    {
                        if (animationType == (Character.eAnimationType)JumpAnimationState.eAnimationType.JUMP_STANDING_LANDIND)
                            Movement.CurrentMoveType = eMoveType.STAND;
                    }

                    return InternalPlayAnimation(animationType, animationSpeed, calculateSpeedFunc, layerType);
            }
        }

        protected virtual Vector2 GetLandingAnimationParameter()
        {
            return new Vector2((int)_landingType, (int)Movement.CurrentMoveType);
        }

        protected virtual void OnAnimation_FallEndEvent()
        {
            _playingAniState = null;
        }

        protected virtual void OnAnimation_LandingEndEvent()
        {
            _playingAniState = null;
            Movement.IsCorrectionRootMotion = false;
            Movement.SetAirbornStateGrounderIKWeight(false);
            
            if (Character is LocalCharacter localCharacter)
                localCharacter.CharacterLookDir = Vector3.zero;
        }

        public void OnAnimation_EnableAnyMovementLandingEvent(int landingType)
        {
            if ((int)_landingType != landingType)
                return;

            _enableAnyMovement = true;
            Movement.UseRootMotionRotation = CharacterRootMotionMode.Ignore;

            if (_landingType == eLandingType.STAND)
                Movement.UseHorizontalRootMotionPosition = CharacterRootMotionMode.Override;
        }

        public void OnAnimation_RestoreGrounderIKWeightEvent(int landingType)
        {
            if ((Movement.CurrentMoveType == eMoveType.RUN || Movement.CurrentMoveType == eMoveType.SPRINT) &&
                (int)_landingType != landingType)
                return;

            Movement.SetAirbornStateGrounderIKWeight(false);
        }
    }
}