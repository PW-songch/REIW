
using System;
using Animancer;
using Animancer.FSM;
using RootMotion.FinalIK;
using UnityEngine;

namespace REIW.Animations.Character
{
    public partial class CharacterAnimation : AnimationBase<eAnimationType, eStateType, CharacterAnimationState, CharacterAnimationStateMachine, CharacterAnimation>
    {
        public CharacterBase Character => _clientCharacter?.LogicalCharacter;
        [SerializeField] private ClientCharacter _clientCharacter;
        public ClientCharacter ClientCharacter => _clientCharacter;
        
        public AnimancerEvents _animancerEvents;
        
        public CharacterAnimationMovement Movement => _movement;
        [SerializeField] private CharacterAnimationMovement _movement;

        public CharacterAnimationParameters Parameters => _parameters;
        [SerializeField] private CharacterAnimationParameters _parameters;

        public override bool IsLocal => Character?.IsLocalCharacter ?? false;

        public override eStateType CurrentBaseStateType => (eStateType)((int)_currentStateType % AnimationConsts.ANIMATION_STATETYPE_INTERVAL_UNIT);
        public override eStateType PrevBaseStateType => (eStateType)((int)_prevStateType % AnimationConsts.ANIMATION_STATETYPE_INTERVAL_UNIT);
        public override eStateType CurrentBaseSubstateType => (eStateType)((int)_currentSubstateType % AnimationConsts.ANIMATION_STATETYPE_INTERVAL_UNIT);
        public override eStateType PrevBaseSubstateType => (eStateType)((int)_prevSubstateType % AnimationConsts.ANIMATION_STATETYPE_INTERVAL_UNIT);

        private OwnerPlayerNetObject _ownerPlayerNetObject;

        public event Action<(AvatarIKGoal footType, float footPower, eKnownSfxSound groundTag)> FootStepEvent
        {
            add    { if (Movement != null) Movement.FootStepEvent += value; }
            remove { if (Movement != null) Movement.FootStepEvent -= value; }
        }

        protected override int AnimationTypeBitDigits => CharacterAnimationEnums.ANIMATION_TYPE_BIT_DIGITS;

        public float ForwardSpeedParameter
        {
            get => _parameters.ForwardSpeed;
            set => _parameters.ForwardSpeed = value;
        }

        public float VerticalSpeedParameter
        {
            get => _parameters.VerticalSpeed;
            set => _parameters.VerticalSpeed = value;
        }

        public bool IsStopping => StateMachine.Run.IsStopping || StateMachine.Sprint.IsStopping || StateMachine.Dash.IsStopping;
        public bool IsCurrentNormalMoveState => StateMachine.IsCurrentNormalMoveState;
        
        // 기존: public virtual bool IsMoving => IsMovingState(false);
        // 로컬이면 파라미터 체크(false), 원격이면 파라미터 체크(true) → NetworkCharacterAnimation의 IsMoving과 동일 동작
        public virtual bool IsMoving => IsMovingState(!IsLocal);

        protected override void Start()
        {
            base.Start();
            if (!IsLocal && Movement != null)
            {
                Movement.EnableIK(false);
            }
        }

        protected override void OnEnable()
        {
            base.OnEnable();
            
            if(Character)
                RegisterAnimationEventListeners(Character.EventBus);
        }

        protected override void OnDisable()
        {
            base.OnDisable();
            
            if(Character)
                UnregisterAnimationEventListeners(Character.EventBus);
        }

        protected override void OnDestroy()
        {
            base.OnDestroy();

            if (Character)
                Character.OnInitialized -= OnCharacterInitialized;
        }

        public override void Init(in EnumAttraction attractionType)
        {
            base.Init(attractionType);

            if (!IsLocal)
            {
                TryForceSetAnimationState(eStateType.NETWORK);
                PlayAnimation(eAnimationType.IDLE, layerType: eLayerType.BASE);
            }
        }

        protected override void InitializeComponents()
        {
            if (_clientCharacter == null)
            {
                _clientCharacter = GetComponent<ClientCharacter>();
                Debug.LogError("ClientCharacter has no client character.");
            }

            if (_animancerEvents == null)
            {
                _animancerEvents = GetComponent<AnimancerEvents>();
                Debug.LogError("AnimancerEvents have no animancer events.");
            }
            

            if (IsLocal)
                _ownerPlayerNetObject ??= Character.GetComponent<OwnerPlayerNetObject>();
        }

        protected override bool InitializeRootMotionSettings()
        {
            if (!IsLocal)
                return true;

            if (!base.InitializeRootMotionSettings())
                return false;

            string soName = string.Format(
                AnimationClipRootMotionSettingsSO.GetRootMotionSettingsSOFileNameFormat(eObjectType.Character),
                UserDataModel.Singleton.PlayerInfoData.Race.ToString().ToLower(),
                UserDataModel.Singleton.PlayerInfoData.Gender.ToString().ToLower());

            _rootMotionSettings = AssetManager.Singleton.GetAnimationClipRootMotionSettingsSO(
                $"{nameof(eObjectType.Character).ToLower()}/{soName}");
            return true;
        }

        protected override void InitializeStateMachine(in EnumAttraction attractionType)
        {
            base.InitializeStateMachine(attractionType);

            StateMachine.Walk?.SetMoveMixer(StateMachine.Run?.MoveMixer);
            StateMachine.Sprint?.SetMoveMixer(StateMachine.Run?.MoveMixer);
        }

        protected override void InitializeAnimationMovement()
        {
            base.InitializeAnimationMovement();
            Animancer.Animator.applyRootMotion = false;
            Movement.Initialize(this);
        }

        protected override void InitializeAnimationParameters()
        {
            Parameters.CreateParameters(_animancer);
        }

        public bool IsMovingState(bool checkParameters)
        {
            return Movement.IsMoving && (!checkParameters || ForwardSpeedParameter > 0f || VerticalSpeedParameter > 0f);
        }
        
        protected override AnimancerState InternalPlayAnimation(
            in eAnimationType animationType,
            in float animationSpeed = 1f,
            in Func<AnimancerState, float> calculateSpeedFunc = null,
            in uint layerIndex = 0)
        {
            if (IsLocal)
            {
                // 로컬은 기존 베이스 로직(트랜지션/믹서 등)을 그대로 사용
                return base.InternalPlayAnimation(animationType, animationSpeed, calculateSpeedFunc);
            }

            // ====== 원격(네트워크) 전용 흐름 (기존 NetworkCharacterAnimation 동작) ======
            var state = (CharacterAnimationState)GetAnimationState(animationType);
            if (!state)
                return null;

            var animancerState = state.PlayAnimation(animationType, animationSpeed, calculateSpeedFunc, eLayerType.BASE);

            if (state.StateType == CurrentStateType)
                return animancerState;

            if (StateMachine?.CurrentState != null)
                StateMachine.CurrentState.DisableStateNetwork();

            _prevStateType = StateMachine.CurrentState.StateType;
            StateMachine.CurrentState = state;

            StateMachine.CurrentState.EnableStateNetwork();
            _currentStateType = state.StateType;

            return animancerState;
        }

        protected override bool CheckAnimationState(CharacterAnimationStateMachine stateMachine,
            ref eStateType currentStateType, ref eStateType prevStateType)
        {
            if (!IsLocal)
                return false;

            return base.CheckAnimationState(stateMachine, ref currentStateType, ref prevStateType);
        }

        protected override AnimancerState PlayAnimancer(in eAnimationType animationType, ITransition transition,
            in float animationSpeed = 1f, in Func<AnimancerState, float> calculateSpeedFunc = null, in uint layerIndex = 0)
        {
            var state = base.PlayAnimancer(animationType, transition, animationSpeed, calculateSpeedFunc, layerIndex);
            if (state.IsValid())
            {
                ChangeAnimationNetObject(animationType, state.Speed);
                return state;
            }

            return null;
        }

        public override AnimancerState PlayAnimancerFade(in eAnimationType animationType, ITransition transition,
            in float fadein = float.MinValue, in float animationSpeed = 1.0f, in uint layerIndex = 0)
        {
            var state = base.PlayAnimancerFade(animationType, transition, fadein, animationSpeed, layerIndex);
            if (state.IsValid())
            {
                ChangeAnimationNetObject(animationType, state.Speed);
                return state;
            }

            return null;
        }

        public void SetPlayTargetAnimation(eAnimationType animationType)
        {
            animationType = EnumUtility.GetUnpackValue(animationType, AnimationTypeBitDigits);
            StateBehaviour state = GetAnimationState(animationType);
            if (state != null && state is PlayTargetAnimationState playTargetAnimationState)
                playTargetAnimationState.PlayAnimationType = animationType;
        }

        protected override bool ChangeAnimationNetObject(in eAnimationType animationType, in float animationSpeed)
        {
            if (!IsLocal)
                return false;
            
            if (base.ChangeAnimationNetObject(animationType, animationSpeed))
            {
                _ownerPlayerNetObject?.ChangeAnimType((int)animationType, animationSpeed);
                return true;
            }

            return false;
        }

        protected override bool TryForceSetAnimationState(CharacterAnimationStateMachine stateMachine,
            CharacterAnimationState state, in eStateType currentStateType, ref eStateType prevStateType)
        {
            if (!stateMachine.IsImmediateNextStateType(state))
                return false;

            return base.TryForceSetAnimationState(stateMachine, state, currentStateType, ref prevStateType);
        }

        public override bool TryForceSetAnimationState(in eStateType stateType)
        {
            StateMachine.SetImmediateNextStateType(stateType);
            return base.TryForceSetAnimationState(stateType);
        }
    }
}