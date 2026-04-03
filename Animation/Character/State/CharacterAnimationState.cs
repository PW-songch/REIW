using UnityEngine;
using REIW.EventLock;

namespace REIW.Animations.Character
{
    public class CharacterAnimationState :
        AnimationState<eAnimationType, eStateType, CharacterAnimationState, CharacterAnimationStateMachine, CharacterAnimation>,
        ICheckEventLockState,
        ICameraEventType
    {
        public override eStateType StateType => eStateType.NONE;

        protected CharacterAnimationMovement Movement => Animation?.Movement;
        protected CharacterAnimationParameters AnimationParameters => Animation?.Parameters;

        private CharacterBase _character;
        protected CharacterBase Character
        {
            get
            {
                if (_character == null)
                    _character = Animation?.Character;
                return _character;
            }
        }

        private LocalCharacter _localCharacter;
        protected LocalCharacter LocalCharacter
        {
            get
            {
                var character = Character;
                if (character != null)
                {
                    if (character.IsLocalCharacter)
                    {
                        _localCharacter =  character as LocalCharacter;

                        if (_localCharacter == null)
                        {
                            Debug.LogError("_localCharacter == null!! ");
                        }
                    }
                }
                else
                {
                    Debug.LogError("Unknown Error: Character == null!! ");
                }
                return _localCharacter;
            }
        }
        
        protected CharacterAnimationStateMachine CharacterStateMacnine => (CharacterAnimationStateMachine)OwnerStateMachine;
        protected CharacterAnimationState CurrentState => OwnerStateMachine.CurrentState;
        protected eStateType CurrentStateType => Animation.CurrentStateType;
        protected eStateType CurrentStateBaseType => Animation.CurrentBaseStateType;

        public override (bool IsChange, eStateType Next) NextStateType
        {
            get
            {
                var immediateNext = CharacterStateMacnine.CheckImmediateNextStateType();
                if (immediateNext.HasNext)
                    return immediateNext;

                var stateType = StateType;
                if (!CanExitState)
                    return (true, stateType);

                var nextType = GetNextStateTypeByModules();
                if (nextType.Valid)
                    return (true, nextType.Next);

                var inpuType = Movement.CurrentActionInputType;
                if (inpuType != eCharacterActionInputType.NONE)
                {
                    var sType = inpuType.ConvertStateType();
                    if (CanChangeNextState(sType))
                        return (true, sType);
                }

                if (CanChangeNextAirborneState)
                    return (true, GetStateType(eStateType.AIRBORNE));

                return (false, DefaultStateType);
            }
        }

        protected virtual bool CanChangeNextDashState => StateType != GetStateType(eStateType.DASH);
        protected virtual bool CanChangeNextJumpState => StateType != GetStateType(eStateType.JUMP);
        protected virtual bool CanChangeNextParkourState => StateType != GetStateType(eStateType.PARKOUR);
        protected virtual bool CanChangeNextGrappleState => StateType != GetStateType(eStateType.GRAPPLE);
        protected virtual bool CanChangeNextAirborneState => StateType != GetStateType(eStateType.AIRBORNE) && Movement.IsAirborne;
        protected virtual bool CanChangeNextMountState => StateType != GetStateType(eStateType.MOUNT) && (Movement.IsGrounded || !Movement.IsAirborne); // Mount 입력에 따른 전환/사용 가능 조건 (ex: 공중 사용 불가)
        protected virtual bool CanChangeNextInteractionState => StateType != GetStateType(eStateType.INTERACTION);
        protected virtual bool CanChangeNextGatheringState => StateType != GetStateType(eStateType.GATHERING);
        protected virtual bool CanChangeNextFishingState => StateType != GetStateType(eStateType.FISHING);
        protected virtual bool CanChangeNextEmoteState => StateType == GetStateType(eStateType.IDLE)
                                                          || StateType == GetStateType(eStateType.WALK)
                                                          || StateType == GetStateType(eStateType.RUN)
                                                          || StateType == GetStateType(eStateType.EMOTE);

        protected override void Start()
        {
            base.Start();
        }

        protected override void BaseOnEnable()
        {
            base.BaseOnEnable();
        }

        protected override void LateUpdate()
        {
            base.LateUpdate();
            Animation.ResetAnimationEventData();
        }

        protected override void UpdateAnimationParameters()
        {
            Movement.UpdateForwardSpeedParameter();
            Movement.UpdateVerticalSpeedParameter();
        }

        protected override void SetUseRootMotion(AnimationClip animationClip)
        {
            var motionSettings = Animation.GetRootMotionSettings(animationClip);
            Movement.UseHorizontalRootMotionPosition = motionSettings.posXZ ? CharacterRootMotionMode.Override : CharacterRootMotionMode.Ignore;
            Movement.UseVerticalRootMotionPosition = motionSettings.posY ? CharacterRootMotionMode.Override : CharacterRootMotionMode.Ignore;
            Movement.UseRootMotionRotation = motionSettings.rotation ? CharacterRootMotionMode.Override : CharacterRootMotionMode.Ignore;
        }

        protected bool CanChangeNextState(in eCharacterActionInputType inputType)
        {
            return CanChangeNextState(inputType.ConvertStateType());
        }

        protected bool CanChangeNextState(in eStateType nextState)
        {
            if (CanChangeNextStateByModules(nextState))
                return true;

            switch (GetBaseStateType(nextState))
            {
                case eStateType.DASH:
                    return CanChangeNextDashState;
                case eStateType.JUMP:
                    return CanChangeNextJumpState;
                case eStateType.PARKOUR:
                    return CanChangeNextParkourState;
                case eStateType.GRAPPLE:
                    return CanChangeNextGrappleState;
                case eStateType.MOUNT:
                    return CanChangeNextMountState;
                case eStateType.INTERACTION:
                    return CanChangeNextInteractionState;
                case eStateType.GATHERING:
                    return CanChangeNextGatheringState;
                case eStateType.FISHING:
                    return CanChangeNextFishingState;
                case eStateType.AIRBORNE:
                    return CanChangeNextAirborneState;
                case eStateType.EMOTE:
                    return CanChangeNextEmoteState;
            }

            return true;
        }

        protected override void InitializeDefineStateType()
        {
            _defineStateType = new DefineStateType();
        }
        
        /// <summary>
        /// 스태미나 액션 실행 시도 - 검증 후 가능하면 소모 처리하고 이벤트 발생
        /// </summary>
        /// <param name="staminaActionType">실행하려는 스태미나 액션 타입</param>
        /// <returns>true: 실행 성공, false: 스태미나 부족으로 실행 불가</returns>
        protected bool CanExecuteStaminaAction(EnumCategory staminaActionType)
        {
            // 스태미나 검증 및 차감 시도
            if (LocalCharacter.StaminaValidator.CanExecuteStaminaAction(staminaActionType) == false)
            {
                // 스태미나 부족 - 실패 처리는 StaminaValidator 내부에서 이벤트 발생
                LogUtil.Log($"Stamina is not enough to execute action: {staminaActionType}".Color(Color.cyan));
                return false;
            }
            
            // 스태미나 소모 성공 - 상태 변경 이벤트 발생
            ChangeStaminaActionType(staminaActionType);
            
            return true;
        }

        /// <summary>
        /// 스태미나 소모 성공 판정 - 상태 변경 이벤트 발생
        /// </summary>
        /// <param name="staminaActionType"></param>
        protected void ChangeStaminaActionType(EnumCategory staminaActionType)
        {
            if (IsLocal)
                Character.EventBus.Post<ICharacterStateEventListener>(_ => _.OnExecuteStaminaActionType(staminaActionType));
        }

        public virtual eEventLockType CurrentEventLockType => eEventLockType.None;
        public virtual eEventLockType ReleaseEventLockType => eEventLockType.None;
        public virtual IngameCameraSystem_Event.CameraEventType CameraEventType =>IngameCameraSystem_Event.CameraEventType.Default;

        public virtual Vector3 CameraEventOffset
        {
            get;
            set;
        }
    }
}