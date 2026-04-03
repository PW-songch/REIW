using KinematicCharacterController;
using UnityEngine;

namespace REIW
{
    [CreateAssetMenu(fileName = "ParkourJumpActionData", menuName = "ScriptableObject/Character/Parkour/ParkourJumpActionData")]
    public class ParkourJumpActionData : ParkourActionData
    {
        [Header("[Jump Action Settings]\nGround Settings")]
        [SerializeField][Tags] TagList groundTags;

        [Header("Landable Ground Settings")]
        [SerializeField][Tags] TagList landableGroundTags;
        [Tooltip("점프 가능한 착지 Ground의 최저 높이")]
        [SerializeField][SelectableConstFloat] float minLandableGroundHeight = float.NaN;
        [Tooltip("점프 가능한 착지 Ground의 최대 높이")]
        [SerializeField][SelectableConstFloat] float maxLandableGroundHeight = float.NaN;
        [Tooltip("점프 가능한 착지 Ground 까지의 최저 거리")]
        [SerializeField][SelectableConstFloat] float minLandableGroundDistance = float.NaN;
        [Tooltip("점프 가능한 착지 Ground 까지의 최대 거리")]
        [SerializeField][SelectableConstFloat] float maxLandableGroundDistance = float.NaN;
        [Tooltip("착지 Ground에 해당 거리의 공간이 있는지 체크")]
        [SerializeField] private float checkSpaceToLand;
        [Tooltip("오를 위치를 체크")]
        [field:SerializeField] public bool CheckClimb { get; private set; }
        [Tooltip("조건에 맞는 착지 지점이 없는 경우 기본 그라운드 찾기")]
        [field:SerializeField] public bool FindBaseLandableGround { get; private set; }

        [Header("Jump Settings")]
        [Tooltip("애니메이션 시간으로 점프 시간 적용 비율")]
        [SerializeField] float jumpTimeApplicationRatio;
        [Tooltip("기본 점프 거리")]
        [SerializeField] private float defaultJumpDistance;
        [SerializeField] float jumpHeightOffset;
        [SerializeField] AnimationCurve jumpHeightCurve = AnimationCurve.EaseInOut(0, 0, 1, 0);
        [SerializeField] AnimationCurve jumpHorizontalSpeedCurve = AnimationCurve.Linear(0, 1, 1, 1);

        public JumpData JumpData { get; private set; }

        public float MaxLandableGroundHeight => maxLandableGroundHeight;
        public float MinLandableGroundDistance => minLandableGroundDistance;
        public float MaxLandableGroundDistance => maxLandableGroundDistance;
        public float JumpTime => TransitionLength * (jumpTimeApplicationRatio > 0f ? jumpTimeApplicationRatio : 1f);
        public AnimationCurve JumpHorizontalSpeedCurve => jumpHorizontalSpeedCurve;
        public AnimationCurve JumpHeightCurve => jumpHeightCurve;

        protected override void InitializeParkourActionData()
        {
            base.InitializeParkourActionData();
            Duration = JumpTime;
        }

        protected override void ResetParkourActionData()
        {
            base.ResetParkourActionData();
            JumpData = default;
        }

        protected override bool CheckManualOperation(bool isManualOperation)
        {
            return !manualOperation || base.CheckManualOperation(isManualOperation);
        }

        public bool CheckIfPossible(KinematicCharacterMotor motor, JumpData jumpData, float moveSpeed, bool isManualOperation)
        {
            if (!base.CheckIfPossible(moveSpeed, isManualOperation))
                return false;

            if (!jumpData.IsValid)
                return false;

            if (!jumpData.CompareGroundTag(groundTags))
                return false;

            for (int i = 0; i < jumpData.landableTargets.Length; ++i)
            {
                var landableTarget = jumpData.landableTargets[i];
                var minHeight = float.IsNormal(minLandableGroundHeight) ? Mathf.Abs(minLandableGroundHeight) : 0f;
                var maxHeight = float.IsNormal(maxLandableGroundHeight) ? Mathf.Abs(maxLandableGroundHeight) : 0f;
                var jumpMaxHeight = Mathf.Max(minHeight, maxHeight);
                jumpData.SetFallState(landableTarget.position, jumpMaxHeight);

                var isFall = landableTarget.isBaseGround && jumpData.IsFall;

                if (!float.IsNaN(minLandableGroundHeight))
                {
                    if (!isFall && MathUtility.Less(landableTarget.HeightFromStart, minLandableGroundHeight))
                        continue;
                }

                if (!float.IsNaN(maxLandableGroundHeight))
                {
                    if (MathUtility.Greater(landableTarget.HeightFromStart, maxLandableGroundHeight))
                        continue;
                }

                if (!float.IsNaN(minLandableGroundDistance))
                {
                    if (MathUtility.Less(landableTarget.HorizontalDistanceFromStart, minLandableGroundDistance))
                        continue;
                }

                if (!float.IsNaN(maxLandableGroundDistance))
                {
                    if (MathUtility.Greater(landableTarget.HorizontalDistanceFromStart, maxLandableGroundDistance))
                        continue;
                }

                if (!landableTarget.CompareTag(landableGroundTags))
                    continue;

                if (!isFall && checkSpaceToLand > 0f)
                {
                    var dirXZ = (landableTarget.position - jumpData.startPosition).normalized;
                    dirXZ.y = 0f;
                    if (!CheckLandingSpace(motor, landableTarget.position, dirXZ, checkSpaceToLand,
                            jumpData.landableTargetLayer, jumpData.landableTargetLayer))
                        continue;
                }

                if (defaultJumpDistance > 0f)
                {
                    var landPos = jumpData.startPosition + jumpData.jumpDirection * defaultJumpDistance;
                    landPos.y = Physics.Raycast(landPos, Vector3.down, out var hit, jumpData.JumpHeight * 2f,
                        jumpData.landableTargetLayer)
                        ? hit.point.y
                        : landableTarget.position.y;

                    if (!isFall)
                    {
                        var dirXZ = (landPos - jumpData.startPosition).normalized;
                        dirXZ.y = 0f;
                        if (!CheckLandingSpace(motor, landPos, dirXZ, 0.01f,
                                jumpData.landableTargetLayer, jumpData.landableTargetLayer))
                            continue;
                    }

                    var disPos = landPos - jumpData.startPosition;
                    var distance = disPos.magnitude;
                    var disY = disPos.y;
                    var disXZ = disPos;
                    disXZ.y = 0;
                    var distanceXZ = disXZ.magnitude;
                    landableTarget.position = landPos;
                    landableTarget.DistanceFromStart = distance;
                    landableTarget.HorizontalDistanceFromStart = distanceXZ;
                    landableTarget.HeightFromStart = disY;
                    landableTarget.JumpHeight = EnvironmentScannerComponent.GetJumpHeight(disY, disXZ);
                }

                landableTarget.JumpHeight += jumpHeightOffset;
                jumpData.SetTargetPoint(landableTarget, motor.CharacterUp, jumpMaxHeight);
                break;
            }

            if (!jumpData.LandTargetFound)
                return false;

            JumpData = jumpData;

            return true;
        }

        private bool CheckCollision(CapsuleCollider collider, Vector3 position, Vector3 dir, float distance, LayerMask collisionMask)
        {
            var radius = Mathf.Max(0.01f, collider.radius - 0.01f);
            var height = Mathf.Max(collider.height, radius * 2f);
            var half = height * 0.5f;

            var up = Vector3.up;
            var startCenter = position + up * (collider.center.y + 0.01f);

            var startBottom = startCenter + up * (-half + radius);
            var startTop = startCenter + up * (half - radius);

            if (dir.sqrMagnitude < 1e-4f)
                return true;

            return Physics.CapsuleCast(startBottom, startTop, radius, dir,
                out _, distance, collisionMask, QueryTriggerInteraction.Ignore);
        }

        private bool CheckLandingSpace(KinematicCharacterMotor motor, Vector3 landingPosition,
            Vector3 moveDir, float moveDistance, LayerMask collisionMask, LayerMask groundMask)
        {
            if (moveDistance <= 0f || moveDir.sqrMagnitude < 1e-4f)
                return true;

            var collider = motor.Capsule;

            if (CheckCollision(collider, landingPosition, moveDir, moveDistance, collisionMask))
                return false;

            var up = Vector3.up;
            var startCenter = landingPosition + up * (collider.center.y + 0.01f);
            if (moveDir.sqrMagnitude < 1e-4f)
                return true;

            var endCenter = startCenter + moveDir * moveDistance;
            var probeStart = endCenter + up * 0.5f;

            if (Physics.Raycast(probeStart, Vector3.down,
                    out var groundHit, 2.0f, groundMask, QueryTriggerInteraction.Ignore))
            {
                var slopeAngle = Vector3.Angle(groundHit.normal, Vector3.up);
                if (MathUtility.Greater(slopeAngle, motor.MaxStableSlopeAngle))
                    return false;

                var drop = (landingPosition.y) - groundHit.point.y;
                var maxSafeDrop = Mathf.Max(motor.MaxStepHeight * 1.5f, 0.5f);
                if (MathUtility.Greater(drop, maxSafeDrop))
                    return false;
            }
            else
            {
                return false;
            }

            return true;
        }
    }
}
