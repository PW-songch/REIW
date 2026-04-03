using System;
using System.Collections.Generic;
using Animancer;
using Animancer.FSM;
using UnityEngine;

namespace REIW.Animations
{
    [DisallowMultipleComponent]
    public abstract class AnimationState<TAnimationType, TStateType, TState, TStateMachine, TAnimation> : StateBehaviour, IOwnedState<TState>
        where TAnimationType : Enum
        where TStateType : Enum
        where TState : AnimationState<TAnimationType, TStateType, TState, TStateMachine, TAnimation>
        where TStateMachine : AnimationStateMachine<TAnimationType, TStateType, TState, TStateMachine, TAnimation>
        where TAnimation : AnimationBase<TAnimationType, TStateType, TState, TStateMachine, TAnimation>
    {
        public abstract TStateType StateType { get; }
        public TStateType StateBaseType => GetBaseStateType(StateType);

        private List<IStateChangeModule<TStateType>> _stateChangeModuleList;

        public AnimancerComponent Animancer => Animation?.Animancer;
        protected TAnimation _animation;
        public TAnimation Animation => _animation;

        public StateMachine<TState> OwnerStateMachine => Animation.StateMachine;
        public TStateMachine StateMachine => (TStateMachine)OwnerStateMachine;
        public StateMachine<TState> OwnerSubstateMachine => Animation.SubstateMachine;
        public TStateMachine SubstateMachine => (TStateMachine)OwnerSubstateMachine;

        protected bool IsLocal => Animation.IsLocal;

        protected TStateType DefaultStateType => GetStateType(StateMachine.DefaultState.StateType);
        protected TStateType DefaultStateBaseType => StateMachine.DefaultState.StateBaseType;

        public virtual (bool IsChange, TStateType Next) NextStateType
        {
            get
            {
                var stateType = StateType;
                if (!CanExitState)
                    return (true, stateType);

                var next = GetNextStateTypeByModules();
                if (next.Valid)
                    return (true, next.Next);

                return (false, DefaultStateType);
            }
        }

        public override bool CanExitState => ExitState || CanExitStateByModules();
        public virtual bool ExitState { set; protected get; } = false;
        public virtual bool CanEnterFromCurrentState => true;

        public virtual Vector3 RootDeltaPosition => Animancer.Animator.deltaPosition;
        public virtual Quaternion RootDeltaRotation => Animancer.Animator.deltaRotation;
        public virtual Quaternion RootMotionRotation => Animancer.Animator.rootRotation;

        public virtual bool ApplyRawRootMotion => false;

        protected AnimancerState _playingAniState;
        protected TStateType _prevStateType;

        public AnimancerState PlayingAniState
        {
            get => _playingAniState;
            set => _playingAniState = value;
        }

        protected IDefineStateType<TStateType> _defineStateType;

#if UNITY_EDITOR
        private string _stateName;
        private string _currentStateName;
#endif

        protected virtual void Awake()
        {
#if UNITY_EDITOR
            _stateName = name;
            _currentStateName = $"{name} (Current)";
#endif
        }

        protected virtual void Start()
        {
        }

        protected virtual void OnEnable()
        {
            BaseOnEnable();
        }

        protected virtual void OnDisable()
        {
            BaseOnDisable();
        }

        protected virtual void BaseOnEnable()
        {
            ExitState = false;
            SetPrevState();
        }

        protected virtual void BaseOnDisable()
        {
            _playingAniState = null;
            ExitState = false;
            ChangedStateWithModules();
        }

        public virtual void Initialize()
        {
        }

        public virtual void EnableStateNetwork()
        {
        }

        public virtual void DisableStateNetwork()
        {
            OnDisable();
        }

        protected virtual void Update()
        {
            UpdateState();
        }

        protected virtual void LateUpdate()
        {
            LateUpdateState();
        }

#if UNITY_EDITOR
        protected override void OnValidate()
        {
            base.OnValidate();

            gameObject.GetComponentInParentOrChildren(ref _animation);
        }

        public override void OnEnterState()
        {
            base.OnEnterState();
            name = _currentStateName;
        }

        public override void OnExitState()
        {
            base.OnExitState();
            name = _stateName;
        }
#endif

        public virtual void Initialize(TAnimation animation, in EnumAttraction attractionType)
        {
            _animation = animation;

            var modules = CreateStateChangeModules(animation, attractionType);
            if (!modules.IsNullOrEmpty())
            {
                foreach (var module in modules)
                    AddStateChangeModule(module);
            }

            InitializeDefineStateType();
        }

        public virtual bool UpdateState()
        {
            return true;
        }

        public virtual bool LateUpdateState()
        {
            Animation.CheckAnimationSubstate();

            if (Animation.CheckAnimationState())
                return false;

            UpdateAnimationParameters();

            return true;
        }

        protected virtual void UpdateAnimationParameters()
        {
        }

        protected virtual AnimancerState InternalPlayAnimation(in TAnimationType animationType,
            in float animationSpeed = 1f, in Func<AnimancerState, float> calculateSpeedFunc = null, in eLayerType layerType = eLayerType.BASE)
        {
            return null;
        }
        
        // NormalizedTime
        protected virtual AnimancerState InternalPlayAnimation(in TAnimationType animationType, in long serverStartTime,
            in float animationSpeed = 1f, in Func<AnimancerState, float> calculateSpeedFunc = null, in eLayerType layerType = eLayerType.BASE)
        {
            return null;
        }
        
        public virtual AnimancerState PlayAnimation(in TAnimationType animationType, in float animationSpeed = 1f,
            in Func<AnimancerState, float> calculateSpeedFunc = null, in eLayerType layerType = eLayerType.BASE)
        {
            return InternalPlayAnimation(animationType, animationSpeed, calculateSpeedFunc, layerType);
        }

        public AnimancerState PlayAnimation(in TAnimationType animationType, in float animationSpeed = 1f,
            in Func<AnimancerState, float> calculateSpeedFunc = null, in uint layerIndex = 0)
        {
            return PlayAnimation(animationType, animationSpeed, calculateSpeedFunc, (eLayerType)layerIndex);
        }
        
        // NormlaizedTime
        public virtual AnimancerState PlayAnimation(in TAnimationType animationType, in long serverStartTime,
            in float animationSpeed = 1f, in Func<AnimancerState, float> calculateSpeedFunc = null, in eLayerType layerType = eLayerType.BASE)
        {
            return InternalPlayAnimation(animationType, serverStartTime, animationSpeed, calculateSpeedFunc, layerType);
        }

        public AnimancerState PlayAnimation(in TAnimationType animationType, in long serverStartTime,
            in float animationSpeed = 1f, in Func<AnimancerState, float> calculateSpeedFunc = null, in uint layerIndex = 0)
        {
            return PlayAnimation(animationType, serverStartTime, animationSpeed, calculateSpeedFunc, (eLayerType)layerIndex);
        }

        public virtual bool IsPlayingAnimation(in float normalizedTime)
        {
            return false;
        }

        protected virtual void SetUseRootMotion(AnimationClip animationClip)
        {
        }

        protected void SetPrevState()
        {
            _prevStateType = Animation.CurrentStateType;
        }

        protected void AddIgnoreChangeNetObjectAnimationType(in TAnimationType animationType)
        {
            Animation.AddIgnoreChangeNetObjectAnimationType(animationType);
        }

        protected bool SetUseRootMotion(AnimancerState animancerState)
        {
            if (!IsLocal || !animancerState.IsValid() || !animancerState.IsCurrent)
                return false;

            var clip = animancerState.Clip;
            if (clip)
            {
                //Debug.Log($"[SetUseRootMotion] {InAnimancerState}".Color(Color.green));
                SetUseRootMotion(clip);
                return true;
            }

            if (animancerState is ParentState parentState)
            {
                var currentState = parentState.GetCurrentState();
                if (currentState != null && animancerState != currentState && SetUseRootMotion(currentState))
                {
                    if (currentState.Parent is AnimancerState)
                    {
                        currentState.SetExitEvent((state) =>
                        {
                            if (state.Parent is not AnimancerState parent)
                                return;

                            if (!SetUseRootMotion(parent) && parent.GetCurrentParent() is { } currentParent)
                                SetUseRootMotion(currentParent);
                        });
                    }

                    return true;
                }
            }

            return false;
        }

        protected void SetAnimationEndEvent(AnimancerState animationState, Action onEnd)
        {
            if (animationState.IsValid())
            {
                // if (animationState is ManualMixerState mixerState)
                // {
                //     var state = mixerState.GetCurrentChildState() ?? animationState;
                //     ClipTransitionSequence clipTransitionSequence = state.Key switch
                //     {
                //         TransitionAsset { Transition: ClipTransitionSequence taCts } => taCts,
                //         ClipTransitionSequence cts => cts,
                //         _ => null
                //     };
                //
                //     if (clipTransitionSequence != null)
                //     {
                //         clipTransitionSequence.OnEnd = onEnd;
                //         return;
                //     }
                // }

                animationState.Events(null).OnEnd = onEnd;
            }
        }

        protected void ExecuteMixerRecalculateWeights(AnimancerState state)
        {
            if (state is not ManualMixerState mixerState)
                return;

            mixerState.RecalculateWeights();
        }

        protected virtual IStateChangeModule<TStateType>[] CreateStateChangeModules(
            TAnimation animation, in EnumAttraction attractionType)
        {
            return null;
        }

        public void AddStateChangeModule(IStateChangeModule<TStateType> module)
        {
            if (module == null)
                return;

            if (_stateChangeModuleList == null)
                _stateChangeModuleList = new List<IStateChangeModule<TStateType>>();

            var index = _stateChangeModuleList.FindIndex(m => m.GetType() == module.GetType());
            if (index < 0)
                _stateChangeModuleList.Add(module);
            else
                _stateChangeModuleList[index] = module;
        }

        public IStateChangeModule<TStateType> GetStateChangeModule(Type moduleType)
        {
            if (_stateChangeModuleList.IsNullOrEmpty())
                return null;

            var module = _stateChangeModuleList.Find(m => m.GetType() == moduleType);
            return module ?? _stateChangeModuleList.Find(m => moduleType.IsAssignableFrom(m.GetType()));
        }

        public T GetStateChangeModule<T>() where T : class, IStateChangeModule<TStateType>
        {
            return GetStateChangeModule(typeof(T)) as T;
        }

        protected (bool Valid, TStateType Next) GetNextStateTypeByModules()
        {
            if (_stateChangeModuleList.IsNullOrEmpty())
                return default;

            for (int i = 0; i < _stateChangeModuleList.Count; ++i)
            {
                var next = _stateChangeModuleList[i].NextStateType;
                if (next.Valid)
                    return next;
            }

            return default;
        }

        protected bool CanChangeNextStateByModules(in TStateType nextState)
        {
            if (_stateChangeModuleList.IsNullOrEmpty())
                return false;

            for (int i = 0; i < _stateChangeModuleList.Count; ++i)
            {
                if (_stateChangeModuleList[i].CanChangeNextState(nextState))
                    return true;
            }

            return false;
        }

        protected bool CanExitStateByModules()
        {
            if (_stateChangeModuleList.IsNullOrEmpty())
                return false;

            for (int i = 0; i < _stateChangeModuleList.Count; ++i)
            {
                if (_stateChangeModuleList[i].CanExitState())
                    return true;
            }

            return false;
        }

        protected void ChangedStateWithModules()
        {
            if (_stateChangeModuleList.IsNullOrEmpty())
                return;

            for (int i = 0; i < _stateChangeModuleList.Count; ++i)
                _stateChangeModuleList[i].ChangedState();
        }

        protected virtual void InitializeDefineStateType()
        {
        }

        protected TStateType GetStateType(in TStateType stateType)
        {
            return _defineStateType != null ? _defineStateType.GetStateType(stateType) : stateType;
        }

        protected TStateType GetBaseStateType(in TStateType stateType)
        {
            return _defineStateType != null ? _defineStateType.GetBaseStateType(stateType) : stateType;
        }
    }
}
