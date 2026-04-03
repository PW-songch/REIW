using UnityEngine;
using UnityEngine.Serialization;

namespace REIW
{
    [CreateAssetMenu(fileName = "CharacterMoveParkourData", menuName = "ScriptableObject/CharacterMoveParkourData")]
    public class CharacterMoveParkourData : ScriptableObject
    {
        [field: Header("Inputs Settings")]
        [field: SerializeField] public CharacterActionInputBuffer InputBuffer { get; private set; } = new();

        [field: Header("Parkour Detection Settings")]
        [field: SerializeField] public LayerMask ObstacleLayer { get; private set; } = 1;
        [field: SerializeField] public LayerMask LedgeLayer { get; private set; } = 1;

        [field: SerializeField] public float obstacleCheckRange = 0.7f;
        [field: SerializeField] public float heightRayLength = 4;
        [field: SerializeField] public float LedgeHeightThreshold { get; private set; } = 1f;
        [field: SerializeField] public int LedgeFoundCount { get; private set; } = 6;

        [field: SerializeField] public float DetectionMoveSpeedMin { get; private set; } = 2f;
        [field: SerializeField] public float DetectionJumpMoveSpeedMin { get; private set; } = 0f;
        [field: SerializeField] public float DetectionHorizontalAngleMax { get; private set; } = 40f;

        [field: Header("Jump Action Settings")]
        [field: SerializeField] public LayerMask JumpLayer { get; private set; } = 1;
        [field: SerializeField] public bool JumpToTheClosestLedge { get; private set; } = false;
        [field: SerializeField] public float JumpHeightOffset { get; private set; } = 1.4f;
        [field: SerializeField] public float JumpHeight { get; private set; } = 1.5f;
        [field: SerializeField] private float minJumpDistance = 1f;
        [field: SerializeField] private float maxJumpDistance = 5f;

        [field: Header("Parkour Actions")]
        [field: SerializeField] public ParkourVaultActionData[] ParkourVaultActions { get; private set; }
        [field: SerializeField] public ParkourJumpActionData[] ParkourJumpActions { get; private set; }

        public float ObstacleCheckRange { get; private set; }
        public float HeightRayLength { get; private set; }
        public float MinJumpDistance { get; private set; }
        public float MaxJumpDistance { get; private set; }

        private void OnValidate()
        {
            ObstacleCheckRange = GetObstacleCheckRange();
            HeightRayLength = GetHeightRayLength();
            MinJumpDistance = GetMinJumpDistance();
            MaxJumpDistance = GetMaxJumpDistance();
        }

        private float GetObstacleCheckRange()
        {
            if (ParkourVaultActions.IsNullOrEmpty())
                return obstacleCheckRange;

            var range = obstacleCheckRange;
            for (int i = 0; i < ParkourVaultActions.Length; ++i)
            {
                if (!float.IsNaN(ParkourVaultActions[i].MaxObstacleDistance))
                    range = Mathf.Max(range, ParkourVaultActions[i].MaxObstacleDistance);
            }

            return range;
        }

        private float GetHeightRayLength()
        {
            if (ParkourVaultActions.IsNullOrEmpty())
                return heightRayLength;

            var length = heightRayLength;
            for (int i = 0; i < ParkourVaultActions.Length; ++i)
            {
                if (!float.IsNaN(ParkourVaultActions[i].MaxObstacleHeight))
                    length = Mathf.Max(length, ParkourVaultActions[i].MaxObstacleHeight);
            }

            return length;
        }

        private float GetMinJumpDistance()
        {
            if (ParkourJumpActions.IsNullOrEmpty())
                return minJumpDistance;

            var distance = minJumpDistance;
            for (int i = 0; i < ParkourJumpActions.Length; ++i)
            {
                if (!float.IsNaN(ParkourJumpActions[i].MinLandableGroundDistance))
                    distance = Mathf.Min(distance, ParkourJumpActions[i].MinLandableGroundDistance);
            }

            return distance;
        }

        private float GetMaxJumpDistance()
        {
            if (ParkourJumpActions.IsNullOrEmpty())
                return maxJumpDistance;

            var distance = maxJumpDistance;
            for (int i = 0; i < ParkourJumpActions.Length; ++i)
            {
                if (!float.IsNaN(ParkourJumpActions[i].MaxLandableGroundDistance))
                    distance = Mathf.Max(distance, ParkourJumpActions[i].MaxLandableGroundDistance);
            }

            return distance;
        }
    }
}
