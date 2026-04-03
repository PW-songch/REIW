using UnityEngine;

namespace REIW
{
    using REIW.EventLock;
    
    /// <summary>
    /// https://www.notion.so/voyagergames/211229b506f28002989efc1e6603e2a6
    /// </summary>
    public class CharacterMoveGrapple : CharacterMoveComponentBase<CharacterMoveGrappleData>
    {
        private const float DistanceDeltaThreshold = 0.01f;
        
        public bool IsPossibleGrapple => curTargetedPoint != null;
        
        public enum GrappleState
        {
            None,
            Detecting,      // 포인트 찾는 중(=Idle)
            Throw,          // 그래플 던짐
            Grappling,      // 그래플 이동 중
            Landing,        // 그래플 도착 후 런치 대기(입력 없으면 종료)
            LaunchWaiting,  // 런치 요청 후 대기
            Launching,      // 그래플 도착 후 크게 점프
        }
        
        public override CharacterMoveType MoveType => CharacterMoveType.Grapple;
        
        // 캐릭터 이동 목표 포지션
        public Vector3 destinationPoint
        {
            get
            {
                var grapplePoint = onGrapplePoint ?? curTargetedPoint;
                return grapplePoint ? grapplePoint.ArrivePosition : Controller.CharacterTransform.position;
            }
        }

        // 포인트 검출에 스크린 기반 검사 과정이 있어서 거기에 사용..
        private Camera mainCamera;

        private Rect detectionScreenRect;
        private float launchLandingCheckVelocity;

        private GrappleState currentState = GrappleState.Detecting;
        private float stateElapsed = 0;

        private GrapplePoint onGrapplePoint;
        private Vector3 grapplePosition;
        private Vector3 grappleDirection;
        private Vector3 characterLookDir;
        private Vector3 launchDir;
        private Vector3 currentVelocity;
        private float startSpeed;
        private float currentSpeed;
        private float grappleDistance;
        private float grappleMoveTime;
        private float launchingHorizontalSpeed;
        private float launchingVerticalSpeed;
        private float launchingVerticalAngle;
        private bool isFarDistance;
        private bool isStartLaunching;

        private Bounds avoidObstacleBounds;
        private Vector3 obstacleAvoidDirection;
        private Vector3 obstaclePlanePoint;
        private Vector3 obstaclePlaneNormal;
        private Vector3[] obstacleAvoidPos = new Vector3[4];
        private float obstacleAvoidTime;
        private float obstacleAvoidDuration;
        private float obstacleAvoidCheckRadius;

        private Vector3 moveInput;
        private bool jumpPressed;

        private GrapplePoint prevTargetedPoint;
        private GrapplePoint curTargetedPoint;

        private float accelDeceleration = 1.0f;
        private float lastRemainDistance = 0f;

        private eEventLockType currentEventLockType;

        public override eEventLockType CurrentEventLockType => base.CurrentEventLockType | currentEventLockType;

        private Vector3 InputDir
        {
            get
            {
                var input = new Vector3(moveInput.x, 0, moveInput.z);
                return mainCamera.transform.TransformDirection(input).normalized;
            }
        }

#if UNITY_EDITOR
        public Vector3[] ObstacleAvoidPos => obstacleAvoidPos;
#endif

        public override void Initialize(ICharacterMoveController controller)
        {
            base.Initialize(controller);

            mainCamera = IngameCameraSystem.Instance ? 
                IngameCameraSystem.Instance.MainCamera : Camera.main;
            if (mainCamera == null)
            {
                LogUtil.LogError("Main Camera not found!");
            }

            CacheDetectingScreenRegion();
        }

        public override void EnterComponent()
        {
            base.EnterComponent();

            if (currentState == GrappleState.Detecting)
                currentEventLockType = eEventLockType.None;
        }

        public override void ExitComponent()
        {
            if (currentState == GrappleState.Throw)
                return;

            base.ExitComponent();
            ClearGrapplePoint();
            currentState = GrappleState.Detecting;
        }

        public override void FixedUpdateComponent()
        {
            stateElapsed += Time.deltaTime;

            switch (currentState)
            {
                case GrappleState.Detecting:
                    DetectGrapplePoint();
                    break;
                case GrappleState.Launching:
                    if (currentVelocity.y < 0f)
                    {
                        // 런칭 후 하락시 포인트 검사
                        DetectGrapplePoint();
                    }
                    break;
                case GrappleState.Landing:
                    FixedUpdateLanding();
                    break;
            }
        }

        private void FixedUpdateLanding()
        {
            if (stateElapsed > MovementData.launchInputWaiting)
            {
                ChangeState(GrappleState.Detecting);
                if (Controller is LocalCharacter localCharacter)
                    localCharacter.ColliderTransformLinker?.RestoreParent();
                return;
            }

            // 그래플 포인트 도착 후에도 지정 시간 안에 점프 입력을 했을 경우 런칭 시작
            if (jumpPressed)
            {
                jumpPressed = false;
                ChangeState(GrappleState.LaunchWaiting);

                Controller.EventBus.Post<IMoveGrappleEventListener>(_ => _.OnGrappleLaunchRequested((isSuccess) =>
                {
                    if (isSuccess)
                    {
                        ChangeState(GrappleState.Launching);
                        isStartLaunching = true;
                        Controller.EventBus.Post<IMoveGrappleEventListener>(_ => _.OnGrappleLaunchStarted());
                    }
                    else
                    {
                        ChangeState(GrappleState.Detecting);
                        if (Controller is LocalCharacter localCharacter)
                            localCharacter.ColliderTransformLinker?.RestoreParent();
                    }
                }));
            }
        }

        
        public override void UpdateInput(PlayerCharacterInputs inputs)
        {
            if (TryRequestStaminaActionFromInput(EnumCategory.LocomotionStateGrappling, inputs.Grapple))
            {
                if (curTargetedPoint != null)
                {
                    if (currentState is GrappleState.Detecting or GrappleState.Launching)
                    {
                        RequestGrapple();
                    }
                }
            }
            else if (inputs.Jump)
            {
                LogUtil.Log($"CharacterMoveGrapple > UpdateInput: inputs.Jump");
                if (isFarDistance)
                {
                    jumpPressed = true;
                }
            }

            moveInput = new Vector3(inputs.Move.x, 0, inputs.Move.y);
        }

        public override bool UpdateVelocity(ref Vector3 velocity, float deltaTime)
        {
            bool fix = false;
            switch (currentState)
            {
                case GrappleState.Detecting:
                    startSpeed = velocity.magnitude;
                break;
                case GrappleState.Throw:
                    UpdateThrowVelocity(ref velocity, ref fix, deltaTime);
                break;
                case GrappleState.Grappling:
                    UpdateGrappleVelocity(ref velocity, ref fix, deltaTime);
                break;
                case GrappleState.Landing:
                    UpdateLandVelocity(ref velocity, ref fix, deltaTime);
                break;
                case GrappleState.LaunchWaiting:
                    UpdateLaunchWaitingVelocity(ref velocity, ref fix, deltaTime);
                break;
                case GrappleState.Launching:
                    UpdateLaunchVelocity(ref velocity, ref fix, deltaTime);
                break;
            }

            currentVelocity = velocity;
            return fix;
        }

        /// <summary>
        /// 그래플 던진 상태의 속도 제어
        /// </summary>
        private void UpdateThrowVelocity(ref Vector3 velocity, ref bool fix, float deltaTime)
        {
            // 이 상태에선 모든 이동을 무시
            velocity = Vector3.zero;
            fix = true;
        }

        /// <summary>
        /// 그래플 중의 속도 제어
        /// </summary>
        private void UpdateGrappleVelocity(ref Vector3 velocity, ref bool fix, float deltaTime)
        {
            var targetPoint = destinationPoint;
            var currentPos = Controller.CharacterTransform.position;
            // 목표 지점과 현재 자신의 위치로 도착 판단
            var remainDistance = Vector3.Distance(currentPos, targetPoint);
            var hasArrived = remainDistance <= MovementData.arrivalDistance;
            if (!hasArrived)
                grappleDirection = (targetPoint - currentPos).normalized;
            
            var minSpeed = velocity.magnitude;
            var maxSpeed = grappleDistance < MovementData.nearDistanceThreshold ? MovementData.nearMaxSpeed : MovementData.farMaxSpeed;
            var timeRatio = stateElapsed / MovementData.timeToMaxSpeed;
            var curveValue = MovementData.speedCurve.Evaluate(timeRatio); // 지정한 커브에 따라 가속/감속 
            currentSpeed = maxSpeed * curveValue;
            currentSpeed = Mathf.Max(minSpeed, currentSpeed);

            // 그래플 중에는 다른 이동 요소를 무시하고 그래플 이동만 적용 
            velocity = grappleDirection * currentSpeed;
            fix = true;

            //var isNotMovingEnough = Mathf.Abs(remainDistance - _lastRemainDistance) <= DistanceDeltaThreshold;
            if (hasArrived /*|| isNotMovingEnough*/)
            {
                OnGrappleArrival(false);
            }
            else if (Vector3.Distance(currentPos, targetPoint) > MovementData.detectionValidObstacleDistance)
            {
                ObstacleAvoidGrappling(ref velocity, deltaTime);
            }

            lastRemainDistance = remainDistance;
        }

        private void ObstacleAvoidGrappling(ref Vector3 velocity, float deltaTime)
        {
            const int AVOID_START_INDEX = 0, AVOID_INDEX = 1, AVOID_DEBUG_INDEX = 2, AVOID_END_INDEX = 3;

            var targetPoint = destinationPoint;
            var currentPos = Controller.CharacterTransform.position;
            var origin = currentPos + grappleDirection * 0.5f + Controller.Up * Controller.Height * 0.5f;
            var distThisFrame = currentSpeed * deltaTime * MovementData.obstacleAvoidCheckDistance;

            if (TrySetupGroupObstacleAvoid(origin, obstacleAvoidCheckRadius, grappleDirection, distThisFrame,
                    targetPoint, MovementData.minGrappleDistance, MovementData.obstacleLayer,
                    out var combinedBounds) && combinedBounds != avoidObstacleBounds)
            {
                GetShortestAvoidDirection(origin, targetPoint, grappleDirection, Controller.Up,
                    combinedBounds, MovementData.obstacleAvoidExtraClearance, out var clearPoint);

                obstacleAvoidCheckRadius = MovementData.detectionValidObstacleSize;
                avoidObstacleBounds = combinedBounds;

                // 시작 위치
                obstacleAvoidPos[AVOID_START_INDEX] = currentPos;
                // 회피 위치
                obstacleAvoidPos[AVOID_INDEX] = clearPoint;
#if UNITY_EDITOR
                // 디버깅
                obstacleAvoidPos[AVOID_DEBUG_INDEX] = obstacleAvoidPos[AVOID_INDEX];
#endif

                var distance01 = Vector3.Distance(obstacleAvoidPos[AVOID_START_INDEX], obstacleAvoidPos[AVOID_INDEX]);
                var toTarget = Vector3.Lerp(obstacleAvoidPos[AVOID_INDEX], targetPoint, 0.5f) - obstacleAvoidPos[AVOID_INDEX];
                var distToTarget = toTarget.magnitude;
                if (distToTarget < 0.001f)
                {
                    // 복귀 위치
                    obstacleAvoidPos[AVOID_END_INDEX] = targetPoint;
                }
                else
                {
                    // 복귀 위치
                    var dirToTarget = toTarget / distToTarget;
                    var segmentLength = Mathf.Min(distance01, distToTarget);
                    obstacleAvoidPos[AVOID_END_INDEX] = obstacleAvoidPos[AVOID_INDEX] + dirToTarget * segmentLength;
                    obstacleAvoidPos[AVOID_END_INDEX].y = targetPoint.y;
                }

                var curveLength = distance01 + Vector3.Distance(
                    obstacleAvoidPos[AVOID_INDEX], obstacleAvoidPos[AVOID_END_INDEX]);
                obstacleAvoidDuration = Mathf.Max(0.1f, curveLength / currentSpeed);
                obstacleAvoidTime = 0f;
            }

            if (obstacleAvoidDuration > 0f)
            {
                obstacleAvoidTime += deltaTime;

                var t = Mathf.Clamp01(obstacleAvoidTime / obstacleAvoidDuration);

                if (obstacleAvoidTime >= obstacleAvoidDuration)
                    obstacleAvoidDuration = 0f;

                var targetOnCurve = EvaluateQuadraticBezier(obstacleAvoidPos[AVOID_START_INDEX],
                    obstacleAvoidPos[AVOID_INDEX], obstacleAvoidPos[AVOID_END_INDEX], t);
                var toCurve = targetOnCurve - currentPos;
                if (toCurve.sqrMagnitude < 0.0001f)
                    return;

                // 회피 방향
                var curveDir = toCurve.normalized;
                // 기본 그래플 방향과 섞어서 회피 방향 생성
                var baseDir = velocity.normalized;
                // 적용 비율만큼만 적용
                var steerWeight = MovementData.obstacleAvoidSteerWeight;
                var targetDir = Vector3.Slerp(baseDir, curveDir, steerWeight);
                var targetVelocity = targetDir * currentSpeed;
                var lerpFactor = Mathf.Clamp01(deltaTime * MovementData.obstacleAvoidStrength);
                velocity = Vector3.Lerp(velocity, targetVelocity, lerpFactor);
            }
        }

        private bool TrySetupGroupObstacleAvoid(Vector3 origin, float radius, Vector3 dir, float distThisFrame,
            Vector3 targetPoint, float minGrappleDistance, LayerMask obstacleLayer, out Bounds combinedBoundsOut)
        {
            combinedBoundsOut = default;

            var hits = Physics.SphereCastAll(
                origin, radius, dir, distThisFrame, obstacleLayer);
            if (hits == null || hits.Length == 0)
                return false;

            // 거리 순 정렬
            System.Array.Sort(hits, (a, b) => a.distance.CompareTo(b.distance));

            // 조건에 맞는 히트들을 모으고, 동시에 바운드를 합치기
            var hasAny = false;
            var combinedBounds = new Bounds();
            RaycastHit? nearestHit = null;
            var nearestDistance = float.MaxValue;

            for (int i = 0; i < hits.Length; ++i)
            {
                var h = hits[i];
                if (!h.collider || h.collider.isTrigger)
                    continue;

                // 도착 지점과 너무 가까운 장애물은 제외
                if (Vector3.Distance(targetPoint, h.point) <= minGrappleDistance)
                    continue;

                // 처음 하나에서 bounds 초기화
                if (!hasAny)
                {
                    combinedBounds = h.collider.bounds;
                    hasAny = true;
                }
                else
                {
                    combinedBounds.Encapsulate(h.collider.bounds);
                }

                // 회피 기준 hit(가장 가까운 것) 하나는 따로 기억
                if (h.distance < nearestDistance)
                {
                    nearestDistance = h.distance;
                    nearestHit = h;
                }
            }

            if (!hasAny || !nearestHit.HasValue)
                return false;

            combinedBoundsOut = combinedBounds;
            var hit = nearestHit.Value;

            return true;
        }

        private void GetShortestAvoidDirection(Vector3 origin, Vector3 target, Vector3 dir, Vector3 up,
            Bounds bounds, float extraClear, out Vector3 bestClearPoint)
        {
            // 수평 기준 진행 방향 (Y 성분 제거)
            var flatDir = Vector3.ProjectOnPlane(dir, up).normalized;
            if (flatDir.sqrMagnitude < 0.0001f)
            {
                flatDir = new Vector3(dir.x, 0f, dir.z);
                if (flatDir.sqrMagnitude < 0.0001f)
                    flatDir = Vector3.forward;
                flatDir.Normalize();
            }

            // 수평 기준 오른쪽/왼쪽 벡터
            var right = new Vector3(flatDir.z, 0f, -flatDir.x);
            var left  = -right;

            // 후보 방향 2개
            Vector3[] candDirs = { right.normalized, left.normalized };

            var bestCost = float.MaxValue;
            bestClearPoint = origin;

            var extents = bounds.extents;

            for (int i = 0; i < candDirs.Length; ++i)
            {
                var cDir = candDirs[i];

                // 이 방향으로 장애물 바운드의 "반지름" + 여유 거리 계산
                var ad = new Vector3(Mathf.Abs(cDir.x), Mathf.Abs(cDir.y), Mathf.Abs(cDir.z));
                var halfSizeAlongDir = Vector3.Dot(extents, ad);
                var offset = halfSizeAlongDir + extraClear;

                // 회피 지점: 장애물 중심에서 cDir 방향으로 offset 만큼 떨어진 포인트
                var clearPoint = bounds.center + cDir * offset;

                // 타겟 높이로
                clearPoint.y = target.y;

                // 총 경로 길이: origin -> clearPoint -> target
                var cost =
                    Vector3.Distance(origin, clearPoint) +
                    Vector3.Distance(clearPoint, target);

                if (cost < bestCost)
                {
                    bestCost = cost;
                    bestClearPoint = clearPoint;
                }
            }
        }

        private Vector3 EvaluateQuadraticBezier(Vector3 p1, Vector3 p2, Vector3 p3, float t)
        {
            var u  = 1f - t;
            return u * u * p1 + 2f * u * t * p2 + t * t * p3;
        }

        private void ResetAvoidObstacleData()
        {
            avoidObstacleBounds = default;
            obstacleAvoidTime = 0f;
            obstacleAvoidDuration = 0f;
            obstacleAvoidCheckRadius = Controller.Radius;

            for (int i = 0; i < obstacleAvoidPos.Length; ++i)
                obstacleAvoidPos[i] = Vector3.zero;
        }

        /// <summary>
        /// 포인트 도착 직후의 속도 제어
        /// </summary>
        private void UpdateLandVelocity(ref Vector3 velocity, ref bool fix, float deltaTime)
        {
            // 이 상태에선 모든 이동을 무시
            velocity.x = 0f;
            velocity.z = 0f;
            fix = true;
        }

        /// <summary>
        /// 런칭 요청 후 속도 제어
        /// </summary>
        private void UpdateLaunchWaitingVelocity(ref Vector3 velocity, ref bool fix, float deltaTime)
        {
            // 이 상태에선 모든 이동을 무시
            velocity.x = 0f;
            velocity.z = 0f;
            fix = true;
        }

        /// <summary>
        /// 런칭 속도 제어
        /// </summary>
        private void UpdateLaunchVelocity(ref Vector3 velocity, ref bool fix, float deltaTime)
        {
            var inputDir = InputDir;

            if (isStartLaunching)
            {
                var characterTransform = Controller.CharacterTransform;
//                _launchpos = characterTransform.position;
                
                // 런칭 수평 방향 계산
                var planarGrappleDir = new Vector3(grappleDirection.x, 0, grappleDirection.z);
                launchDir = planarGrappleDir;
                if (moveInput.magnitude > 0.1f)
                {
                    // 이동 입력이 있을 경우, 입력 방향으로 틀어준다
                    launchDir = Vector3.Normalize(launchDir + inputDir);

                    // 각도 상한은 존재
                    var angle = Vector3.Angle(planarGrappleDir, launchDir);
                    if (angle > MovementData.launchHorizontalAngleLimit)
                    {
                        var axis = Vector3.Cross(Controller.Forward, launchDir).normalized;
                        launchDir = Quaternion.AngleAxis(MovementData.launchHorizontalAngleLimit, axis) * Controller.Forward;
                    }

                    accelDeceleration = GetAccelDecelation(planarGrappleDir, inputDir);
                }
                else
                    accelDeceleration = 1.0f;

                // 런칭 수직 방향 계산
                var angleRad = Mathf.Deg2Rad * launchingVerticalAngle;
                launchDir = new Vector3(launchDir.x, Mathf.Sin(angleRad), launchDir.z);
                launchDir = launchDir.normalized;

                isStartLaunching = false;

                if (MovementData.showLaunchRays)
                {
                    var originalDir = Quaternion.AngleAxis(-launchingVerticalAngle, characterTransform.right) * characterTransform.forward;
                    var dest = characterTransform.position + originalDir * 3;
                    Debug.DrawLine(characterTransform.position, dest, Color.red);

                    dest = characterTransform.position + inputDir * 3;
                    Debug.DrawLine(characterTransform.position, dest, Color.green);

                    dest = characterTransform.position + inputDir * 3;
                    Debug.DrawLine(characterTransform.position, dest, Color.blue);
                }

                ResetSmoothRotation(launchDir);
            }

            var horizontalTimeRatio = stateElapsed / MovementData.launchHorizontalTimeToMaxSpeed;
            var verticalTimeRatio = stateElapsed / MovementData.launchVerticalTimeToMaxSpeed;
            
            var minSpeed = velocity.magnitude;
            var horizontalCurveValue = MovementData.launchHorizontalSpeedCurve.Evaluate(horizontalTimeRatio);
            var horizontalSpeed = Mathf.Max(minSpeed, launchingHorizontalSpeed * horizontalCurveValue);

            Vector3 launchDirXZ = new Vector3(launchDir.x, 0f, launchDir.z).normalized;
            Vector3 horiz = launchDirXZ * (horizontalSpeed * accelDeceleration);

            velocity.x = horiz.x;
            velocity.z = horiz.z;
            // 런칭 방향과 현재 방향에 따른 감속 
            DecelByCharacterMoveDir(ref velocity, inputDir, launchDirXZ);

            if (verticalTimeRatio <= 1f)
            {
                var verticalCurveValue = MovementData.launchVerticalSpeedCurve.Evaluate(verticalTimeRatio);
                velocity.y = launchDir.y * Mathf.Max(minSpeed, launchingVerticalSpeed * verticalCurveValue);

            }
            else
            {
                // 런칭 감속
                if (MovementData.launchDeceleration > 0f)
                {
                    var decelerated = velocity.magnitude + (MovementData.launchDeceleration * deltaTime);
                    var decelerationVelocity = velocity.normalized * decelerated;
                    velocity.x = decelerationVelocity.x;
                    velocity.z = decelerationVelocity.z;
                }
            }

            // 런칭 중 땅에 착지하면 Detecting 상태로 전환
            if (Controller.IsStableOnCollider && stateElapsed > MovementData.launchLandingCheckDelay)
            {
                ChangeState(GrappleState.Detecting);
                Controller.EventBus.Post<IMoveGrappleEventListener>(_ => _.OnGrappleLaunchLanding());
            }
            
            // 런칭 중에는 다른 속도 제어 요소는 무시
            fix = true;
        }

        void DecelByCharacterMoveDir(ref Vector3 currentvelocity, Vector3 movedir, Vector3 launchdir)
        {
            Vector3 vXZ = new Vector3(currentvelocity.x, 0f, currentvelocity.z);
            float speed = vXZ.magnitude;
            if (speed < Mathf.Epsilon)
                return;
            
            Vector3 velDir = vXZ / Mathf.Max(speed, Mathf.Epsilon);
            velDir.Normalize();

            Vector3 moveDir = new Vector3(movedir.x, 0f, movedir.z);
            if (moveDir.sqrMagnitude <= Mathf.Epsilon) 
                moveDir = velDir;
            else 
                moveDir.Normalize();

            Vector3 ldir = new Vector3(launchdir.x, 0f, launchdir.z);
            if (ldir.sqrMagnitude <= Mathf.Epsilon)
                ldir = velDir; 
            else 
                ldir.Normalize();
            
            float align = Vector3.Dot(moveDir, ldir); // -1..1 (1: 같은 방향, -1: 반대)
            float back = Mathf.Clamp01(-align); // 0→1 (같은→반대)
            float side = 1f - Mathf.Abs(align); // 0→1 (같거나 반대→직각)

            float launchDecelAgainst = MovementData.launchDecelAgainst;
            float launchDecelPerp = launchDecelAgainst * 0.5f;
            float launchDecelWith = 0;
            
            float decelRate = (align >= 0f)
                ? Mathf.Lerp(launchDecelWith, launchDecelPerp, side) // 같은/비슷한 방향: 약~중 감속
                : Mathf.Lerp(launchDecelPerp, launchDecelAgainst, back); // 반대쪽일수록 더 큰 감속
            
            float newSpeed = speed - (speed * decelRate);
            Vector3 newVXZ = launchdir * newSpeed;
#if JIN_TEST
            Debugging.LogGreen($"movedir : {movedir} , launchdir : {launchdir}, align : {align}, decelRate : {decelRate}, speed : {speed}, newSpeed : {newSpeed}");
#endif
            currentvelocity.x = newVXZ.x;
            currentvelocity.z = newVXZ.z;
        }

        private float GetAccelDecelation(Vector3 launchDir, Vector3 currentForward)
        {
            Vector3 dH = Vector3.ProjectOnPlane(launchDir, Vector3.up).normalized;
            Vector3 fH = Vector3.ProjectOnPlane(currentForward, Vector3.up).normalized;

            float dot = Vector3.Dot(dH, fH);
            float thresholdDot = Mathf.Cos(120f * Mathf.Deg2Rad);
            float targetFactor;
            if (dot <= thresholdDot)
                targetFactor = 0f;
            else
                targetFactor = (dot - thresholdDot) / (1f - thresholdDot);

            return targetFactor;
        }
        
        private Vector3 _smoothedLookDir = Vector3.zero;
        
        public override bool UpdateRotation(ref Quaternion rotation, float deltaTime)
        {
            Vector3? lookDirection =  null;
            switch (currentState)
            {
                case GrappleState.Grappling:
                    if (onGrapplePoint != null)
                    {
                        _smoothedLookDir = Vector3.zero;
                        // 그래플 중, 시선 방향은 포인트로 고정
                        lookDirection = grappleDirection;
                    }
                    break;

                case GrappleState.Launching:
                {
                    float moveTimeRatio = stateElapsed / MovementData.launchMoveMaxSpeed;
                    Vector3 launchDirXZ = new Vector3(launchDir.x, 0f, launchDir.z).normalized;
                    Vector3 movenomalized;
                    // 일정시간까지 런칭 방향 유지
                    if (moveTimeRatio <= 1f)
                    {
                        movenomalized = launchDirXZ;
                    }
                    else
                    {
                        float time = Mathf.Clamp01(MovementData.launchMoveRotaionPower * deltaTime);
                        movenomalized = Vector3.Slerp(_smoothedLookDir, InputDir, time);
                    }

                    lookDirection = movenomalized;
                    break;
                }
            }

//            DrawLine();

            if (TryRotate(lookDirection, deltaTime * MovementData.rotationSpeed, ref rotation))
            {
                return true;
            }

            return false;

/*            void DrawLine()
            {
                Vector3 lookdir = (Controller.CharacterTransform.position - _launchpos).normalized;
                Debug.DrawLine(_launchpos, _launchpos + (lookdir * 100), Color.yellow );
            }*/
        }

        private void ResetSmoothRotation(Vector3 launch)
        {
            _smoothedLookDir = launch.normalized;
        }

        bool TryRotate(Vector3? lookDirection, float deltaTime, ref Quaternion rotation)
        {
            if (!lookDirection.HasValue) return false;

            // 1) Y 제거 + 데드존
            Vector3 rawDir = Vector3.ProjectOnPlane(lookDirection.Value, Vector3.up);
            if (rawDir.sqrMagnitude < Mathf.Epsilon) return false;

            rawDir.Normalize();

            // 2) 180° 근처 뒤집힘 억제(선택)
            if (_smoothedLookDir.sqrMagnitude > Mathf.Epsilon &&
                Vector3.Angle(_smoothedLookDir, rawDir) > 175f)
            {
                rawDir = _smoothedLookDir;
            }

            // 3) 입력 방향 평활화(각속도 제한)
            float maxRadStep = Mathf.Deg2Rad * MovementData.launchDirTrackDegPerSec * deltaTime;
            _smoothedLookDir = (_smoothedLookDir.sqrMagnitude < Mathf.Epsilon)
                ? rawDir
                : Vector3.RotateTowards(_smoothedLookDir, rawDir, maxRadStep, 0f);

            // ==== Yaw만 사용 ====
            float targetYaw = Mathf.Atan2(_smoothedLookDir.x, _smoothedLookDir.z) * Mathf.Rad2Deg;
            Quaternion targetYawOnly = Quaternion.Euler(0f, targetYaw, 0f);

            // 4) 최종 회전도 각속도 제한 (Yaw만 따라감)
            float maxYawStep = MovementData.launchYawDegPerSec * deltaTime;
            Quaternion stepped = Quaternion.RotateTowards(Controller.CharacterTransform.rotation, targetYawOnly, maxYawStep);

            // 혹시 모를 Pitch/Roll 제거(보수적)
            float newYaw = stepped.eulerAngles.y;
            rotation = Quaternion.Euler(0f, newYaw, 0f);

            if (Controller is LocalCharacter local)
                local.CharacterLookDir = _smoothedLookDir;

// #if JIN_TEST
//             Debugging.LogOrange(
//                 $"cur:{Controller.CharacterTransform.rotation.eulerAngles}, " +
//                 $"targetYaw:{targetYaw:F2}, newYaw:{newYaw:F2}, dt:{deltaTime:F6}"
//             );
// #endif

            return true;
        }

        private bool CheckGrappleCondition()
        {
            if (Controller is not LocalCharacter local)
                return false;

            if (local.IsEventLockType(eEventLockType.CharacterGraple))
                return false;

            CharacterMoveWallClimb wallclimb = local.CharacterMoveComponentsHandler.GetMoveComponent<CharacterMoveWallClimb>();
            return wallclimb.IsPossibleGrapple;
        }

        private void DetectGrapplePoint()
        {
            if (CheckGrappleCondition() == false)
            {
                ReleaseGrapplePoint();
                return;
            }
            
            if(MovementData.refreshDetectionScreenRegion)
                CacheDetectingScreenRegion();

            var characterTransform = Controller.CharacterTransform;
            var nearestPoint = EnvironmentScanner.DetectGrapplePoint(mainCamera, detectionScreenRect, MovementData);

            if (nearestPoint != null && (curTargetedPoint == null || nearestPoint != curTargetedPoint.transform))
            {
                prevTargetedPoint = curTargetedPoint;
                // curTargetedPoint = nearestPoint.GetComponent<GrapplePoint>();
                curTargetedPoint = nearestPoint;
                grapplePosition = curTargetedPoint.GrapplePosition;
                Controller.EventBus.Post<IMoveGrappleEventListener>(_=>_.OnGrapplePointTargeted(prevTargetedPoint, curTargetedPoint, grapplePosition));
            }
            else if (curTargetedPoint != null && (nearestPoint == null || !curTargetedPoint.IsEnable(Controller.CharacterTransform)))
            {
                ReleaseGrapplePoint();
            }

            if (MovementData.showAngleRays)
            {
                if (curTargetedPoint != null)
                {
                    var dirToPoint = (curTargetedPoint.Position - mainCamera.transform.position).normalized;
                    var cAngle = Vector3.Angle(mainCamera.transform.forward, dirToPoint);
                    Debug.DrawLine(mainCamera.transform.position, curTargetedPoint.Position, Color.magenta);
                    var end = mainCamera.transform.position + mainCamera.transform.forward * 3;
                    Debug.DrawLine(mainCamera.transform.position, end, Color.magenta);

                    dirToPoint = (curTargetedPoint.Position - characterTransform.position).normalized;
                    var pAngle = Vector3.Angle(characterTransform.up, dirToPoint);
                    //Debug.DrawLine(characterTransform.position, curTargetedPoint.Position, Color.cyan);
                    end = characterTransform.position + characterTransform.up * 3;
                    //Debug.DrawLine(characterTransform.position, end, Color.cyan);
                }
            }
        }

        private void ReleaseGrapplePoint()
        {
            if (curTargetedPoint == null)
                return;

            EnvironmentScanner.ResetDebugGrapplePoint();
            prevTargetedPoint = curTargetedPoint;
            curTargetedPoint = null;

            Controller.EventBus.Post<IMoveGrappleEventListener>(_=>_.OnGrapplePointTargeted(prevTargetedPoint, curTargetedPoint, Vector3.negativeInfinity));
        }

        private void ClearGrapplePoint()
        {
            if (curTargetedPoint == null)
                return;

            ReleaseGrapplePoint();
        }
        
        private void RequestGrapple()
        {
            if (curTargetedPoint == null)
                return;

            onGrapplePoint = curTargetedPoint;
            grappleDirection = (destinationPoint - Controller.CharacterTransform.position).normalized;
            grappleDistance = Vector3.Distance(Controller.CharacterTransform.position, destinationPoint);
            lastRemainDistance = 0;

            isFarDistance = grappleDistance > MovementData.nearDistanceThreshold;
            currentSpeed = startSpeed;
            jumpPressed = false;

            Controller.EventBus.Post<IMoveGrappleEventListener>(_ => _.OnGrappleRequested(onGrapplePoint, grapplePosition, grappleDistance, isFarDistance, StartGrapple));
        }

        public void StartThrow()
        {
            ChangeState(GrappleState.Throw);
        }
        
        private void StartGrapple(bool isSuccess)
        {
            if (isSuccess)
            {
                if (curTargetedPoint == null)
                    return;
                
                if (Controller is LocalCharacter localCharacter)
                    localCharacter.ExecuteStaminaAction(EnumCategory.LocomotionStateGrappling);

                ResetAvoidObstacleData();

                ChangeState(GrappleState.Grappling);

                grappleMoveTime = MovementData.speedCurve.CalculateMoveTime(grappleDistance,
                    grappleDistance < MovementData.nearDistanceThreshold ?
                    MovementData.nearMaxSpeed : MovementData.farMaxSpeed, MovementData.timeToMaxSpeed,
                    MovementData.moveTimeCalculateSamples);

                Controller.EventBus.Post<IMoveGrappleEventListener>(_ => _.OnGrappleStarted(onGrapplePoint, grappleMoveTime));
            }
            else
            {
                ChangeState(GrappleState.Detecting);
                onGrapplePoint = null;

                if (Controller is LocalCharacter localCharacter)
                    localCharacter.LockMoveInput = false;
            }
        }

        private void OnGrappleArrival(bool force)
        {
            var grapplePoint = onGrapplePoint ?? curTargetedPoint;
            if (grapplePoint != null)
            {
                if (grapplePoint.IsValidLaunchHorizontalSpeed)
                    launchingHorizontalSpeed = grapplePoint.LaunchHorizontalSpeed;
                else
                    launchingHorizontalSpeed = MovementData.launchHorizontalSpeed;

                if (grapplePoint.IsValidLaunchVerticalSpeed)
                    launchingVerticalSpeed = grapplePoint.LaunchVerticalSpeed;
                else
                    launchingVerticalSpeed = MovementData.launchVerticalSpeed;

                if (grapplePoint.IsValidLaunchVerticalAngle)
                    launchingVerticalAngle = grapplePoint.LaunchVerticalAngle;
                else
                    launchingVerticalAngle = MovementData.launchVerticalAngle;
            }

            ChangeState(GrappleState.Landing);
            ReleaseGrapplePoint();
            onGrapplePoint = null;
            
            Controller.EventBus.Post<IMoveGrappleEventListener>(_ => _.OnGrappleArrival(force));
        }

        private void ChangeState(GrappleState newState)
        {
            currentState = newState;
            stateElapsed = 0;

            switch (currentState)
            {
                case GrappleState.Throw:
                    LocalCharacter.LockMoveInput = false;
                    currentEventLockType |= eEventLockType.CharacterInputLock;
                    break;
                case GrappleState.Landing:
                    currentEventLockType &= ~eEventLockType.CharacterInputLock;
                    if (!jumpPressed)
                        LocalCharacter.LockMoveInput = false;
                    break;
            }
        }
        
        private void CacheDetectingScreenRegion()
        {
            var scale = Screen.height / 1080f;
            detectionScreenRect.xMin = (Screen.width * 0.5f) - (MovementData.detectionHorizontalPx * scale);
            detectionScreenRect.xMax = (Screen.width * 0.5f) + (MovementData.detectionHorizontalPx * scale);
            detectionScreenRect.yMin = (Screen.height * 0.5f) + (MovementData.detectionBotPx * scale);
            detectionScreenRect.yMax = Screen.height;
        }
        
        public bool IsCurrentState(GrappleState state) => currentState == state;

        public bool AvailableLandingMove(float normaltime)
        {
            return normaltime >= MovementData.landingNormalMoveEnableDelay;
        }

        public bool ShowGrapplePoint => MovementData.showGrapplePoint;
    }
}