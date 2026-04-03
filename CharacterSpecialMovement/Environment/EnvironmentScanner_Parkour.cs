using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.Pool;

namespace REIW
{
    public partial class EnvironmentScannerComponent
    {
        private CharacterMoveParkourData _parkourData;

        public const int FLOOR_DECIMALS = 3;

        private readonly Vector2 BOX_CAST_RANGE = new(0.1f, 1.2f);
        private readonly Vector3 PARABOLA_BOX_CAST_HALF = new(0.1f, 0.01f, 0.01f);
        private readonly Vector3 DOWNWARD_BOX_CAST_HALF = new(0.1f, 0.01f, 0.2f);

#if UNITY_EDITOR
        private List<Vector3> _debugFindJumpPoints = new();
        private List<Vector3> _debugLastDetectedJumpPoints;
#endif

        public void SetParkourData(CharacterMoveParkourData data)
        {
            _parkourData = data;
        }

        public ObstacleHitData ObstacleCheck(bool performHeightCheck = true, float forwardOriginOffset = 0.5f)
        {
            if (!_parkourData)
                return default;

            var hitData = new ObstacleHitData();
            var forward = characterDirection;
            var forwardOffset = forwardOriginOffset;
            var forwardOrigin = Vector3.zero;

            for (int i = 0; i < 5; ++i, forwardOffset += 0.1f)
            {
                forwardOrigin = _character.CharacterTransform.position + Vector3.up * forwardOffset;
                hitData.forwardHitFound = Physics.BoxCast(forwardOrigin, new Vector3(0.1f, forwardOffset, 0.01f),
                    forward, out var forwardHit, Quaternion.LookRotation(forward), _parkourData.ObstacleCheckRange,
                    _parkourData.ObstacleLayer);

                if (hitData.forwardHitFound)
                {
                    hitData.SetForwardHit(forwardHit);
                    break;
                }
            }

            if (hitData.forwardHitFound && performHeightCheck)
            {
                var heightOrigin = hitData.forwardHit.point + Vector3.up * _parkourData.HeightRayLength;
                var spaceCheckOrigin = _character.CharacterTransform.position + Vector3.up * _parkourData.HeightRayLength;
                spaceCheckOrigin.y = heightOrigin.y;
                if (Physics.Raycast(spaceCheckOrigin, Vector3.down, out hitData.heightHit, _parkourData.HeightRayLength,
                        _parkourData.ObstacleLayer) && hitData.heightHit.point.y > hitData.forwardHit.point.y)
                    heightOrigin.y = hitData.heightHit.point.y;

                for (var i = 0; i < 4; ++i)
                {
                    hitData.heightHitFound = Physics.SphereCast(heightOrigin, 0.2f, Vector3.down, out hitData.heightHit,
                        _parkourData.HeightRayLength, _parkourData.ObstacleLayer);
                    if (hitData.heightHitFound && Vector3.Angle(Vector3.up, hitData.heightHit.normal) < 45f)
                        break;

                    hitData.heightHitFound = false;
                    heightOrigin += forward * 0.15f;
                }

                if (hitData.heightHitFound)
                {
                    var length = 0.8f;
                    forwardOrigin = hitData.heightHit.point;
                    forwardOrigin.y = hitData.heightHit.point.y + 0.2f + length * 1.5f / 2;
                    // hitData.hasSpace = !Physics.CheckBox(forwardOrigin, new Vector3(0.5f, 1.5f, 0.7f) * length / 2,
                    //     Quaternion.LookRotation(forward), _parkourData.ObstacleLayer);

                    var spaceOrigin = hitData.heightHit.point + forward * 0.5f + Vector3.up * 0.6f;
                    hitData.hasSpaceToVault = Physics.SphereCast(spaceOrigin, 0.1f,
                        Vector3.down, out var spaceHit, 1f, _parkourData.ObstacleLayer);

                    var dir = hitData.heightHit.point - _character.CharacterTransform.position;
                    dir.y = 0;
                    heightOrigin = hitData.heightHit.point;
                    heightOrigin.y += 0.8f;
                    hitData.ledgeHit = hitData.heightHit;

                    var i = 1;
                    for (; i <= _parkourData.LedgeFoundCount; ++i)
                    {
                        var ledgeHitFound = Physics.CheckSphere(heightOrigin, 0.4f, _parkourData.ObstacleLayer);

                        if (!ledgeHitFound)
                        {
                            ledgeHitFound = Physics.SphereCast(heightOrigin, 0.3f, Vector3.down,
                                out RaycastHit ledgeHit, 2f, _parkourData.ObstacleLayer);

                            if (ledgeHitFound && Mathf.Abs(ledgeHit.point.y - hitData.heightHit.point.y) < 0.4f)
                            {
                                hitData.ledgeHit = ledgeHit;
                                hitData.ledgeHitFound = true;
                            }
                            else
                                break;

                            heightOrigin += forward * 0.4f;
                        }
                        else
                        {
                            hitData.ledgeHitFound = false;
                            break;
                        }
                    }

                    if (hitData.ledgeHitFound)
                        hitData.ledgeHitFound = i <= _parkourData.LedgeFoundCount && !Physics.CheckSphere(
                            hitData.ledgeHit.point + Vector3.up * 0.5f, 0.3f, _parkourData.ObstacleLayer);
                }

            }

            hitData.SetHitInfo(_character.CharacterTransform);
            return hitData;
        }

        public Transform ClimbLedgeCheck(Vector3 jumpDir)
        {
            if (!_parkourData)
                return null;

            if (jumpDir == Vector3.zero)
                jumpDir = characterDirection;

            Physics.BoxCast(_character.CharacterTransform.position + Vector3.up * 2f, new Vector3(0.3f, 1f, 0.01f),
                jumpDir, out var ledgeHit, Quaternion.LookRotation(jumpDir), 0.6f, _parkourData.LedgeLayer);
            return ledgeHit.transform;
        }

        public bool ClimbLedgeCheck(Vector3 dir, out ClimbLedgeData climbLedgeData)
        {
            if (!_parkourData)
            {
                climbLedgeData = default;
                return false;
            }

            climbLedgeData = new ClimbLedgeData();

            if (dir == Vector3.zero)
                return false;

            var origin = _character.CharacterTransform.position + Vector3.up;

            for (int i = 0; i < 15; ++i)
            {
                if (Physics.Raycast(origin + new Vector3(0, 0.15f, 0) * i, dir, out RaycastHit hit, 1f, _parkourData.LedgeLayer))
                {
                    climbLedgeData.ledgeHit = hit;
                    return true;
                }
            }

            return false;
        }

        public bool DropLedgeCheck(Vector3 moveDir, out ClimbLedgeData climbLedgeData)
        {
            if (!_parkourData)
            {
                climbLedgeData = default;
                return false;
            }

            climbLedgeData = new ClimbLedgeData();

            var forward = characterDirection;
            var origin = _character.CharacterTransform.position + forward * 1 + Vector3.down * 0.1f;

            if (Physics.SphereCast(origin, 0.1f, -forward, out RaycastHit hit, 1f + 0.4f, _parkourData.LedgeLayer))
            {
                climbLedgeData.ledgeHit = hit;
                return true;
            }

            return false;
        }

        public bool LedgeCheck(Vector3 moveDir, out LedgeData ledgeData)
        {
            if (!_parkourData)
            {
                ledgeData = default;
                return false;
            }

            ledgeData = new LedgeData();

            var forward = characterDirection;

            if (moveDir == Vector3.zero)
                moveDir = forward;

            var rigthVec = Vector3.Cross(Vector3.up, moveDir);
            var origin = _character.CharacterTransform.position + moveDir * 0.6f + Vector3.up;
            var surfaceOrgin = _character.CharacterTransform.position + moveDir.normalized * 0.8f +
                               Vector3.down * 0.05f;

            if (ledgeData.surfaceHitFound == Physics.Raycast(surfaceOrgin, -moveDir, out ledgeData.surfaceHit, 1f, _parkourData.ObstacleLayer))
            {
                ledgeData.angle = Vector3.Angle(forward, ledgeData.surfaceHit.normal);

                var distPoint = ledgeData.surfaceHit.point;
                distPoint.y = _character.CharacterTransform.position.y;
                var distVec = distPoint - _character.CharacterTransform.position;

                ledgeData.distance = Vector3.Dot(distVec, ledgeData.surfaceHit.normal);

                surfaceOrgin = _character.CharacterTransform.position + Vector3.up * _parkourData.LedgeHeightThreshold +
                               moveDir.normalized * 1f;
                if (Physics.Raycast(surfaceOrgin, Vector3.down, out var heightHit, 2f, _parkourData.ObstacleLayer))
                    if ((ledgeData.height = heightHit.distance) < _parkourData.LedgeHeightThreshold * 2)
                    {
                        return false;
                    }

                return true;
            }


            return false;
        }

        public JumpData FindPointToJump(Vector3 jumpDir, float minJumpDistance, float maxJumpDistance,
            float jumpHeight, bool checkClimb = true, bool findBaseGround = false)
        {
            if (!_parkourData)
                return default;

            var forward = characterDirection;
            var jumpPoint = new JumpData();

            jumpDir = (jumpDir == Vector3.zero) ? forward : jumpDir.normalized;

            jumpPoint.startPosition = _character.CharacterTransform.position;
            jumpPoint.jumpDirection = jumpDir;
            jumpPoint.landableTargetLayer = _parkourData.JumpLayer;

            var defaultVelocity = jumpDir * maxJumpDistance + Vector3.up * Mathf.Sqrt(-2 * -_character.GravityMagnitude * jumpHeight);
            var startPos = _character.CharacterTransform.position;
            var lastPos = startPos;
            var newPos = startPos;

            var landableTargets = ListPool<LandableTarget>.Get();

            var hit = new RaycastHit();
            var hitVarying = new RaycastHit();

            var hasHeightVariation = false;
            var lastHitPoint = _character.CharacterTransform.position;
            jumpPoint.pointBeforeledge = _character.CharacterTransform.position;
            jumpPoint.isClimbable = false;

#if UNITY_EDITOR
            _debugFindJumpPoints.Clear();
            _debugFindJumpPoints.Add(startPos);
            _debugFindJumpPoints.Add(startPos + new Vector3(defaultVelocity.x, 0f, defaultVelocity.z));
#endif

            for (var time = 0.05f; time < 2f; time += (0.4f / Mathf.Abs(maxJumpDistance)))
            {
                newPos = startPos + defaultVelocity * time;
                newPos.y = startPos.y + defaultVelocity.y * time + 0.5f * -_character.GravityMagnitude * time * time;

#if UNITY_EDITOR
                _debugFindJumpPoints.Add(newPos);
                _debugFindJumpPoints.Add(lastPos);
#endif

                var disp = newPos - lastPos;
                var dispLen = disp.magnitude + 0.2f;
                var dispDir = disp.normalized;

                var parabolaVaryingBoxHalf = PARABOLA_BOX_CAST_HALF;
                var downwardVaryingBoxHalf = DOWNWARD_BOX_CAST_HALF;
                parabolaVaryingBoxHalf.x = Mathf.Lerp(BOX_CAST_RANGE.x, BOX_CAST_RANGE.y, time);
                downwardVaryingBoxHalf.x = Mathf.Lerp(BOX_CAST_RANGE.x, BOX_CAST_RANGE.y, time);

                var hitFound = false;
                var ledgeBoxCast = parabolaVaryingBoxHalf;
                var origin = lastPos - dispDir * 0.01f;
                hitFound = Physics.SphereCast(origin, 0.1f, dispDir,
                    out hitVarying, dispLen, _parkourData.JumpLayer);

                float yOffset;
                if (checkClimb)
                {
                    yOffset = Mathf.Lerp(1.5f + disp.y, 0.7f, time * 4f);
                    ledgeBoxCast.y = Mathf.Lerp(1f + disp.y, 0.7f, time * 4f);
                }
                else
                {
                    yOffset = Mathf.Lerp(disp.y, 0.7f, time * 4f);
                    ledgeBoxCast.y = Mathf.Lerp(0, 0.7f, time * 4f);
                }

                if (!jumpPoint.isClimbable)
                {
                    var ledgeHitFound = Physics.BoxCast(origin + Vector3.up * yOffset, ledgeBoxCast, dispDir,
                        out var ledgeHit, Quaternion.LookRotation(jumpDir), dispLen + 0.2f, _parkourData.LedgeLayer);
                    if (ledgeHitFound)
                    {
                        jumpPoint.climbHitData = ledgeHit;
                        jumpPoint.isClimbable = true;
                    }
                }

                origin = newPos;

                var dispVec = (newPos - startPos).normalized;
                var distToNewPos = dispVec.magnitude * Mathf.Cos(Vector3.Angle(dispVec, jumpDir) * Mathf.Deg2Rad);

                var landableTarget = new LandableTarget
                {
                    pointBeforeledge = _character.CharacterTransform.position
                };

                if (hitFound)
                {
                    landableTarget.SetHit(hitVarying);
                    landableTarget.position = hitVarying.point;
                    landableTarget.ParabolaDistance = distToNewPos;
                    landableTargets.Add(landableTarget);
                    break;
                }

                var centerHitFound = Physics.BoxCast(origin + Vector3.up * DOWNWARD_BOX_CAST_HALF.y, DOWNWARD_BOX_CAST_HALF,
                    Vector3.down, out hit, Quaternion.LookRotation(jumpDir), 5f, _parkourData.JumpLayer);
                hitFound = Physics.BoxCast(origin + Vector3.up * downwardVaryingBoxHalf.y, downwardVaryingBoxHalf,
                    Vector3.down, out hitVarying, Quaternion.LookRotation(jumpDir), 5f, _parkourData.JumpLayer);

                if (!centerHitFound || (hitFound && hitVarying.point.y > hit.point.y &&
                                        Mathf.Abs(hitVarying.point.y - hit.point.y) > 0.2f))
                    hit = hitVarying;

                if (!hasHeightVariation)
                {
                    if (hit.point != Vector3.zero && Mathf.Abs(hit.point.y - lastHitPoint.y) <= 0.2f)
                    {
                        landableTarget.pointBeforeledge = hit.point;
                        lastHitPoint = hit.point;
                    }
                    else
                    {
                        if (hit.point == Vector3.zero || _character.CharacterTransform.position.y - hit.point.y > _parkourData.LedgeHeightThreshold * 2)
                        {
                            landableTarget.hasLedge = true;
                        }

                        hasHeightVariation = true;
                    }
                }

                if (hitFound)
                {
                    if (distToNewPos > minJumpDistance && hasHeightVariation && Vector3.Angle(hit.normal, Vector3.up) <= 45f)
                    {
                        landableTarget.SetHit(hit);
                        landableTarget.position = hit.point;
                        landableTarget.ParabolaDistance = distToNewPos;
                        landableTargets.Add(landableTarget);
                    }
                }

                lastPos = newPos;
            }

            if (findBaseGround && Physics.Raycast(startPos + jumpDir * maxJumpDistance,
                    _character.Gravity, out hit, float.MaxValue, _parkourData.JumpLayer))
            {
                var baseGround = new LandableTarget() { position = hit.point };
                baseGround.SetHit(hit);
                baseGround.isBaseGround = true;
                landableTargets.Add(baseGround);
            }

            List<LandableTarget> resultLandableTargets = null;
            if (landableTargets.Count > 0)
            {
                resultLandableTargets = ListPool<LandableTarget>.Get();
                var prevPoint = _character.CharacterTransform.position;

                using (ListPool<List<LandableTarget>>.Get(out var priorityGroup))
                {
                    var group = ListPool<LandableTarget>.Get();
                    foreach (var p in landableTargets)
                    {
                        var diff = p.position.y - prevPoint.y;
                        if (Mathf.Abs(diff) > 0.2f)
                            group = ListPool<LandableTarget>.Get();
                        if (!_parkourData.JumpToTheClosestLedge)
                            group.Insert(0, p);
                        else
                            group.Add(p);
                        if (group.Count > 0 && !priorityGroup.Contains(group))
                            priorityGroup.Add(group);
                        prevPoint = p.position;
                    }

                    var sortedGroup = priorityGroup.OrderByDescending(p => p[0].position.y).ToList();
                    foreach (var item in sortedGroup)
                    {
                        while (item.Count > 0)
                        {
                            var targetPoint = _parkourData.JumpToTheClosestLedge
                                ? item[Mathf.Clamp(0, 0, item.Count - 1)]
                                : item[Mathf.Clamp(item.Count / 2, 0, 3)];
                            var disPos = targetPoint.position - _character.CharacterTransform.position;
                            var distance = disPos.magnitude;
                            var disY = disPos.y;
                            var disXZ = disPos;
                            disXZ.y = 0;
                            var distanceXZ = disXZ.magnitude;

                            var upOffset = Vector3.up * _parkourData.JumpHeightOffset;
                            var height = GetJumpHeight(disY, disXZ);
                            var heightestPoint = _character.CharacterTransform.position + disXZ / 2 + Vector3.up * height + upOffset;

                            if ((!((_character.CharacterTransform.position.y - targetPoint.position.y) > 0f) || !(distanceXZ < minJumpDistance)) &&
                                !Physics.Linecast(_character.CharacterTransform.position + upOffset, heightestPoint, _parkourData.ObstacleLayer) &&
                                !Physics.Linecast(heightestPoint, targetPoint.position + upOffset + disXZ.normalized * 0.3f, _parkourData.ObstacleLayer) &&
                                !Physics.CheckSphere(targetPoint.position + Vector3.up * 0.5f, 0.2f, _parkourData.ObstacleLayer))
                            {
                                targetPoint.DistanceFromStart = distance;
                                targetPoint.HorizontalDistanceFromStart = distanceXZ;
                                targetPoint.HeightFromStart = disY;
                                targetPoint.JumpHeight = height;
                                resultLandableTargets.Add(targetPoint);
                            }

                            item.RemoveRange(0, Mathf.Min(item.Count, 2));
                        }
                    }

                    priorityGroup.ForEach(ListPool<LandableTarget>.Release);
                }
            }

            if (!resultLandableTargets.IsNullOrEmpty())
                jumpPoint.landableTargets = resultLandableTargets.ToArray();

#if UNITY_EDITOR
            if (!landableTargets.IsNullOrEmpty())
                _debugLastDetectedJumpPoints = landableTargets.GetRange(0, landableTargets.Count).Select(x => x.position).ToList();
#endif

            ListPool<LandableTarget>.Release(landableTargets);
            if (resultLandableTargets != null)
                ListPool<LandableTarget>.Release(resultLandableTargets);

            return jumpPoint;
        }

        public static float GetJumpHeight(float displacementY, Vector3 displacementXZ)
        {
            var h = Mathf.Max(displacementY, 0.07f);
            h += (displacementXZ.magnitude * 0.08f);
            return h;
        }

        private void DrawBounds(Bounds b, float delay = 0)
        {
            // bottom
            var p1 = new Vector3(b.min.x, b.min.y, b.min.z);
            var p2 = new Vector3(b.max.x, b.min.y, b.min.z);
            var p3 = new Vector3(b.max.x, b.min.y, b.max.z);
            var p4 = new Vector3(b.min.x, b.min.y, b.max.z);

            Debug.DrawLine(p1, p2, Color.blue, delay);
            Debug.DrawLine(p2, p3, Color.red, delay);
            Debug.DrawLine(p3, p4, Color.yellow, delay);
            Debug.DrawLine(p4, p1, Color.magenta, delay);

            // top
            var p5 = new Vector3(b.min.x, b.max.y, b.min.z);
            var p6 = new Vector3(b.max.x, b.max.y, b.min.z);
            var p7 = new Vector3(b.max.x, b.max.y, b.max.z);
            var p8 = new Vector3(b.min.x, b.max.y, b.max.z);

            Debug.DrawLine(p5, p6, Color.blue, delay);
            Debug.DrawLine(p6, p7, Color.red, delay);
            Debug.DrawLine(p7, p8, Color.yellow, delay);
            Debug.DrawLine(p8, p5, Color.magenta, delay);

            // sides
            Debug.DrawLine(p1, p5, Color.white, delay);
            Debug.DrawLine(p2, p6, Color.gray, delay);
            Debug.DrawLine(p3, p7, Color.green, delay);
            Debug.DrawLine(p4, p8, Color.cyan, delay);
        }

        public void DrawAxis(Vector3 pos, float r, Color color)
        {
            Debug.DrawLine(pos - new Vector3(r, 0, 0), pos + new Vector3(r, 0, 0), color);
            Debug.DrawLine(pos - new Vector3(0, r, 0), pos + new Vector3(0, r, 0), color);
            Debug.DrawLine(pos - new Vector3(0, 0, r), pos + new Vector3(0, 0, r), color);
        }
    }

#region Parkour Data
    public struct ObstacleHitData
    {
        public bool forwardHitFound;
        public bool heightHitFound;
        public bool ledgeHitFound;
        public bool hasSpaceToVault;
        public bool hasSpace;
        public RaycastHit heightHit;
        public RaycastHit ledgeHit;
        public RaycastHit forwardHit { get; private set; }
        public float hitWidth { get; private set; }
        public float hitDepth { get; private set; }
        public float hitHeight { get; private set; }
        public float hitDistance => forwardHit.distance;

        private LayerMask hitLayer;
        private TagComponent tagComponent;

        private bool IsGroundLayer => hitLayer == Layer.LAYER_GROUND;

        public void SetForwardHit(RaycastHit hit)
        {
            forwardHit = hit;
            if (forwardHit.transform)
            {
                hitLayer = forwardHit.transform.gameObject.layer;
                tagComponent = forwardHit.transform.GetComponent<TagComponent>();
            }
        }

        public bool CompareTag(string tag)
        {
            return CompareGroundTag(tag) || (tagComponent?.HasAny(tag) ??
                   (forwardHitFound && forwardHit.transform.CompareTag(TagHandle.GetExistingTag(tag))));
        }

        public bool CompareTag(TagList tagList)
        {
            if (tagList == null)
                return false;

            if (tagComponent)
                return tagComponent.HasAny(tagList.Tags);

            return forwardHitFound && (CompareGroundTag(tagList.Tags) || tagList.Contains(forwardHit.transform));
        }

        private bool CompareGroundTag(string tag)
        {
            return IsGroundLayer && ReIWTags.Ground_Name.Equals(tag);
        }

        private bool CompareGroundTag(string[] tags)
        {
            return IsGroundLayer && !tags.IsNullOrEmpty() && tags.Contains(ReIWTags.Ground_Name);
        }

        public void SetHitInfo(Transform parkourer)
        {
            if (!forwardHitFound || !heightHitFound)
                return;

            PhysicsUtility.GetHitBounds(heightHit, parkourer, BoundsFrameMode.Surface,
                out var width, out _, out var depth);
            hitWidth = width;
            hitDepth = depth;
            hitHeight = heightHit.point.y - parkourer.position.y;
        }
    }

    public struct LedgeData
    {
        public float height;
        public float angle;
        public float distance;
        public RaycastHit surfaceHit;
        public bool surfaceHitFound;
    }

    public struct ClimbLedgeData
    {
        public RaycastHit ledgeHit;
    }

    public struct JumpData
    {
        public Vector3 startPosition;
        public Vector3 jumpDirection;

        public bool isClimbable;
        public RaycastHit climbHitData;
        public Vector3 pointBeforeledge;

        public LayerMask landableTargetLayer;
        public LandableTarget[] landableTargets;

        private Vector3 targetPosition;
        private Transform ground;
        private LandableTarget? landTarget;
        private TagComponent groundTagComponent;
        private bool isFall;

        public bool IsValid => !landableTargets.IsNullOrEmpty();
        public bool LandTargetFound => landTarget != null;
        public bool HasLedge => LandTargetFound && landTarget is { hasLedge: true };
        public bool IsGroundLayer => ground && ground.gameObject.layer == Layer.LAYER_GROUND;
        public bool IsFall => isFall;
        public LandableTarget LandTarget => landTarget.Value;
        public Vector3 TargetPosition => targetPosition;
        public float JumpHeight => LandTargetFound ? landTarget.Value.JumpHeight : 0f;

        public void SetGround(Transform ground)
        {
            this.ground = ground;
            if (ground != null)
                groundTagComponent = ground.GetComponent<TagComponent>();
        }

        public void SetFallState(Vector3 targetPos, float jumpHeight)
        {
            if (jumpHeight > 0f && startPosition.y > targetPos.y && startPosition.y - targetPos.y > jumpHeight)
                isFall = true;
        }

        public void SetTargetPoint(LandableTarget target, Vector3 up, float jumpHeight)
        {
            var targetPos = target.position;
            if (isFall)
                targetPos.y = startPosition.y - jumpHeight;

            var down = -up;
            var padding = 0.2f;
            var yOffsetPad = up * 0.2f;

            var jumpDirRight = Vector3.Cross(down, jumpDirection).normalized;
            Vector3 offset = Vector3.zero;

            var dir = jumpDirection.normalized;
            var spaceFrontFound = Physics.Raycast(targetPos + dir * padding + yOffsetPad, down,
                out RaycastHit spaceFrontHit, 0.3f, landableTargetLayer);
            var spaceBackFound = Physics.Raycast(targetPos - dir * padding + yOffsetPad, down,
                out RaycastHit spaceBackHit, 0.3f, landableTargetLayer);
            var spaceRightFound = Physics.Raycast(targetPos + jumpDirRight * padding + yOffsetPad, down,
                out RaycastHit spaceRightHit, 0.3f, landableTargetLayer);
            var spaceLeftFound = Physics.Raycast(targetPos - jumpDirRight * padding + yOffsetPad, down,
                out RaycastHit spaceLeftHit, 0.3f, landableTargetLayer);

            padding = 0.15f;

            if (spaceFrontFound) offset += dir * padding;
            if (spaceBackFound) offset += dir * -padding;
            if (!Physics.Linecast(targetPos + yOffsetPad, targetPos + jumpDirRight * padding + yOffsetPad, landableTargetLayer) && spaceRightFound)
                offset += jumpDirRight * padding;
            if (!Physics.Linecast(targetPos + yOffsetPad, targetPos - jumpDirRight * padding + yOffsetPad, landableTargetLayer) && spaceLeftFound)
                offset += jumpDirRight * -padding;

            offset.y = 0;
            targetPos += offset;

            pointBeforeledge = target.pointBeforeledge;
            targetPosition = targetPos;
            landTarget = target;
        }

        public bool CompareGroundTag(string tag)
        {
            return CompareGroundLayerTag(tag) || (groundTagComponent?.HasAny(tag) ?? (ground && ground.CompareTag(TagHandle.GetExistingTag(tag))));
        }

        public bool CompareGroundTag(TagList tagList)
        {
            if (tagList == null)
                return false;

            if (groundTagComponent)
                return groundTagComponent.HasAny(tagList.Tags);

            return CompareGroundLayerTag(tagList.Tags) || (ground && tagList.Contains(ground));
        }

        private bool CompareGroundLayerTag(string tag)
        {
            return IsGroundLayer && ReIWTags.Ground_Name.Equals(tag);
        }

        private bool CompareGroundLayerTag(string[] tags)
        {
            return IsGroundLayer && !tags.IsNullOrEmpty() && tags.Contains(ReIWTags.Ground_Name);
        }
    }

    public struct LandableTarget
    {
        public RaycastHit hit;
        public Vector3 position;
        private float parabolaDistance;
        private float distanceFromStart;
        private float horizontalDistanceFromStart;
        private float heightFromStart;
        private float jumpHeight;
        public bool hasLedge;
        public bool isBaseGround;
        public Vector3 pointBeforeledge;

        private TagComponent tagComponent;

        private bool IsGroundLayer => hit.transform && hit.transform.gameObject.layer == Layer.LAYER_GROUND;

        public float ParabolaDistance
        {
            get => parabolaDistance;
            set => parabolaDistance = FloorDecimals(value);
        }
        public float DistanceFromStart
        {
            get => distanceFromStart;
            set => distanceFromStart = FloorDecimals(value);
        }
        public float HorizontalDistanceFromStart
        {
            get => horizontalDistanceFromStart;
            set => horizontalDistanceFromStart = FloorDecimals(value);
        }
        public float HeightFromStart
        {
            get => heightFromStart;
            set => heightFromStart = FloorDecimals(value);
        }
        public float JumpHeight
        {
            get => jumpHeight;
            set => jumpHeight = FloorDecimals(value);
        }

        public void SetHit(RaycastHit hit)
        {
            this.hit = hit;

            if (hit.transform)
                this.tagComponent = hit.transform.GetComponent<TagComponent>();
        }

        public bool CompareTag(string tag)
        {
            return CompareGroundTag(tag) || (tagComponent?.HasAny(tag) ??
                   (hit.transform && hit.transform.CompareTag(TagHandle.GetExistingTag(tag))));
        }

        public bool CompareTag(TagList tagList)
        {
            if (tagList == null)
                return false;

            if (tagComponent)
                return tagComponent.HasAny(tagList.Tags);

            if (!hit.transform)
                return false;

            return CompareGroundTag(tagList.Tags) || tagList.Contains(hit.transform);
        }

        private bool CompareGroundTag(string tag)
        {
            return IsGroundLayer && ReIWTags.Ground_Name.Equals(tag);
        }

        private bool CompareGroundTag(string[] tags)
        {
            return IsGroundLayer && !tags.IsNullOrEmpty() && tags.Contains(ReIWTags.Ground_Name);
        }

        private float FloorDecimals(float value)
        {
            return MathUtility.FloorDecimals(value, EnvironmentScannerComponent.FLOOR_DECIMALS);
        }
    }
#endregion
}
