using System;
using Animancer.Units;
using UnityEngine;
using static Animancer.Validate;

namespace REIW.Animations.Character
{
    public partial class CharacterAnimationMovement
    {
        [Header("Grounded Foot Settings")]
        [SerializeField, Meters(Rule = Value.IsNotNegative)]
        private float _walkGroundedFrontFootCheckDistance = 0f;
        [SerializeField, Seconds(Rule = Value.IsNotNegative)]
        private float _walkGroundedFootCheckTime = 0f;
        [SerializeField, Seconds(Rule = Value.IsNotNegative)]
        private float _runGroundedFootCheckTime = 0f;
        [SerializeField, Seconds(Rule = Value.IsNotNegative)]
        private float _sprintGroundedFootCheckTime = 0f;

        [Header("Foot Step Settings")]
        [SerializeField, Degrees(Rule = Value.IsNotNegative)]
        private float _footStepSlopeAngleMax = 40.0f;
        [SerializeField, MetersPerSecond(Rule = Value.IsNotNegative)]
        private float _footStepDownSpeedMin = 0.1f;
        [SerializeField, MetersPerSecond(Rule = Value.IsNotNegative)]
        private float _footStepDownSpeedMax = 1.0f;
        [SerializeField, Seconds(Rule = Value.IsNotNegative)]
        private float _footStepCoolTime = 0.1f;
        [SerializeField] private float _footStepBasePower = 1.0f;

        private float[] _footGroundedTimes;
        private float[] _footStepCoolTimes;
        private bool[] _footGroundedStates;
        private Vector3[] _footStepPositions;
        private eSlopeDirection[] _footStepSlopes;

        public bool ForceFindGroundedFoot { set; private get; }
        public AvatarIKGoal JumpFoot { get; private set; }

        public AvatarIKGoal FrontFoot
        {
            get
            {
                AvatarIKGoal footType = NONE_AVATAR_IK_TYPE;

                if (IsValidGrounderIK)
                {
                    var groundedLegs = UnityEngine.Pool.ListPool<(AvatarIKGoal FootType, Vector3 IKPosition)>.Get();
                    if (_grounderIK.solver.legs[LEFT_FOOT_INDEX].isGrounded)
                        groundedLegs.Add((AvatarIKGoal.LeftFoot,
                            _grounderIK.solver.legs[LEFT_FOOT_INDEX].IKPosition));
                    if (_grounderIK.solver.legs[RIGHT_FOOT_INDEX].isGrounded)
                        groundedLegs.Add((AvatarIKGoal.RightFoot,
                            _grounderIK.solver.legs[RIGHT_FOOT_INDEX].IKPosition));

                    if (groundedLegs.Count == 0)
                    {
                        groundedLegs.Add((AvatarIKGoal.LeftFoot,
                            _grounderIK.solver.legs[LEFT_FOOT_INDEX].IKPosition));
                        groundedLegs.Add((AvatarIKGoal.RightFoot,
                            _grounderIK.solver.legs[RIGHT_FOOT_INDEX].IKPosition));
                    }

                    float footZ = float.MinValue;
                    for (int i = 0; i < groundedLegs.Count; ++i)
                    {
                        float z = Character.CharacterTransform.InverseTransformPoint(groundedLegs[i].IKPosition).z;
                        if (footZ < z)
                        {
                            footZ = z;
                            footType = groundedLegs[i].FootType;
                        }
                    }

                    UnityEngine.Pool.ListPool<(AvatarIKGoal FootType, Vector3 IKPosition)>.Release(groundedLegs);
                }

                return footType;
            }
        }

        private float GroundedFootCheckTime => IsSprint ? _sprintGroundedFootCheckTime :
            (IsWalking ? _walkGroundedFootCheckTime : _runGroundedFootCheckTime);

        public event Action<(AvatarIKGoal footType, float footPower, eKnownSfxSound groundTag)> FootStepEvent;

        private void UpdateFoots()
        {
            if (!IsValidGrounderIK)
                return;

            AvatarIKGoal prev = JumpFoot;
            JumpFoot = NONE_AVATAR_IK_TYPE;

            if (!_grounderIK.solver.isGrounded ||
                (!IsMoving && Mathf.Approximately(AnimationParameters.ForwardSpeed, 0f)))
            {
                for (int i = 0; i < _footGroundedTimes.Length; ++i)
                    _footGroundedTimes[i] = 0f;
                return;
            }

            float groundedFootCheckTime = GroundedFootCheckTime;
            float time = Time.time;
            float footZ = 0f;
            int footIndex = -1;

            for (int i = 0; i < _grounderIK.solver.legs.Length; ++i)
            {
                if (_grounderIK.solver.legs[i].isGrounded)
                {
                    float z = Character.CharacterTransform.InverseTransformPoint(_grounderIK.solver.legs[i].IKPosition).z;
                    if (_footGroundedTimes[i] == 0f && (ForceFindGroundedFoot || z > _grounderIK.solver.footRadius))
                        _footGroundedTimes[i] = time;

                    if (time - _footGroundedTimes[i] <= groundedFootCheckTime)
                    {
                        if (footIndex >= 0)
                        {
                            if (z > footZ)
                            {
                                footIndex = i;
                                footZ = z;
                            }
                        }
                        else
                        {
                            footIndex = i;
                            footZ = z;
                        }

                        JumpFoot = footIndex == LEFT_FOOT_INDEX ? AvatarIKGoal.LeftFoot : AvatarIKGoal.RightFoot;
                    }
                }
                else
                {
                    _footGroundedTimes[i] = 0f;
                }
            }

            if (JumpFoot != NONE_AVATAR_IK_TYPE)
            {
                return;
            }
            else if (ForceFindGroundedFoot)
            {
                for (int i = 0; i < _grounderIK.solver.legs.Length; ++i)
                {
                    if (_grounderIK.solver.legs[i].isGrounded)
                    {
                        float z = Character.CharacterTransform.InverseTransformPoint(_grounderIK.solver.legs[i].IKPosition).z;
                        if (footIndex >= 0)
                        {
                            if (z > footZ)
                            {
                                footIndex = i;
                                footZ = z;
                            }
                        }
                        else
                        {
                            footIndex = i;
                            footZ = z;
                        }

                        JumpFoot = footIndex == LEFT_FOOT_INDEX ? AvatarIKGoal.LeftFoot : AvatarIKGoal.RightFoot;
                    }
                }

                if (JumpFoot != NONE_AVATAR_IK_TYPE)
                    return;
            }

            if (IsWalking)
            {
                float foot0 = Character.CharacterTransform
                    .InverseTransformPoint(_grounderIK.solver.legs[LEFT_FOOT_INDEX].IKPosition).z;
                float foot1 = Character.CharacterTransform
                    .InverseTransformPoint(_grounderIK.solver.legs[RIGHT_FOOT_INDEX].IKPosition).z;
                if (foot0 - foot1 > _walkGroundedFrontFootCheckDistance)
                    JumpFoot = AvatarIKGoal.LeftFoot;
                else if (foot1 - foot0 > _walkGroundedFrontFootCheckDistance)
                    JumpFoot = AvatarIKGoal.RightFoot;
            }
        }

        private void UpdateFootStep()
        {
            if (FootStepEvent == null || !CheckGroundedFoot || !IsValidGrounderIK)
                return;

            var locomotionMoving = IsLocomotionMoving;
            var frontFoot = IsLocomotionMoving ? FrontFoot : NONE_AVATAR_IK_TYPE;
            var slope = GetSlopeDirection(Character.Forward);
            var footSlopes = ClassifyFootSlope(Character.CurrentMoveVelocity,
                _footStepSlopes[LEFT_FOOT_INDEX], _footStepSlopes[RIGHT_FOOT_INDEX],
                _grounderIK.solver.legs[LEFT_FOOT_INDEX].IKPosition,
                _grounderIK.solver.legs[RIGHT_FOOT_INDEX].IKPosition,
                GetGroundNormal(), Character.Up);
            _footStepSlopes[LEFT_FOOT_INDEX] = footSlopes.left;
            _footStepSlopes[RIGHT_FOOT_INDEX] = footSlopes.right;

            for (int i = 0; i < _grounderIK.solver.legs.Length; ++i)
            {
                _footStepCoolTimes[i] -= Time.deltaTime;

                var checkStep = slope == eSlopeDirection.Uphill || frontFoot == NONE_AVATAR_IK_TYPE || (int)frontFoot == i;
                FootStepProcess(checkStep, locomotionMoving, slope, _footStepSlopes[i], footSlopes.isTraverse, i,
                    ref _footGroundedStates[i], ref _footStepPositions[i],  ref _footStepCoolTimes[i]);
            }
        }
        
        private void FootStepProcess(in bool checkStep, in bool locomotionMoving,
            in eSlopeDirection slope, in eSlopeDirection footSlope, in bool traverse, in int footIndex,
            ref bool wasGrounded, ref Vector3 lastPos, ref float coolTime)
        {
            var leg = _grounderIK.solver.legs[footIndex];
            var grounded = leg.isGrounded && IsGrounded;
            var curPos = leg.IKPosition;
            var velocity = Time.deltaTime > 0f ? (curPos - lastPos) / Time.deltaTime : Vector3.zero;
            var hit = leg.GetHitPoint;
            var hasHit = hit.collider;
            var n = hasHit ? hit.normal.normalized : Character.Up;
            var p = hasHit ? hit.point : curPos;
            var approachSpeed = Mathf.Max(0f, -Vector3.Dot(velocity, n));
            var distToPlane = Vector3.Dot(curPos - p, n);
            var slopeDeg = slope != eSlopeDirection.Flat && !traverse ? Vector3.Angle(n, Character.Up) : 0f;
            var tSlope = Mathf.InverseLerp(0f, _footStepSlopeAngleMax, slopeDeg);
            var enterHeight  = Mathf.Lerp(IsWalking ? 0.2f : 0.25f, 0.5f, tSlope);
            var exitHeight   = Mathf.Lerp(slope == eSlopeDirection.Flat ||
                                          (!traverse && footSlope == eSlopeDirection.Downhill) ||
                                          (traverse && slope == eSlopeDirection.Uphill) ? 0.1f : 0.3f, 0.1f, tSlope);
            var minApproach  = Mathf.Lerp(_footStepDownSpeedMin, _footStepDownSpeedMin * 0.25f, tSlope);
            var maxApproach  = Mathf.Lerp(_footStepDownSpeedMax, _footStepDownSpeedMax * 0.50f, tSlope);

            if (!wasGrounded && checkStep && grounded && coolTime <= 0f &&
                hasHit && distToPlane < enterHeight && approachSpeed >= minApproach)
            {
                var t = Mathf.InverseLerp(minApproach, maxApproach, approachSpeed);
                var footPower = _footStepBasePower * Mathf.Clamp01(t);

                var soundType = eKnownSfxSound.None;
                if (hit.collider.CompareTag(ReIWTags.Ground))
                    soundType = eKnownSfxSound.SE_Footstep_Run_Normal;
                else if (hit.collider.CompareTag(ReIWTags.Grass))
                    soundType = eKnownSfxSound.SE_Footstep_Run_Grass;
                else if (hit.collider.CompareTag(ReIWTags.Metal))
                    soundType = eKnownSfxSound.SE_Footstep_Run_Metal;
                else if (hit.collider.CompareTag(ReIWTags.Water))
                    soundType = eKnownSfxSound.SE_Footstep_Run_Water;

                FootStepEvent?.Invoke(
                    (footIndex == LEFT_FOOT_INDEX ? AvatarIKGoal.LeftFoot : AvatarIKGoal.RightFoot,
                     footPower, soundType));

                wasGrounded = true;

                #region Test Log
                // Debug.Log(
                //     ($"FootDown [{InFootIndex}] tag:{hit.collider?.tag} / slope:{InSlope} / InFootSlope: {InFootSlope} / InTraverse: {InTraverse}" +
                //      $" / slopeDeg: {slopeDeg:F3} / locomotionMoving: {InLocomotionMoving} / velocity: {velocity:F3}" +
                //      $" / approach: {approachSpeed:F3} / minApproach: {minApproach:F3} / maxApproach: {maxApproach:F3}" +
                //      $" / dist:{distToPlane:F3} / enterHeight: {enterHeight:F3} / exitHeight: {exitHeight:F3}")
                //     .Color(Color.green));
                #endregion
            }
            else if (wasGrounded)
            {
                var leavingPlane = false;
                if (slope == eSlopeDirection.Uphill && !traverse)
                {
                    leavingPlane = !grounded || !locomotionMoving ||
                                   (velocity.y > 0f && Vector3.Dot(velocity, n) > 0.15f &&
                                    Vector3.Dot(Character.Forward, (leg.IKPosition - Character.CharacterTransform.position).normalized) < 0f);
                }
                else
                {
                    leavingPlane = !grounded || (velocity.y > 0f && distToPlane > exitHeight && Vector3.Dot(velocity, n) > 0.15f);
                }

                if (leavingPlane)
                {
                    wasGrounded = false;
                    coolTime = _footStepCoolTime;

                    #region Test Log
                    // if (IsMoving)
                    //     Debug.Log(
                    //         ($"FootUp [{InFootIndex}] tag:{hit.collider?.tag} / slope:{InSlope} / InFootSlope: {InFootSlope} / InTraverse: {InTraverse}" +
                    //          $" / slopeDeg: {slopeDeg:F3} / locomotionMoving: {InLocomotionMoving} / velocity: {velocity.y:F3}" +
                    //          $" / approach: {approachSpeed:F3} / minApproach: {minApproach:F3} / maxApproach: {maxApproach:F3}" +
                    //          $" / dist:{distToPlane:F3} / enterHeight: {enterHeight:F3} / exitHeight: {exitHeight:F3}")
                    //         .Color(Color.yellow));
                    #endregion
                }
                #region Test Log
                // else
                // {
                //     if (InSlope == eSlopeDirection.Uphill && IsMoving && InTraverse)
                //     {
                //         Debug.LogError(
                //             $"FootUp Failed [{InFootIndex}] tag:{hit.collider?.tag} / slope:{InSlope} / InFootSlope: {InFootSlope} / InTraverse: {InTraverse}" +
                //             $" / slopeDeg: {slopeDeg:F3} / velocity: {velocity.y:F3} / approach:{approachSpeed:F3} / minApproach: {minApproach:F3}" +
                //             $" / maxApproach: {maxApproach:F3} / dist:{distToPlane:F3} / enterHeight: {enterHeight:F3} / exitHeight: {exitHeight:F3}" +
                //             $" / Dot: {Vector3.Dot(v, n)}");
                //     }
                // }
                #endregion
            }

            #region Test Log
            // if (!InWasGrounded && InCheckStep && InSlope == eSlopeDirection.Uphill && InTraverse && InCoolTime <= 0f && hasHit && IsMoving)
            // {
            //     Debug.LogError(
            //         $"FootDown Failed [{InFootIndex}] tag:{hit.collider?.tag} / slope:{InSlope} / InFootSlope: {InFootSlope} / InTraverse: {InTraverse}" +
            //         $" / slopeDeg: {slopeDeg:F3} / velocity: {velocity.y:F3} / approach:{approachSpeed:F3} / minApproach: {minApproach:F3}" +
            //         $" / maxApproach: {maxApproach:F3} / dist:{distToPlane:F3} / enterHeight: {enterHeight:F3} / exitHeight: {exitHeight:F3}");
            // }
            #endregion

            lastPos = curPos;
        }
        
        private (bool isTraverse, eSlopeDirection left, eSlopeDirection right) ClassifyFootSlope(
            in Vector3 moveVelocity, in eSlopeDirection leftPrevSlope, in eSlopeDirection rightPrevSlope,
            in Vector3 leftFootPos, in Vector3 rightFootPos, in Vector3 groundNormal, in Vector3 worldUp)
        {
            const float FRAC_THRESHOLD_ENTER = 0.18f;
            const float FRACT_HRESHOLD_EXIT  = 0.12f;
            const float MIN_TAUMETERS       = 0.02f;

            var slopeFrame = BuildSlopeFrame(groundNormal, worldUp);
            var slopeDeg = Vector3.Angle(slopeFrame.normal, worldUp);

            var mid = 0.5f * (leftFootPos + rightFootPos);
            var lP = Vector3.ProjectOnPlane(leftFootPos  - mid, slopeFrame.normal);
            var rP = Vector3.ProjectOnPlane(rightFootPos - mid, slopeFrame.normal);

            var sL = Vector3.Dot(lP, slopeFrame.downhillDirection);
            var sR = Vector3.Dot(rP, slopeFrame.downhillDirection);

            var stanceWidth = (lP - rP).magnitude;
            var tauEnter = Mathf.Max(MIN_TAUMETERS, stanceWidth * FRAC_THRESHOLD_ENTER);
            var tauExit  = Mathf.Max(MIN_TAUMETERS * 0.5f, stanceWidth * FRACT_HRESHOLD_EXIT);

            var left  = ApplyHysteresis(leftPrevSlope, sL, tauEnter, tauExit);
            var right = ApplyHysteresis(rightPrevSlope, sR, tauEnter, tauExit);

            return (slopeDeg < _flatGroundAngle || !IsParallelToContour(moveVelocity, slopeFrame), left, right);
        }

        private eSlopeDirection ApplyHysteresis(in eSlopeDirection prev,
            in float signedDist, in float tauEnter, in float tauExit)
        {
            switch (prev)
            {
                case eSlopeDirection.Uphill:
                    return signedDist > -tauExit ? eSlopeDirection.Flat : eSlopeDirection.Uphill;
                case eSlopeDirection.Downhill:
                    return signedDist < tauExit ? eSlopeDirection.Flat : eSlopeDirection.Downhill;
                default:
                    if (signedDist <= -tauEnter)
                        return eSlopeDirection.Uphill;
                    return signedDist >= tauEnter ? eSlopeDirection.Downhill : eSlopeDirection.Flat;
            }
        }

        private (Vector3 normal, Vector3 downhillDirection, Vector3 contourParallel) BuildSlopeFrame(
            in Vector3 groundNormal, in Vector3 worldUp)
        {
            var normal = (groundNormal.sqrMagnitude > 1e-6f ? groundNormal : worldUp).normalized;
            var downhillDirection = Vector3.ProjectOnPlane(-worldUp, normal);
            if (downhillDirection.sqrMagnitude < 1e-6f)
                downhillDirection = Vector3.Cross(normal, Vector3.right);
            downhillDirection.Normalize();
            var contourParallel = Vector3.Cross(normal, downhillDirection).normalized;
            return (normal, downhillDirection, contourParallel);
        }

        private static bool IsParallelToContour(in Vector3 velocityWorld,
            in (Vector3 normal, Vector3 downhillDirection, Vector3 contourParallel) slopeFrame)
        {
            const float DOT_THRESH = 0.75f;
            const float DOMINANCE = 1.20f;

            var v = Vector3.ProjectOnPlane(velocityWorld, slopeFrame.normal);
            var s = v.magnitude;
            var vN = v / s;
            var alignA = Mathf.Abs(Vector3.Dot(vN, slopeFrame.contourParallel));
            var alignD = Mathf.Abs(Vector3.Dot(vN, slopeFrame.downhillDirection));

            return (alignA >= DOT_THRESH) && (alignA > alignD * DOMINANCE);
        }

// #if UNITY_EDITOR
//         private void OnGUI()
//         {
//             if (_footGroundedStates[0])
//             {
//                 Vector3 screenPos = Camera.main.WorldToScreenPoint(_grounderIK.solver.legs[0].IKPosition);
//                 GUI.color = Color.blue;
//                 GUI.DrawTexture(new Rect(screenPos.x - 15, Screen.height - screenPos.y - 15, 30, 30), Texture2D.normalTexture);
//             }
//             if (_footGroundedStates[1])
//             {
//                 Vector3 screenPos = Camera.main.WorldToScreenPoint(_grounderIK.solver.legs[1].IKPosition);
//                 GUI.color = Color.red;
//                 GUI.DrawTexture(new Rect(screenPos.x - 15, Screen.height - screenPos.y - 15, 30, 30), Texture2D.normalTexture);
//             }
//         }
// #endif
    }
}
