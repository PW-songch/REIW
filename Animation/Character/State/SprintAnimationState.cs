using System;
using System.Threading;
using Animancer;
using Animancer.Units;
using Cysharp.Threading.Tasks;
using UnityEngine;

namespace REIW.Animations.Character
{
    public class SprintAnimationState : RunAnimationState
    {
        [AnimationType(eStateType.SPRINT)]
        public enum eAnimationType : uint
        {
            TYPE_START = Animations.Character.eAnimationType.SPRINT_TYPE_START,
            SPRINT = eMoveAnimationType.MOVE + TYPE_START,
            SPRINT_QUICK_TURN_LEFT = eMoveAnimationType.QUICK_TURN_LEFT + TYPE_START,
            SPRINT_QUICK_TURN_RIGHT,
            SPRINT_STAND_STOP,
            SPRINT_MOVE_STOP,
            TYPE_END
        }

        public override eStateType StateType => eStateType.SPRINT;

        [Tooltip("스탑 애니메이션이 실행되기 까지의 입력 간격")] [SerializeField, Seconds(Rule = Validate.Value.IsNotNegative)]
        private float _stopInputInterval = 0.05f;

        private float _noInputTime;
        private CancellationTokenSource _cts;

        public event Action StartSprintEvent;

        protected override int AnimationStartTypeIndex => (int)eAnimationType.TYPE_START;

        protected override bool CanExitStopState
        {
            get
            {
                if (Movement.IsAnyActionInput || Movement.IsAirborne)
                    return true;
                if (!Movement.IsMoveInput || UpdateTurn())
                    return false;
                return true;
            }
        }

        protected override bool CanExitMoveState
        {
            get
            {
                if (Movement.IsAnyActionInput || Movement.IsAirborne)
                    return true;
                if (_prevMovementType == eMovementType.QUICK_TURN)
                    return true;
                if (Movement.IsSprint)
                    return false;
                return true;
            }
        }

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
                    if (Movement.IsSprint)
                        return (true, GetStateType(eStateType.SPRINT));
                }

                // if (!isMoveInput)
                //     AnimationParameters.ReservedMoveType = eMovementType.STOP;
                return (true, GetStateType(eStateType.RUN));
            }
        }

        protected override void OnEnable()
        {
            // if (CanExecuteStaminaAction(eStaminaActionType.Sprint) == false)
            // {
            //     Reset();
            //     ExitState = true;
            //     return;
            // }
            
            BaseOnEnable();

            _currentForwardSpeed = AnimationParameters.ForwardSpeed;

            if (!ApplyReservedMoveType())
            {
                PlayMoveAnimation(true);
                Movement.IsCorrectionRootMotion = true;
            }
            
            _cts?.Cancel();
            _cts = new CancellationTokenSource();

            Character.CharacterEffectSound.LoopingFxUniTask(eKnownEffect.SPRINT_Loop, _cts.Token).Forget();
        }

        protected override void OnDisable()
        {
            base.OnDisable();

            if (Character is LocalCharacter localCharacter)
                localCharacter.ResetRootMotionVelocityQueue();
            
            StartSprintEvent = null;
            
            _cts?.Cancel();
            _cts?.Dispose();
            _cts = null;
            
            Character.CharacterEffectSound.StopLoopingFx(eKnownEffect.SPRINT_Loop);
        }

        protected override void SetState(in eMovementType type)
        {
            switch (type)
            {
                case eMovementType.MOVE:
                    if (StateType == GetStateType(eStateType.SPRINT))
                        Movement.CurrentMoveType = eMoveType.SPRINT;
                    break;
            }

            base.SetState(type);
        }

        protected override void UpdateStop()
        {
            if (_currentMovementType != eMovementType.MOVE ||
                _currentForwardSpeed <= AnimationParameters.ForwardSpeed || Movement.IsMoveInput)
            {
                _noInputTime = 0f;
                return;
            }

            if (_noInputTime == 0f)
                _noInputTime = Time.time;

            if (Time.time - _noInputTime >= _stopInputInterval)
            {
                var state = PlayAnimation(eMoveAnimationType.MOVE_STOP);
                SetAnimationEndEvent(state, OnAnimation_EndEvent);
            }
        }

        protected override void UpdateCurrentState()
        {
            // 스프린트 상태에서만 스태미나 검사
            if (Movement.CurrentMoveType == eMoveType.SPRINT)
            {
                bool canSprint = LocalCharacter.StaminaValidator.CanExecuteStaminaAction(EnumCategory.LocomotionStateSprinting);
                
                if (!canSprint)
                {
                    Movement.IsSprintInput = false;
                    ExitState = true;
                    Movement.CurrentMoveType = eMoveType.RUN;
                    Character.LockMoveInput = false;
                    return;
                }
            }
            
            base.UpdateCurrentState();
        }

        protected override void UpdateMove()
        {
        }

        protected override void PlayMoveAnimation(in bool checkFoot = false)
        {
            if (((LocalCharacter)Character).StaminaValidator.CanExecuteStaminaAction(EnumCategory.LocomotionStateSprinting) == false)
            {
                // 스태미나 부족: 스프린트 종료 후 일반 이동 상태로 전환
                Movement.IsSprintInput = false;
                Movement.CurrentMoveType = eMoveType.RUN; // 또는 RUN, WALK 등 프로젝트 규칙에 맞게

                // 필요하다면 스프린트 애니메이션 대신 일반 달리기 애니메이션 재생
                base.PlayMoveAnimation(checkFoot);
                return;
            }
            
            base.PlayMoveAnimation(in checkFoot);
            StartSprintEvent?.Invoke();
        }

        protected override void PlayStopAnimation()
        {
            if (Movement.IsMoveInput && IsMoving)
            {
                if (_moveStop.IsValid)
                {
                    var state = PlayAnimation(eMoveAnimationType.MOVE_STOP);
                    SetAnimationEndEvent(state, OnAnimation_EndEvent);
                }
            }
            else
            {
                if (_standStop.IsValid)
                {
                    var state = PlayAnimation(eMoveAnimationType.STAND_STOP);
                    SetAnimationEndEvent(state, OnAnimation_EndEvent);
                }
            }

            if (_currentMovementType != eMovementType.STOP)
                SetState(eMovementType.IDLE);
        }

        public void SetMoveMixer(LinearMixerTransition moveMixer)
        {
            _moveMixer = moveMixer;
        }

        protected override AnimancerState InternalPlayAnimation(in Character.eAnimationType animationType,
            in float animationSpeed = 1f, in Func<AnimancerState, float> calculateSpeedFunc = null, in eLayerType layerType = eLayerType.BASE)
        {
            AnimancerState state = null;

            switch (animationType)
            {
                case Animations.Character.eAnimationType.RUN:
                case (Character.eAnimationType)eAnimationType.SPRINT:
                    return base.InternalPlayAnimation(animationType, animationSpeed, calculateSpeedFunc, layerType);
                default:
                    var moveAniType = ConvertAnimationType(animationType);
                    if (moveAniType != eMoveAnimationType.NONE)
                        state = PlayAnimation(moveAniType, animationSpeed, calculateSpeedFunc, layerType);
                    else
                        return base.InternalPlayAnimation(animationType, animationSpeed, calculateSpeedFunc, layerType);
                    break;
            }

            ExecuteMixerRecalculateWeights(state);
            SetUseRootMotion(state);
            return state;
        }

        protected override void OnAnimation_EndEvent()
        {
            Character.LockMoveInput = false;

            if (Movement.IsMoveInput)
            {
                if (_currentMovementType == eMovementType.QUICK_TURN)
                    AnimationParameters.ForwardSpeed = Movement.RunSpeed;

                PlayMoveAnimation(_currentMovementType == eMovementType.QUICK_TURN);
            }
            else
            {
                if (_currentMovementType == eMovementType.QUICK_TURN)
                    PlayStopAnimation();
                else
                    PlayIdleAnimation();
            }

            Movement.RootMotionPositionCorrectionFunc -= QuickTurnAnimationCorrectionPosition;
            Movement.RootMotionRotationCorrectionFunc -= TurnAnimationCorrectionRotation;
        }
        
        private void Reset()
        {
            // 입력/플래그 원복
            Movement.IsSprintInput = false;
            Movement.IsDashInput = false;
            Character.LockMoveInput = false;

            // 진행중 애니메이션/상태 정리
            _noInputTime = 0f;
            _playingAniState = null;
            _currentMovementType = eMovementType.IDLE;
            AnimationParameters.ReservedMoveType = eMovementType.IDLE;

            // 루트모션 복구
            Movement.UseHorizontalRootMotionPosition = CharacterRootMotionMode.Ignore;
            Movement.UseVerticalRootMotionPosition = CharacterRootMotionMode.Ignore;
            Movement.UseRootMotionRotation = CharacterRootMotionMode.Ignore;
            Movement.IsCorrectionRootMotion = false;
            Movement.RootMotionPositionCorrectionFunc -= QuickTurnAnimationCorrectionPosition;
            Movement.RootMotionRotationCorrectionFunc -= TurnAnimationCorrectionRotation;

            // 로컬 캐릭터 루트모션 큐 정리
            if (Character is LocalCharacter localCharacter)
                localCharacter.ResetRootMotionVelocityQueue();

            // 루프 이펙트/토큰 정리
            _cts?.Cancel();
            _cts?.Dispose();
            _cts = null;
            Character.CharacterEffectSound.StopLoopingFx(eKnownEffect.SPRINT_Loop);
        }
    }
}
