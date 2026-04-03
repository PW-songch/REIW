using REIW.Animations.Character;
using UnityEngine;

namespace REIW
{
    public abstract class ParkourActionData : ScriptableObject
    {
        [Tooltip("우선순위 (오름차순)")]
        [SerializeField] int priority;

        [Header("Animation Settings")]
        [Tooltip("Default or left animation type")]
        [SerializeField] protected ParkourAnimationState.eAnimationType animationType;
        [Tooltip("Mirror animation type")]
        [SerializeField] protected ParkourAnimationState.eAnimationType mirrorAnimationType;
        [Tooltip("액션의 애니메이션 길이")]
        [SerializeField] protected float animationLength;
        [Tooltip("애니메이션 속도를 지속 시간에 따라 변경")]
        [SerializeField] private bool changeAnimationSpeedBasedDuration;
        [Tooltip("발 체크하여 애니메이션 선택")]
        [SerializeField] private bool checkFootToSelectAnimation;

        [Header("Root Motion Settings")]
        [Tooltip("XZ 위치 루트모션 적용 타입")]
        [SerializeField] protected  CharacterRootMotionMode horizontalPosRootMotion = CharacterRootMotionMode.Ignore;
        [Tooltip("Y 위치 루트모션 적용 타입")]
        [SerializeField] protected  CharacterRootMotionMode verticalPosRootMotion = CharacterRootMotionMode.Ignore;
        [Tooltip("회전 루트모션 적용 타입")]
        [SerializeField] protected  CharacterRootMotionMode rotationRootMotion = CharacterRootMotionMode.Ignore;
        [Tooltip("애니메이션의 루트모션 XZ 위치 값")]
        [SerializeField] protected float rootMotionHorizontalPos;
        [Tooltip("애니메이션의 루트모션 Y 위치 값")]
        [SerializeField] protected float rootMotionVerticalPos;
        [Tooltip("타겟 방향으로 회전 여부")]
        [SerializeField] bool rotateToTarget;

        [Header("Input Settings")]
        [Tooltip("수동 조작 여부")]
        [SerializeField] protected bool manualOperation;
        [Tooltip("이동 조작 잠금 여부")]
        [SerializeField] protected bool lockMoveInput = true;

        [Header("Motor Settings")]
        [Tooltip("캐릭터 모터에서 Ground Solving 비활성화 여부")]
        [SerializeField] protected bool inactivationGroundSolving = true;
        [Tooltip("캐릭터 모터에서 캐릭터와 충돌체의 충돌 미적용 여부")]
        [SerializeField] protected bool inactivationCollisionWithHitTarget = true;
        [Tooltip("캐릭터 Collider와 애니메이션의 Transform 동기화 여부")]
        [SerializeField] protected bool syncColliderAnimationTransform = true;

        [Header("Additional Settings")]
        [Tooltip("액션 가능한 최저 이동 속도")]
        [SerializeField] protected float movementThreshold = 2f;
        [Tooltip("액션 시작 딜레이")]
        [SerializeField] protected float startActionDelay;
        [Tooltip("액션 후 종료 딜레이")]
        [SerializeField] protected float postActionDelay;

        public eDirection Direction { get; protected set; }
        public float Duration { get; set; }
        public float TransitionLength { get; set; }
        public bool ApplySolverHorizontalVelocity { get; set; }
        public bool ApplySolverVerticalVelocity { get; set; }

        protected bool IsMirror => Direction == eDirection.Right;

        public eAnimationType AnimationType => (eAnimationType)(IsMirror ? mirrorAnimationType : animationType);
        public float AnimationLength => animationLength > 0f ? animationLength : TransitionLength;
        public bool ChangeAnimationSpeedBasedDuration => changeAnimationSpeedBasedDuration;
        public bool CheckFootToSelectAnimation => checkFootToSelectAnimation;
        public CharacterRootMotionMode HorizontalPosRootMotion => horizontalPosRootMotion;
        public CharacterRootMotionMode VerticalPosRootMotion => verticalPosRootMotion;
        public CharacterRootMotionMode RotationRootMotion => rotationRootMotion;
        public bool RotateToTarget => rotateToTarget;
        public bool ManualOperation => manualOperation;
        public bool LockMoveInput => lockMoveInput;
        public bool AtStartLockMoveInput => RotateToTarget || LockMoveInput;
        public bool InactivationGroundSolving => inactivationGroundSolving;
        public bool SyncColliderAnimationTransform => syncColliderAnimationTransform;
        public float MovementThreshold => movementThreshold;
        public float StartActionDelay => startActionDelay;
        public float PostActionDelay => postActionDelay;

#if UNITY_EDITOR
        private ParkourAnimationState.eAnimationType prevAnimationType;
#endif

        protected virtual void OnValidate()
        {
            if (animationType != ParkourAnimationState.eAnimationType.NONE)
            {
                if (mirrorAnimationType == ParkourAnimationState.eAnimationType.NONE)
                    mirrorAnimationType = animationType;
#if UNITY_EDITOR
                else if (prevAnimationType != 0 && animationType != prevAnimationType)
                    mirrorAnimationType = animationType;
#endif
            }

            if (checkFootToSelectAnimation && animationType == mirrorAnimationType)
                checkFootToSelectAnimation = false;

#if UNITY_EDITOR
            prevAnimationType = animationType;
#endif
        }

        protected virtual void InitializeParkourActionData()
        {
            Duration = AnimationLength;
            ApplySolverHorizontalVelocity = true;
            ApplySolverVerticalVelocity = true;
        }

        protected virtual void ResetParkourActionData()
        {
            Direction = eDirection.None;
            Duration = 0f;
            TransitionLength = 0f;
            ApplySolverHorizontalVelocity = true;
            ApplySolverVerticalVelocity = true;
        }

        public virtual void StartParkourAction()
        {
            InitializeParkourActionData();
        }

        public virtual void FinishParkourAction()
        {
            ResetParkourActionData();
        }

        public virtual int Compare(ParkourActionData a, ParkourActionData b)
        {
            return a.priority.CompareTo(b.priority);
        }

        protected virtual bool CheckManualOperation(bool isManualOperation)
        {
            return manualOperation == isManualOperation;
        }

        public eAnimationType GetAnimationType(AvatarIKGoal footType)
        {
            if (!CheckFootToSelectAnimation)
                return AnimationType;

            LogUtil.Log($"[GetAnimationType] footType: {footType}".Color(Color.yellow));
            return (eAnimationType)(footType == AvatarIKGoal.RightFoot ? animationType : mirrorAnimationType);
        }

        public bool CheckIfPossible(float moveSpeed, bool isManualOperation)
        {
            if (!CheckManualOperation(isManualOperation))
                return false;

            return !MathUtility.Greater(movementThreshold, 0) || !MathUtility.Greater(movementThreshold, moveSpeed);
        }
    }
}
