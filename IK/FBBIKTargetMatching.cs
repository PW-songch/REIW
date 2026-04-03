using Animancer;
using Animancer.Units;
using RootMotion.FinalIK;
using UnityEngine;
using static Animancer.Validate;

namespace REIW.Animations.Character
{
    [DisallowMultipleComponent]
    public class FBBIKTargetMatching : MonoBehaviour
    {
        [Header("Behavior")]
        [Tooltip("매칭 가중치 프로파일 (0~1 입력에 대한 가중치)")]
        [SerializeField] private AnimationCurve weightCurve = AnimationCurve.Linear(0, 0, 1, 1);
        [Tooltip("루트 매칭 가중치 프로파일 (0~1 입력에 대한 가중치)")]
        [SerializeField] private AnimationCurve rootWeightCurve = AnimationCurve.Linear(0, 0, 1, 1);

        [Tooltip("초당 최대 루트 보정 이동(m/s)"), SerializeField, MetersPerSecond(Rule = Value.IsNotNegative)]
        private float maxRootWarpMetersPerSec = 2.0f;
        [Tooltip("초당 최대 루트 보정 회전(deg/s)"), SerializeField, DegreesPerSecond(Rule = Value.IsNotNegative)]
        private float maxRootWarpDegPerSec = 120f;
        [Tooltip("가중치 해제 스피드"), SerializeField]
        private float releaseWeightSpeed = 10.0f;
        [Tooltip("루트 워핑 강도 스칼라"), SerializeField]
        private float rootWarpFactor = 1.0f;
        [Tooltip("루트 회전을 수평면(Y-up) 기준으로만 정렬"), SerializeField]
        private bool planarRotateToTarget = true;

        private CharacterBase character;
        private CharacterAnimation characterAnimation;
        private FullBodyBipedIK ik;
        private ActiveMatch matchJob;
        private ActiveMatch rootMatchJob;
        private Vector3 rootWarpAccumulatedClampedDelta;
        private Vector3 rootWarpRestoreClampedDelta;
        private Vector3 rootMatchAccumulatedClampedDelta;
        private Vector3 rootMatchRestoreClampedDelta;
        private bool useRootWarp;

        public bool RestoreRootWarp { private get; set; }
        public bool RestoreRootMatching { private get; set; }

        private void OnDisable()
        {
            matchJob.active = false;
            rootMatchJob.active = false;
            ZeroEffectorWeights(matchJob.avatarTarget);

            if (characterAnimation)
                characterAnimation.AnimatorMoveEvent -= OnAnimatorMoveEvent;
        }

        public void Initialize(CharacterBase character, CharacterAnimation characterAnimation, FullBodyBipedIK ik)
        {
            this.character = character;
            this.characterAnimation = characterAnimation;
            this.ik = ik;
            enabled = false;
        }

        public void TargetMatching(TargetMatchingInfo targetMatchingInfo, AnimancerState state, bool rootWarp)
        {
            if (ik == null || state == null || targetMatchingInfo == null)
                return;

            matchJob = new ActiveMatch
            {
                active = true,
                matchPos = targetMatchingInfo.MatchPosition,
                matchRot = targetMatchingInfo.MatchRotation,
                avatarTarget = targetMatchingInfo.MatchBodyPart,
                mask = targetMatchingInfo.WeightMask,
                matchWeight = targetMatchingInfo.MatchWeight,
                startTimeN = Mathf.Repeat(targetMatchingInfo.MatchStartTime, 1f),
                endTimeN = Mathf.Repeat(targetMatchingInfo.MatchTargetTime, 1f),
                state = state
            };

            useRootWarp = rootWarp;
            RestoreRootWarp = false;
            rootWarpAccumulatedClampedDelta = Vector3.zero;
            rootWarpRestoreClampedDelta = Vector3.zero;
            enabled = true;

            if (characterAnimation)
            {
                characterAnimation.AnimatorMoveEvent -= OnAnimatorMoveEvent;
                characterAnimation.AnimatorMoveEvent += OnAnimatorMoveEvent;
            }
        }

        public void RootTargetMatching(TargetMatchingInfo targetMatchingInfo, AnimancerState state)
        {
            if (ik == null || characterAnimation == null || targetMatchingInfo == null)
                return;

            rootMatchJob = new ActiveMatch
            {
                active = true,
                matchPos = targetMatchingInfo.MatchPosition,
                avatarTarget = targetMatchingInfo.MatchBodyPart,
                mask = targetMatchingInfo.WeightMask,
                matchWeight = targetMatchingInfo.MatchWeight,
                startTimeN = Mathf.Repeat(targetMatchingInfo.MatchStartTime, 1f),
                endTimeN = Mathf.Repeat(targetMatchingInfo.MatchTargetTime, 1f),
                state = state
            };

            RestoreRootMatching = false;
            rootMatchAccumulatedClampedDelta = Vector3.zero;
            rootMatchRestoreClampedDelta = Vector3.zero;
            enabled = true;

            if (characterAnimation)
            {
                characterAnimation.AnimatorMoveEvent -= OnAnimatorMoveEvent;
                characterAnimation.AnimatorMoveEvent += OnAnimatorMoveEvent;
            }
        }

        public void StopMatching(bool setDisable = false)
        {
            matchJob.active = false;
            useRootWarp = false;
            RestoreRootWarp = false;

            if (setDisable)
            {
                StopRootMatching();
                enabled = false;
            }
        }

        public void StopRootMatching()
        {
            rootMatchJob.active = false;
            RestoreRootMatching = false;
        }

        private void Update()
        {
            if (matchJob.state == null || !matchJob.state.IsPlaying)
                StopMatching();
        }

        private void LateUpdate()
        {
            if (ik == null || !matchJob.active)
                return;

            float weight = EvaluateWindowWeight(matchJob.state.NormalizedTime, matchJob.startTimeN, matchJob.endTimeN, weightCurve) * matchJob.matchWeight;
            var eff = GetEffector(matchJob.avatarTarget);
            var effBone = GetEffectorBone(matchJob.avatarTarget);

            if (eff == null || effBone == null)
            {
                if (weight <= 0f)
                    StopMatching();
                return;
            }

            if (weight > 0f)
            {
                Vector3 preEffPos = effBone.position;
                Vector3 tgtPosAxis = AxisBlend(preEffPos, matchJob.matchPos, matchJob.mask.positionXYZWeight);
                float posScalar = Mathf.Clamp01(Mathf.Max(matchJob.mask.positionXYZWeight.x, matchJob.mask.positionXYZWeight.y, matchJob.mask.positionXYZWeight.z));

                eff.position = tgtPosAxis;
                eff.positionWeight = Mathf.Max(eff.positionWeight, posScalar * weight);

                if (matchJob.matchRot != Quaternion.identity)
                {
                    eff.rotation = matchJob.matchRot;
                    eff.rotationWeight = Mathf.Max(eff.rotationWeight, Mathf.Clamp01(matchJob.mask.rotationWeight) * weight);
                }
            }
            else if (matchJob.state.NormalizedTime >= matchJob.startTimeN)
            {
                if (matchJob.state.IsPlaying)
                {
                    eff.positionWeight = Mathf.Clamp01(eff.positionWeight - Time.deltaTime * releaseWeightSpeed);
                    eff.rotationWeight = Mathf.Clamp01(eff.rotationWeight - Time.deltaTime * releaseWeightSpeed);
                    if (eff.positionWeight == 0f)
                        StopMatching();
                }
            }
        }

        private void RootWarp()
        {
            if (!matchJob.active)
                return;

            float weight = EvaluateWindowWeight(matchJob.state.NormalizedTime, matchJob.startTimeN, matchJob.endTimeN, weightCurve) * matchJob.matchWeight;
            if (weight <= 0f)
            {
                if (RestoreRootWarp && matchJob.state.IsPlaying && matchJob.state.NormalizedTime >= matchJob.startTimeN)
                {
                    if ((rootWarpAccumulatedClampedDelta - rootWarpRestoreClampedDelta).sqrMagnitude > 0.0001f)
                    {
                        Vector3 moveTowards = Vector3.MoveTowards(rootWarpRestoreClampedDelta,
                            rootWarpAccumulatedClampedDelta, Time.deltaTime * releaseWeightSpeed);
                        character.AddRootMotionPosition(moveTowards - rootWarpRestoreClampedDelta);
                        rootWarpRestoreClampedDelta = moveTowards;
                    }
                    else
                    {
                        RestoreRootWarp = false;
                    }
                }
                return;
            }

            if (!matchJob.active || !useRootWarp)
                return;

            // if (animator)
            //     animator.ApplyBuiltinRootMotion();

            Transform effBone = GetEffectorBone(matchJob.avatarTarget);
            if (effBone == null)
                return;

            Vector3 preEffPos = effBone.position;
            Vector3 tgtPosAxis = AxisBlend(preEffPos, matchJob.matchPos, matchJob.mask.positionXYZWeight);
            Vector3 err = tgtPosAxis - preEffPos;
            Vector3 desiredRootDelta = err * (rootWarpFactor * weight);
            float maxPosThisFrame = maxRootWarpMetersPerSec * Time.deltaTime;
            Vector3 clampedDelta = Vector3.ClampMagnitude(desiredRootDelta, maxPosThisFrame);
            rootWarpAccumulatedClampedDelta -= clampedDelta;
            character.AddRootMotionPosition(clampedDelta);

            float maxDegThisFrame = maxRootWarpDegPerSec * Time.deltaTime;
            Quaternion deltaRot = Quaternion.identity;

            if (matchJob.mask.rotationWeight > 0f)
            {
                if (planarRotateToTarget)
                {
                    Vector3 toTarget = (tgtPosAxis - character.CharacterTransform.position);
                    Vector3 fwd = Vector3.ProjectOnPlane(character.Forward, Vector3.up).normalized;
                    Vector3 dir = Vector3.ProjectOnPlane(toTarget, Vector3.up).normalized;

                    if (dir.sqrMagnitude > 1e-6f && fwd.sqrMagnitude > 1e-6f)
                    {
                        Quaternion want = Quaternion.FromToRotation(fwd, dir);
                        deltaRot = Quaternion.Slerp(Quaternion.identity, want, weight * matchJob.mask.rotationWeight);
                    }
                }
                else
                {
                    Quaternion want = Quaternion.FromToRotation(effBone.rotation * Vector3.forward,
                        matchJob.matchRot * Vector3.forward);
                    deltaRot = Quaternion.Slerp(Quaternion.identity, want, weight * matchJob.mask.rotationWeight);
                }
            }

            deltaRot.ToAngleAxis(out float angle, out Vector3 axis);
            if (!float.IsNaN(axis.x))
            {
                angle = Mathf.Min(angle, maxDegThisFrame);
                character.AddRootMotionRotation(Quaternion.AngleAxis(angle, axis) * character.CharacterTransform.rotation);
            }
        }

        private void RootMatching()
        {
            if (!rootMatchJob.active)
                return;

            float weight = EvaluateWindowWeight(rootMatchJob.state.NormalizedTime, rootMatchJob.startTimeN, rootMatchJob.endTimeN, rootWeightCurve) * rootMatchJob.matchWeight;
            if (weight <= 0f)
            {
                if (RestoreRootMatching && rootMatchJob.state.IsPlaying && rootMatchJob.state.NormalizedTime >= rootMatchJob.startTimeN)
                {
                    if ((rootMatchAccumulatedClampedDelta - rootMatchRestoreClampedDelta).sqrMagnitude > 0.0001f)
                    {
                        Vector3 moveTowards = Vector3.MoveTowards(rootMatchRestoreClampedDelta,
                            rootMatchAccumulatedClampedDelta, Time.deltaTime * releaseWeightSpeed);
                        character.AddRootMotionPosition(moveTowards - rootMatchRestoreClampedDelta);
                        rootMatchRestoreClampedDelta = moveTowards;
                    }
                    else
                    {
                        StopRootMatching();
                    }
                }
                return;
            }

            Vector3 tgtPosAxis = AxisBlend(Vector3.zero, rootMatchJob.matchPos, rootMatchJob.mask.positionXYZWeight);
            Vector3 desiredRootDelta = Vector3.MoveTowards(Vector3.zero, tgtPosAxis, weight);
            float maxPosThisFrame = maxRootWarpMetersPerSec * Time.deltaTime;
            Vector3 clampedDelta = Vector3.ClampMagnitude(desiredRootDelta, maxPosThisFrame);
            rootMatchAccumulatedClampedDelta -= clampedDelta;
            rootMatchJob.matchPos -= clampedDelta;
            character.AddRootMotionPosition(clampedDelta);
        }

        private void OnAnimatorMoveEvent()
        {
            RootWarp();
            RootMatching();
        }

        private float EvaluateWindowWeight(float normalizedTime, float start, float end, AnimationCurve curve)
        {
            float nt = Mathf.Repeat(normalizedTime, 1f);
            float t01;
            if (!ComputeWindowT(nt, start, end, out t01))
                return 0f;

            return Mathf.Clamp01(curve.Evaluate(t01));
        }

        private bool ComputeWindowT(float nt01, float start, float end, out float t01)
        {
            nt01 = Mathf.Repeat(nt01, 1f);
            start = Mathf.Repeat(start, 1f);
            end   = Mathf.Repeat(end, 1f);

            if (Mathf.Approximately(start, end))
            {
                t01 = 0f;
                return false;
            }

            if (start < end)
            {
                if (nt01 < start || nt01 > end)
                {
                    t01 = 0f;
                    return false;
                }

                t01 = Mathf.InverseLerp(start, end, nt01);
                return true;
            }
            else
            {
                bool inWin = (nt01 >= start) || (nt01 <= end);
                if (!inWin)
                {
                    t01 = 0f;
                    return false;
                }

                float len = (1f - start) + end;
                float pos = (nt01 >= start) ? (nt01 - start) : (1f - start + nt01);
                t01 = (len > 1e-6f) ? Mathf.Clamp01(pos / len) : 0f;
                return true;
            }
        }

        private Vector3 AxisBlend(Vector3 from, Vector3 to, Vector3 axisWeight)
        {
            return new Vector3(
                Mathf.Lerp(from.x, to.x, Mathf.Clamp01(axisWeight.x)),
                Mathf.Lerp(from.y, to.y, Mathf.Clamp01(axisWeight.y)),
                Mathf.Lerp(from.z, to.z, Mathf.Clamp01(axisWeight.z))
            );
        }

        private IKEffector GetEffector(AvatarTarget target)
        {
            if (ik == null)
                return null;

            switch (target)
            {
                default:
                case AvatarTarget.Body:
                case AvatarTarget.Root:
                    return ik.solver.bodyEffector;
                case AvatarTarget.LeftHand:
                    return ik.solver.leftHandEffector;
                case AvatarTarget.RightHand:
                    return ik.solver.rightHandEffector;
                case AvatarTarget.LeftFoot:
                    return ik.solver.leftFootEffector;
                case AvatarTarget.RightFoot:
                    return ik.solver.rightFootEffector;
            }
        }

        private Transform GetEffectorBone(AvatarTarget target)
        {
            if (ik == null || ik.references == null)
                return null;

            switch (target)
            {
                default:
                case AvatarTarget.Body:
                case AvatarTarget.Root:
                    return ik.references.pelvis ? ik.references.pelvis : ik.references.root;
                case AvatarTarget.LeftHand:
                    return ik.references.leftHand;
                case AvatarTarget.RightHand:
                    return ik.references.rightHand;
                case AvatarTarget.LeftFoot:
                    return ik.references.leftFoot;
                case AvatarTarget.RightFoot:
                    return ik.references.rightFoot;
            }
        }

        private void ZeroEffectorWeights(AvatarTarget target)
        {
            var e = GetEffector(target);
            if (e == null)
                return;

            e.positionWeight = 0f;
            e.rotationWeight = 0f;
        }

#if UNITY_EDITOR
        private void OnDrawGizmosSelected()
        {
            if (matchJob.active)
            {
                Gizmos.color = Color.cyan;
                Gizmos.DrawWireSphere(matchJob.matchPos, 0.03f);
                Gizmos.color = Color.yellow;
                Gizmos.DrawRay(matchJob.matchPos, (matchJob.matchRot * Vector3.forward) * 0.12f);
            }

            if (rootMatchJob.active)
            {
                Gizmos.color = Color.magenta;
                Gizmos.DrawWireSphere(rootMatchJob.matchPos, 0.03f);
            }
        }
#endif

        private struct ActiveMatch
        {
            public bool active;
            public Vector3 matchPos;
            public Quaternion matchRot;
            public AvatarTarget avatarTarget;
            public MatchTargetWeightMask mask;
            public float matchWeight;
            public float startTimeN;
            public float endTimeN;
            public AnimancerState state;
        }
    }
}
