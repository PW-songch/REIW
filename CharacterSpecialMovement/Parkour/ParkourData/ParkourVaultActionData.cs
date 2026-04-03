using System;
using System.Collections.Generic;
using REIW.Animations.Character;
using UnityEngine;

namespace REIW
{
    public enum VaultType { Any, VaultOver, VaultOn }

    [CreateAssetMenu(fileName = "ParkourVaultActionData", menuName = "ScriptableObject/Character/Parkour/ParkourVaultActionData")]
    public class ParkourVaultActionData : ParkourActionData
    {
        [Header("[Vault Action Settings]")]
        [SerializeField] VaultType vaultType;

        [Header("Obstacle Settings")]
        [SerializeField][Tags] TagList obstacleTags;
        [Tooltip("파쿠르 가능한 Obstacle 까지의 최저 거리")]
        [SerializeField] float minObstacleDistance;
        [Tooltip("파쿠르 가능한 Obstacle 까지의 최대 거리")]
        [SerializeField] float maxObstacleDistance;
        [Tooltip("파쿠르 가능한 Obstacle의 최저 높이")]
        [SerializeField] float minObstacleHeight;
        [Tooltip("파쿠르 가능한 Obstacle의 최대 높이")]
        [SerializeField] float maxObstacleHeight;
        [Tooltip("파쿠르 가능한 Obstacle의 최저 깊이")]
        [SerializeField] float minObstacleDepth = 0f;
        [Tooltip("파쿠르 가능한 Obstacle의 최대 깊이")]
        [SerializeField] float maxObstacleDepth = 0f;
        [Tooltip("파쿠르 가능한 Obstacle의 최저 가로 길이")]
        [SerializeField] float minObstacleWidth = 0.5f;

        [Header("Move To Target Settings")]
        [Tooltip("타겟 위치까지 이동 최소 시간")]
        [SerializeField] float minMoveToTargetTime;
        [Tooltip("타겟 위치까지 이동 최대 시간")]
        [SerializeField] float maxMoveToTargetTime;
        [Tooltip("타겟 위치까지 이동 적용 방향 가중치")]
        [SerializeField] Vector3 moveToTargetDirectionWeight;
        [SerializeField] AnimationCurve moveHorizontalSpeedCurve = AnimationCurve.Linear(0, 1, 1, 1);

        [Header("Target Matching Settings")]
        [Tooltip("Target Matching 적용 여부")]
        [SerializeField] bool enableTargetMatching = true;
        [Tooltip("Target Matching시 RootWarp 적용 여부")]
        [SerializeField] bool enableTargetMatchingRootWarp;
        [Tooltip("Target Matching시 회전 적용 여부")]
        [SerializeField] bool matchRotateToObstacle;
        [Tooltip("Target Matching 정보")]
        [SerializeField] TargetMatchingInfo targetMatchingInfo;

        [Tooltip("Root Target Matching 적용 여부")]
        [SerializeField] bool enableRootTargetMatching;
        [Tooltip("Height 위치와 유지 거리")]
        [SerializeField] float maintenanceDistanceWithHeight;
        [Tooltip("Root Target Matching 정보")]
        [SerializeField] TargetMatchingInfo[] rootTargetMatchingInfos = new TargetMatchingInfo[2];
        [Tooltip("Root Target Matching 서브 정보")]
        [SerializeField] private TargetMatchingInfo[] rootTargetMatchingSubInfos;
        [Tooltip("BodyIK Weights 정보")]
        [SerializeField] private BodyIKWeight[] bodyIKWeights;

        private Queue<TargetMatchingInfo> _rootTargetMatchingSubInfoQueue;
        private AvatarTarget[] _bodyIKWeightTypes;

        public ObstacleHitData HitData { get; private set; }
        public bool InactivationCollisionWithHitTarget { get; set; }

        public float MinObstacleDistance => minObstacleDistance;
        public float MaxObstacleDistance => maxObstacleDistance;
        public float MaxObstacleHeight => maxObstacleHeight;
        public float MinMoveToTargetTime => minMoveToTargetTime;
        public float MaxMoveToTargetTime => maxMoveToTargetTime;
        public Vector3 MoveToTargetDirectionWeight => moveToTargetDirectionWeight;
        public AnimationCurve MoveHorizontalSpeedCurve => moveHorizontalSpeedCurve;
        public bool EnableTargetMatching => enableTargetMatching;
        public bool EnableTargetMatchingRootWarp => enableTargetMatchingRootWarp;
        public bool MatchRotateToObstacle => matchRotateToObstacle;
        public TargetMatchingInfo TargetMatchingInfo => targetMatchingInfo;
        public bool EnableRootTargetMatching => enableRootTargetMatching;
        public TargetMatchingInfo RootTargetMatchingInfo
        {
            get
            {
                if (!rootTargetMatchingInfos.IsNullOrEmpty())
                {
                    for (int i = 0; i < rootTargetMatchingInfos.Length; ++i)
                    {
                        if (rootTargetMatchingInfos[i].MatchPosition != Vector3.zero)
                            return rootTargetMatchingInfos[i];
                    }
                }

                return null;
            }
        }
        public TargetMatchingInfo NextRootTargetMatchingSubInfo => _rootTargetMatchingSubInfoQueue?.Dequeue();
        public BodyIKWeight[] BodyIKWeights => bodyIKWeights;

        public AvatarTarget[] BodyIKWeightTypes
        {
            get
            {
                if (_bodyIKWeightTypes == null)
                {
                    _bodyIKWeightTypes = new AvatarTarget[bodyIKWeights.Length];
                    for  (int i = 0; i < bodyIKWeights.Length; ++i)
                        _bodyIKWeightTypes[i] = bodyIKWeights[i].Type;
                }

                return _bodyIKWeightTypes;
            }
        }

        protected override void OnValidate()
        {
            base.OnValidate();

            if (animationType != ParkourAnimationState.eAnimationType.NONE)
            {
                bool isMirror = animationType != mirrorAnimationType;
                targetMatchingInfo.SetMatchBodyPart(isMirror);
                if (!rootTargetMatchingInfos.IsNullOrEmpty())
                {
                    for (int i = 0; i < rootTargetMatchingInfos.Length; ++i)
                        rootTargetMatchingInfos[i].SetMatchBodyPart(isMirror);
                }
            }
        }

        protected override void InitializeParkourActionData()
        {
            base.InitializeParkourActionData();
            InactivationCollisionWithHitTarget = inactivationCollisionWithHitTarget;
            if (!rootTargetMatchingSubInfos.IsNullOrEmpty())
                _rootTargetMatchingSubInfoQueue = new Queue<TargetMatchingInfo>(rootTargetMatchingSubInfos);
        }

        protected override void ResetParkourActionData()
        {
            base.ResetParkourActionData();
            HitData = default;
            _rootTargetMatchingSubInfoQueue?.Clear();
            _rootTargetMatchingSubInfoQueue = null;
        }

        private TargetMatchingInfo GetRootTargetMatchingInfo(bool isUp)
        {
            return rootTargetMatchingInfos.IsNullOrEmpty() ? null : rootTargetMatchingInfos[rootTargetMatchingInfos.Length < 2 ? 0 : (isUp ? 0 : 1)];
        }

        public bool CheckIfPossible(ObstacleHitData hitData, Transform parkourer, float moveSpeed, float maxDegree, bool isManualOperation)
        {
            if (!base.CheckIfPossible(moveSpeed, isManualOperation))
                return false;

            if (!hitData.forwardHitFound)
                return false;

            if (vaultType == VaultType.VaultOn && !hitData.hasSpaceToVault)
                return false;

            if (vaultType == VaultType.VaultOver && minObstacleDepth == 0 && hitData.hasSpaceToVault)
                return false;

            if (!hitData.CompareTag(obstacleTags))
                return false;

            if (MathUtility.Less(hitData.hitHeight, minObstacleHeight) || MathUtility.Greater(hitData.hitHeight, maxObstacleHeight))
                return false;

            if (minObstacleDistance > 0f)
            {
                if (MathUtility.Less(hitData.hitDistance, minObstacleDistance))
                    return false;
            }

            if (maxObstacleDistance > 0f)
            {
                if (MathUtility.Greater(hitData.hitDistance, maxObstacleDistance))
                    return false;
            }

            if (minObstacleDepth > 0)
            {
                if (MathUtility.Less(hitData.hitDepth, minObstacleDepth))
                    return false;
            }

            if (maxObstacleDepth > 0)
            {
                if (MathUtility.Greater(hitData.hitDepth, maxObstacleDepth))
                    return false;
            }

            if (minObstacleWidth > 0)
            {
                if (MathUtility.Less(hitData.hitWidth, minObstacleWidth))
                    return false;
            }

            targetMatchingInfo.MatchPosition = Vector3.zero;
            targetMatchingInfo.MatchRotation = Quaternion.identity;

            if (!rootTargetMatchingInfos.IsNullOrEmpty())
            {
                for (int i = 0; i < rootTargetMatchingInfos.Length; ++i)
                {
                    rootTargetMatchingInfos[i].MatchPosition = Vector3.zero;
                    rootTargetMatchingInfos[i].MatchRotation = Quaternion.identity;
                }
            }

            if (!rootTargetMatchingSubInfos.IsNullOrEmpty())
            {
                for (int i = 0; i < rootTargetMatchingSubInfos.Length; ++i)
                {
                    rootTargetMatchingSubInfos[i].MatchPosition = Vector3.zero;
                    rootTargetMatchingSubInfos[i].MatchRotation = Quaternion.identity;
                }
            }

            var dir = hitData.heightHit.point - parkourer.position;
            dir.y = 0;
            dir.Normalize();

            Direction = Vector3.Dot(parkourer.right, dir) <= 0 ? eDirection.Left : eDirection.Right;

            targetMatchingInfo.IsMirror = IsMirror;
            if (!rootTargetMatchingInfos.IsNullOrEmpty())
            {
                for (int i = 0; i < rootTargetMatchingInfos.Length; ++i)
                    rootTargetMatchingInfos[i].IsMirror = IsMirror;
            }

            if (enableTargetMatching)
            {
                var normal = hitData.forwardHit.normal;
                normal.y = 0f;
                var charDir = parkourer.forward;
                charDir.y = 0f;
                charDir.Normalize();
                var cosMax = Mathf.Cos(maxDegree * Mathf.Deg2Rad);
                var dot = Mathf.Abs(Vector3.Dot(charDir, -normal));
                var weight   = Mathf.InverseLerp(1f, cosMax, Mathf.Clamp(dot, cosMax, 1f));
                targetMatchingInfo.MatchPosition = hitData.heightHit.point +
                    Vector3.ProjectOnPlane(targetMatchingInfo.MatchPosOffset, normal) * weight;
            }

            if (enableRootTargetMatching && hitData.heightHitFound)
            {
                if (!Mathf.Approximately(Mathf.Abs(hitData.hitHeight), Mathf.Abs(rootMotionVerticalPos)))
                {
                    var rootTargetMatchingInfo = GetRootTargetMatchingInfo(MathUtility.GreaterOrEqual(hitData.hitHeight, rootMotionVerticalPos));
                    if (rootTargetMatchingInfo != null)
                    {
                        var rootWarpOffset = rootTargetMatchingInfo.MatchPosOffset;
                        rootWarpOffset.y += hitData.hitHeight - rootMotionVerticalPos - maintenanceDistanceWithHeight;
                        rootTargetMatchingInfo.MatchPosition = rootWarpOffset;
                        rootTargetMatchingInfo.SetMatchWeight(minObstacleHeight, rootMotionVerticalPos, maxObstacleHeight, hitData.hitHeight);
                    }

                    if (!rootTargetMatchingSubInfos.IsNullOrEmpty())
                    {
                        for (int i = 0; i < rootTargetMatchingSubInfos.Length; ++i)
                        {
                            var info = rootTargetMatchingSubInfos[i];
                            info.MatchPosition = info.MatchPosOffset;
                            info.SetMatchWeight(0f, 0.5f, 1f, 1f);
                        }
                    }
                }
            }

            if (MatchRotateToObstacle)
                targetMatchingInfo.MatchRotation = Quaternion.LookRotation(dir);

            HitData = hitData;

            return true;
        }
    }

    [Serializable]
    public class TargetMatchingInfo
    {
        [Tooltip("매칭 타겟")]
        [SerializeField] private AvatarTarget matchBodyPart;
        [Tooltip("매칭 Weight")]
        [SerializeField] private float matchWeight = 1f;
        [Tooltip("매칭 시작 시간")]
        [SerializeField, Range(0, 0.999f)] private float matchStartTime;
        [Tooltip("매칭 종료 시간")]
        [SerializeField, Range(0, 0.999f)] private float matchTargetTime;
        [Tooltip("매칭 위치 오프셋")]
        [SerializeField] private Vector3 matchPosOffset;
        [Tooltip("매칭 위치 Weight")]
        [SerializeField] private Vector3 matchPosWeight = new Vector3(0, 1, 0);

        private AvatarTarget mirrorMatchBodyPart;

        public AvatarTarget MatchBodyPart => IsMirror ? mirrorMatchBodyPart : matchBodyPart;
        public float MatchStartTime => matchStartTime;
        public float MatchTargetTime => matchTargetTime;
        public Vector3 MatchPosOffset => !IsMirror ? matchPosOffset : new Vector3(matchPosOffset.x, matchPosOffset.y, -matchPosOffset.z);
        public MatchTargetWeightMask WeightMask => new (matchPosWeight, 0);

        public Vector3 MatchPosition { get; set; }
        public Quaternion MatchRotation { get; set; }
        public bool IsMirror { private get; set; }
        public float MatchWeight { get; private set; } = 1f;

        public void SetMatchBodyPart(bool isMirror)
        {
            if (!isMirror)
            {
                mirrorMatchBodyPart = matchBodyPart;
            }
            else
            {
                mirrorMatchBodyPart = matchBodyPart switch
                {
                    AvatarTarget.LeftFoot => AvatarTarget.RightFoot,
                    AvatarTarget.RightFoot => AvatarTarget.LeftFoot,
                    AvatarTarget.LeftHand => AvatarTarget.RightHand,
                    AvatarTarget.RightHand => AvatarTarget.LeftHand,
                    _ => matchBodyPart
                };
            }
        }

        public float GetMatchWeight(float min, float middle, float max, float value)
        {
            if (value <= middle)
            {
                var t = Mathf.InverseLerp(min, middle, value);
                return Mathf.SmoothStep(matchWeight, 1f, t);
            }
            else
            {
                var t = Mathf.InverseLerp(middle, max, value);
                return Mathf.SmoothStep(1f, matchWeight, t);
            }
        }

        public void SetMatchWeight(float min, float middle, float max, float value)
        {
            MatchWeight = GetMatchWeight(min, middle, max, value);
        }
    }

    [Serializable]
    public struct BodyIKWeight
    {
        [field:SerializeField] public AvatarTarget Type { get; private set; }
        [field:SerializeField, Range(0, 1)] public float Weight { get; private set; }
        [field:SerializeField, Range(0, 1)] public float MaintainRotationWeight { get; private set; }
    }
}