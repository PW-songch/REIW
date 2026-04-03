using System;
using System.Collections.Generic;
using Animancer;
using Animancer.Units;
using Cysharp.Threading.Tasks;
using RootMotion.FinalIK;
using UnityEngine;
using REIW.EventLock;

namespace REIW.Animations.Character
{
    public class GrappleAnimationState : AirborneAnimationState
    {
        [AnimationType(eStateType.GRAPPLE)]
        public enum eAnimationType : uint
        {
            TYPE_START = Animations.Character.eAnimationType.GRAPPLE_TYPE_START,
            GRAPPLE_THROW_UP,
            GRAPPLE_THROW,
            GRAPPLE_THROW_DOWN,
            GRAPPLE_THROW_AIR_UP,
            GRAPPLE_THROW_AIR,
            GRAPPLE_THROW_AIR_DOWN,
            GRAPPLE_MOVE_SHORT,
            GRAPPLE_MOVE_MEDIUM,
            GRAPPLE_MOVE_REG,
            GRAPPLE_MOVE_SPIN,
            GRAPPLE_ARRIVE_SHORT,
            GRAPPLE_ARRIVE,
            GRAPPLE_ARRIVE_WOBBLE,
            GRAPPLE_ARRIVE_SPIN_LANDING,
            GRAPPLE_LAUNCH,
            GRAPPLE_LAUNCH_SPIN,
            GRAPPLE_FALL,
            GRAPPLE_LANDING,
            TYPE_END
        }

        private enum eGrappleAnimationState
        {
            NONE,
            THROW,
            MOVE,
            ARRIVE,
            LAUNCH,
            FALL,
            LANDING
        }

        public override eStateType StateType => eStateType.GRAPPLE;

        [SerializeField] private MixerTransition2D _throwMixer;
        [SerializeField] private MixerTransition2D _moveMixer;
        [SerializeField] private LinearMixerTransition _arriveMixer;
        [SerializeField] private LinearMixerTransition _launchMixer;

        [SerializeField, Meters(Rule = Validate.Value.IsNotNegative)]
        private float _moveShortAniMaxDistance = 7f;

        [SerializeField, Meters(Rule = Validate.Value.IsNotNegative)]
        private float _moveMediumAniMaxDistance = 15f;

        [SerializeField, Meters(Rule = Validate.Value.IsNotNegative)]
        private float _moveRegAniMaxDistance = 25f;

        [SerializeField, Meters(Rule = Validate.Value.IsNotNegative)]
        private float _moveSpinAniMaxDistance = 43f;

        [Tooltip("상단 Throw 모션의 적용 각도")] [SerializeField, Degrees]
        private float _throwUpAngle = 30f;

        [Tooltip("하단 Throw 모션의 적용 각도")] [SerializeField, Degrees]
        private float _throwDownAngle = -30f;

        [Tooltip("Throw 모션의 IK 적용 부위")] [SerializeField]
        private AvatarIKGoal[] _throwIKGoals;

        [Tooltip("Throw 모션의 IK 적용 Weight")] [SerializeField, Range(0, 1)]
        private float[] _throwIKPositionWeights;

        [Tooltip("Throw 모션의 IK 적용 Weight 변화 속도")] [SerializeField]
        private float _throwIKPositionWeightSpeed = 1f;

        private GrappleInformation _grappleInfo;
        private eGrappleAnimationState _currentGrappleState;
        private Character.eAnimationType _playingAnimationType;
        private Character.eAnimationType _throwAnimationType;
        private Character.eAnimationType _arriveAnimationType;
        private Character.eAnimationType _launchAnimationType;

        public override bool CanEnterState => _grappleInfo.IsValid;
        public override bool CanExitState => (Movement.IsGrappleInput || _isLanding) && (IsArriveEnd || IsLandingEndByAniState || IsLandingEndByMovement);

        private bool IsArriveEnd => (_currentGrappleState == eGrappleAnimationState.ARRIVE && _playingAniState == null);
        private bool IsLandingEndByAniState => _currentGrappleState == eGrappleAnimationState.LANDING && _playingAniState == null;
        private bool IsLandingEndByMovement => _currentGrappleState == eGrappleAnimationState.LANDING && Movement.MovementDirection != Vector3.zero &&
            (_playingAniState != null && (Movement.GetMovementData<CharacterMoveGrapple>()?.AvailableLandingMove(_playingAniState.NormalizedTime) ?? false));

        public bool IsEnableThrowGrapple => _currentGrappleState == eGrappleAnimationState.LAUNCH ||
                                            _currentGrappleState == eGrappleAnimationState.FALL ||
                                            !enabled;

        public bool IsGrappling => enabled && eGrappleAnimationState.NONE < _currentGrappleState &&
                                   _currentGrappleState <= eGrappleAnimationState.ARRIVE;

        private bool CanThrowGrapple => _currentGrappleState switch
        {
            eGrappleAnimationState.LAUNCH or eGrappleAnimationState.FALL => true,
            _                                                            => false,
        };

        private FullBodyBipedIK BodyIK => Movement.BodyIK;
        private bool _onCameraEventGrapple = false;

        protected override void OnEnable()
        {
            BaseOnEnable();
            PlayThrowAnimation();
            _onCameraEventGrapple = true;
        }

        protected override void OnDisable()
        {
            base.OnDisable();

            if (Movement.IsJumpInput)
            {
                Movement.IsContinuousJump = true;

                if (!_grappleInfo.IsFar)
                    Movement.CurrentMoveType = eMoveType.SPRINT;
            }

            if(Movement.IsContinuousJump == false)
                PlayerController.Instance.OwnerPlayerNetObject.AddSnapShot_ActionCancel();

            Movement.IsGrappleInput = false;
            _currentGrappleState = eGrappleAnimationState.NONE;
            _onCameraEventGrapple = false;
        }

        public override bool LateUpdateState()
        {
            if (!base.LateUpdateState())
                return false;

            UpdateCurrentState();
            UpdateThrowAnimationIK();

            return true;
        }

        protected override void UpdateLanding()
        {
            if (_currentGrappleState == eGrappleAnimationState.FALL)
                base.UpdateLanding();
        }

        private void UpdateCurrentState()
        {
            if (CanThrowGrapple == false)
                return;

            UpdateThrow();
        }

        private void UpdateThrow()
        {
            if (Movement.IsGrappleInput)
            {
                if (IsEnableThrowGrapple)
                    PlayThrowAnimation();
                else
                    _grappleInfo.StartGrapple(false);
            }
        }

        private void PlayThrowAnimation()
        {
            Reset();

            _playingAniState = InternalPlayAnimation(GetThrowAnimationType());
            SetAnimationEndEvent(_playingAniState, OnAnimation_ThrowEndEvent);

            Movement.SetAirbornStateGrounderIKWeight(true);
            Movement.IsLanding = false;
            Movement.IsGrappleInput = false;

            if (IsLocal)
            {
                CharacterMoveGrapple grapple = LocalCharacter.CharacterMoveComponentsHandler.GetMoveComponent<CharacterMoveGrapple>();
                grapple?.StartThrow();
            }
        }

        private void PlayMoveAnimation()
        {
            _playingAniState = InternalPlayAnimation(GetMoveAnimationType(),
                calculateSpeedFunc: (state) =>
                {
                    if (state.IsValid() && _moveMixer.State != null)
                    {
                        _moveMixer.State.Parameter = GetMoveAnimationParameter();
                        _moveMixer.State.RecalculateWeights();
                    }
                    return _grappleInfo.GetMoveAnimationSpeed(state.Length);
                });
            SetThrowAnimationIK();
        }

        private void PlayArriveAnimation(bool force)
        {
            Character.eAnimationType type = force ? (Character.eAnimationType)eAnimationType.GRAPPLE_ARRIVE : GetArriveAnimationType();
            _playingAniState = InternalPlayAnimation(type);
            SetAnimationEndEvent(_playingAniState, OnAnimation_ArriveEndEvent);

            Movement.SetAirbornStateGrounderIKWeight(false);
            Movement.IsLanding = false;
            _isLanding = true;
        }

        private void PlayLaunchAnimation()
        {
            if (_grappleInfo.StartLaunchCallback == null)
                return;

            _enableAnyMovement = false;
            Movement.SetAirbornStateGrounderIKWeight(true);
            _playingAniState = InternalPlayAnimation(GetLaunchAnimationType());
            SetAnimationEndEvent(_playingAniState, OnAnimation_LaunchEndEvent);
        }

        private void PlayFallAnimation()
        {
            _playingAniState = InternalPlayAnimation((Character.eAnimationType)eAnimationType.GRAPPLE_FALL);
        }

        protected override bool PlayLandingAnimation()
        {
            if (!base.PlayLandingAnimation())
                return false;

            Movement.SetAirbornStateGrounderIKWeight(false);
            Movement.IsLanding = false;

            Character.CharacterEffectSound.StopLoopingSfx((int)eKnownSfxSound.SE_Cynox_F_GrappleFall );

            return true;
        }

        public override bool IsPlayingAnimation(in float normalizedTime)
        {
            if (base.IsPlayingAnimation(normalizedTime))
                return true;
            if (Animancer.States.TryGet(_throwMixer, out var state) && state.IsPlaying &&
                state.NormalizedTime < normalizedTime)
                return true;
            if (Animancer.States.TryGet(_moveMixer, out state) && state.IsPlaying &&
                state.NormalizedTime < normalizedTime)
                return true;
            if (Animancer.States.TryGet(_arriveMixer, out state) && state.IsPlaying &&
                state.NormalizedTime < normalizedTime)
                return true;
            if (Animancer.States.TryGet(_launchMixer, out state) && state.IsPlaying &&
                state.NormalizedTime < normalizedTime)
                return true;
            return false;
        }

        private Dictionary<Character.eAnimationType, eKnownSfxSound> _sounds = new Dictionary<Character.eAnimationType, eKnownSfxSound>()
        {
            // { (eAnimationType)eGrappleAnimationType.GRAPPLE_TYPE_START,  eKnownSfxSound.   },
            { (Character.eAnimationType)eAnimationType.GRAPPLE_THROW_UP, eKnownSfxSound.SE_Cynox_F_GrappleThrow },
            { (Character.eAnimationType)eAnimationType.GRAPPLE_THROW, eKnownSfxSound.SE_Cynox_F_GrappleThrow },
            { (Character.eAnimationType)eAnimationType.GRAPPLE_THROW_DOWN, eKnownSfxSound.SE_Cynox_F_GrappleThrow },
            { (Character.eAnimationType)eAnimationType.GRAPPLE_THROW_AIR_UP, eKnownSfxSound.SE_Cynox_F_GrappleThrow },
            { (Character.eAnimationType)eAnimationType.GRAPPLE_THROW_AIR, eKnownSfxSound.SE_Cynox_F_GrappleThrow },
            { (Character.eAnimationType)eAnimationType.GRAPPLE_THROW_AIR_DOWN, eKnownSfxSound.SE_Cynox_F_GrappleThrow },

            { (Character.eAnimationType)eAnimationType.GRAPPLE_MOVE_SHORT, eKnownSfxSound.SE_Cynox_F_GrappleMoveShortMid }, //once
            { (Character.eAnimationType)eAnimationType.GRAPPLE_MOVE_MEDIUM, eKnownSfxSound.SE_Cynox_F_GrappleMoveMediumMid }, //once
            { (Character.eAnimationType)eAnimationType.GRAPPLE_MOVE_REG, eKnownSfxSound.SE_Cynox_F_GrappleMoveReg },  // reg looping
            { (Character.eAnimationType)eAnimationType.GRAPPLE_MOVE_SPIN, eKnownSfxSound.SE_Cynox_F_GrappleMoveSpin }, // once

            { (Character.eAnimationType)eAnimationType.GRAPPLE_ARRIVE_SHORT, eKnownSfxSound.SE_Cynox_F_GrappleArriveShort },
            { (Character.eAnimationType)eAnimationType.GRAPPLE_ARRIVE, eKnownSfxSound.SE_Cynox_F_GrappleArrive },
            { (Character.eAnimationType)eAnimationType.GRAPPLE_ARRIVE_WOBBLE, eKnownSfxSound.SE_Cynox_F_GrappleArriveWobble },
            { (Character.eAnimationType)eAnimationType.GRAPPLE_ARRIVE_SPIN_LANDING, eKnownSfxSound.SE_Cynox_F_GrappleArriveSpinLand },

            { (Character.eAnimationType)eAnimationType.GRAPPLE_LAUNCH, eKnownSfxSound.SE_Cynox_F_GrappleLandJump },
            { (Character.eAnimationType)eAnimationType.GRAPPLE_LAUNCH_SPIN, eKnownSfxSound.SE_Cynox_F_GrappleSpinLandJump },
            //

            { (Character.eAnimationType)eAnimationType.GRAPPLE_LANDING, eKnownSfxSound.SE_Cynox_F_GrappleArriveLand}, //?
            { (Character.eAnimationType)eAnimationType.GRAPPLE_FALL, eKnownSfxSound.SE_Cynox_F_GrappleFall }, // loop

            // { (eAnimationType)eGrappleAnimationType.GRAPPLE_TYPE_END, eKnownSfxSound. },

        };

        protected override AnimancerState InternalPlayAnimation(in Character.eAnimationType animationType,
            in float animationSpeed = 1f, in Func<AnimancerState, float> calculateSpeedFunc = null, in eLayerType layerType = eLayerType.BASE)
        {
            _playingAnimationType = animationType;

            AnimancerState state = null;

            switch (animationType)
            {
                case (Character.eAnimationType)eAnimationType.GRAPPLE_THROW:
                case (Character.eAnimationType)eAnimationType.GRAPPLE_THROW_UP:
                case (Character.eAnimationType)eAnimationType.GRAPPLE_THROW_DOWN:
                case (Character.eAnimationType)eAnimationType.GRAPPLE_THROW_AIR:
                case (Character.eAnimationType)eAnimationType.GRAPPLE_THROW_AIR_UP:
                case (Character.eAnimationType)eAnimationType.GRAPPLE_THROW_AIR_DOWN:
                {
                    _currentGrappleState = eGrappleAnimationState.THROW;
                    _throwAnimationType = animationType;

                    state = Animation.PlayAnimation(animationType, _throwMixer, animationSpeed, calculateSpeedFunc, layerType);
                    if (state.IsValid() && _throwMixer.State != null)
                    {
                        if (_sounds.ContainsKey(animationType))
                        {

                            var ct = Character.GetCancellationTokenOnDestroy();
                            if (Character.IsLocalCharacter)
                            {
                                //Debug.LogWarning("InAnimationType:" +InAnimationType);
                                // Effect Wire Action
                                Character.CharacterEffectSound.WireTargetPointActivateEvent(_grappleInfo.GrapplePosition, ct).Forget();
                                Character.CharacterEffectSound.WireAction(_grappleInfo.GrapplePosition, ct).Forget();
                                Character.CharacterEffectSound.AddSnapShot_GrapplePosition(_grappleInfo.GrapplePosition);
                            }
                            Character.CharacterEffectSound.PlayCharacterSfx((int)_sounds[animationType]);
                        }

                    }

                    break;
                }
                case (Character.eAnimationType)eAnimationType.GRAPPLE_MOVE_SHORT:
                case (Character.eAnimationType)eAnimationType.GRAPPLE_MOVE_MEDIUM:
                case (Character.eAnimationType)eAnimationType.GRAPPLE_MOVE_REG:
                case (Character.eAnimationType)eAnimationType.GRAPPLE_MOVE_SPIN:
                {
                    _currentGrappleState = eGrappleAnimationState.MOVE;

                    //Debug.LogWarning("InAnimationType:" + InAnimationType);

                    state = Animation.PlayAnimation(animationType, _moveMixer, animationSpeed, calculateSpeedFunc, layerType);
                    if (state.IsValid() && _moveMixer.State != null)
                    {
                        _moveMixer.State.Parameter = GetMoveAnimationParameter();
                        if (_sounds.ContainsKey(animationType))
                        {
                            if (animationType != (Character.eAnimationType)eAnimationType.GRAPPLE_MOVE_REG)
                            {
                                Character.CharacterEffectSound.PlayCharacterSfx((int)_sounds[animationType]);
                            }
                            // Debug.LogWarning("PlayLoopSfx");
                            Character.CharacterEffectSound.PlayCharacterLoopSfx((int)eKnownSfxSound.SE_Cynox_F_GrappleMoveReg);
                        }
                    }
                    break;
                }
                case (Character.eAnimationType)eAnimationType.GRAPPLE_ARRIVE_SHORT:
                case (Character.eAnimationType)eAnimationType.GRAPPLE_ARRIVE:
                case (Character.eAnimationType)eAnimationType.GRAPPLE_ARRIVE_WOBBLE: //좁은땅
                case (Character.eAnimationType)eAnimationType.GRAPPLE_ARRIVE_SPIN_LANDING:
                {
                    _currentGrappleState = eGrappleAnimationState.ARRIVE;
                    _arriveAnimationType = animationType;

                    state = Animation.PlayAnimation(animationType, _arriveMixer, animationSpeed, calculateSpeedFunc, layerType);
                    if (state.IsValid() && _arriveMixer.State != null)
                    {
                        _arriveMixer.State.Parameter = GetArriveAnimationParameter();
                        if (_sounds.ContainsKey(animationType))
                        {
                            Character.CharacterEffectSound.PlayCharacterSfx((int)_sounds[animationType]);
                        }
                    }

                    if (Character.IsLocalCharacter)
                    {
                        Character.CharacterEffectSound.AddSnapShot_GrappleEnd();
                    }


                    Character.CharacterEffectSound.StopWireAction();

                    try
                    {
                        // Debug.LogWarning("StopLoopSfx");
                        Character.CharacterEffectSound.StopLoopingSfx((int)eKnownSfxSound.SE_Cynox_F_GrappleMoveReg);
                    }
                    catch (Exception e)
                    {
                        LogUtil.LogError("Unknown Try Catch :");
                    }
                    break;
                }
                case (Character.eAnimationType)eAnimationType.GRAPPLE_LAUNCH:
                case (Character.eAnimationType)eAnimationType.GRAPPLE_LAUNCH_SPIN:
                {
                    _currentGrappleState = eGrappleAnimationState.LAUNCH;
                    _launchAnimationType = animationType;

                    state = Animation.PlayAnimation(animationType, _launchMixer, animationSpeed, calculateSpeedFunc, layerType);
                    if (state.IsValid() && _launchMixer.State != null)
                    {
                        _launchMixer.State.Parameter = GetLaunchAnimationParameter();
                        if (_sounds.ContainsKey(animationType))
                        {
                            Character.CharacterEffectSound.PlayCharacterSfx((int)_sounds[animationType]);
                        }
                    }
                    break;
                }
                case (Character.eAnimationType)eAnimationType.GRAPPLE_LANDING:
                {
                    _currentGrappleState = eGrappleAnimationState.LANDING;

                    state = Animation.PlayAnimation(animationType, _landingMixer, animationSpeed, calculateSpeedFunc, layerType);
                    if (state.IsValid() && _landingMixer.State != null)
                    {
                        _landingMixer.State.Parameter = GetLandingAnimationParameter();
                        if (_sounds.ContainsKey(animationType))
                        {
                            Character.CharacterEffectSound.PlayCharacterSfx((int)_sounds[animationType]);
                        }
                    }
                    break;
                }
                case (Character.eAnimationType)eAnimationType.GRAPPLE_FALL:
                    _currentGrappleState = eGrappleAnimationState.FALL;

                    state = Animation.PlayAnimation(animationType, _fall, animationSpeed, calculateSpeedFunc, layerType);
                    if (state.IsValid() && _sounds.ContainsKey(animationType))
                    {
                        Character.CharacterEffectSound.PlayCharacterLoopSfx((int)_sounds[animationType]);
                    }
                    break;
            }

            ExecuteMixerRecalculateWeights(state);
            SetUseRootMotion(state);

            return state;
        }

        private void SetThrowAnimationIK(in Transform InTarget = null)
        {
            var bodyIK = BodyIK;
            if (!bodyIK)
                return;

            for (int i = 0; i < _throwIKGoals.Length; ++i)
            {
                switch (_throwIKGoals[i])
                {
                    case AvatarIKGoal.LeftHand:
                        bodyIK.solver.leftHandEffector.target = InTarget;
                        break;
                    case AvatarIKGoal.RightHand:
                        bodyIK.solver.rightHandEffector.target = InTarget;
                        break;
                }
            }
        }

        private void UpdateThrowAnimationIK()
        {
            if (_currentGrappleState == eGrappleAnimationState.NONE ||
                _currentGrappleState > eGrappleAnimationState.MOVE)
                return;

            var bodyIK = BodyIK;
            if (!bodyIK)
                return;

            for (int i = 0; i < _throwIKGoals.Length; ++i)
            {
                IKEffector hand = null;
                switch (_throwIKGoals[i])
                {
                    case AvatarIKGoal.LeftHand:
                        hand = bodyIK.solver.leftHandEffector;
                        break;
                    case AvatarIKGoal.RightHand:
                        hand = bodyIK.solver.rightHandEffector;
                        break;
                }

                if (hand != null)
                {
                    hand.positionWeight = Mathf.Clamp(hand.positionWeight + _throwIKPositionWeightSpeed * Time.deltaTime *
                        (hand.target == null ? -1f : 1f), 0, _throwIKPositionWeights[i]);
                }
            }
        }

        public void SetGrappleInfo(GrappleInformation InGrappleInfo)
        {
            _grappleInfo = InGrappleInfo;
        }

        public void StartGrapple(in GrapplePoint InTarget, in float InGrappleMoveTime)
        {
            _grappleInfo.Target = InTarget;
            _grappleInfo.GrappleMoveTime = InGrappleMoveTime;

            PlayMoveAnimation();
        }

        public void ArriveGrapple(bool force)
        {
            if (!enabled)
                return;

            PlayArriveAnimation(force);
        }

        public void LaunchRequested(in Action<bool> InStartLaunchCallback)
        {
            if (!enabled || !_grappleInfo.IsFar)
            {
                InStartLaunchCallback?.Invoke(false);
                return;
            }

            _grappleInfo.StartLaunchCallback = InStartLaunchCallback;
            Movement.IsJumpInput = false;
            Movement.UseHorizontalRootMotionPosition = CharacterRootMotionMode.Ignore;
        }

        public void LandingLaunch()
        {
            if (!enabled)
                return;

            PlayLandingAnimation();
        }

        private float GetMoveAnimationMaxDistance(in Character.eAnimationType InAnimationType)
        {
            return InAnimationType switch
            {
                (Character.eAnimationType)eAnimationType.GRAPPLE_MOVE_SHORT => _moveShortAniMaxDistance,
                (Character.eAnimationType)eAnimationType.GRAPPLE_MOVE_MEDIUM => _moveMediumAniMaxDistance,
                (Character.eAnimationType)eAnimationType.GRAPPLE_MOVE_REG => _moveRegAniMaxDistance,
                (Character.eAnimationType)eAnimationType.GRAPPLE_MOVE_SPIN => _moveSpinAniMaxDistance,
                _ => _moveShortAniMaxDistance
            };
        }

        private Character.eAnimationType GetThrowAnimationType()
        {
            float angle =
                Movement.GetVerticalAngleToTarget(Character.CharacterTransform, _grappleInfo.TargetTransform);
            if (Movement.IsGrounded)
            {
                if (angle > _throwUpAngle)
                    return (Character.eAnimationType)eAnimationType.GRAPPLE_THROW_UP;
                if (angle < _throwDownAngle)
                    return (Character.eAnimationType)eAnimationType.GRAPPLE_THROW_DOWN;
                return (Character.eAnimationType)eAnimationType.GRAPPLE_THROW;
            }
            else
            {
                if (angle > _throwUpAngle)
                    return (Character.eAnimationType)eAnimationType.GRAPPLE_THROW_AIR_UP;
                if (angle < _throwDownAngle)
                    return (Character.eAnimationType)eAnimationType.GRAPPLE_THROW_AIR_DOWN;
                return (Character.eAnimationType)eAnimationType.GRAPPLE_THROW_AIR;
            }
        }

        private Character.eAnimationType GetMoveAnimationType()
        {
            return _grappleInfo.GrappleDistance switch
            {
                var distance when distance < _moveShortAniMaxDistance => (Character.eAnimationType)eAnimationType.GRAPPLE_MOVE_SHORT,
                var distance when distance < _moveMediumAniMaxDistance => (Character.eAnimationType)eAnimationType.GRAPPLE_MOVE_MEDIUM,
                var distance when distance < _moveRegAniMaxDistance => (Character.eAnimationType)eAnimationType.GRAPPLE_MOVE_REG,
                _ => (Character.eAnimationType)eAnimationType.GRAPPLE_MOVE_SPIN
            };
        }

        private Character.eAnimationType GetArriveAnimationType()
        {
            return _grappleInfo.Target != null
                ? _grappleInfo.Target.GetArriveAnimationType(_playingAnimationType)
                : (Character.eAnimationType)eAnimationType.GRAPPLE_ARRIVE;
        }

        private Character.eAnimationType GetLaunchAnimationType()
        {
            switch (_arriveAnimationType)
            {
                case (Character.eAnimationType)eAnimationType.GRAPPLE_ARRIVE_SPIN_LANDING:
                    return (Character.eAnimationType)eAnimationType.GRAPPLE_LAUNCH_SPIN;
                default:
                    return (Character.eAnimationType)eAnimationType.GRAPPLE_LAUNCH;
            }
        }

        protected override Character.eAnimationType GetLandingAnimationType()
        {
            return (Character.eAnimationType)eAnimationType.GRAPPLE_LANDING;
        }

        private Vector2 GetThrowAnimationParameter()
        {
            switch (_playingAnimationType)
            {
                default:
                case (Character.eAnimationType)eAnimationType.GRAPPLE_THROW_UP:
                    return new Vector2(1, 1);
                case (Character.eAnimationType)eAnimationType.GRAPPLE_THROW:
                    return new Vector2(1, 2);
                case (Character.eAnimationType)eAnimationType.GRAPPLE_THROW_DOWN:
                    return new Vector2(1, 3);
                case (Character.eAnimationType)eAnimationType.GRAPPLE_THROW_AIR_UP:
                    return new Vector2(2, 1);
                case (Character.eAnimationType)eAnimationType.GRAPPLE_THROW_AIR:
                    return new Vector2(2, 2);
                case (Character.eAnimationType)eAnimationType.GRAPPLE_THROW_AIR_DOWN:
                    return new Vector2(2, 3);
            }
        }

        private Vector2 GetMoveAnimationParameter()
        {
            var x = _playingAnimationType switch
            {
                (Character.eAnimationType)eAnimationType.GRAPPLE_MOVE_SHORT => 1,
                (Character.eAnimationType)eAnimationType.GRAPPLE_MOVE_MEDIUM => 2,
                (Character.eAnimationType)eAnimationType.GRAPPLE_MOVE_REG => 3,
                (Character.eAnimationType)eAnimationType.GRAPPLE_MOVE_SPIN => 4,
                _ => 1
            };

            var y = 1;
            switch (_playingAnimationType)
            {
                case (Character.eAnimationType)eAnimationType.GRAPPLE_MOVE_SHORT:
                case (Character.eAnimationType)eAnimationType.GRAPPLE_MOVE_MEDIUM:
                    y = _throwAnimationType switch
                    {
                        (Character.eAnimationType)eAnimationType.GRAPPLE_THROW_UP => 1,
                        (Character.eAnimationType)eAnimationType.GRAPPLE_THROW => 2,
                        (Character.eAnimationType)eAnimationType.GRAPPLE_THROW_DOWN => 3,
                        (Character.eAnimationType)eAnimationType.GRAPPLE_THROW_AIR_UP => 1,
                        (Character.eAnimationType)eAnimationType.GRAPPLE_THROW_AIR => 2,
                        (Character.eAnimationType)eAnimationType.GRAPPLE_THROW_AIR_DOWN => 3,
                        _ => 1
                    };
                    break;
            }

            return new Vector2(x, y);
        }

        private int GetArriveAnimationParameter()
        {
            return _arriveAnimationType switch
            {
                (Character.eAnimationType)eAnimationType.GRAPPLE_ARRIVE_SHORT => 1,
                (Character.eAnimationType)eAnimationType.GRAPPLE_ARRIVE => 2,
                (Character.eAnimationType)eAnimationType.GRAPPLE_ARRIVE_WOBBLE => 3,
                (Character.eAnimationType)eAnimationType.GRAPPLE_ARRIVE_SPIN_LANDING => 4,
                _ => 1
            };
        }

        private int GetLaunchAnimationParameter()
        {
            return _launchAnimationType switch
            {
                (Character.eAnimationType)eAnimationType.GRAPPLE_LAUNCH => 1,
                (Character.eAnimationType)eAnimationType.GRAPPLE_LAUNCH_SPIN => 2,
                _ => 1
            };
        }

        protected override Vector2 GetLandingAnimationParameter()
        {
            return new Vector2(_playingAnimationType switch
            {
                (Character.eAnimationType)eAnimationType.GRAPPLE_LANDING => 1,
                _ => 1
            }, 0);
        }

        private void OnAnimation_ThrowEndEvent()
        {
            _grappleInfo.StartGrapple(true);
        }

        private void OnAnimation_ArriveEndEvent()
        {
            _playingAniState = null;
            _grappleInfo.StartLaunch(false);
        }

        private void OnAnimation_LaunchEndEvent()
        {
            if (_currentGrappleState != eGrappleAnimationState.LAUNCH)
                return;

            PlayFallAnimation();
        }

        public void OnAnimation_EnableAnyMovementArriveEvent(int InArriveType)
        {
            if (GetArriveAnimationParameter() != InArriveType)
                return;

            _enableAnyMovement = true;
            _grappleInfo.StartLaunch(false);
        }

        public void OnAnimation_PlayLaunchAnimationEvent(int InArriveType)
        {
            if (GetArriveAnimationParameter() != InArriveType)
                return;

            _onCameraEventGrapple = false;
            PlayLaunchAnimation();
        }

        public void OnAnimation_StartLaunchEvent(int InLaunchType)
        {
            if (GetLaunchAnimationParameter() != InLaunchType)
                return;

            _grappleInfo.StartLaunch(true);
        }

//        private bool IsLandingAirBone => _isLanding && Movement.CurrentMoveType == eMoveType.AIRBORNE;
        public override eEventLockType CurrentEventLockType
        {
            get
            {
                int eventlock = (int)(CanThrowGrapple ? base.CurrentEventLockType : eEventLockType.CharacterGlide);
                return (eEventLockType)eventlock;
            }
        }
        
        public override eEventLockType ReleaseEventLockType
        {
            get
            {
                eEventLockType release = eEventLockType.CharacterMove;
                release |= eEventLockType.CameraRotate;
                release |= eEventLockType.CharacterJump;
                return release;
            }
        }

        public override IngameCameraSystem_Event.CameraEventType CameraEventType
        {
            get
            {
                if (_onCameraEventGrapple)
                    return IngameCameraSystem_Event.CameraEventType.Grapple;

                return base.CameraEventType;
            }
        }

// #if UNITY_EDITOR
//         private void OnGUI()
//         {
//             if (_currentGrappleState == eGrappleAnimationState.THROW && PlayerController.Instance.IsStandalone)
//             {
//                 Vector3 screenPos = Camera.main.WorldToScreenPoint(_grappleInfo.GrapplePosition);
//                 GUI.DrawTexture(new Rect(screenPos.x - 15, Screen.height - screenPos.y - 15, 30, 30), Texture2D.normalTexture);
//             }
//         }
// #endif

        public struct GrappleInformation
        {
            public GrapplePoint Target;
            public Vector3 GrapplePosition;
            public float GrappleDistance;
            public float GrappleMoveTime;
            public bool IsFar;
            public Action<bool> StartGrappleCallback;
            public Action<bool> StartLaunchCallback;

            internal Transform TargetTransform => Target?.transform;
            internal bool IsValid => Target != null;

            public static GrappleInformation Create(GrapplePoint InTarget, Vector3 InGrapplePosition,
                float InGrappleDistance, bool InFar, Action<bool> InStartGrappleCallback)
            {
                LogUtil.Log($"Is Far : {InFar}");
                return new GrappleInformation()
                {
                    Target = InTarget,
                    GrapplePosition = InGrapplePosition,
                    GrappleDistance = InGrappleDistance,
                    IsFar = InFar,
                    StartGrappleCallback = InStartGrappleCallback
                };
            }

            public float GetMoveAnimationSpeed(float InAnimationLength)
            {
                return Mathf.Clamp(InAnimationLength / GrappleMoveTime, 0.2f, 3f) * 0.95f;
            }

            public void StartGrapple(bool InSuccess)
            {
                StartGrappleCallback?.Invoke(InSuccess);
                StartGrappleCallback = null;
            }

            public void StartLaunch(bool InSuccess)
            {
                StartLaunchCallback?.Invoke(InSuccess);
                StartLaunchCallback = null;
            }
        }
    }
}