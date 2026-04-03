using System;
using System.Collections.Generic;
using Animancer.FSM;
using UnityEngine;

namespace REIW.Animations
{
    [DisallowMultipleComponent]
    public abstract class AnimationStateMachine<TAnimationType, TStateType, TState, TStateMachine, TAnimation> : StateMachine<TState>.WithDefault
        where TAnimationType : Enum
        where TStateType : Enum
        where TState : AnimationState<TAnimationType, TStateType, TState, TStateMachine, TAnimation>
        where TStateMachine : AnimationStateMachine<TAnimationType, TStateType, TState, TStateMachine, TAnimation>
        where TAnimation : AnimationBase<TAnimationType, TStateType, TState, TStateMachine, TAnimation>
    {
        [SerializeField] protected AnimationStateLoader _stateLoader;
        [SerializeField, Range(0, 1)] private float _checkPlayingAnimationNormalizedTime = 0.8f;

        protected Dictionary<TStateType, TState> _dicStates = new();
        protected TState[] _secondaryCheckNextStateList;

        protected Dictionary<TStateType, List<IStateChangeModule<TStateType>>> _stateChangeModules;

        public new TState CurrentState { get; set; }
        public new TState PreviousState { get; set; }

        public virtual bool IsPlayingAnyAnimation
        {
            get
            {
                if (IsPlayingPrevStateAnimation)
                    return true;
                if (CurrentState && CurrentState.IsPlayingAnimation(_checkPlayingAnimationNormalizedTime))
                    return true;
                return false;
            }
        }

        public virtual bool IsPlayingPrevStateAnimation
        {
            get
            {
                if (PreviousState && PreviousState.IsPlayingAnimation(_checkPlayingAnimationNormalizedTime))
                    return true;
                return false;
            }
        }

        public virtual void Initialize(TAnimation animation, in EnumAttraction attractionType)
        {
            if (!_stateLoader)
                return;

            _dicStates.Clear();

            _stateLoader.LoadStates(attractionType);

            CreateNextStateChangeModules(animation, attractionType);

            var states = _stateLoader.GetStates<TState>(true);
            foreach (var state in states)
            {
                _dicStates.TryAdd(state.StateType, state);
                state.Initialize(animation, attractionType);

                var nscModules = GetNextStateChangeModules(state.StateType);
                if (nscModules.IsNullOrEmpty())
                    continue;

                foreach (var module in nscModules)
                    state.AddStateChangeModule(module);
            }

            SetSecondaryCheckStates();
        }

        public void InitializeStates()
        {
            if (_dicStates.IsNullOrEmpty())
                return;

            foreach (var state in _dicStates.Values)
                state.Initialize();
        }

        public void InitializeStateLoader(TAnimation animation)
        {
            if (!_stateLoader)
                _stateLoader = animation.GetComponentInChildren<AnimationStateLoader>(true);
        }

        public TState GetNextStateSecondaryCheck()
        {
            for (int i = 0; i < _secondaryCheckNextStateList.Length; ++i)
            {
                if (_secondaryCheckNextStateList[i].CanEnterState)
                    return _secondaryCheckNextStateList[i];
            }

            return null;
        }

        protected virtual void SetSecondaryCheckStates()
        {
        }

        protected virtual void CreateNextStateChangeModules(TAnimation animation, in EnumAttraction attractionType)
        {
        }

        protected List<IStateChangeModule<TStateType>> GetNextStateChangeModules(in TStateType stateType)
        {
            return _stateChangeModules?.TryGetValue(stateType, out var modules) == true ? modules : null;
        }

        public bool GetNextStateChangeModules<T>(List<T> moduleList) where T : IStateChangeModule<TStateType>
        {
            if (moduleList == null || _stateChangeModules.IsNullOrEmpty())
                return false;

            var type = typeof(T);

            foreach (var modules in _stateChangeModules.Values)
            {
                foreach (var module in modules)
                {
                    var md = (T)module;
                    if (type.IsAssignableFrom(module.GetType()) && !moduleList.Contains(md))
                        moduleList.Add(md);
                }
            }

            return !moduleList.IsNullOrEmpty();
        }

        public void ChangedStateWithModules()
        {
            if (_stateChangeModules.IsNullOrEmpty())
                return;

            foreach (var modules in _stateChangeModules.Values)
            {
                foreach (var module in modules)
                    module.ChangedState();
            }
        }

        public TState GetAnimationState(in TStateType stateType)
        {
            return _dicStates.GetValueOrDefault(stateType);
        }

        public T GetAnimationState<T>(in TStateType stateType) where T : TState
        {
            return GetAnimationState(stateType) as T;
        }

        public T GetAnimationState<T>() where T : TState
        {
            foreach (var state in _dicStates.Values)
            {
                if (state is T t) 
                    return t;
            }

            return null;
        }
    }
}
