using System;
using UnityEngine;
using Animancer.Units;
using RootMotion.FinalIK;
using static Animancer.Validate;

namespace REIW.Animations.Character
{
    [DisallowMultipleComponent]
    public partial class CharacterAnimationMovement : MonoBehaviour
    {
        [Header("Movement Settings")]
        [SerializeField, MetersPerSecond(Rule = Value.IsNotNegative)]
        private float _maxSpeed = 10;
        public float MaxSpeed => _maxSpeed;

        [SerializeField, MetersPerSecond(Rule = Value.IsNotNegative)]
        private float _walkSpeed = 4;
        public float WalkSpeed => _walkSpeed;

        [SerializeField, MetersPerSecond(Rule = Value.IsNotNegative)]
        private float _runSpeed = 8;
        public float RunSpeed => _runSpeed;

        [SerializeField, MetersPerSecond(Rule = Value.IsNotNegative)]
        private float _stopSpeed = 7;
        public float StopSpeed => _stopSpeed;

        [SerializeField, MetersPerSecondPerSecond(Rule = Value.IsNotNegative)]
        private float _acceleration = 10;
        public float Acceleration => _acceleration;

        [SerializeField, MetersPerSecondPerSecond(Rule = Value.IsNotNegative)]
        private float _sprintAcceleration = 15;
        public float SprintAcceleration => _sprintAcceleration;

        [SerializeField, MetersPerSecondPerSecond(Rule = Value.IsNotNegative)]
        private float _deceleration = 10;
        public float Deceleration => _deceleration;

        [SerializeField, MetersPerSecondPerSecond(Rule = Value.IsNotNegative)]
        private float _verticalSpeed = 10;
        public float VerticalSpeed => _verticalSpeed;

        [SerializeField, DegreesPerSecond(Rule = Value.IsNotNegative)]
        private float _minTurnSpeed = 400;
        public float MinTurnSpeed => _minTurnSpeed;

        [SerializeField, DegreesPerSecond(Rule = Value.IsNotNegative)]
        private float _maxTurnSpeed = 1200;
        public float MaxTurnSpeed => _maxTurnSpeed;

        [SerializeField, Meters(Rule = Value.IsNotNegative)]
        private float _checkMovingDistance = 0f;

        [SerializeField, MetersPerSecond] private float _checkAirborneVerticalSpeed = 0f;
        [SerializeField, MetersPerSecond] private float _checkFallingVerticalSpeed = 0f;

        [SerializeField] private CharacterAnimation _characterAnimation;
        public CharacterAnimation CharacterAnimation => _characterAnimation;
        
        private float _desiredForwardSpeed;
        private float _verticalSpeedParameter;
        private bool _isSprintInput;
        private bool _isAirborne;
        private bool _isContinuousJump;
        private bool _isValidRootMotionTurnDirection;
        private bool _isApplyRootMotionRotationWithCharacterLookDir;

        private CharacterBase Character => _characterAnimation.Character;
        private LocalCharacter MyLocalCharacter => _characterAnimation.Character as LocalCharacter;
        private CharacterAnimationParameters AnimationParameters => _characterAnimation.Parameters;

        private bool IsLocalCharacter => _characterAnimation.IsLocal;

        public Vector3 LookDir => Character?.CharacterLookDir ?? Vector3.zero;
        public Vector3 MovementDirection => Character?.CharacterMoveDir ?? Vector3.zero;
        public Vector3 CurrentMoveVelocity => Character?.CurrentMoveVelocity ?? Vector3.zero;
        public eMoveType CurrentMoveType { get; set; }
        public eTurnDirection RootMotionTurnDirection { get; set; }

        public bool IsMoving => CurrentMoveVelocity.sqrMagnitude > _checkMovingDistance;
        public bool IsLocomotionMoving => IsMoving && (_characterAnimation.CurrentBaseStateType == eStateType.RUN || IsWalking || IsSprint);
        public bool IsWalking => IsWalkInput && _characterAnimation.StateMachine.Walk.IsWalking;
        public bool IsSprint => Mathf.Floor(AnimationParameters.ForwardSpeed * 1000f) / 1000f > RunSpeed;
        public bool IsGrappling => _characterAnimation.StateMachine.Grapple.IsGrappling;

        public T GetMovementData<T>() where T : class, ICharacterMoveComponent
        {
            if (Character is not LocalCharacter local)
                return null;

            return local.CharacterMoveComponentsHandler.GetMoveComponent<T>();
        }

        public bool IsContinuousJump
        {
            get
            {
                var isContinuousJump = _isContinuousJump;
                _isContinuousJump = false;
                return isContinuousJump;
            }
            set => _isContinuousJump = value;
        }

        public bool IsFalling => VerticalSpeedParameter < _checkFallingVerticalSpeed;

        public float CurrentTurnSpeed => Mathf.Lerp(MaxTurnSpeed, MinTurnSpeed,
            AnimationParameters.ForwardSpeed / _desiredForwardSpeed);

        public float RootMotionRotationSpeed { set; private get; }
        public bool IsCorrectionRootMotion { set; private get; }
        public bool CheckGroundedFoot { set; private get; }

        public bool IsApplyRootMotionRotationWithCharacterLookDir
        {
            set
            {
                _isApplyRootMotionRotationWithCharacterLookDir = value;
                _isValidRootMotionTurnDirection = false;
            }
        }

        public float VerticalSpeedParameter
        {
            get => _verticalSpeedParameter;
            set
            {
                _verticalSpeedParameter = value;
                AnimationParameters.VerticalSpeed = _verticalSpeedParameter;
            }
        }

        public CharacterRootMotionMode UseHorizontalRootMotionPosition
        {
            set
            {
                if (Character) Character.ModeRootMotionHorizontalPos = value;
            }
        }

        public CharacterRootMotionMode UseVerticalRootMotionPosition
        {
            set
            {
                if (Character) Character.ModeRootMotionVerticalPos = value;
            }
        }

        public CharacterRootMotionMode UseRootMotionRotation
        {
            set
            {
                if (Character) Character.ModeRootMotionRotation = value;
            }
        }

        public Vector3 RootMotionPosition
        {
            set
            {
                if (Character)
                {
                    Character.AddRootMotionPosition(value);
                }
            }
        }

        public Quaternion RootMotionRotation
        {
            set
            {
                if (Character) Character.AddRootMotionRotation(value);
            }
        }

        public event Func<Vector3, Vector3> RootMotionPositionCorrectionFunc;
        public event Func<Quaternion, Quaternion, Quaternion> RootMotionRotationCorrectionFunc;

        private void LateUpdate()
        {
            if (IsLocalCharacter)
            {
                UpdateInputState();
                UpdateAirborneState();
                UpdateFootStep();

                // 점프 로직 변경으로 주석처리
                //UpdateFoots();
            }

            UpdateGrounderIK();
            UpdateAimIKController();
        }

        public void Initialize(CharacterAnimation characterAnimation)
        {
            _characterAnimation = characterAnimation;

            InitializeIK();

            if (_characterAnimation && IsLocalCharacter)
                _characterAnimation.AnimatorMoveEvent += OnAnimatorMoveEvent;
        }

        public void UpdateForwardSpeedParameter()
        {
            var movement = MovementDirection;

            var maxSpeed = IsSprintInput ? MaxSpeed : (IsWalkInput ? WalkSpeed : RunSpeed);
            _desiredForwardSpeed = Mathf.Min(movement.magnitude * maxSpeed, maxSpeed);

            var parameters = AnimationParameters;
            var deltaSpeed = movement != Vector3.zero
                ? (IsSprintInput && parameters.ForwardSpeed > WalkSpeed ? SprintAcceleration : Acceleration)
                : Deceleration;
            parameters.ForwardSpeed = Mathf.MoveTowards(parameters.ForwardSpeed, _desiredForwardSpeed,
                deltaSpeed * Time.deltaTime);
        }

        public void UpdateVerticalSpeedParameter(bool applyLerp = false)
        {
            _verticalSpeedParameter = Mathf.MoveTowards(_verticalSpeedParameter,
                CurrentMoveVelocity.y, VerticalSpeed * Time.deltaTime);

            if (applyLerp)
                AnimationParameters.VerticalSpeed = Mathf.MoveTowards(AnimationParameters.VerticalSpeed,
                    _verticalSpeedParameter, VerticalSpeed * Time.deltaTime);
            else
                AnimationParameters.VerticalSpeed = _verticalSpeedParameter;
        }

        public float GetVerticalAngleToTarget(Transform source, Transform target)
        {
            var direction = target.position - source.position;
            var horizontalDir = new Vector3(direction.x, 0, direction.z);
            return Mathf.Atan2(direction.y, horizontalDir.magnitude) * Mathf.Rad2Deg;
        }

        public bool GetTurnAngles(Vector3 direction, Vector3 forward, Vector3 up, out float curAngle, out float targetAngle)
        {
            if (direction == Vector3.zero)
            {
                curAngle = float.NaN;
                targetAngle = float.NaN;
                return false;
            }

            var alignToWorldUp = Quaternion.FromToRotation(up, Vector3.up);

            var curXZ = Vector3.ProjectOnPlane(alignToWorldUp * forward, Vector3.up).normalized;
            curAngle = Mathf.Atan2(curXZ.x, curXZ.z) * Mathf.Rad2Deg;

            var targetXZ = Vector3.ProjectOnPlane(alignToWorldUp * direction, Vector3.up).normalized;
            targetAngle = Mathf.Atan2(targetXZ.x, targetXZ.z) * Mathf.Rad2Deg;
            return true;
        }

        public void TurnTowards(float currentAngle, float targetAngle, float speed)
        {
            currentAngle = Mathf.MoveTowardsAngle(currentAngle, targetAngle, speed * Time.deltaTime);

            if (Character)
                Character.CharacterTransform.eulerAngles = new(0, currentAngle, 0);
            else
                transform.eulerAngles = new(0, currentAngle, 0);
        }

        private Vector3 GetRootMotion()
        {
            var rawMotion = _characterAnimation.StateMachine.CurrentState.RootDeltaPosition;

            if (IsCorrectionRootMotion)
            {
                if (rawMotion.magnitude < 0.01f)
                    rawMotion = Character.Forward * CurrentMoveVelocity.magnitude * Time.deltaTime;
            }

            if (_characterAnimation.StateMachine.CurrentState.ApplyRawRootMotion)
                return rawMotion;

            var direction = MovementDirection;
            if (direction == Vector3.zero)
                return rawMotion;

            var magnitude = direction.magnitude;
            direction /= magnitude;

            var controlledMotion = direction * Vector3.Dot(direction, rawMotion);
            var resultMotion = Vector3.Max(rawMotion, Vector3.Lerp(rawMotion, controlledMotion, magnitude));
            if (IsCorrectionRootMotion && resultMotion.magnitude < 0.01f)
                resultMotion = Character.Forward * Time.deltaTime;
            return resultMotion;
        }

        private void UpdateInputState()
        {
            // if (IsSprintInput)
            // {
            //     if (IsSprintInputBuffer)
            //     {
            //         if (IsMoveInput)
            //             IsSprintInputBuffer = false;
            //     }
            //     else if (!IsMoveInput)
            //     {
            //         if (_actionInputBuffer.value == eCharacterActionInputType.NONE)
            //             IsSprintInputBuffer = true;
            //         else if (_actionInputBuffer.value == eCharacterActionInputType.SPRINT &&
            //                  !_actionInputBuffer.hasBuffer)
            //             IsSprintInput = false;
            //     }
            // }
        }

        private void OnAnimatorMoveEvent()
        {
            if (!Character)
                return;

            var rootDeltaPosition = GetRootMotion();
            if (RootMotionPositionCorrectionFunc != null)
            {
                rootDeltaPosition = RootMotionPositionCorrectionFunc.Invoke(rootDeltaPosition);
            }

            var rootDeltaRotation = Character.ModeRootMotionRotation != CharacterRootMotionMode.Ignore ?
                _characterAnimation.StateMachine.CurrentState.RootDeltaRotation : Quaternion.identity;
            if (RootMotionRotationCorrectionFunc != null)
            {
                rootDeltaRotation = RootMotionRotationCorrectionFunc.Invoke(rootDeltaRotation,
                    _characterAnimation.StateMachine.CurrentState.RootMotionRotation);
            }

            if (_isApplyRootMotionRotationWithCharacterLookDir)
            {
                if (rootDeltaRotation.eulerAngles != Vector3.zero)
                {
                    if (GetTurnAngles(Character.CharacterLookDir, Character.Forward, Character.Up, out float currentAngle, out float targetAngle))
                    {
                        if (RootMotionRotationSpeed > 0f)
                        {
                            rootDeltaRotation.ToAngleAxis(out float angle, out Vector3 axis);
                            angle = Mathf.Min(RootMotionRotationSpeed * Time.deltaTime, angle);
                            rootDeltaRotation *= Quaternion.AngleAxis(angle, axis);
                            currentAngle += angle * (currentAngle > 0d ? -1f : 1f);
                        }

                        var deltaAngle = Mathf.DeltaAngle(currentAngle, targetAngle);
                        var absDeltaAngle = Mathf.Abs(deltaAngle);
                        var direction = deltaAngle < 0 ? eTurnDirection.LEFT : eTurnDirection.RIGHT;
                        if (!_isValidRootMotionTurnDirection && direction == RootMotionTurnDirection)
                            _isValidRootMotionTurnDirection = true;

                        if (_isValidRootMotionTurnDirection && (absDeltaAngle < 2f || (RootMotionTurnDirection != direction && absDeltaAngle < 45f)))
                        {
                            UseRootMotionRotation = CharacterRootMotionMode.Ignore;
                            rootDeltaRotation = Quaternion.identity;
                        }
                    }
                    else
                    {
                        rootDeltaRotation = Quaternion.identity;
                    }
                }
            }
            else
            {
                RootMotionTurnDirection = eTurnDirection.NONE;
                _isValidRootMotionTurnDirection = false;
            }

            RootMotionPosition = rootDeltaPosition;
            RootMotionRotation = rootDeltaRotation;
        }
    }
}