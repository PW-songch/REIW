using System;
using System.Collections.Generic;
using Animancer;
using REIW.EventLock;
using UnityEngine;

namespace REIW.Animations.Character
{
    /// <summary>
    /// ParkourAnimationState
    /// TransitionAsset의 이벤트와 연결되는 스크립트의 이벤트 콜백들의 이름은 이벤트의 StringAsset명과 일치 해야함.
    /// </summary>
    public class ParkourAnimationState : CharacterAnimationState, IPlayModeState
    {
        [AnimationType(eStateType.PARKOUR)]
        public enum eAnimationType : uint
        {
            NONE = TYPE_START,
            TYPE_START = Animations.Character.eAnimationType.PARKOUR_TYPE_START,
            // PARKOUR_VAULT
            PARKOUR_VAULT_ONE_STEP,
            PARKOUR_VAULT_OVER_LEFT,
            PARKOUR_VAULT_OVER_RIGHT,
            PARKOUR_VAULT_CLIMB_UP,
            PARKOUR_VAULT_TYPE_END,
            // PARKOUR_JUMP
            PARKOUR_JUMP_TYPE_START = PARKOUR_VAULT_TYPE_END + 1,
            PARKOUR_JUMP_LEFT_FOOT,
            PARKOUR_JUMP_RIGHT_FOOT,
            PARKOUR_JUMP_MIDDLE,
            PARKOUR_JUMP_FAR,
            PARKOUR_JUMP_LEDGE_OFF,
            PARKOUR_JUMP_TYPE_END,
            // PARKOUR_CLIMB
            PARKOUR_CLIMB_TYPE_START = PARKOUR_JUMP_TYPE_END + 1,
            PARKOUR_CLIMB_TYPE_END,
            // PARKOUR_WALL_RUN
            PARKOUR_WALL_RUN_TYPE_START = PARKOUR_CLIMB_TYPE_END + 1,
            PARKOUR_WALL_RUN_START_LEFT,
            PARKOUR_WALL_RUN_START_RIGHT,
            PARKOUR_WALL_RUN_LEFT,
            PARKOUR_WALL_RUN_RIGHT,
            PARKOUR_WALL_RUN_JUMP_LEFT,
            PARKOUR_WALL_RUN_JUMP_RIGHT,
            TYPE_END
        }

        public override eStateType StateType => eStateType.PARKOUR;
        public CharacterMovePlayMode MovePlayMode => CharacterMovePlayMode.Parkour;

        // vault
        [SerializeField] private TransitionAsset[] _vaultTransitions;
        // jump
        [SerializeField] private TransitionAsset[] _jumpTransitions;
        // climb
        [SerializeField] private TransitionAsset[] _climbTransitions;
        // wall run
        [SerializeField] private TransitionAsset[] _wallRunTransitions;

        private ParkourInformation _parkourInfo;
        private bool _enableAnyMovement = false;

        private readonly Dictionary<StringReference, Action<int>> _dicAnimationEvents = new();

        private static readonly eEventLockType[] EVENT_LOCK_TYPES = (eEventLockType[])Enum.GetValues(typeof(eEventLockType));

        public override bool CanEnterState => _parkourInfo.IsValid;
        public override bool CanExitState => base.CanExitState || _playingAniState == null || Movement.IsAnyActionInput;

        public override bool CanEnterFromCurrentState
        {
            get
            {
                switch (GetBaseStateType(CurrentStateType))
                {
                    case eStateType.IDLE:
                    case eStateType.RUN:
                    case eStateType.SPRINT:
                    case eStateType.JUMP:
                    case eStateType.PARKOUR:
                        return true;
                }
                return false;
            }
        }

        public override eEventLockType CurrentEventLockType => _parkourInfo.IsValid && _playingAniState != null ?
            eEventLockType.CharacterParkour | _parkourInfo.EventLockType : base.CurrentEventLockType;

        public override eEventLockType ReleaseEventLockType
        {
            get
            {
                eEventLockType release = eEventLockType.CharacterMove;
                release |= eEventLockType.CameraRotate;
                release |= eEventLockType.CameraOffset;
                release |= _parkourInfo.ReleaseEventLockType;
                return release;
            }
        }

        protected override void Awake()
        {
            base.Awake();

            CacheAnimationEvents(_vaultTransitions);
            CacheAnimationEvents(_jumpTransitions);
            CacheAnimationEvents(_climbTransitions);
            CacheAnimationEvents(_wallRunTransitions);
        }

        protected override void OnEnable()
        {
            BaseOnEnable();
            StartParkourAction();
        }

        public override bool LateUpdateState()
        {
            UpdateAnyMovement();

            if (!base.LateUpdateState())
                return false;

            return true;
        }

        private void UpdateAnyMovement()
        {
            if (_playingAniState == null || !_enableAnyMovement)
                return;

            if (Movement.IsGrounded && Movement.IsActualMoveInput)
                FinishParkourAction();
        }

        private void CacheAnimationEvents(TransitionAsset[] transitionAssets)
        {
            if (transitionAssets.IsNullOrEmpty())
                return;

            for (int i = 0; i < transitionAssets.Length; ++i)
                CacheAnimationEvents(transitionAssets[i]);
        }

        private void CacheAnimationEvents(TransitionAsset transitionAsset)
        {
            if (transitionAsset?.IsValid == false)
                return;

            var events = transitionAsset.GetAllDescendantsEvents();
            if (events.IsNullOrEmpty())
                return;

            foreach (var sequence in events)
            {
                if (sequence.IsEmpty)
                    continue;

                for (int i = 0; i < sequence.Count; ++i)
                {
                    var name = sequence.Names[i];
                    if (_dicAnimationEvents.ContainsKey(name))
                        continue;
                    if (TypeUtility.TryGetAction<int>(this, name, out var action))
                        _dicAnimationEvents.Add(name, action);
                    else
                        LogUtil.LogError($"Event callback name is not defined - [{name}]");
                }
            }
        }

        private void SetAnimationEvents(AnimancerState aniState)
        {
            if (!aniState.IsValid() || _dicAnimationEvents.IsNullOrEmpty())
                return;

            foreach (var aniEvent in _dicAnimationEvents)
                aniState.AddCallbacks(aniEvent.Key, aniEvent.Value);
        }

        private void StartParkourAction()
        {
            _enableAnyMovement = false;

            if (!_parkourInfo.IsValid)
                return;

            Movement.IsParkourInput = false;

            var transition = GetParkourTransition(_parkourInfo.ActionData.AnimationType);
            var success = transition.IsValid();
            _parkourInfo.StartParkourAction(success, transition?.MaximumLength ?? 0f);
            if (!success)
                FinishParkourAction();
        }

        private void PlayParkourAnimation()
        {
            if (!_parkourInfo.IsValid)
                return;

            _parkourInfo.PlayParkourAnimation();

            AnimancerState aniState = null;
            Character.eAnimationType aniType = _parkourInfo.ActionData.CheckFootToSelectAnimation
                ? _parkourInfo.ActionData.GetAnimationType(Movement.FrontFoot)
                : _parkourInfo.ActionData.AnimationType;

            bool requiredSetupEvents = GetParkourTransition(aniType) is IHasKey key && !Animancer.States.TryGet(key, out aniState);
            if (aniState is { IsPlaying: true })
                aniState.Stop();

            _playingAniState = InternalPlayAnimation(aniType, calculateSpeedFunc: state =>_parkourInfo.GetAnimationSpeed(state.Length));

            if (requiredSetupEvents)
                SetAnimationEvents(_playingAniState);
            SetAnimationEndEvent(_playingAniState, OnAnimation_ParkourEndEvent);

            if (_parkourInfo.IsVaultAction)
            {
                if (_parkourInfo.VaultActionData.EnableTargetMatching)
                {
                    Movement.AnimationTargetMatching(_parkourInfo.VaultActionData.TargetMatchingInfo,
                        _playingAniState, _parkourInfo.VaultActionData.EnableTargetMatchingRootWarp);
                }

                if (_parkourInfo.VaultActionData.EnableRootTargetMatching)
                {
                    Movement.AnimationRootTargetMatching(_parkourInfo.VaultActionData.RootTargetMatchingInfo, _playingAniState);
                    _playingAniState.SetChildExitEvent(SetNextChildRootTargetMatching);
                }

                if (!_parkourInfo.VaultActionData.BodyIKWeights.IsNullOrEmpty())
                {
                    foreach (var weight in _parkourInfo.VaultActionData.BodyIKWeights)
                        Movement.SetBodyIKWeights(weight.Type, weight.Weight, weight.MaintainRotationWeight);
                }
            }
        }

        private void SetNextChildRootTargetMatching(AnimancerState state)
        {
            if (_playingAniState == null || !_parkourInfo.IsVaultAction ||
                _parkourInfo.VaultActionData.NextRootTargetMatchingSubInfo is not {} info ||
                _playingAniState.GetCurrentState() is not { } currentState)
                return;

            Movement.AnimationRootTargetMatching(info, currentState);
        }

        private void RestoreUseRootMotion()
        {
            Movement.UseHorizontalRootMotionPosition = CharacterRootMotionMode.Ignore;
            Movement.UseVerticalRootMotionPosition = CharacterRootMotionMode.Ignore;
            Movement.UseRootMotionRotation = CharacterRootMotionMode.Ignore;
        }

        public void SetParkourInfo(in ParkourInformation parkourInfo)
        {
            _parkourInfo = parkourInfo;
            if (enabled)
                StartParkourAction();
        }

        public void StartParkourAction(in Action finishedParkourCallback)
        {
            if (!_parkourInfo.IsValid)
                return;

            PlayParkourAnimation();

            _parkourInfo.FinishedParkourCallback = finishedParkourCallback;
        }

        public void FinishParkourAction()
        {
            if (!_parkourInfo.IsValid)
                return;

            _playingAniState = null;
            Character.LockMoveInput = false;

            RestoreUseRootMotion();

            if (_parkourInfo.IsVaultAction)
            {
                if (_parkourInfo.VaultActionData.EnableTargetMatching)
                    Movement.AnimationStopMatching();

                if (!_parkourInfo.VaultActionData.BodyIKWeights.IsNullOrEmpty())
                    Movement.RestoreBodyIKWeights(_parkourInfo.VaultActionData.BodyIKWeightTypes);
            }

            Movement.IsAirborne = false;

            _parkourInfo.FinishParkourAction();
        }

        private ITransition GetVaultTransition(in Character.eAnimationType animationType)
        {
            return _vaultTransitions[GetVaultTransitionIndex(animationType)];
        }

        private ITransition GetJumpTransition(in Character.eAnimationType animationType)
        {
            return _jumpTransitions[GetJumpTransitionIndex(animationType)];
        }

        private ITransition GetClimbTransition(in Character.eAnimationType animationType)
        {
            return _climbTransitions[GetClimbTransitionIndex(animationType)];
        }

        private ITransition GetWallRunTransition(in Character.eAnimationType animationType)
        {
            return _wallRunTransitions[GetWallRunTransitionIndex(animationType)];
        }

        private int GetVaultTransitionIndex(in Character.eAnimationType animationType)
        {
            return (int)(animationType - Animations.Character.eAnimationType.PARKOUR_TYPE_START) - 1;
        }

        private int GetJumpTransitionIndex(in Character.eAnimationType animationType)
        {
            return (int)(animationType - (Character.eAnimationType)eAnimationType.PARKOUR_JUMP_TYPE_START) - 1;
        }

        private int GetClimbTransitionIndex(in Character.eAnimationType animationType)
        {
            return (int)(animationType - (Character.eAnimationType)eAnimationType.PARKOUR_CLIMB_TYPE_START) - 1;
        }

        private int GetWallRunTransitionIndex(in Character.eAnimationType animationType)
        {
            return (int)(animationType - (Character.eAnimationType)eAnimationType.PARKOUR_WALL_RUN_TYPE_START) - 1;
        }

        private ITransition GetParkourTransition(Character.eAnimationType animationType)
        {
            // vault
            if (animationType is > (Character.eAnimationType)eAnimationType.TYPE_START and < (Character.eAnimationType)eAnimationType.PARKOUR_JUMP_TYPE_START)
                return GetVaultTransition(animationType);
            // jump
            if (animationType is > (Character.eAnimationType)eAnimationType.PARKOUR_JUMP_TYPE_START and < (Character.eAnimationType)eAnimationType.PARKOUR_CLIMB_TYPE_START)
                return GetJumpTransition(animationType);
            // climb
            if (animationType is > (Character.eAnimationType)eAnimationType.PARKOUR_CLIMB_TYPE_START and < (Character.eAnimationType)eAnimationType.PARKOUR_WALL_RUN_TYPE_START)
                return GetClimbTransition(animationType);
            // wall run
            if (animationType is > (Character.eAnimationType)eAnimationType.PARKOUR_WALL_RUN_TYPE_START and < (Character.eAnimationType)eAnimationType.TYPE_END)
                return GetWallRunTransition(animationType);
            return null;
        }

        protected override AnimancerState InternalPlayAnimation(in Character.eAnimationType animationType,
            in float animationSpeed = 1f, in Func<AnimancerState, float> calculateSpeedFunc = null, in eLayerType layerType = eLayerType.BASE)
        {
            // switch (animationType)
            // {
            //     // vault
            //     case eAnimationType.PARKOUR_VAULT_OVER_LEFT:
            //     case eAnimationType.PARKOUR_VAULT_OVER_RIGHT:
            //     case eAnimationType.PARKOUR_VAULT_ON:
            //         return Animation.PlayAnimation(animationType, GetVaultTransition(animationType), animationSpeed, calculateSpeedFunc, layerType);
            //     // jump
            //     case eAnimationType.PARKOUR_JUMP_LEFT_FOOT:
            //     case eAnimationType.PARKOUR_JUMP_RIGHT_FOOT:
            //     case eAnimationType.PARKOUR_JUMP_ROLL_LANDING:
            //         return Animation.PlayAnimation(animationType, GetJumpTransition(animationType), animationSpeed, calculateSpeedFunc, layerType);
            //     // climb
            //     case eAnimationType.PARKOUR_CLIMB_UP_JUMP:
            //         return Animation.PlayAnimation(animationType, GetClimbTransition(animationType), animationSpeed, calculateSpeedFunc, layerType);
            //     // wall run
            //     case eAnimationType.PARKOUR_WALL_RUN_START_LEFT:
            //     case eAnimationType.PARKOUR_WALL_RUN_START_RIGHT:
            //     case eAnimationType.PARKOUR_WALL_RUN_LEFT:
            //     case eAnimationType.PARKOUR_WALL_RUN_RIGHT:
            //     case eAnimationType.PARKOUR_WALL_RUN_JUMP_LEFT:
            //     case eAnimationType.PARKOUR_WALL_RUN_JUMP_RIGHT:
            //         return Animation.PlayAnimation(animationType, GetWallRunTransition(animationType), animationSpeed, calculateSpeedFunc, layerType);
            // }

            var state = GetParkourTransition(animationType) is { } transition ?
                Animation.PlayAnimation(animationType, transition, animationSpeed, calculateSpeedFunc, layerType) :
                base.InternalPlayAnimation(animationType, animationSpeed, calculateSpeedFunc, layerType);
            ExecuteMixerRecalculateWeights(state);
            SetUseRootMotion(state);
            return state;
        }

        private void SetEventLock(int eventIndex, bool isLock)
        {
            if (EVENT_LOCK_TYPES.Length <= eventIndex)
                return;

            var lockType = EVENT_LOCK_TYPES[eventIndex];
            if (lockType == eEventLockType.Max)
                return;

            if (isLock)
            {
                _parkourInfo.EventLockType |= lockType;
                _parkourInfo.ReleaseEventLockType &= ~lockType;
            }
            else
            {
                _parkourInfo.EventLockType &= ~lockType;
                _parkourInfo.ReleaseEventLockType |= lockType;
            }
        }

        #region Events callback
        private bool IsValidEventValue(in int value) => value is 0 or 1;

        private void OnParkourAni_LockMoveInputEvent(int value)
        {
            if (!IsLocal || !_parkourInfo.IsValid || !IsValidEventValue(value))
                return;

            Character.LockMoveInput = Convert.ToBoolean(value) && _parkourInfo.ActionData.LockMoveInput;
            if (Character.LockMoveInput)
            {
                _parkourInfo.EventLockType |= eEventLockType.CharacterInputLock;
                _parkourInfo.ReleaseEventLockType &= ~eEventLockType.CharacterInputLock;
            }
            else
            {
                _parkourInfo.EventLockType &= ~eEventLockType.CharacterInputLock;
                _parkourInfo.ReleaseEventLockType |= eEventLockType.CharacterInputLock;
            }
        }

        private void OnParkourAni_LockEventTypeEvent(int value)
        {
            if (!IsLocal || !_parkourInfo.IsValid || value <= (int)eEventLockType.None)
                return;

            SetEventLock(value, true);
        }

        private void OnParkourAni_UnlockEventTypeEvent(int value)
        {
            if (!IsLocal || !_parkourInfo.IsValid || value <= (int)eEventLockType.None)
                return;

            SetEventLock(value, false);
        }

        private void OnParkourAni_RootMotionHorizontalEvent(int value)
        {
            if (!IsLocal || !_parkourInfo.IsValid)
                return;

            var mode = (CharacterRootMotionMode)value;
            Movement.UseHorizontalRootMotionPosition = mode != CharacterRootMotionMode.None ?
                mode : _parkourInfo.ActionData.HorizontalPosRootMotion;
        }

        private void OnParkourAni_RootMotionVerticalEvent(int value)
        {
            if (!IsLocal || !_parkourInfo.IsValid)
                return;

            var mode = (CharacterRootMotionMode)value;
            Movement.UseVerticalRootMotionPosition = mode != CharacterRootMotionMode.None ?
                mode : _parkourInfo.ActionData.VerticalPosRootMotion;
        }

        private void OnParkourAni_RootMotionRotationEvent(int value)
        {
            if (!IsLocal || !_parkourInfo.IsValid)
                return;

            var mode = (CharacterRootMotionMode)value;
            Movement.UseRootMotionRotation = mode != CharacterRootMotionMode.None ?
                mode : _parkourInfo.ActionData.RotationRootMotion;
        }

        private void OnParkourAni_SolverHorizontalVelocityEvent(int value)
        {
            if (!IsLocal || !_parkourInfo.IsValid || !IsValidEventValue(value))
                return;

            var isApply = Convert.ToBoolean(value);
            _parkourInfo.ActionData.ApplySolverHorizontalVelocity = isApply;
        }

        private void OnParkourAni_SolverVerticalVelocityEvent(int value)
        {
            if (!IsLocal || !_parkourInfo.IsValid || !IsValidEventValue(value))
                return;

            var isApply = Convert.ToBoolean(value);
            _parkourInfo.ActionData.ApplySolverVerticalVelocity = isApply;
        }

        private void OnParkourAni_ChangeAirborneEvent(int value)
        {
            if (!IsLocal || !IsValidEventValue(value))
                return;

            var isAirborne = Convert.ToBoolean(value);
            Movement.IsAirborne = isAirborne;
        }

        public void OnParkourAni_ChangeFallEvent(int value)
        {
            if (!_parkourInfo.IsValid || !_parkourInfo.IsJumpAction || !_parkourInfo.JumpActionData.JumpData.IsFall)
                return;

            FinishParkourAction();
        }

        private void OnParkourAni_ForceUngroundEvent(int value)
        {
            if (!IsLocal)
                return;

            LocalCharacter?.ForceUnground();
        }

        private void OnParkourAni_ActivationCollisionWithHitTargetEvent(int value)
        {
            if (!IsLocal || !_parkourInfo.IsValid || !_parkourInfo.IsVaultAction || !IsValidEventValue(value))
                return;

            var isActive = Convert.ToBoolean(value);
            _parkourInfo.VaultActionData.InactivationCollisionWithHitTarget = !isActive;
        }

        private void OnParkourAni_ActivationGroundSolvingEvent(int value)
        {
            if (!IsLocal || !_parkourInfo.IsValid || !IsValidEventValue(value))
                return;

            var isActive = Convert.ToBoolean(value);
            if (isActive || _parkourInfo.ActionData.InactivationGroundSolving)
                LocalCharacter?.ActivationMotorGroundSolving(isActive);
        }

        private void OnParkourAni_AniRootMatchingRestoreEvent(int value)
        {
            if (!_parkourInfo.IsValid || !_parkourInfo.IsVaultAction)
                return;

            if (_parkourInfo.VaultActionData.EnableTargetMatching)
                Movement.AnimationRestoreRootMatching();
        }

        private void OnParkourAni_AniMatchingStopEvent(int value)
        {
            if (!_parkourInfo.IsValid || !_parkourInfo.IsVaultAction)
                return;

            if (_parkourInfo.VaultActionData.EnableTargetMatching)
                Movement.AnimationStopMatching();
        }

        private void OnParkourAni_ChangeBodyIKWeightsEvent(int value)
        {
            if (!_parkourInfo.IsValid || !_parkourInfo.IsVaultAction || !IsValidEventValue(value))
                return;

            var isSet = Convert.ToBoolean(value);
            if (!_parkourInfo.VaultActionData.BodyIKWeights.IsNullOrEmpty())
            {
                if (isSet)
                {
                    foreach (var weight in _parkourInfo.VaultActionData.BodyIKWeights)
                        Movement.SetBodyIKWeights(weight.Type, weight.Weight, weight.MaintainRotationWeight);
                }
                else
                {
                    Movement.RestoreBodyIKWeights(_parkourInfo.VaultActionData.BodyIKWeightTypes);
                }
            }
            else
            {
                Movement.RestoreBodyIKWeights((AvatarTarget[])Enum.GetValues(typeof(AvatarTarget)));
            }
        }

        private void OnParkourAni_EnableGrounderIKEvent(int value)
        {
            if (!_parkourInfo.IsValid || !_parkourInfo.IsVaultAction || !IsValidEventValue(value))
                return;

            var isEnable = Convert.ToBoolean(value);
            Movement.EnableGrounderIK(isEnable);
        }

        private void OnParkourAni_RestoreAnimationSpeedEvent(int value)
        {
            if (!_parkourInfo.IsValid || !IsValidEventValue(value) || _playingAniState == null)
                return;

            _playingAniState.Speed = 1f;
        }

        public void OnParkourAni_EnableAnyMovementEvent(int value)
        {
            if (!_parkourInfo.IsValid)
                return;

            _enableAnyMovement = true;
        }

        private void OnAnimation_ParkourEndEvent()
        {
            if (_playingAniState == null || !_parkourInfo.IsValid)
                return;

            FinishParkourAction();
        }
        #endregion

// #if UNITY_EDITOR
//         private void OnGUI()
//          {
//              if (!_parkourInfo.IsValid)
//                  return;
//
//              if (_parkourInfo.ActionData is ParkourJumpActionData jumpActionData)
//              {
//                  Vector3 screenPos = Camera.main.WorldToScreenPoint(jumpActionData.JumpData.TargetPosition);
//                  GUI.DrawTexture(new Rect(screenPos.x - 15, Screen.height - screenPos.y - 15, 30, 30), Texture2D.normalTexture);
//              }
//          }
//
//         private void OnDrawGizmosSelected()
//         {
//             if (!_parkourInfo.IsValid)
//                 return;
//
//             if (_parkourInfo.ActionData is ParkourVaultActionData vaultActionData)
//             {
//                 Gizmos.color = Color.red;
//                 Gizmos.DrawWireSphere(vaultActionData.HitData.forwardHit.point, 0.03f);
//
//                 if (vaultActionData.HitData.heightHitFound)
//                 {
//                     Gizmos.color = Color.green;
//                     Gizmos.DrawWireSphere(vaultActionData.HitData.heightHit.point, 0.03f);
//                 }
//
//                 if (vaultActionData.HitData.ledgeHitFound)
//                 {
//                     Gizmos.color = Color.yellow;
//                     Gizmos.DrawWireSphere(vaultActionData.HitData.ledgeHit.point, 0.03f);
//                 }
//             }
//             else if (_parkourInfo.ActionData is ParkourJumpActionData jumpActionData)
//             {
//                 Gizmos.color = Color.red;
//                 Gizmos.DrawWireSphere(jumpActionData.JumpData.TargetPosition, 0.03f);
//             }
//         }
// #endif

        public struct ParkourInformation
        {
            internal ParkourActionData ActionData;
            internal Action<bool, float> StartParkourCallback;
            internal Action FinishedParkourCallback;
            internal eEventLockType EventLockType;
            internal eEventLockType ReleaseEventLockType;
            internal bool IsStarted;

            internal bool IsValid => ActionData != null;
            internal bool IsVaultAction => VaultActionData != null;
            internal bool IsJumpAction => JumpActionData != null;

            internal ParkourVaultActionData VaultActionData { get; private set; }
            internal ParkourJumpActionData JumpActionData { get; private set; }

            public static ParkourInformation Create(ParkourActionData actionData, Action<bool, float> startParkourCallback)
            {
                return new ParkourInformation()
                {
                    ActionData = actionData,
                    StartParkourCallback = startParkourCallback,
                    EventLockType = eEventLockType.None,
                    ReleaseEventLockType =  eEventLockType.None,
                    VaultActionData = actionData as ParkourVaultActionData,
                    JumpActionData = actionData as ParkourJumpActionData
                };
            }

            public float GetAnimationSpeed(float animationLength)
            {
                return ActionData.ChangeAnimationSpeedBasedDuration && ActionData.Duration > 0 ?
                    Mathf.Clamp((ActionData.AnimationLength > 0 ?
                        ActionData.AnimationLength : animationLength) / ActionData.Duration, 0.2f, 3f) : 1f;
            }

            public void StartParkourAction(in bool success, in float animationLength)
            {
                if (IsStarted)
                    return;

                IsStarted = success;
                StartParkourCallback?.Invoke(success, animationLength);
                StartParkourCallback = null;
            }

            public void PlayParkourAnimation()
            {
            }

            public void FinishParkourAction()
            {
                EventLockType = eEventLockType.None;
                ReleaseEventLockType = eEventLockType.None;
                IsStarted = false;
                ActionData = null;
                VaultActionData = null;
                JumpActionData = null;
                FinishedParkourCallback?.Invoke();
                FinishedParkourCallback = null;
            }
        }
    }
}