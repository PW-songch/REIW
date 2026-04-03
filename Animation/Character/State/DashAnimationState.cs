using System;
using Animancer;
using Animancer.Units;
using UnityEngine;

namespace REIW.Animations.Character
{
    public class DashAnimationState : LocomotionAnimationState
    {
        [AnimationType(eStateType.DASH)]
        public enum eAnimationType : uint
        {
            TYPE_START = Animations.Character.eAnimationType.DASH_TYPE_START,
            DASH = eMoveAnimationType.MOVE + TYPE_START,
            DASH_STOP = eMoveAnimationType.MOVE_STOP + TYPE_START,
            TYPE_END
        }

        public override eStateType StateType => eStateType.DASH;

        [SerializeField] private ClipTransition _dash;

        [Tooltip("이동관련 입력을 잠그는 대쉬 애니메이션의 NormalizedTime")] [SerializeField, Range(0, 1)]
        private float _lockMoveInputDashNormalizedTime = 0.05f;

        [Tooltip("이동관련 입력을 잠금 해제하는 대쉬 애니메이션의 NormalizedTime")] [SerializeField, Range(0, 1)]
        private float _unlockMoveInputDashNormalizedTime = 0.9f;

        [SerializeField, DegreesPerSecond(Rule = Validate.Value.IsNotNegative)] [Tooltip("대쉬 애니메이션 동작간 회전 속도")]
        private float _dashOrientationSharpness = 30f;

        [SerializeField] private bool _applyRawRootMotion = true;

        private float _originalOrientationSharpness;
        private bool _isDash;
        private bool _isEnableQuickTurn;

        protected override int AnimationStartTypeIndex => (int)eAnimationType.TYPE_START;

        public override (bool IsChange, eStateType Next) NextStateType
        {
            get
            {
                var nextState = base.NextStateType;
                if (nextState.IsChange)
                    return nextState;

                if (Movement.IsMoveInput)
                {
                    if (_isDash && Movement.IsSprintInput)
                        return (true, GetStateType(eStateType.SPRINT));
                }
                else
                {
                    if (_isDash)
                        AnimationParameters.ReservedMoveType = eMovementType.STOP;
                }

                return (true, GetStateType(eStateType.RUN));
            }
        }

        public override bool CanEnterState => Movement.IsGrounded;
        public override bool ApplyRawRootMotion => _applyRawRootMotion;

        protected override bool CanExitMoveState =>
            Movement.IsAirborne || (!Character.LockMoveInput && Movement.IsJumpInput);

        protected override void OnEnable()
        {
            // if (Animancer.IsPlaying(_dash))
            //     return;
            //
            // if (CanExecuteStaminaAction(eStaminaActionType.Dash) == false)
            // {
            //     ExitState = true;
            //     UpdateStop();
            //     PlayStopAnimation();
            //     return;
            // }

            base.OnEnable();

            _originalOrientationSharpness = Character.CurrentOrientationSharpness;

            if (!ApplyReservedMoveType())
                PlayDashAnimation();
        }

        protected override void OnDisable()
        {
            base.OnDisable();

            if (!_isDash || (Movement.IsAnyActionInput && !Movement.IsJumpInput))
                Movement.IsSprintInput = false;
            else
                Movement.CurrentMoveType = eMoveType.SPRINT;

            _isDash = false;
            _isEnableQuickTurn = false;
            Character.LockMoveInput = false;
            Character.CurrentOrientationSharpness = _originalOrientationSharpness;

            if (Character is LocalCharacter localCharacter)
                localCharacter.ResetRootMotionVelocityQueue();
        }

        protected override void SetState(in eMovementType type)
        {
            switch (type)
            {
                case eMovementType.MOVE:
                    if (StateType == GetStateType(eStateType.DASH))
                        Movement.CurrentMoveType = eMoveType.DASH;
                    break;
            }

            base.SetState(type);
        }

        public override bool LateUpdateState()
        {
            if (_playingAniState is { IsCurrent: true })
            {
                if (_playingAniState.NormalizedTime > _unlockMoveInputDashNormalizedTime)
                {
                    _playingAniState = null;
                    Character.LockMoveInput = false;
                }
                else if (_playingAniState.NormalizedTime > _lockMoveInputDashNormalizedTime)
                {
                    Character.LockMoveInput = true;
                }
            }

            return base.LateUpdateState();
        }

        protected override void UpdateCurrentState()
        {
            switch (_currentMovementType)
            {
                case eMovementType.STOP:
                case eMovementType.QUICK_TURN:
                    if (_currentMovementType == eMovementType.STOP)
                    {
                        if (!Movement.IsMoveInput)
                            AnimationParameters.ForwardSpeed = 0f;
                    }

                    if (Movement.IsDashInput)
                        PlayDashAnimation();
                    break;
            }
        }

        protected override bool PlayQuickTurnAnimation()
        {
            if (!_isEnableQuickTurn && (_currentMovementType == eMovementType.MOVE ||
                                        _currentMovementType == eMovementType.QUICK_TURN))
                return false;

            if (base.PlayQuickTurnAnimation())
            {
                _isEnableQuickTurn = false;
                return true;
            }

            return false;
        }

        protected override void UpdateStop()
        {
            if (_currentMovementType == eMovementType.MOVE)
                _currentForwardSpeed = AnimationParameters.ForwardSpeed;
            base.UpdateStop();
        }

        protected override void PlayStopAnimation()
        {
            base.PlayStopAnimation();
            Movement.IsSprintInput = false;
        }

        private void PlayDashAnimation()
        {
            if (!_dash.IsValid) return;
            if (_isDash) return;
            if (!((LocalCharacter)Character).StaminaValidator.CanExecuteStaminaAction(EnumCategory.LocomotionStateDash)) return;

            Character.CurrentOrientationSharpness = _dashOrientationSharpness;
            Movement.ForceFindGroundedFoot = true;

            _playingAniState = InternalPlayAnimation((Character.eAnimationType)eAnimationType.DASH);
            SetAnimationEndEvent(_playingAniState, OnAnimation_DashEndEvent);
            
            // 스태미나 소모
            if(_playingAniState.IsActive)
                ((LocalCharacter)Character).ExecuteStaminaAction(EnumCategory.LocomotionStateDash);
            
            _isDash = true;
            _isEnableQuickTurn = false;
            Movement.IsDashInput = false;
        }

        public override bool IsPlayingAnimation(in float normalizedTime)
        {
            if (base.IsPlayingAnimation(normalizedTime))
                return true;
            if (Animancer.States.TryGet(_dash, out var state) && state.IsPlaying && state.NormalizedTime < normalizedTime)
                return true;
            return false;
        }

        protected override AnimancerState InternalPlayAnimation(in Character.eAnimationType animationType,
            in float animationSpeed = 1f, in Func<AnimancerState, float> calculateSpeedFunc = null, in eLayerType layerType = eLayerType.BASE)
        {
            AnimancerState state = null;

            switch (animationType)
            {
                case (Character.eAnimationType)eAnimationType.DASH:
                    SetState(eMovementType.MOVE);
                    state = Animation.PlayAnimation(animationType, _dash, animationSpeed, calculateSpeedFunc, layerType);
                    break;
                default:
                    var moveAniType = ConvertAnimationType(animationType);
                    if (moveAniType != eMoveAnimationType.NONE)
                        state = PlayAnimation(moveAniType, animationSpeed, calculateSpeedFunc, layerType);
                    break;
            }

            ExecuteMixerRecalculateWeights(state);
            SetUseRootMotion(state);
            return state;
        }

        protected override void OnAnimation_EndEvent()
        {
            if (Movement.IsMoveInput)
            {
                if (_currentMovementType != eMovementType.STOP)
                    _isDash = true;

                SetState(eMovementType.IDLE);
            }
            else
            {
                switch (_currentMovementType)
                {
                    case eMovementType.STOP:
                        _isDash = false;
                        if (Animancer.States.TryGet(_moveStop, out var state) && state.NormalizedTime > 0.9f)
                            SetState(eMovementType.IDLE);
                        break;
                    case eMovementType.QUICK_TURN:
                        _isDash = true;
                        SetState(eMovementType.IDLE);
                        break;
                }
            }

            Movement.UseRootMotionRotation = CharacterRootMotionMode.Ignore;
        }

        private void OnAnimation_DashEndEvent()
        {
            _isEnableQuickTurn = true;
            Character.LockMoveInput = false;
            Character.CurrentOrientationSharpness = _originalOrientationSharpness;

            if (!Movement.IsMoveInput)
            {
                Movement.UseRootMotionRotation = CharacterRootMotionMode.Override;
                PlayStopAnimation();
            }
            else
            {
                SetState(eMovementType.IDLE);
                Movement.UseRootMotionRotation = CharacterRootMotionMode.Ignore;
            }
        }

        public void OnAnimation_EnableQuickTurnEvent()
        {
            _isEnableQuickTurn = true;
        }
        
        private void Reset()
        {
            
        }
    }
}
