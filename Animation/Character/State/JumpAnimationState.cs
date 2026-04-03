using System;
using Animancer;
using Animancer.Units;
using UnityEngine;

namespace REIW.Animations.Character
{
    public class JumpAnimationState : AirborneAnimationState
    {
        [AnimationType(eStateType.JUMP)]
        public enum eAnimationType : uint
        {
            TYPE_START = Animations.Character.eAnimationType.JUMP_TYPE_START,
            JUMP_STANDING,
            JUMP_LEFT_FOOT,
            JUMP_RIGHT_FOOT,
            JUMP_STANDING_LANDIND,
            JUMP_WALK_LEFT_FOOT_LANDING,
            JUMP_WALK_RIGHT_FOOT_LANDING,
            JUMP_RUN_LEFT_FOOT_LANDING,
            JUMP_RUN_RIGHT_FOOT_LANDING,
            JUMP_SPRINT_LEFT_FOOT_LANDING,
            JUMP_SPRINT_RIGHT_FOOT_LANDING,
            TYPE_END
        }

        public enum eJumpType
        {
            NONE = 0,
            STAND,
            LEFT_FOOT,
            RIGHT_FOOT,
        }

        public override eStateType StateType => eStateType.JUMP;

        [SerializeField] private LinearMixerTransition _standJumpMixer;
        [SerializeField] private LinearMixerTransition _leftFootMoveJumpMixer;
        [SerializeField] private LinearMixerTransition _rightFootMoveJumpMixer;

        [SerializeField]
        private CharacterRootMotionMode _useHorizontalRootMotionPosition = CharacterRootMotionMode.Ignore;

        [SerializeField]
        private CharacterRootMotionMode _useVerticalRootMotionPosition = CharacterRootMotionMode.Ignore;

        [Tooltip("제자리 연속 점프시 점프 애니메이션의 NormalizedTime")] [SerializeField, Range(0, 1)]
        private float _standContinuousJumpNormalizedTime = 0.2f;

        [Tooltip("착지 애니메이션의 NormalizedTime 내에 점프시 연속 점프로 판정하는 값")] [SerializeField, Range(0, 1)]
        private float _continuousJumpCheckLandingNormalizedTime = 0.2f;

        [Tooltip("연속 점프시의 점프 애니메이션 VerticalSpeed 파라미터 값")] [SerializeField, MetersPerSecondPerSecond]
        private float _continuousJumpVerticalSpeed = float.MaxValue;

        [Tooltip("연속 점프 가능한 점프 유지 시간")] [SerializeField, Seconds(Rule = Validate.Value.IsNotNegative)]
        private float _continuousJumpValidJumpTime = 0.2f;

        [Tooltip("이동 연속 점프의 딜레이 시간")] [SerializeField, Seconds(Rule = Validate.Value.IsNotNegative)]
        private float _continuousJumpDelayTime = 0.3f;

        [Tooltip("걷기 연속 점프의 딜레이 시간")] [SerializeField, Seconds(Rule = Validate.Value.IsNotNegative)]
        private float _walkingContinuousJumpDelayTime = 0.5f;

        [Tooltip("이동 점프 착지로 판정하는 점프 거리")] [SerializeField, Meters(Rule = Validate.Value.IsNotNegative)]
        private float _moveLandingJumpDistance = 2f;

        [Tooltip("점프 애니메이션 VerticalSpeed 파라미터의 최저 값")] [SerializeField, MetersPerSecondPerSecond]
        private float _minJumpVerticalSpeed = -3f;

        private AnimancerState _playingJumpAniState;
        private bool _isStartJump = false;
        private bool _isInStateContinuousJump = false;
        private float _jumpStartTime;
        private float _lastJumpTime;
        private eJumpType _currentJumpType = eJumpType.NONE;
        private eJumpType _nextJumpType = eJumpType.NONE;
        private Vector3 _jumpPosition;

        public override (bool IsChange, eStateType Next) NextStateType
        {
            get
            {
                var nextState = base.NextStateType;
                if (nextState.IsChange)
                    return nextState;

                switch (_landingType)
                {
                    case eLandingType.STAND:
                    case eLandingType.AIRBORNE:
                        return (true, DefaultStateType);
                    case eLandingType.LEFT_FOOT:
                    case eLandingType.RIGHT_FOOT:
                        AnimationParameters.ReservedMoveType = LocomotionAnimationState.eMovementType.STOP;
                        return (true, GetStateType(eStateType.RUN));
                }

                return (false, DefaultStateType);
            }
        }

        public override bool CanEnterState => Movement.IsGrounded;
        public override bool CanExitState => !Movement.IsJumpInput &&
                                             (base.CanExitState || (_playingAniState == null && !Movement.IsGrounded));

        protected override bool CanChangeNextAirborneState => base.CanChangeNextAirborneState && CanExitState;

        private float ContinuousJumpDelayTime =>
            Movement.IsWalking ? _walkingContinuousJumpDelayTime : _continuousJumpDelayTime;

        private bool IsContinuousJump => Time.time - _lastJumpTime <= ContinuousJumpDelayTime;
        private bool IsPossibleContinuousJump => Time.time - _jumpStartTime > _continuousJumpValidJumpTime;
        private bool ApplyTakeTurnsJumping => _prevStateType != GetStateType(eStateType.AIRBORNE) &&
                                              _prevStateType != GetStateType(eStateType.GRAPPLE);

        protected override void OnEnable()
        {
            base.OnEnable();

            PlayJumpAnimation();
        }

        protected override void OnDisable()
        {
            if (_isStartJump && !_isLanding)
                _lastJumpTime = Time.time;

            _isInStateContinuousJump = false;

            base.OnDisable();
        }

        protected override void Reset()
        {
            base.Reset();

            _jumpStartTime = 0f;
            _isStartJump = false;
            _jumpPosition = Vector3.zero;
            _nextJumpType = eJumpType.NONE;
            _landingType = eLandingType.NONE;
            _playingJumpAniState = null;

            if (Movement)
            {
                Movement.ForceFindGroundedFoot = false;
                Movement.IsLanding = false;
            }

            if (Character is LocalCharacter localCharacter)
                localCharacter.UpdateRootmotionVelocity = true;
        }

        public override bool LateUpdateState()
        {
            if (_isLanding && !Movement.IsGrounded)
            {
                _playingAniState = null;
                return false;
            }

            if (!base.LateUpdateState())
                return false;

            UpdateJump();

            return true;
        }

        protected override void UpdateAnimationParameters()
        {
            Movement.UpdateForwardSpeedParameter();
            if (_isStartJump || _playingJumpAniState == null)
            {
                Movement.UpdateVerticalSpeedParameter(Movement.IsAirborne);

                float verticalSpeed = Movement.VerticalSpeedParameter;

                if (_isInStateContinuousJump && verticalSpeed > _continuousJumpVerticalSpeed)
                    AnimationParameters.VerticalSpeed = _continuousJumpVerticalSpeed;

                if (!Movement.IsAirborne)
                {
                    if (verticalSpeed < _minJumpVerticalSpeed)
                        AnimationParameters.VerticalSpeed = _minJumpVerticalSpeed;
                }
            }
        }

        protected override bool UpdateAnyMovement()
        {
            if (!_enableAnyMovement)
                return false;

            switch (_landingType)
            {
                case eLandingType.STAND:
                    if (Movement.IsJumpInput)
                    {
                        if (IsPossibleContinuousJump)
                            PlayJumpAnimation();
                    }
                    else if (Movement.IsAnyMovementInput)
                    {
                        _playingAniState = null;
                    }
                    break;
                case eLandingType.AIRBORNE:
                    if (Movement.IsAnyMovementInput)
                        _playingAniState = null;
                    break;
                case eLandingType.LEFT_FOOT:
                case eLandingType.RIGHT_FOOT:
                    if (Movement.IsJumpInput)
                    {
                        if (IsPossibleContinuousJump)
                            PlayJumpAnimation();
                    }
                    else if (Movement.IsAnyActionInput || !Movement.IsMoveInput ||
                             (Movement.IsWalkInput ? Movement.CurrentMoveType != eMoveType.WALK : Movement.CurrentMoveType == eMoveType.WALK))
                    {
                        _playingAniState = null;
                    }
                    break;
            }

            return true;
        }

        private void UpdateJump()
        {
            if (_isLanding || _playingJumpAniState != null)
                return;

            PlayJumpAnimation();
        }

        private void PlayJumpAnimation()
        {
            if (!Movement.IsGrounded)
                return;

            _isInStateContinuousJump = false;

            if (ApplyTakeTurnsJumping && _nextJumpType == eJumpType.NONE && IsContinuousJump)
            {
                if (_playingAniState == null ||
                    _playingAniState.NormalizedTime < _continuousJumpCheckLandingNormalizedTime)
                    _isInStateContinuousJump = true;

                switch (_currentJumpType)
                {
                    case eJumpType.LEFT_FOOT:
                        _nextJumpType = eJumpType.RIGHT_FOOT;
                        break;
                    case eJumpType.RIGHT_FOOT:
                        _nextJumpType = eJumpType.LEFT_FOOT;
                        break;
                }
            }

            if (Movement.CurrentMoveType != eMoveType.DASH && (!Movement.IsMoveInput ||
                                                               (!Movement.IsMoving &&
                                                                AnimationParameters.ForwardSpeed <
                                                                Movement.WalkSpeed)))
            {
                _playingAniState = InternalPlayAnimation((Character.eAnimationType)eAnimationType.JUMP_STANDING);
                if (Movement.IsContinuousJump || (IsContinuousJump && _landingType == eLandingType.STAND))
                    _playingAniState.NormalizedTime = _standContinuousJumpNormalizedTime;

                Movement.CurrentMoveType = eMoveType.STAND;
                _currentJumpType = eJumpType.STAND;
                _isInStateContinuousJump = false;
            }
            else
            {
                AvatarIKGoal footType = _nextJumpType switch
                {
                    eJumpType.NONE => Movement.FrontFoot,
                    eJumpType.LEFT_FOOT => AvatarIKGoal.LeftFoot,
                    eJumpType.RIGHT_FOOT => AvatarIKGoal.RightFoot,
                    _ => Movement.FrontFoot,
                };

                switch (footType)
                {
                    case AvatarIKGoal.LeftFoot:
                        _currentJumpType = eJumpType.LEFT_FOOT;
                        _playingAniState = InternalPlayAnimation((Character.eAnimationType)eAnimationType.JUMP_LEFT_FOOT);
                        break;
                    case AvatarIKGoal.RightFoot:
                        _currentJumpType = eJumpType.RIGHT_FOOT;
                        _playingAniState = InternalPlayAnimation((Character.eAnimationType)eAnimationType.JUMP_RIGHT_FOOT);
                        break;
                }

                if (_isInStateContinuousJump)
                {
                    if (!Mathf.Approximately(_continuousJumpVerticalSpeed, float.MaxValue))
                        Movement.VerticalSpeedParameter = _continuousJumpVerticalSpeed;
                    else
                        _isInStateContinuousJump = false;

                    Character.StartJump(false);
                }
            }

            if (_playingAniState != null)
            {
                Reset();

                _jumpStartTime = Time.time;
                _nextJumpType = eJumpType.NONE;
                _jumpPosition = Character.CharacterTransform.position;
                _playingJumpAniState = _playingAniState;

                Movement.UseHorizontalRootMotionPosition = _useHorizontalRootMotionPosition;
                Movement.UseVerticalRootMotionPosition = _useVerticalRootMotionPosition;
                Movement.IsCorrectionRootMotion = false;
                Movement.IsJumpInput = false;

                if (Character is LocalCharacter localCharacter)
                {
                    localCharacter.UpdateRootmotionVelocity = false;
                    localCharacter.SetMoveSpeed((int)Movement.CurrentMoveType);
                }
            }
            else
            {
                Movement.CheckGroundedMaxStepGrounderIK();
            }
        }

        private AnimancerState PlayJumpAnimation_OnNetwork(in Character.eAnimationType animationType,
            in float animationSpeed = 1f, in Func<AnimancerState, float> calculateSpeedFunc = null, in eLayerType layerType = eLayerType.BASE)
        {
            switch (animationType)
            {
                case (Character.eAnimationType)eAnimationType.JUMP_STANDING:
                    Movement.CurrentMoveType = eMoveType.STAND;
                    _currentJumpType = eJumpType.STAND;
                    _playingJumpAniState = InternalPlayAnimation((Character.eAnimationType)eAnimationType.JUMP_STANDING,
                        animationSpeed, calculateSpeedFunc, layerType);
                    break;
                case (Character.eAnimationType)eAnimationType.JUMP_LEFT_FOOT:
                    _currentJumpType = eJumpType.LEFT_FOOT;
                    _playingJumpAniState = InternalPlayAnimation((Character.eAnimationType)eAnimationType.JUMP_LEFT_FOOT,
                        animationSpeed, calculateSpeedFunc, layerType);
                    break;
                case (Character.eAnimationType)eAnimationType.JUMP_RIGHT_FOOT:
                    _currentJumpType = eJumpType.RIGHT_FOOT;
                    _playingJumpAniState = InternalPlayAnimation((Character.eAnimationType)eAnimationType.JUMP_RIGHT_FOOT,
                        animationSpeed, calculateSpeedFunc, layerType);
                    break;
            }

            _jumpPosition = Character.CharacterTransform.position;
            return _playingJumpAniState;
        }

        protected override void UpdateLanding()
        {
            if (!_isStartJump || _playingJumpAniState == null)
                return;

            base.UpdateLanding();
        }

        protected override bool PlayLandingAnimation()
        {
            if (base.PlayLandingAnimation())
            {
                _playingAniState.MoveTime(0, true);

                _playingJumpAniState = null;
                _lastJumpTime = Time.time;
                Movement.IsCorrectionRootMotion =
                    _landingType != eLandingType.STAND && _landingType != eLandingType.AIRBORNE &&
                    (Movement.CurrentMoveType == eMoveType.RUN || Movement.CurrentMoveType == eMoveType.SPRINT);
                return true;
            }

            return false;
        }

        protected override Character.eAnimationType GetLandingAnimationType()
        {
            if (Movement.IsFalling)
            {
                Movement.CurrentMoveType = eMoveType.AIRBORNE;
                return (Character.eAnimationType)AirborneAnimationState.eAnimationType.AIRBORNE_LANDIND;
            }

            Vector3 position = Character.CharacterTransform.position;
            position.y = 0f;
            _jumpPosition.y = 0f;

            if (!Movement.IsMoveInput && Vector3.Distance(_jumpPosition, position) < _moveLandingJumpDistance)
            {
                Movement.CurrentMoveType = eMoveType.STAND;
                return (Character.eAnimationType)eAnimationType.JUMP_STANDING_LANDIND;
            }
            else
            {
                if (Movement.CurrentMoveType == eMoveType.SPRINT && !Movement.IsSprintInput)
                    Movement.CurrentMoveType = eMoveType.RUN;

                switch (Movement.CurrentMoveType)
                {
                    case eMoveType.STAND:
                        return (Character.eAnimationType)eAnimationType.JUMP_STANDING_LANDIND;
                    case eMoveType.WALK:
                        return _currentJumpType == eJumpType.LEFT_FOOT
                            ? (Character.eAnimationType)eAnimationType.JUMP_WALK_LEFT_FOOT_LANDING
                            : (Character.eAnimationType)eAnimationType.JUMP_WALK_RIGHT_FOOT_LANDING;
                    case eMoveType.RUN:
                        return _currentJumpType == eJumpType.LEFT_FOOT
                            ? (Character.eAnimationType)eAnimationType.JUMP_RUN_LEFT_FOOT_LANDING
                            : (Character.eAnimationType)eAnimationType.JUMP_RUN_RIGHT_FOOT_LANDING;
                    case eMoveType.SPRINT:
                        return _currentJumpType == eJumpType.LEFT_FOOT
                            ? (Character.eAnimationType)eAnimationType.JUMP_SPRINT_LEFT_FOOT_LANDING
                            : (Character.eAnimationType)eAnimationType.JUMP_SPRINT_RIGHT_FOOT_LANDING;
                    case eMoveType.AIRBORNE:
                        return (Character.eAnimationType)AirborneAnimationState.eAnimationType.AIRBORNE_LANDIND;
                }
            }

            return (Character.eAnimationType)AirborneAnimationState.eAnimationType.AIRBORNE_LANDIND;
        }

        public override bool IsPlayingAnimation(in float normalizedTime)
        {
            if (base.IsPlayingAnimation(normalizedTime))
                return true;
            if (Animancer.States.TryGet(_standJumpMixer, out var state) && state.IsPlaying &&
                state.NormalizedTime < normalizedTime)
                return true;
            if (Animancer.States.TryGet(_leftFootMoveJumpMixer, out state) && state.IsPlaying &&
                state.NormalizedTime < normalizedTime)
                return true;
            if (Animancer.States.TryGet(_rightFootMoveJumpMixer, out state) && state.IsPlaying &&
                state.NormalizedTime < normalizedTime)
                return true;
            return false;
        }

        protected override AnimancerState InternalPlayAnimation(in Character.eAnimationType animationType,
            in float animationSpeed = 1f, in Func<AnimancerState, float> calculateSpeedFunc = null, in eLayerType layerType = eLayerType.BASE)
        {
            AnimancerState state = null;

            switch (animationType)
            {
                case (Character.eAnimationType)eAnimationType.JUMP_STANDING:
                    Movement.VerticalSpeedParameter = _standJumpMixer.DefaultParameter;
                    state = Animation.PlayAnimation(animationType, _standJumpMixer, animationSpeed, calculateSpeedFunc, layerType);
                    break;
                case (Character.eAnimationType)eAnimationType.JUMP_LEFT_FOOT:
                    Movement.VerticalSpeedParameter = _leftFootMoveJumpMixer.DefaultParameter;
                    state = Animation.PlayAnimation(animationType, _leftFootMoveJumpMixer, animationSpeed, calculateSpeedFunc, layerType);
                    break;
                case (Character.eAnimationType)eAnimationType.JUMP_RIGHT_FOOT:
                    Movement.VerticalSpeedParameter = _rightFootMoveJumpMixer.DefaultParameter;
                    state = Animation.PlayAnimation(animationType, _rightFootMoveJumpMixer, animationSpeed, calculateSpeedFunc, layerType);
                    break;
                default:
                    return base.InternalPlayAnimation(animationType, animationSpeed, calculateSpeedFunc, layerType);
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
                case (Character.eAnimationType)eAnimationType.JUMP_STANDING:
                case (Character.eAnimationType)eAnimationType.JUMP_LEFT_FOOT:
                case (Character.eAnimationType)eAnimationType.JUMP_RIGHT_FOOT:
                    return PlayJumpAnimation_OnNetwork(animationType, animationSpeed, calculateSpeedFunc, layerType);
                default:
                    return base.PlayAnimation(animationType, animationSpeed, calculateSpeedFunc, layerType);
            }
        }

        public void StartJump()
        {
            _isStartJump = true;
            Movement.IsAirborne = true;
        }

        public void OnAnimation_StartJumpEvent(int jumpType)
        {
            if (_isStartJump || (int)_currentJumpType != jumpType)
                return;

            if (!Character.StartJump(false))
                ExitState = true;
        }
    }
}