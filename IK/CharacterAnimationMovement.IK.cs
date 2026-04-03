using System;
using System.Collections;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using Animancer;
using UnityEngine;
using Animancer.Units;
using RootMotion.FinalIK;
using UnityEngine.Serialization;
using static Animancer.Validate;

namespace REIW.Animations.Character
{
    public partial class CharacterAnimationMovement
    {
        private const AvatarIKGoal NONE_AVATAR_IK_TYPE = (AvatarIKGoal)int.MinValue;
        private const int LEFT_FOOT_INDEX = 0;
        private const int RIGHT_FOOT_INDEX = 1;

        [Header("IK")]
        [SerializeField] private FullBodyBipedIK _bodyIK;
        [SerializeField] private GrounderFBBIK _grounderIK;
        [SerializeField] private AimIK _aimIK;

        [Header("GrounderIK Settings")]
        [SerializeField, Meters] private float _checkLandingDistance = 0f;
        [SerializeField, Range(0, 1)] private float _airbornGrounderIKWeight = 0.01f;
        [SerializeField, Range(0, 1)] private float _slopeGrounderIKWeight = 0.5f;
        [SerializeField] private float _airborneGrounderIKMaxStep = 2;
        [SerializeField] private float _lowGrounderIKMaxStep = 0.2f;
        [SerializeField] private float _lowGrounderIKFootSpeed = 1f;
        [SerializeField] private float _applyingGroundIKRotationOffset = 200f;
        [SerializeField, Degrees(Rule = Value.IsNotNegative)]
        private float _flatGroundAngle = 5f;
        [SerializeField, Seconds(Rule = Value.IsNotNegative)]
        private float _checkStopDelay = 2f;
        [SerializeField] private float _stopGrounderIKFootSpeed = 0.01f;
        [SerializeField, Range(0, 1)] private float _movingPlatformGrounderIKPelvisDamper = 0f;
        [SerializeField] private float _movingPlatformGrounderIKMaxStep = 0.1f;

        [FormerlySerializedAs("_targetMatching")]
        [Header("FBBIKTargetMatching")]
        [SerializeField] private FBBIKTargetMatching _fbbIKTargetMatching;

        [Header("AimIKController")]
        [SerializeField] private AimIKController _aimIKController;

        [Header("Mount IK Settings")]
        private float _initMountGrounderIKPelvisDamper;
        private const float _isMountGrounderIKPelvisDamper = 0.05f;

        private float _targetGrounderIKWeight;
        private float _stopStartTime;
        private float _desiredGrounderIKFootSpeed;
        private ReadOnlyValue<float> _grounderIK_Weight;
        private ReadOnlyValue<float> _grounderIK_FootSpeed;
        private ReadOnlyValue<float> _grounderIK_MaxStep;
        private ReadOnlyValue<bool> _grounderIK_RotateSolver;
        private ReadOnlyValue<float> _grounderIK_PelvisDamper;
        private ReadOnlyDictionary<AvatarTarget, ReadOnlyValue<(float weight, float maintainRotationWeight)>> _bodyIKMappingWeights;

        private Coroutine _restoreBodyIKMappingWeightsCoroutine;

        public FullBodyBipedIK BodyIK => _bodyIK;
        public GrounderFBBIK GrounderIK => _grounderIK;

        private bool IsValidBodyIK => _bodyIK && _bodyIK.solver.initiated && _bodyIK.solver.IKPositionWeight > 0f;
        private bool IsValidGrounderIK => _grounderIK && _grounderIK.initiated && _grounderIK.weight > 0f;

        public bool IsGrounded => Character ? Character.IsStableOnCollider : (!IsValidGrounderIK || _grounderIK.solver.isGrounded);
        private bool IsMovingPlatformState => Mathf.Approximately(_grounderIK.solver.pelvisDamper, _movingPlatformGrounderIKPelvisDamper);

        public bool IsLanding { get; set; }

        public bool IsAirborne
        {
            get => _isAirborne;
            set
            {
                _isAirborne = value;
                if (_isAirborne)
                    IsLanding = false;
                if (IsValidGrounderIK)
                {
                    EnableGrounderIK(true);
                    SetAirbornStateGrounderIKMaxStep(_isAirborne);

                    if (_isAirborne)
                        SetAirbornStateGrounderIKWeight(true);
                }
            }
        }

        public float DesiredGrounderIKFootSpeed { set => _desiredGrounderIKFootSpeed = value; }
        public float LowGrounderIKFootSpeed => _lowGrounderIKFootSpeed;
        public float GrounderIKFootSpeed => _grounderIK_FootSpeed;

        private void InitializeIK()
        {
            if (_bodyIK)
            {
                Dictionary<AvatarTarget, ReadOnlyValue<(float, float)>> bodyIKMappingWeights =  new()
                {
                    { AvatarTarget.Root, GetBodyIKWeights(AvatarTarget.Root) },
                    { AvatarTarget.Body, GetBodyIKWeights(AvatarTarget.Body) }
                };

                foreach (AvatarTarget type in Enum.GetValues(typeof(AvatarTarget)))
                {
                    if (bodyIKMappingWeights.ContainsKey(type))
                        continue;

                    var weights = GetBodyIKWeights(type);
                    if (weights.weight >= 0f)
                        bodyIKMappingWeights.Add(type, (weights.weight, weights.maintainRotationWeight));
                }

                _bodyIKMappingWeights = new ReadOnlyDictionary<AvatarTarget, ReadOnlyValue<(float, float)>>(bodyIKMappingWeights);
            }

            if (_grounderIK)
            {
                _grounderIK.ik = _bodyIK;
                _targetGrounderIKWeight = _grounderIK.weight;
                _grounderIK_Weight = _grounderIK.weight;
                _grounderIK_FootSpeed = _grounderIK.solver.footSpeed;
                _grounderIK_RotateSolver = _grounderIK.solver.rotateSolver;
                _grounderIK_PelvisDamper = _grounderIK.solver.pelvisDamper;
                _footGroundedTimes = new float[RIGHT_FOOT_INDEX + 1];
                _footStepCoolTimes = new float[RIGHT_FOOT_INDEX + 1];
                _footGroundedStates = new bool[RIGHT_FOOT_INDEX + 1];
                _footStepPositions = new Vector3[RIGHT_FOOT_INDEX + 1];
                _footStepSlopes = new eSlopeDirection[RIGHT_FOOT_INDEX + 1];

                if (IsLocalCharacter)
                {
                    //_grounderIK.solver.maxStep = ((LocalCharacter)Character).Motor.MaxStepHeight;
                    CheckGroundedFoot = true;
                }

                _grounderIK_MaxStep = _grounderIK.solver.maxStep;
                _initMountGrounderIKPelvisDamper = _grounderIK.solver.pelvisDamper;
            }

            if (_aimIK)
                _aimIK.enabled = false;

            _fbbIKTargetMatching?.Initialize(Character, _characterAnimation, _bodyIK);

            if (_aimIKController)
            {
                _aimIKController.Initialize(_bodyIK, _aimIK);
                EnableAimIKController(false);
            }
        }

        public void EnableIK(bool enable, bool update = false)
        {
            EnableBodyIK(enable, update);
            EnableGrounderIK(enable, update);
        }

        public void EnableBodyIK(bool enable, bool update = false)
        {
            if (!_bodyIK || _bodyIK.enabled == enable)
                return;

            _bodyIK.enabled = enable;

            if (enable && update)
                _bodyIK.UpdateSolverExternal();
        }

        public void EnableGrounderIK(bool enable, bool update = false)
        {
            if (!_grounderIK || _grounderIK.enabled == enable)
                return;

            _grounderIK.enabled = enable;

            if (enable && update)
                _grounderIK.solver.Update();
        }

        public void EnableAimIK(bool enable, bool update = false)
        {
            if (!_aimIK || _aimIK.enabled == enable)
                return;

            _aimIK.enabled = enable;

            if (enable && update)
                _aimIK.UpdateSolverExternal();
        }

        public void EnableAimIKController(bool enable)
        {
            if (!_aimIKController || _aimIKController.enabled == enable)
                return;

            _aimIKController.Enable(enable);
        }

        public void SetBodyIKWeights(AvatarTarget type, float weight, float maintainRotationWeight, bool stopCoroutine = true)
        {
            if (!_bodyIK)
                return;

            switch (type)
            {
                case AvatarTarget.Root:
                    _bodyIK.solver.SetIKPositionWeight(weight);
                    break;
                case AvatarTarget.Body:
                    _bodyIK.solver.spineMapping.twistWeight = weight;
                    break;
                default:
                    var chainType = ConvertType(type);
                    var mapping = _bodyIK.solver.GetLimbMapping(chainType);
                    if (mapping != null)
                    {
                        mapping.weight = weight;
                        mapping.maintainRotationWeight = maintainRotationWeight;
                    }
                    break;
            }

            if (stopCoroutine && _restoreBodyIKMappingWeightsCoroutine != null)
            {
                StopCoroutine(_restoreBodyIKMappingWeightsCoroutine);
                _restoreBodyIKMappingWeightsCoroutine = null;
            }
        }

        public void RestoreBodyIKWeights(AvatarTarget[] types)
        {
            if (!_bodyIK || types.IsNullOrEmpty())
                return;

            if (_restoreBodyIKMappingWeightsCoroutine != null)
                StopCoroutine(_restoreBodyIKMappingWeightsCoroutine);

            _restoreBodyIKMappingWeightsCoroutine = StartCoroutine(CorRestoreBodyIKWeights(types));
        }

        private (float weight, float maintainRotationWeight) GetBodyIKWeights(AvatarTarget type)
        {
            if (!_bodyIK)
                return (-1, -1);

            switch (type)
            {
                case AvatarTarget.Root:
                    return (_bodyIK.solver.GetIKPositionWeight(), 0f);
                case AvatarTarget.Body:
                    return (_bodyIK.solver.spineMapping.twistWeight, 0f);
                default:
                    var chainType = ConvertType(type);
                    var mapping = _bodyIK.solver.GetLimbMapping(chainType);
                    if (mapping != null)
                        return (mapping.weight, mapping.maintainRotationWeight);
                    break;
            }

            return (-1, -1);
        }

        private IEnumerator CorRestoreBodyIKWeights(AvatarTarget[] types)
        {
            if (!_bodyIK || types.IsNullOrEmpty())
            {
                _restoreBodyIKMappingWeightsCoroutine = null;
                yield break;
            }

            while (true)
            {
                var completed = 0;
                var t = Time.deltaTime * 10f;
                for (int i = 0; i < types.Length; ++i)
                {
                    var type = types[i];
                    if (_bodyIKMappingWeights.TryGetValue(type, out var originalWeights))
                    {
                        var weights = GetBodyIKWeights(type);
                        SetBodyIKWeights(type, Mathf.Clamp01(Mathf.Lerp(weights.weight, originalWeights.Value.weight, t)),
                            Mathf.Clamp01(Mathf.Lerp(weights.maintainRotationWeight, originalWeights.Value.maintainRotationWeight, t)), false);

                        if (Mathf.Approximately(GetBodyIKWeights(type).weight, originalWeights.Value.weight) && ++completed == types.Length)
                        {
                            _restoreBodyIKMappingWeightsCoroutine = null;
                            yield break;
                        }
                    }
                }

                completed = 0;
                yield return null;
            }
        }

        public void CheckGroundedMaxStepGrounderIK()
        {
            if (!IsValidGrounderIK)
                return;

            if (IsGrounded && !_grounderIK.solver.isGrounded)
                _grounderIK.solver.maxStep += 0.1f;
        }

        public void SetAirbornStateGrounderIKMaxStep(bool airborne)
        {
            if (!IsValidGrounderIK)
                return;

            _grounderIK.solver.maxStep = airborne ? _airborneGrounderIKMaxStep : _grounderIK_MaxStep;
        }

        public void SetAirbornStateGrounderIKWeight(bool set)
        {
            if (!IsValidGrounderIK)
                return;

            _targetGrounderIKWeight = set ? _airbornGrounderIKWeight : _grounderIK_Weight;
            DesiredGrounderIKFootSpeed = set ? GrounderIKFootSpeed : 0f;

            if (set)
                _grounderIK.weight = _airbornGrounderIKWeight;
        }

        public void SetMovingPlatformStateGrounderIK(bool set)
        {
            if (!IsValidGrounderIK)
                return;

            _grounderIK.solver.pelvisDamper = set ? _movingPlatformGrounderIKPelvisDamper : _grounderIK_PelvisDamper;
            _grounderIK.solver.maxStep = set ? _movingPlatformGrounderIKMaxStep : _grounderIK_MaxStep;
        }

        public bool IsApplyingGrounderIK()
        {
            if (!IsValidGrounderIK)
                return false;

            return _grounderIK.solver.legs[LEFT_FOOT_INDEX].rotationOffset.eulerAngles.magnitude >
                   _applyingGroundIKRotationOffset ||
                   _grounderIK.solver.legs[RIGHT_FOOT_INDEX].rotationOffset.eulerAngles.magnitude >
                   _applyingGroundIKRotationOffset ||
                   !IsFlatGround();
        }

        public void GravityChange(bool worldGravity)
        {
            if (!IsValidGrounderIK)
                return;

            _grounderIK.solver.rotateSolver = !worldGravity || _grounderIK_RotateSolver;
        }

        public void AnimationTargetMatching(TargetMatchingInfo targetMatchingInfo, AnimancerState state, bool rootWarp)
        {
            if (!_fbbIKTargetMatching || targetMatchingInfo == null)
                return;

            _fbbIKTargetMatching.TargetMatching(targetMatchingInfo, state, rootWarp);
        }

        public void AnimationRootTargetMatching(TargetMatchingInfo targetMatchingInfo, AnimancerState state)
        {
            if (!_fbbIKTargetMatching || targetMatchingInfo == null)
                return;

            _fbbIKTargetMatching.RootTargetMatching(targetMatchingInfo, state);
        }

        public void AnimationStopMatching()
        {
            if (!_fbbIKTargetMatching)
                return;

            _fbbIKTargetMatching.StopMatching(true);
        }

        public void AnimationRestoreRootMatching()
        {
            if (!_fbbIKTargetMatching)
                return;

            _fbbIKTargetMatching.RestoreRootWarp = true;
            _fbbIKTargetMatching.RestoreRootMatching = true;
        }

		public bool IsFlatGround()
        {
            if (!IsValidGrounderIK)
                return true;

            return Vector3.Angle(GetGroundNormal(), -Character.Gravity) < _flatGroundAngle;
        }

        private Vector3 GetGroundNormal()
        {
            return IsGrounded ? Character.SurfaceStatus.SurfaceNormal : Character.Up;
        }

        public eSlopeDirection GetSlopeDirection(Vector3 forward)
        {
            var up = -Character.Gravity.normalized;
            var n = GetGroundNormal();
            if (n.sqrMagnitude < 1e-6f)
                n = up;

            if (Vector3.Angle(n, up) < _flatGroundAngle)
                return eSlopeDirection.Flat;

            var gravity = -up;
            var downhill = Vector3.ProjectOnPlane(gravity, n);
            var downhillMag = downhill.magnitude;
            if (downhillMag > 1e-6f)
                downhill /= downhillMag;
            else
                downhill = Vector3.zero;

            var fwd = Vector3.ProjectOnPlane(forward, up).normalized;
            var dot = (downhill == Vector3.zero) ? 0f : Vector3.Dot(fwd, downhill);

            const float crossEps = 0.1f;
            var dir = dot switch
            {
                > crossEps => eSlopeDirection.Downhill,
                _ => eSlopeDirection.Uphill,
            };

            return dir;
        }

        public void SetMountGrounderIKPelvisDamper(bool isMounted)
        {
            if (!_grounderIK)
                return;

            // 마운트 탑승/하차 시, GrounderIK의 pelvisDamper 값을 변경
            _grounderIK.solver.pelvisDamper = isMounted ? _isMountGrounderIKPelvisDamper : _initMountGrounderIKPelvisDamper;
        }

        private void UpdateGrounderIK()
        {
            if (!IsValidGrounderIK)
                return;

            bool isMoving = _characterAnimation.IsMoving;
            if (isMoving)
                _stopStartTime = 0f;
            else if (_stopStartTime == 0f)
                _stopStartTime = Time.time;

            if (!isMoving && Time.time - _stopStartTime > _checkStopDelay)
            {
                _grounderIK.solver.footSpeed = _stopGrounderIKFootSpeed;
                _grounderIK.solver.frontFootOverstepFallsDown = false;
            }
            else
            {
                _grounderIK.solver.footSpeed = _desiredGrounderIKFootSpeed > 0f ? _desiredGrounderIKFootSpeed :
                    (isMoving || AnimationParameters.ForwardSpeed > WalkSpeed || !IsGrounded ?
                        _grounderIK_FootSpeed : _lowGrounderIKFootSpeed);
                _grounderIK.solver.frontFootOverstepFallsDown = IsGrounded;
            }

            if (!IsLocalCharacter)
            {
                _grounderIK.solver.quality = isMoving ? Grounding.Quality.Fastest : Grounding.Quality.Simple;
            }
            else if (!IsAirborne)
            {
                if (isMoving)
                {
                    _targetGrounderIKWeight = IsFlatGround() ? _grounderIK_Weight : _slopeGrounderIKWeight;
                    _grounderIK.solver.maxStep = AnimationParameters.ForwardSpeed > WalkSpeed ? _lowGrounderIKMaxStep :
                            (IsMovingPlatformState ? _movingPlatformGrounderIKMaxStep : _grounderIK_MaxStep);
                }
                else
                {
                    _targetGrounderIKWeight = _grounderIK_Weight;
                    _grounderIK.solver.maxStep = IsMovingPlatformState ? _movingPlatformGrounderIKMaxStep : _grounderIK_MaxStep;
                }
            }

            _grounderIK.weight = Mathf.Lerp(_grounderIK.weight, _targetGrounderIKWeight, Time.deltaTime * 10f);
        }

        private void UpdateAimIKController()
        {
            if (!_aimIKController || !_aimIKController.enabled)
                return;

            var aimTarget = MyLocalCharacter.CameraTarget.position +
                            (MyLocalCharacter.Forward + MyLocalCharacter.CameraTarget.forward).normalized * 10f;
            _aimIKController.UpdateAim(aimTarget);
        }

        private void UpdateAirborneState()
        {
            if (!IsValidGrounderIK)
                return;

            if (IsAirborne)
            {
                if (IsGrounded)
                {
                    IsAirborne = false;
                    IsLanding = true;
                }
                else if (CharacterAnimation.CurrentBaseStateType == eStateType.JUMP
                         && _grounderIK.enabled && !IsLanding && CurrentMoveVelocity.y < 0f && !IsMovingPlatformState)
                {
                    var heightFromGround = float.MaxValue;
                    for (int i = 0; i < _grounderIK.solver.legs.Length; ++i)
                        heightFromGround = Mathf.Min(heightFromGround, _grounderIK.solver.legs[i].heightFromGround);

                    if (heightFromGround < _checkLandingDistance)
                        IsLanding = true;
                }
            }
            else if (!IsGrounded)
            {
                if (VerticalSpeedParameter < _checkAirborneVerticalSpeed)
                    IsAirborne = true;
            }
        }

        private FullBodyBipedChain ConvertType(AvatarTarget type)
        {
            return type switch
            {
                AvatarTarget.LeftFoot => FullBodyBipedChain.LeftLeg,
                AvatarTarget.RightFoot => FullBodyBipedChain.RightLeg,
                AvatarTarget.LeftHand => FullBodyBipedChain.LeftArm,
                AvatarTarget.RightHand => FullBodyBipedChain.RightArm,
                _ => (FullBodyBipedChain)(-1)
            };
        }
    }
}
