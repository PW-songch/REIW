using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using System.Text;
using Animancer;
using Animancer.FSM;
using AYellowpaper.SerializedCollections;
using UnityEngine;

namespace REIW.Animations
{
    [DisallowMultipleComponent]
    public abstract class AnimationBase<TAnimationType, TStateType, TState, TStateMachine, TAnimation> : MonoBehaviour
        where TAnimationType : Enum
        where TStateType : Enum
        where TState : AnimationState<TAnimationType, TStateType, TState, TStateMachine, TAnimation>
        where TStateMachine : AnimationStateMachine<TAnimationType, TStateType, TState, TStateMachine, TAnimation>
        where TAnimation : AnimationBase<TAnimationType, TStateType, TState, TStateMachine, TAnimation>
    {
        [SerializeField] protected AnimancerComponent _animancer;
        public AnimancerComponent Animancer => _animancer;

        [SerializeField] private TStateMachine _stateMachine;
        public TStateMachine StateMachine => _stateMachine;

        [SerializeField] private TStateMachine _substateMachine;
        public TStateMachine SubstateMachine => _substateMachine;

        [SerializeField, SerializedDictionary("Layer Type", "AvatarMask")]
        private SerializedDictionary<eLayerType, AvatarMask> _layerAvatarMasks;

#if UNITY_EDITOR
        [SerializeField] private bool ShowLog;
#endif
        
        public FacialAnimationController FacialController { get; private set; }

        protected TStateType _currentStateType;
        protected TStateType _prevStateType;
        protected TStateType _currentSubstateType;
        protected TStateType _prevSubstateType;
        protected TAnimationType _currentAnimationType;
        protected AnimationClipRootMotionSettingsSO _rootMotionSettings;
        protected Dictionary<Type, AnimationEventListener> _eventListeners;
        
        private List<TAnimationType> _ignoreChangeNetObjectAnimationTypes = new();
        private Coroutine _resetCumulativeTimeCoroutine;

        public TStateType CurrentStateType => _currentStateType;
        public virtual TStateType CurrentBaseStateType => _currentStateType;
        public TStateType PrevStateType => _prevStateType;
        public virtual TStateType PrevBaseStateType => _prevStateType;
        public TStateType CurrentSubstateType => _currentSubstateType;
        public virtual TStateType CurrentBaseSubstateType => _currentSubstateType;
        public TStateType PrevSubstateType => _prevSubstateType;
        public virtual TStateType PrevBaseSubstateType => _prevSubstateType;
        public TState CurrentState => _stateMachine.CurrentState;
        public TState CurrentSubstate => _substateMachine.CurrentState;

        public virtual bool IsLocal => false;
        public bool IsFacialAvailable => FacialController != null;

        public virtual int CurrentAnimation
        {
            get => _currentAnimationType.ToInt();
            set => _currentAnimationType = value.ToEnum<TAnimationType>();
        }

        public float CurrentAnimationSpeed
        {
            get => _animancer.States.Current?.Speed ?? 1f;
            set
            {
                if (_animancer.States.Current != null)
                    _animancer.States.Current.Speed = value;
            }
        }
        
        public float CurrentAnimationNormalizedTime
        {
            get => _animancer.States.Current?.NormalizedTime ?? 0f;
            set
            {
                if (_animancer.States.Current != null)
                    _animancer.States.Current.NormalizedTime = value;
            }
        }

        protected virtual int AnimationTypeBitDigits { get; }

        public event Action AnimatorMoveEvent;

        private string LOG_NAME;
        private string LOG_PLAY_ANIMANCER;
        private string LOG_PLAY_ANIMANCER_FADE;
        
        protected virtual void Awake()
        {
            LOG_NAME = GetType().Name.Color(Color.green);
            LOG_PLAY_ANIMANCER = nameof(PlayAnimancer).Color(Color.cyan);
            LOG_PLAY_ANIMANCER_FADE = nameof(PlayAnimancerFade).Color(Color.cyan);

            InitializeComponents();
            InitializeLayerAvatarMasks();
            InitializeAnimationParameters();
            InitializeFacialAnimationController();
        }

        protected virtual void Start()
        {
            InitializeRootMotionSettings();
        }

        protected virtual void OnEnable()
        {
            _stateMachine.CurrentState = _stateMachine.DefaultState;
            _substateMachine.CurrentState = _substateMachine.DefaultState;
        }

        protected virtual void OnDisable()
        {
        }

        protected virtual void OnDestroy()
        {
        }

        protected virtual void Reset()
        {
            _animancer ??= GetComponent<AnimancerComponent>();
            InitializeStateMachine(EnumAttraction.Default);
            InitializeSubstateMachine(EnumAttraction.Default);
        }

        public virtual void Init(in EnumAttraction attractionType = EnumAttraction.Default)
        {
            InitializeComponents();
            InitializeAnimationMovement();
            InitializeAnimationEventListener();
            InitializeStateMachine(attractionType);
            InitializeSubstateMachine(attractionType);
            InitializeStates();
            InitializeFacialAnimationController();
        }

        protected virtual void InitializeComponents()
        {
        }

        protected virtual bool InitializeRootMotionSettings()
        {
            return IsLocal;
        }

        protected virtual void InitializeStateMachine(in EnumAttraction attractionType)
        {
            _stateMachine.InitializeStateLoader((TAnimation)this);
            InitializeStateMachine(_stateMachine, attractionType);
        }

        protected virtual void InitializeSubstateMachine(in EnumAttraction attractionType)
        {
            InitializeStateMachine(_substateMachine, attractionType);
        }

        protected virtual void InitializeStates()
        {
            _stateMachine.InitializeStates();
            _substateMachine.InitializeStates();
        }

        protected virtual void InitializeAnimationMovement()
        {
        }

        protected virtual void InitializeAnimationParameters()
        {
        }

        protected virtual void InitializeAnimationEventListener()
        {
        }
        
        protected virtual void InitializeFacialAnimationController()
        {
            FacialController = GetComponent<FacialAnimationController>();
            FacialController?.Initialize(Animancer);
        }

        private void InitializeStateMachine(TStateMachine stateMachine, in EnumAttraction attraction)
        {
            stateMachine.Initialize((TAnimation)this, attraction);
            stateMachine.InitializeAfterDeserialize();

            stateMachine.DefaultState = stateMachine.GetAnimationState((TStateType)default(TStateType).NextValue());
            stateMachine.CurrentState = stateMachine.DefaultState;
        }

        private void InitializeLayerAvatarMasks()
        {
            if (_layerAvatarMasks.IsNullOrEmpty())
                return;

            var layerCount = _animancer.Layers.Count;
            foreach (var lam in _layerAvatarMasks)
                layerCount = Mathf.Max(layerCount, (int)lam.Key + 1);

            _animancer.Layers.Capacity = layerCount;

            foreach (var lam in _layerAvatarMasks)
            {
                if (lam.Value != null)
                    _animancer.Layers[(int)lam.Key].Mask = lam.Value;
            }
        }

        public void PlayAnimation(in int animationType, in float animationSpeed = 1f, in uint layerIndex = 0)
        {
            PlayAnimation(animationType.ToEnum<TAnimationType>(), animationSpeed, null, layerIndex);
        }

        public void PlayAnimation(in int animationType, in float animationSpeed = 1f, in eLayerType layerType = eLayerType.BASE)
        {
            PlayAnimation(animationType, animationSpeed, (uint)layerType);
        }
        
        public void PlayAnimation(in int animationType, in long serverStartTime, in float animationSpeed = 1f, in uint layerIndex = 0)
        {
            PlayAnimation(animationType.ToEnum<TAnimationType>(), serverStartTime, animationSpeed, null, layerIndex);
        }

        public void PlayAnimation(in int animationType, in long serverStartTime, in float animationSpeed = 1f, in eLayerType layerType = eLayerType.BASE)
        {
            PlayAnimation(animationType, serverStartTime, animationSpeed, (uint)layerType);
        }
        
        public AnimancerState PlayAnimation(in TAnimationType animationType, in float animationSpeed = 1f,
            in Func<AnimancerState, float> calculateSpeedFunc = null, in uint layerIndex = 0)
        {
            return InternalPlayAnimation(animationType, animationSpeed, calculateSpeedFunc, layerIndex);
        }

        public AnimancerState PlayAnimation(in TAnimationType animationType, in float animationSpeed = 1f,
            in Func<AnimancerState, float> calculateSpeedFunc = null, in eLayerType layerType = eLayerType.BASE)
        {
            return PlayAnimation(animationType, animationSpeed, calculateSpeedFunc, (uint)layerType);
        }
        
        // NormalizedTime
        public AnimancerState PlayAnimation(in TAnimationType animationType, in long serverStartTime,
            in float animationSpeed = 1f, in Func<AnimancerState, float> calculateSpeedFunc = null, in uint layerIndex = 0)
        {
            return InternalPlayAnimation(animationType, serverStartTime, animationSpeed, calculateSpeedFunc, layerIndex);
        }

        public AnimancerState PlayAnimation(in TAnimationType animationType, in long serverStartTime,
            in float animationSpeed = 1f, in Func<AnimancerState, float> calculateSpeedFunc = null, in eLayerType layerType = eLayerType.BASE)
        {
            return PlayAnimation(animationType, serverStartTime, animationSpeed, calculateSpeedFunc, (uint)layerType);
        }

        public AnimancerState PlayAnimation(in TAnimationType animationType, in ITransition transition,
            in float animationSpeed = 1f, in Func<AnimancerState, float> calculateSpeedFunc = null, in uint layerIndex = 0)
        {
            return transition.IsValid ? PlayAnimancer(animationType, transition, animationSpeed, calculateSpeedFunc, layerIndex) : null;
        }

        public AnimancerState PlayAnimation(in TAnimationType animationType, in ITransition transition,
            in float animationSpeed = 1f, in Func<AnimancerState, float> calculateSpeedFunc = null, in eLayerType layerType = eLayerType.BASE)
        {
            return PlayAnimation(animationType, transition, animationSpeed, calculateSpeedFunc, (uint)layerType);
        }

        public AnimancerState PlayAnimation(in TAnimationType animationType, in ITransition transition, in float normalizedTime = 0f,
            in float animationSpeed = 1f, in Func<AnimancerState, float> calculateSpeedFunc = null, in uint layerIndex = 0)
        {
            return transition.IsValid ? PlayAnimancer(animationType, transition, normalizedTime, animationSpeed, calculateSpeedFunc, layerIndex) : null;
        }

        public AnimancerState PlayAnimation(in TAnimationType animationType, in ITransition transition, in float normalizedTime = 0f,
            in float animationSpeed = 1f, in Func<AnimancerState, float> calculateSpeedFunc = null, in eLayerType layerType = eLayerType.BASE)
        {
            return PlayAnimation(animationType, transition, normalizedTime, animationSpeed, calculateSpeedFunc, (uint)layerType);
        }

        protected virtual AnimancerState InternalPlayAnimation(in TAnimationType animationType, in float animationSpeed = 1f,
            in Func<AnimancerState, float> calculateSpeedFunc = null, in uint layerIndex = 0)
        {
            var at = EnumUtility.GetUnpackValue(animationType, AnimationTypeBitDigits);
            if (GetAnimationState(at) is TState state)
                return state.PlayAnimation(at, animationSpeed, calculateSpeedFunc, layerIndex);
            return null;
        }
        
        // NormalizedTime
        protected virtual AnimancerState InternalPlayAnimation(in TAnimationType animationType, in long serverStartTime,
            in float animationSpeed = 1f, in Func<AnimancerState, float> calculateSpeedFunc = null, in uint layerIndex = 0)
        {
            var at = EnumUtility.GetUnpackValue(animationType, AnimationTypeBitDigits);
            if (GetAnimationState(at) is TState state)
                return state.PlayAnimation(at, serverStartTime, animationSpeed, calculateSpeedFunc, layerIndex);
            return null;
        }
        
        protected virtual AnimancerState PlayAnimancer(in TAnimationType animationType, ITransition transition,
            in float animationSpeed = 1f, in Func<AnimancerState, float> calculateSpeedFunc = null, in uint layerIndex = 0)
        {
#if UNITY_EDITOR
            if (ShowLog)
                Debug.Log($"Animancer Play: {transition}");
#endif

            LogConsole.Normal(eLogCategory.Animation, $"[{LOG_NAME}] {LOG_PLAY_ANIMANCER}: {transition}", this);
            _currentAnimationType = EnumUtility.GetUnpackValue(animationType, AnimationTypeBitDigits);
            Animancer.Animator.enabled = true;
            var state = Animancer.Layers[(int)layerIndex].Play(transition);
            SetAnimancerStateSpeed(state, animationSpeed, calculateSpeedFunc);
            RunResetCumulativeTimeCoroutine(state);
            return state;
        }
        
        public virtual AnimancerState PlayAnimancer(in TAnimationType animationType, ITransition transition, in float normalizedTime = 0f,
            in float animationSpeed = 1f, in Func<AnimancerState, float> calculateSpeedFunc = null, in uint layerIndex = 0)
        {
#if UNITY_EDITOR
            if (ShowLog)
                Debug.Log($"Animancer Play: {transition}");
#endif

            LogConsole.Normal(eLogCategory.Animation, $"[{LOG_NAME}] {LOG_PLAY_ANIMANCER}: {transition}", this);
            _currentAnimationType = EnumUtility.GetUnpackValue(animationType, AnimationTypeBitDigits);
            Animancer.Animator.enabled = true;
            var state = Animancer.Layers[(int)layerIndex].Play(transition);
            SetAnimancerStateSpeed(state, animationSpeed, calculateSpeedFunc);
            SetAnimancerStateNormalizedTime(state, normalizedTime);
            RunResetCumulativeTimeCoroutine(state);
            return state;
        }
        
        public virtual AnimancerState PlayAnimancerFade(in TAnimationType animationType, ITransition transition,
            in float fadein = 0.2f, in float animationSpeed = 1.0f, in uint layerIndex = 0)
        {
#if UNITY_EDITOR
            if (ShowLog)
                Debug.Log($"Animancer Play: {transition}");
#endif

            LogConsole.Normal(eLogCategory.Animation, $"[{LOG_NAME}] {LOG_PLAY_ANIMANCER_FADE}: {transition}", this);
            _currentAnimationType = EnumUtility.GetUnpackValue(animationType, AnimationTypeBitDigits);
            Animancer.Animator.enabled = true;
            AnimancerState state = Animancer.Layers[(int)layerIndex].Play(transition, fadein);
            SetAnimancerStateSpeed(state, animationSpeed);
            RunResetCumulativeTimeCoroutine(state);
            return state;
        }

        protected virtual bool CheckAnimationState(TStateMachine stateMachine, ref TStateType currentStateType, ref TStateType prevStateType)
        {
            if (!stateMachine.CurrentState)
                return false;

            currentStateType = stateMachine.CurrentState.StateType;

            var next = stateMachine.CurrentState.NextStateType;
            TState state = stateMachine.GetAnimationState(next.Next);
            if (!state)
                return false;

            if (currentStateType.Equals(state.StateType))
            {
                TState secondaryState = stateMachine.GetNextStateSecondaryCheck();
                if (secondaryState)
                    state = secondaryState;
            }

            if (state == stateMachine.CurrentState)
                return false;
            
            if (TryForceSetAnimationState(stateMachine, state, currentStateType, ref prevStateType))
                return true;

            if (stateMachine.TryResetState(state))
            {
                stateMachine.PreviousState = stateMachine.CurrentState;
                stateMachine.CurrentState = state;
                prevStateType = currentStateType;
                stateMachine.ChangedStateWithModules();
                return true;
            }

            return false;
        }

        public bool CheckAnimationState()
        {
            return CheckAnimationState(_stateMachine, ref _currentStateType, ref _prevStateType);
        }

        public bool CheckAnimationSubstate()
        {
            return CheckAnimationState(_substateMachine, ref _currentSubstateType, ref _prevSubstateType);
        }

        protected virtual bool ChangeAnimationNetObject(in TAnimationType animationType, in float animationSpeed)
        {
            return IsChangeAnimationNetObject(animationType);
        }

        protected virtual bool SetAnimationState(TStateMachine stateMachine,
            TState state, in TStateType currentStateType, ref TStateType prevStateType)
        {
            if (!stateMachine.TryResetState(state))
                return false;

            stateMachine.PreviousState = stateMachine.CurrentState;
            stateMachine.CurrentState = state;
            prevStateType = currentStateType;
            return true;
        }

        public virtual bool SetAnimationState(in TStateType stateType)
        {
            var animationState = _stateMachine.GetAnimationState(stateType);
            if (animationState == null)
                return false;

            return SetAnimationState(_stateMachine, animationState, CurrentStateType, ref _prevStateType);
        }

        public virtual bool SetAnimationSubstate(in TStateType stateType)
        {
            var animationState = _substateMachine.GetAnimationState(stateType);
            if (animationState == null)
                return false;

            return SetAnimationState(_substateMachine, animationState, CurrentStateType, ref _prevStateType);
        }

        protected virtual bool TryForceSetAnimationState(TStateMachine stateMachine,
            TState state, in TStateType currentStateType, ref TStateType prevStateType)
        {
            stateMachine.ForceSetState(state);
            stateMachine.PreviousState = stateMachine.CurrentState;
            stateMachine.CurrentState = state;
            prevStateType = currentStateType;
            return true;
        }

        public virtual bool TryForceSetAnimationState(in TStateType stateType)
        {
            var animationState = _stateMachine.GetAnimationState(stateType);
            if (animationState == null)
                return false;

            return TryForceSetAnimationState(_stateMachine, animationState, CurrentStateType, ref _prevStateType);
        }

        public bool TryForceSetAnimationState<T>() where T : TState
        {
            var animationState = _stateMachine.GetAnimationState<T>();
            if (animationState == null)
                return false;

            return TryForceSetAnimationState(_stateMachine, animationState, CurrentStateType, ref _prevStateType);
        }

        public virtual bool TryForceSetAnimationSubstate(in TStateType stateType)
        {
            var animationState = _substateMachine.GetAnimationState(stateType);
            if (animationState == null)
                return false;

            return TryForceSetAnimationState(_substateMachine, animationState, CurrentSubstateType, ref _prevSubstateType);
        }

        public bool TryForceSetAnimationSubstate<T>() where T : TState
        {
            var animationState = _substateMachine.GetAnimationState<T>();
            if (animationState == null)
                return false;

            return TryForceSetAnimationState(_substateMachine, animationState, CurrentSubstateType, ref _prevSubstateType);
        }

        protected virtual void OnAnimatorMove()
        {
            AnimatorMoveEvent?.Invoke();
        }

        protected bool IsChangeAnimationNetObject(in TAnimationType animationType)
        {
            return !EnumUtility.GetUnpackFlag(animationType, AnimationTypeBitDigits) &&
                   !_ignoreChangeNetObjectAnimationTypes.Contains(animationType);
        }

        protected TStateType GetAnimationStateType(in TAnimationType animationType)
        {
            return (animationType.ToInt() / AnimationConsts.ANIMATIONTYPE_WITH_STATETYPE_CONVERSION_UNIT).ToEnum<TStateType>();
        }

        protected StateBehaviour GetAnimationState(in TAnimationType animationType)
        {
            var stateType = GetAnimationStateType(animationType);
            var state = _stateMachine.GetAnimationState(stateType);
            if (!state)
                state = _substateMachine.GetAnimationState(stateType);
            if (!state)
                state = _stateMachine.DefaultState;
            return state;
        }

        public void AddIgnoreChangeNetObjectAnimationType(in TAnimationType animationType)
        {
            if (!_ignoreChangeNetObjectAnimationTypes.Contains(animationType))
                _ignoreChangeNetObjectAnimationTypes.Add(animationType);
        }

        public RootMotionSettings GetRootMotionSettings(AnimationClip animationClip)
        {
            return _rootMotionSettings ? _rootMotionSettings.GetRootMotionSettings(animationClip) : default;
        }

        private void SetAnimancerStateSpeed(AnimancerState state, in float animationSpeed, in Func<AnimancerState, float> calculateSpeedFunc = null)
        {
            if (!state.IsValid())
                return;

            state.Speed = calculateSpeedFunc?.Invoke(state) ?? animationSpeed;
        }

        private void SetAnimancerStateNormalizedTime(AnimancerState state, in float normalizedTime)
        {
            if (!state.IsValid())
                return;

            state.NormalizedTime = normalizedTime;
        }

        private void RunResetCumulativeTimeCoroutine(AnimancerState state)
        {
            if (_resetCumulativeTimeCoroutine != null)
            {
                StopCoroutine(_resetCumulativeTimeCoroutine);
                _resetCumulativeTimeCoroutine = null;
            }

            if (!state.IsValid() || !state.IsLooping)
                return;

            _resetCumulativeTimeCoroutine = StartCoroutine(CorResetCumulativeTime(state));
        }

        private IEnumerator CorResetCumulativeTime(AnimancerState state)
        {
            do
            {
                state.ResetCumulativeTime();
                yield return new WaitForSeconds(state.Length * 100f);
            }
            while (state.IsCurrent);
        }

        #region Event Listener Setting

        protected void CreateAnimationEventListeners(IEnumerable<Type> derivedTypes, EventBus eventBus)
        {
            _eventListeners = new();
            foreach (var member in derivedTypes)
                _eventListeners.Add(member, (AnimationEventListener)member.CreateInstance(args: eventBus));
        }

        protected void SetAnimationEventListeners()
        {
            if (_eventListeners.IsNullOrEmpty())
                return;

            var thisType = GetType();
            var baseNameSpace = thisType.BaseType.Namespace;

            foreach (var type in _eventListeners.Keys)
            {
                var methodName = type.Name;
                var nameSpaces = type.Namespace.Replace(baseNameSpace, "").Split('.');
                if (!nameSpaces.IsNullOrEmpty())
                {
                    var sb = new StringBuilder();
                    foreach (var ns in nameSpaces)
                    {
                        if (!string.IsNullOrEmpty(ns))
                            sb.Append($"{ns}_");
                    }

                    methodName = $"{sb}{methodName}";
                }

                var method = thisType.GetMethod($"ConnectingEvents_{methodName}",
                    BindingFlags.Instance | BindingFlags.NonPublic);

                if (method != null)
                    method.Invoke(this, null);
                else
                    LogUtil.LogError($"AnimationEventListener setup function is not defined - {type.FullName}");
            }
        }
        
        protected T GetAnimationEventListeners<T>() where T : AnimationEventListener
        {
            return !_eventListeners.IsNullOrEmpty() && _eventListeners.TryGetValue(typeof(T), out var el) ? el as T : null;
        }
        
        protected void RegisterAnimationEventListeners(EventBus eventBus)
        {
            if (_eventListeners.IsNullOrEmpty())
                return;

            foreach (var el in _eventListeners.Values)
                el?.Register(eventBus);
        }

        protected void UnregisterAnimationEventListeners(EventBus eventBus)
        {
            if (_eventListeners.IsNullOrEmpty())
                return;

            foreach (var el in _eventListeners.Values)
                el?.Unregister(eventBus);
        }
        
        public virtual void ResetAnimationEventData()
        {
        }
        
        #endregion
    }
}
