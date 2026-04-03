using System;
using Animancer;
using Animancer.Units;
using UnityEngine;
using static Animancer.Validate;

namespace REIW.Animations.Character
{
    public interface IWallFailState
    {
        void OnWallFail();
    }

    public abstract class LocomotionAnimationState : CharacterAnimationState, IWallFailState
    {
        public enum eMovementType
        {
            NONE,
            IDLE,
            STOP,
            MOVE,
            TURN,
            QUICK_TURN,
        }

        public enum eMoveAnimationType
        {
            NONE,
            MOVE_START,
            MOVE,
            TURN_LEFT,
            TURN_RIGHT,
            QUICK_TURN_LEFT,
            QUICK_TURN_RIGHT,
            STAND_STOP,
            MOVE_STOP,
        }

        [SerializeField] protected ClipTransition _moveStart;
        [SerializeField] protected LinearMixerTransition _moveMixer;
        [Tooltip("왼발로 이동 시작시 _moveMixer의 NormalizedTime")] [SerializeField, Range(0, 1)]
        private float _leftFootStartMoveNormalizedTime;
        [Tooltip("오른발로 이동 시작시 _moveMixer의 NormalizedTime")] [SerializeField, Range(0, 1)]
        private float _rightFootStartMoveNormalizedTime;

        [SerializeField] protected ClipTransition _turnLeft;
        [SerializeField] protected ClipTransition _turnRight;
        [SerializeField] protected CharacterAnimationRotationData _turnLeftRotationData;
        [SerializeField] protected CharacterAnimationRotationData _turnRightRotationData;

        [SerializeField, Degrees(Rule = Value.IsNotNegative)]
        protected float _turnAngle = 145;

        [SerializeField, DegreesPerSecond(Rule = Value.IsNotNegative)]
        protected float _turnRootMotionRotationSpeed = 2f;

        [SerializeField, Range(0, 1)] protected float _turnRootMotionRoationSpeedNormalizedTime = 0.2f;

        [SerializeField] protected ClipTransition _quickTurnLeft;
        [SerializeField] protected ClipTransition _quickTurnRight;
        [SerializeField] protected CharacterAnimationRotationData _quickTurnLeftRotationData;
        [SerializeField] protected CharacterAnimationRotationData _quickTurnRightRotationData;

        [SerializeField, MetersPerSecond(Rule = Value.IsNotNegative)]
        protected float _quickTurnMoveSpeed = 2;

        [SerializeField, Degrees(Rule = Value.IsNotNegative)]
        protected float _quickTurnAngle = 145;

        [SerializeField, DegreesPerSecond(Rule = Value.IsNotNegative)]
        protected float _quickTurnRootMotionRotationSpeed = 2f;

        [Tooltip("퀵턴 애니메이션 실행 후 연속적으로 퀵턴 애니메이션이 실행될 수 있는 퀵턴 애니메이션의 NormalizedTime")] [SerializeField, Range(0, 1)]
        protected float _continuousQuickTurnNormalizedTime = 0.7f;

        [Tooltip("스탑 애니메이션 실행 중 턴 애니메이션이 실행될 수 있는 스탑 애니메이션의 NormalizedTime")] [SerializeField, Range(0, 1)]
        protected float _turnEnableStopNormalizedTime = 1f;

        [SerializeField] protected ClipTransition _standStop;
        [SerializeField] protected ClipTransition _moveStop;

        [SerializeField, Meters(Rule = Value.IsNotNegative)]
        private float _checkMovingDistance = 0f;

        protected eMovementType _currentMovementType;
        protected eMovementType _prevMovementType;
        protected float _currentForwardSpeed;
        protected AnimancerState _playingTurnState;

        private bool _enableStopQuickTurn;
        private bool _enableAnyMovement;

        protected virtual int AnimationStartTypeIndex => 0;
        public virtual bool IsStopping => Animancer.States.TryGet(_moveStop, out var state) && state.IsPlaying &&
                                          state.NormalizedTime >= _turnEnableStopNormalizedTime;

        protected virtual bool CanExitStopState => Movement.IsAnyActionInput || Movement.IsAirborne;
        protected virtual bool CanExitMoveState => Movement.IsAnyMovementInput || Movement.IsAirborne;
        protected virtual bool CanExitTurnState => Movement.IsAnyActionInput || Movement.IsAirborne;
        protected virtual bool CanExitQuickTurnState => Movement.IsCancelCurrentActionInput || Movement.IsAirborne;

        protected virtual bool IsMoving => _checkMovingDistance > 0f
            ? Movement.CurrentMoveVelocity.sqrMagnitude > _checkMovingDistance
            : Movement.IsMoving;

        public override bool CanEnterState => Movement.IsGrounded;
        public override bool CanExitState
        {
            get
            {
                if (base.CanExitState)
                    return true;

                switch (_currentMovementType)
                {
                    case eMovementType.STOP:
                        return CanExitStopState;
                    case eMovementType.MOVE:
                        return CanExitMoveState;
                    case eMovementType.TURN:
                        return CanExitTurnState;
                    case eMovementType.QUICK_TURN:
                        return CanExitQuickTurnState;
                }

                return true;
            }
        }

        public eMovementType CurrentMovementType => _currentMovementType;
        public eMovementType PrevMovementType => _prevMovementType;

        protected bool IsEndTurn => !_playingTurnState.IsValid() || !_playingTurnState.IsCurrent || _playingTurnState.NormalizedTime >= 1f;

        #region Property name provider
        #if UNITY_EDITOR
        public const string MoveStartName = nameof(_moveStart);
        public const string MoveMixerName = nameof(_moveMixer);

        public const string LeftFootStartMoveNormalizedTimeName = nameof(_leftFootStartMoveNormalizedTime);
        public const string RightFootStartMoveNormalizedTimeName = nameof(_rightFootStartMoveNormalizedTime);

        public const string TurnLeftName = nameof(_turnLeft);
        public const string TurnRightName = nameof(_turnRight);
        public const string TurnLeftRotationDataName = nameof(_turnLeftRotationData);
        public const string TurnRightRotationDataName = nameof(_turnRightRotationData);
        public const string TurnAngleName = nameof(_turnAngle);
        public const string TurnRootMotionRotationSpeedName = nameof(_turnRootMotionRotationSpeed);
        public const string TurnRootMotionRoationSpeedNormalizedTimeName = nameof(_turnRootMotionRoationSpeedNormalizedTime);

        public const string QuickTurnLeftName = nameof(_quickTurnLeft);
        public const string QuickTurnRightName = nameof(_quickTurnRight);
        public const string QuickTurnLeftRotationDataName = nameof(_quickTurnLeftRotationData);
        public const string QuickTurnRightRotationDataName = nameof(_quickTurnRightRotationData);
        public const string QuickTurnMoveSpeedName = nameof(_quickTurnMoveSpeed);
        public const string QuickTurnAngleName = nameof(_quickTurnAngle);
        public const string QuickTurnRootMotionRotationSpeedName = nameof(_quickTurnRootMotionRotationSpeed);
        public const string ContinuousQuickTurnNormalizedTimeName = nameof(_continuousQuickTurnNormalizedTime);

        public const string StandStopName = nameof(_standStop);
        public const string MoveStopName = nameof(_moveStop);
        public const string TurnEnableStopNormalizedTimeName = nameof(_turnEnableStopNormalizedTime);
        #endif
        #endregion

        protected override void OnEnable()
        {
            base.OnEnable();

            ChangeStaminaActionType(EnumCategory.LocomotionStateIdle);

            _currentForwardSpeed = AnimationParameters.ForwardSpeed;
            Movement.ForceFindGroundedFoot = false;
        }

        protected override void OnDisable()
        {
            base.OnDisable();

            _currentMovementType = eMovementType.NONE;
            _prevMovementType = eMovementType.NONE;
            _enableStopQuickTurn = false;
            _enableAnyMovement = false;

            Movement.RootMotionTurnDirection = eTurnDirection.NONE;
            Movement.IsApplyRootMotionRotationWithCharacterLookDir = false;
            Movement.IsCorrectionRootMotion = false;
            Movement.RootMotionPositionCorrectionFunc -= QuickTurnAnimationCorrectionPosition;
            Movement.RootMotionRotationCorrectionFunc -= TurnAnimationCorrectionRotation;
        }

        protected virtual void SetState(in eMovementType type)
        {
            if (!enabled || _currentMovementType == type)
                return;

            _prevMovementType = _currentMovementType;
            _currentMovementType = type;

            switch (_prevMovementType)
            {
                case eMovementType.MOVE:
                    Movement.IsCorrectionRootMotion = false;
                    break;
                case eMovementType.TURN:
                case eMovementType.QUICK_TURN:
                    Movement.RootMotionTurnDirection = eTurnDirection.NONE;
                    Movement.IsApplyRootMotionRotationWithCharacterLookDir = false;
                    Character.LockMoveInput = false;
                    break;
            }

            switch (_currentMovementType)
            {
                case eMovementType.MOVE:
                    if (Character is LocalCharacter localCharacter)
                        localCharacter.SetMoveSpeed((int)Movement.CurrentMoveType);
                    break;
                case eMovementType.IDLE:
                case eMovementType.STOP:
                    Movement.CurrentMoveType = eMoveType.STAND;
                    break;
            }
        }

        public override bool LateUpdateState()
        {
            if (!base.LateUpdateState())
                return false;

            UpdateTurn();
            UpdateStop();
            UpdateMove();
            UpdateCurrentState();

            _currentForwardSpeed = AnimationParameters.ForwardSpeed;

            return true;
        }

        protected override void UpdateAnimationParameters()
        {
            if (_currentMovementType != eMovementType.TURN)
                Movement.UpdateForwardSpeedParameter();

            Movement.UpdateVerticalSpeedParameter();
        }

        protected virtual void UpdateCurrentState()
        {
            switch (_currentMovementType)
            {
                case eMovementType.QUICK_TURN:
                    if (_enableStopQuickTurn && !Movement.IsMoveInput)
                    {
                        _enableStopQuickTurn = false;
                        var state = PlayAnimation(eMoveAnimationType.MOVE_STOP);
                        SetAnimationEndEvent(state, OnAnimation_EndEvent);
                    }

                    if (_enableAnyMovement)
                    {
                        if (Movement.IsMoveInput)
                            Movement.UseRootMotionRotation = CharacterRootMotionMode.Ignore;
                        if (Movement.IsAnyActionInput || Movement.IsSprintInput)
                            ExitState = true;
                    }

                    break;
            }
        }

        protected virtual void UpdateMove()
        {
            if (AnimationParameters.ForwardSpeed > _currentForwardSpeed)
            {
                if (_currentMovementType == eMovementType.IDLE || _currentMovementType == eMovementType.STOP)
                    PlayMoveAnimation();

                if (_currentMovementType == eMovementType.TURN || _currentMovementType == eMovementType.QUICK_TURN)
                    return;

                if (_prevMovementType != eMovementType.STOP && _prevMovementType != eMovementType.TURN)
                {
                    if (_prevStateType != GetStateType(eStateType.WALK) && IsMoving && AnimationParameters.ForwardSpeed <= Movement.RunSpeed)
                        AnimationParameters.ForwardSpeed = Movement.RunSpeed;
                }
                else
                {
                    if (AnimationParameters.ForwardSpeed < Movement.WalkSpeed)
                        AnimationParameters.ForwardSpeed = Movement.WalkSpeed + (Movement.RunSpeed - Movement.WalkSpeed) * 0.5f;
                }
            }
        }

        protected virtual bool UpdateTurn()
        {
            if (_currentMovementType != eMovementType.MOVE && _currentMovementType != eMovementType.STOP)
            {
                if (_currentMovementType == eMovementType.TURN)
                    return false;
                if (_currentMovementType == eMovementType.QUICK_TURN)
                    return _enableAnyMovement && PlayQuickTurnAnimation();
                return PlayTurnAnimation();
            }

            if (_currentMovementType == eMovementType.MOVE)
                return IsEndTurn && PlayQuickTurnAnimation();
            else
                return PlayTurnAnimation();
        }

        protected virtual void UpdateStop()
        {
            if (_currentMovementType != eMovementType.MOVE ||
                _currentForwardSpeed <= AnimationParameters.ForwardSpeed || Movement.IsMoveInput)
                return;

            PlayStopAnimation();
        }

        protected virtual void PlayIdleAnimation()
        {
            SetState(eMovementType.IDLE);
        }

        protected virtual void PlayMoveAnimation(in bool checkFoot = false)
        {
            if (!_moveMixer.IsValid())
                return;

            if (Movement.IsMoveInput)
            {
                if (_moveStart.IsValid() && _moveStart.State is not { IsCurrent: true } &&
                    _currentMovementType != eMovementType.QUICK_TURN &&
                    (AnimationParameters.ForwardSpeed < Movement.WalkSpeed || !IsMoving))
                {
                    Movement.IsCorrectionRootMotion = false;
                    var state = PlayAnimation(eMoveAnimationType.MOVE_START);
                    SetAnimationEndEvent(state, OnAnimation_EndEvent);
                }
                else
                {
                    if (_moveMixer.State is { IsCurrent: true })
                    {
                        SetState(eMovementType.MOVE);
                        return;
                    }

                    if (checkFoot)
                        CheckFrontFootOnMoveAnimation();

                    PlayAnimation(eMoveAnimationType.MOVE);
                }
            }
            else
            {
                PlayIdleAnimation();
            }
        }

        protected virtual bool PlayTurnAnimation(in bool InForce = false)
        {
            if (!Movement.IsMoveInput || !Movement.GetTurnAngles(Character.CharacterLookDir, Character.Forward,
                    Character.Up, out float currentAngle, out float targetAngle))
                return false;

            float deltaAngle = Mathf.DeltaAngle(currentAngle, targetAngle);
            if (Mathf.Abs(deltaAngle) > _turnAngle)
            {
                var direction = deltaAngle < 0 ? eTurnDirection.LEFT : eTurnDirection.RIGHT;
                var transition = GetClipTransition(direction == eTurnDirection.LEFT ?
                    eMoveAnimationType.TURN_LEFT : eMoveAnimationType.TURN_RIGHT);

                if (transition.IsValid() && (InForce || !Movement.IsMoving) &&
                    (_playingTurnState is not { IsPlaying: true } || _playingTurnState != transition.State) &&
                    !Animation.StateMachine.IsPlayingPrevStateAnimation)
                {
                    Movement.RootMotionRotationCorrectionFunc -= TurnAnimationCorrectionRotation;
                    Movement.RootMotionRotationCorrectionFunc += TurnAnimationCorrectionRotation;
                    Movement.RootMotionTurnDirection = direction;

                    _playingTurnState = PlayAnimation(direction == eTurnDirection.LEFT ?
                        eMoveAnimationType.TURN_LEFT : eMoveAnimationType.TURN_RIGHT);
                    SetAnimationEndEvent(_playingTurnState, OnAnimation_TurnEndEvent);

                    Movement.IsApplyRootMotionRotationWithCharacterLookDir = true;
                    if (_turnRootMotionRotationSpeed > 0f)
                        Movement.RootMotionRotationSpeed = _turnRootMotionRotationSpeed;
                    return true;
                }
            }

            return false;
        }

        protected virtual bool PlayQuickTurnAnimation()
        {
            if (!Movement.IsMoveInput || !Movement.GetTurnAngles(Character.CharacterLookDir, Character.Forward,
                    Character.Up, out float currentAngle, out float targetAngle))
                return false;

            float deltaAngle = Mathf.DeltaAngle(currentAngle, targetAngle);
            if (Mathf.Abs(deltaAngle) > _quickTurnAngle)
            {
                if (AnimationParameters.ForwardSpeed >= _quickTurnMoveSpeed && Movement.IsMoving)
                {
                    ClipTransition turn = deltaAngle < 0 ? _quickTurnLeft : _quickTurnRight;
                    if (turn.IsValid() && (_playingTurnState is not { IsCurrent: true } || _playingTurnState.NormalizedTime > _continuousQuickTurnNormalizedTime) && !Animation.IsStopping)
                    {
                        if (_playingTurnState is { IsPlaying: true })
                        {
                            Movement.RootMotionRotationCorrectionFunc -= TurnAnimationCorrectionRotation;
                            Movement.RootMotionRotationCorrectionFunc += TurnAnimationCorrectionRotation;
                        }

                        Movement.RootMotionTurnDirection = deltaAngle < 0 ? eTurnDirection.LEFT : eTurnDirection.RIGHT;

                        _playingTurnState = PlayAnimation(Movement.RootMotionTurnDirection == eTurnDirection.LEFT ?
                            eMoveAnimationType.QUICK_TURN_LEFT : eMoveAnimationType.QUICK_TURN_RIGHT);
                        SetAnimationEndEvent(_playingTurnState, OnAnimation_EndEvent);

                        Character.LockMoveInput = true;
                        Movement.IsApplyRootMotionRotationWithCharacterLookDir = true;
                        if (_quickTurnRootMotionRotationSpeed > 0f)
                            Movement.RootMotionRotationSpeed = _quickTurnRootMotionRotationSpeed;
                        return true;
                    }
                }
            }

            if (PlayTurnAnimation())
                return true;

            return false;
        }

        protected virtual void PlayStopAnimation()
        {
            if (IsMoving || (_moveStart.IsValid() && _moveStart.State is { IsPlaying: true }))
            {
                if (_moveStop.IsValid())
                {
                    var state = PlayAnimation(eMoveAnimationType.MOVE_STOP);
                    SetAnimationEndEvent(state, OnAnimation_EndEvent);
                }
            }
            else
            {
                if (_standStop.IsValid())
                {
                    var state = PlayAnimation(eMoveAnimationType.STAND_STOP);
                    SetAnimationEndEvent(state, OnAnimation_EndEvent);
                }
            }

            if (_currentMovementType != eMovementType.STOP)
                SetState(eMovementType.IDLE);
        }

        protected virtual Vector3 QuickTurnAnimationCorrectionPosition(Vector3 InDeltaPosition)
        {
            if (InDeltaPosition == Vector3.zero)
                return InDeltaPosition;

            var up = Character.Up;
            var planarDelta = Vector3.ProjectOnPlane(InDeltaPosition, up);
            var forward = Vector3.ProjectOnPlane(Character.CharacterLookDir, up).normalized;
            var forwardDist = Vector3.Dot(planarDelta, forward);
            return forward * forwardDist;
        }

        protected virtual Quaternion TurnAnimationCorrectionRotation(Quaternion InDeltaRotation, Quaternion InRootRotation)
        {
            if (InDeltaRotation.eulerAngles == Vector3.zero || _playingTurnState == null)
                return InDeltaRotation;

            CharacterAnimationRotationData rotationData = null;
            if (_currentMovementType == eMovementType.TURN)
                rotationData = _playingTurnState.Clip == _turnLeft.Clip ? _turnLeftRotationData : _turnRightRotationData;
            else
                rotationData = _playingTurnState.Clip == _quickTurnLeft.Clip ? _quickTurnLeftRotationData : _quickTurnRightRotationData;

            if (rotationData != null)
                InDeltaRotation = rotationData.GetRotation(_playingTurnState.Time) * Quaternion.Inverse(rotationData.GetRotation(_playingTurnState.Time - Time.deltaTime));

            return InDeltaRotation;
        }

        public override bool IsPlayingAnimation(in float normalizedTime)
        {
            if (Animancer.States.TryGet(_turnLeft, out var state) && state.IsPlaying &&
                state.NormalizedTime < normalizedTime)
                return true;
            if (Animancer.States.TryGet(_turnRight, out state) && state.IsPlaying &&
                state.NormalizedTime < normalizedTime)
                return true;
            if (Animancer.States.TryGet(_quickTurnLeft, out state) && state.IsPlaying &&
                state.NormalizedTime < normalizedTime)
                return true;
            if (Animancer.States.TryGet(_quickTurnRight, out state) && state.IsPlaying &&
                state.NormalizedTime < normalizedTime)
                return true;
            if (Animancer.States.TryGet(_standStop, out state) && state.IsPlaying &&
                state.NormalizedTime < normalizedTime)
                return true;
            if (Animancer.States.TryGet(_moveStop, out state) && state.IsPlaying &&
                state.NormalizedTime < normalizedTime)
                return true;
            return false;
        }

        protected virtual bool ApplyReservedMoveType()
        {
            var moveType = AnimationParameters.ReservedMoveType;
            if (moveType == eMovementType.NONE)
                return false;

            switch (moveType)
            {
                case eMovementType.IDLE:
                    PlayIdleAnimation();
                    return true;
                case eMovementType.STOP:
                    if (!Movement.IsMoveInput)
                        PlayStopAnimation();
                    break;
                case eMovementType.MOVE:
                    PlayMoveAnimation();
                    break;
                case eMovementType.TURN:
                    PlayTurnAnimation();
                    break;
                case eMovementType.QUICK_TURN:
                    PlayQuickTurnAnimation();
                    break;
            }

            AnimationParameters.ReservedMoveType = eMovementType.NONE;

            return _currentMovementType == moveType;
        }

        protected virtual eAnimationType ConvertAnimationType(in eMoveAnimationType moveAnimationType)
        {
            return (eAnimationType)((int)moveAnimationType + AnimationStartTypeIndex);
        }

        protected virtual eMoveAnimationType ConvertAnimationType(in eAnimationType animationType)
        {
            return (eMoveAnimationType)((int)animationType - AnimationStartTypeIndex);
        }

        protected virtual AnimancerState PlayAnimation(in eMoveAnimationType moveAnimationType,
            in float animationSpeed = 1f, in Func<AnimancerState, float> calculateSpeedFunc = null, in eLayerType layerType = eLayerType.BASE)
        {
            _enableStopQuickTurn = false;
            _enableAnyMovement = false;

            AnimancerState state = null;

            switch (moveAnimationType)
            {
                case eMoveAnimationType.MOVE_START:
                    SetState(eMovementType.MOVE);
                    state = Animation.PlayAnimation(ConvertAnimationType(moveAnimationType),
                        _moveStart, animationSpeed, calculateSpeedFunc, layerType);
                    break;
                case eMoveAnimationType.MOVE:
                    SetState(eMovementType.MOVE);
                    state = Animation.PlayAnimation(ConvertAnimationType(moveAnimationType),
                        _moveMixer, animationSpeed, calculateSpeedFunc, layerType);
                    break;
                case eMoveAnimationType.TURN_LEFT:
                    SetState(eMovementType.TURN);
                    state = Animation.PlayAnimation(ConvertAnimationType(moveAnimationType),
                        _turnLeft, animationSpeed, calculateSpeedFunc, layerType);
                    break;
                case eMoveAnimationType.TURN_RIGHT:
                    SetState(eMovementType.TURN);
                    state =  Animation.PlayAnimation(ConvertAnimationType(moveAnimationType),
                        _turnRight, animationSpeed, calculateSpeedFunc, layerType);
                    break;
                case eMoveAnimationType.QUICK_TURN_LEFT:
                    SetState(eMovementType.QUICK_TURN);
                    state =  Animation.PlayAnimation(ConvertAnimationType(moveAnimationType),
                        _quickTurnLeft, animationSpeed, calculateSpeedFunc, layerType);
                    break;
                case eMoveAnimationType.QUICK_TURN_RIGHT:
                    SetState(eMovementType.QUICK_TURN);
                    state =  Animation.PlayAnimation(ConvertAnimationType(moveAnimationType),
                        _quickTurnRight, animationSpeed, calculateSpeedFunc, layerType);
                    break;
                case eMoveAnimationType.STAND_STOP:
                    SetState(eMovementType.STOP);
                    state =  Animation.PlayAnimation(ConvertAnimationType(moveAnimationType),
                        _standStop, animationSpeed, calculateSpeedFunc, layerType);
                    break;
                case eMoveAnimationType.MOVE_STOP:
                    SetState(eMovementType.STOP);
                    state =  Animation.PlayAnimation(ConvertAnimationType(moveAnimationType),
                        _moveStop, animationSpeed, calculateSpeedFunc, layerType);
                    break;
            }

            SetUseRootMotion(state);

            return state;
        }

        private ClipTransition GetClipTransition(eMoveAnimationType InMoveAnimationType)
        {
            return InMoveAnimationType switch
            {
                eMoveAnimationType.TURN_LEFT => _turnLeft,
                eMoveAnimationType.TURN_RIGHT => _turnRight,
                eMoveAnimationType.QUICK_TURN_LEFT => _quickTurnLeft,
                eMoveAnimationType.QUICK_TURN_RIGHT => _quickTurnRight,
                eMoveAnimationType.STAND_STOP => _standStop,
                eMoveAnimationType.MOVE_STOP => _moveStop,
                _ => null
            };
        }

        protected void CheckFrontFootOnMoveAnimation()
        {
            if (_moveMixer.State != null && (!IsLocal || !_moveMixer.State.IsPlaying))
            {
                if (!IsLocal)
                    Movement.EnableGrounderIK(true, true);

                var footType = Movement.FrontFoot;
                if (footType == AvatarIKGoal.LeftFoot)
                    _moveMixer.State.NormalizedTime = _leftFootStartMoveNormalizedTime;
                else if (footType == AvatarIKGoal.RightFoot)
                    _moveMixer.State.NormalizedTime = _rightFootStartMoveNormalizedTime;
            }
        }

        protected virtual void OnAnimation_EndEvent()
        {
            _enableStopQuickTurn = false;
            _enableAnyMovement = false;
            Character.LockMoveInput = false;

            if (Movement.IsMoveInput)
            {
                if (!PlayQuickTurnAnimation())
                    PlayMoveAnimation(_currentMovementType == eMovementType.QUICK_TURN);
            }
            else if (_currentMovementType == eMovementType.QUICK_TURN)
            {
                var state = PlayAnimation(eMoveAnimationType.MOVE_STOP);
                SetAnimationEndEvent(state, OnAnimation_EndEvent);
            }
            else
            {
                PlayIdleAnimation();
            }

            Movement.RootMotionPositionCorrectionFunc -= QuickTurnAnimationCorrectionPosition;
            Movement.RootMotionRotationCorrectionFunc -= TurnAnimationCorrectionRotation;
        }

        protected virtual void OnAnimation_TurnEndEvent()
        {
            if (!Movement.IsMoveInput && !IsEndTurn)
            {
                if (_playingTurnState != null && (!_playingTurnState.IsCurrent ||
                                                  _playingTurnState.NormalizedTime >
                                                  _turnRootMotionRoationSpeedNormalizedTime))
                    Movement.RootMotionRotationSpeed = 0f;
                return;
            }

            if (Movement.IsMoveInput)
            {
                if (!PlayTurnAnimation())
                    PlayMoveAnimation();
            }
            else
            {
                PlayIdleAnimation();
            }

            AnimationParameters.ForwardSpeed = 0f;

            if (_currentMovementType != eMovementType.TURN && _currentMovementType != eMovementType.QUICK_TURN)
            {
                Movement.UseRootMotionRotation = CharacterRootMotionMode.Ignore;
                Movement.RootMotionRotationSpeed = 0f;
            }
        }

        public void OnAnimation_CorrectionRootMotionQuickTurnEvent()
        {
            Movement.RootMotionPositionCorrectionFunc -= QuickTurnAnimationCorrectionPosition;
            Movement.RootMotionPositionCorrectionFunc += QuickTurnAnimationCorrectionPosition;
        }

        public void OnAnimation_ResetRootMotionRoationSpeedQuickTurnEvent()
        {
            Movement.RootMotionRotationSpeed = 0f;
        }

        public void OnAnimation_EnableStopQuickTurnEvent()
        {
            _enableStopQuickTurn = true;
        }

        public void OnAnimation_EnableAnyMovementQuickTurnEvent()
        {
            _enableAnyMovement = true;
            Character.LockMoveInput = false;
        }

        public void OnWallFail()
        {
            StateMachine.SetImmediateNextStateType(GetStateType(eStateType.MOVE_FAIL));
        }

        public bool CanOtherWhileMovement()
        {
            switch(_currentMovementType)
            {
                case eMovementType.TURN:
                case eMovementType.QUICK_TURN:
                    return false;
                case eMovementType.STOP:
                    if(!CanExitStopState)
                        return false;
                    break;
            }

            return true;
        }
    }
}
