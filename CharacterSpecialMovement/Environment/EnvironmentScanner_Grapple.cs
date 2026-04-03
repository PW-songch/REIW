using System.Collections.Generic;
using UnityEngine;

namespace REIW
{
    public partial class EnvironmentScannerComponent
    {
        // private readonly Collider[] _detectGrapplePoints = new Collider[Application.isEditor ? 50 : 30];

#if UNITY_EDITOR
        private Transform _debugUndetectedGrapplePoint;
#endif

        public GrapplePoint DetectGrapplePoint(Camera InCamera, Rect InDetectionScreenRect, CharacterMoveGrappleData InMovementData)
        {
            var candidates = new List<GrapplePoint>();
            var characterTransform = _character.CharacterTransform;
            var characterPos = characterTransform.position;
            var camPos = InCamera.transform.position;
            var camToChar = characterPos - camPos;
            var distCamChar = camToChar.magnitude;
            camToChar.Normalize();
            var camAngle = Vector3.Angle(characterTransform.up, InCamera.transform.forward);

            // for (int i = 0; i < _detectGrapplePoints.Length; ++i)
            //     _detectGrapplePoints[i] = null;

            // 구 형태로 1차적 포인트 검사
            // var hitCount = Physics.OverlapSphereNonAlloc(characterPos, InMovementData.detectionDistance, _detectGrapplePoints, InMovementData.grapplePointLayer);
            
            foreach (var grapplePoint in GrapplePoint._sLoadedGrapplePoints)
            {
                var grapplePointPos = grapplePoint.transform.position;
                Vector3 screenPos = InCamera.WorldToScreenPoint(grapplePointPos);

                // Check object's screen position
                var inScreenRange = screenPos.z > 0 &&
                              screenPos.x >= InDetectionScreenRect.xMin && screenPos.x <= InDetectionScreenRect.xMax &&
                              screenPos.y >= InDetectionScreenRect.yMin && screenPos.y <= InDetectionScreenRect.yMax;
                if(!inScreenRange)
                    continue;

                var camToTarget = grapplePointPos - camPos;
                var distCamTarget = camToTarget.magnitude;
                // 타겟이 카메라 뒤쪽에 있는지 체크
                if (Vector3.Dot(camToChar, camToTarget.normalized) <= 0f) // 각도 허용 범위 (cos 8~10도 정도)
                    continue;
                // 거리상으로 카메라와 캐릭터 사이에 있는지
                if (distCamTarget < distCamChar)
                    continue;

                // Check player vertical angle
                var dirToPoint = (grapplePointPos - characterPos).normalized;
                var angle = Vector3.Angle(characterTransform.up, dirToPoint);
                if (angle < InMovementData.detectionCharacterVerticalAngle)
                    continue;

                // Check distance
                var distance = Vector3.Distance(characterPos, grapplePointPos);
                if (distance > InMovementData.maxGrappleDistance)
                    continue;
                if (distance < InMovementData.minGrappleDistance)
                    continue;

                distance -= _character.Radius;

                // Check obstacle
                var origin = characterPos + characterTransform.up * _character.Radius;
                var direction = (grapplePointPos - origin).normalized;
                if (Physics.Raycast(origin + direction * _character.Radius, direction, out var hit,
                        distance, InMovementData.grapplePointLayer | InMovementData.obstacleLayer) &&
                    ((1 << hit.transform.gameObject.layer) & InMovementData.obstacleLayer) != 0)
                {
                    if (CheckValidObstacle(grapplePointPos, hit, InMovementData))
                    {
#if UNITY_EDITOR
                        _debugUndetectedGrapplePoint = grapplePoint.transform;
#endif
                        continue;
                    }
                }

                origin = characterPos + characterTransform.up * (_character.Height - _character.Radius);
                direction = (grapplePointPos - origin).normalized;
                if (camAngle > InMovementData.detectionSphereCastCamAngle)
                {
                    if (Physics.SphereCast(origin + direction * _character.Radius,
                            _character.Radius, direction, out hit, distance,
                            InMovementData.grapplePointLayer | InMovementData.obstacleLayer) &&
                        ((1 << hit.transform.gameObject.layer) & InMovementData.obstacleLayer) != 0)
                    {
                        if (CheckValidObstacle(grapplePointPos, hit, InMovementData))
                        {
#if UNITY_EDITOR
                            _debugUndetectedGrapplePoint = grapplePoint.transform;
#endif
                            continue;
                        }
                    }
                }
                else
                {
                    if (Physics.Raycast(origin + direction * _character.Radius, direction, out hit,
                            distance, InMovementData.grapplePointLayer | InMovementData.obstacleLayer) &&
                        ((1 << hit.transform.gameObject.layer) & InMovementData.obstacleLayer) != 0)
                    {
                        if (CheckValidObstacle(grapplePointPos, hit, InMovementData))
                        {
#if UNITY_EDITOR
                            _debugUndetectedGrapplePoint = grapplePoint.transform;
#endif
                            continue;
                        }
                    }
                }

                candidates.Add(grapplePoint);
            }

            // select nearest point from screen's center
            GrapplePoint nearestPoint = null;
            var minScreenDistance = float.MaxValue;
            var screenCenter = new Vector2(Screen.width * 0.5f, Screen.height * 0.5f);
            foreach (var point in candidates)
            {
                var screenPos = InCamera.WorldToScreenPoint(point.transform.position);
                if (screenPos.z < 0)
                    continue;

                var screenPos2D = new Vector2(screenPos.x, screenPos.y);
                var screenDistance = (screenCenter - screenPos2D).sqrMagnitude;
                if (screenDistance < minScreenDistance)
                {
                    // var grapplePoint = point.GetComponent<GrapplePoint>();
                    var grapplePoint = point;
                    if (grapplePoint?.IsEnable(characterTransform) == true)
                    {
                        minScreenDistance = screenDistance;
                        nearestPoint = point;
                    }
                }
            }

#if UNITY_EDITOR
            if (nearestPoint)
                _debugUndetectedGrapplePoint = null;
#endif

            return nearestPoint;
        }

        private bool CheckValidObstacle(Vector3 targetPos, RaycastHit hit, CharacterMoveGrappleData InMovementData)
        {
            var checkDistance = InMovementData.detectionValidObstacleDistance;
            if (hit.distance < checkDistance || Vector3.Distance(hit.point, targetPos) < checkDistance)
                return true;

            PhysicsUtility.GetHitBounds(hit, _character.CharacterTransform,
                BoundsFrameMode.Viewer, out var width, out var height, out var depth);
            return depth > InMovementData.detectionValidObstacleDepth ||
                   (width > InMovementData.detectionValidObstacleSize &&
                    height > InMovementData.detectionValidObstacleSize);
        }

        public void ResetDebugGrapplePoint()
        {
#if UNITY_EDITOR
            _debugUndetectedGrapplePoint = null;
#endif
        }
    }
}
