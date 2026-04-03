using UnityEngine;

namespace REIW
{
	
    [CreateAssetMenu(fileName = "CharacterMoveGrappleData", menuName = "ScriptableObject/CharacterMoveGrappleData")]
    public class CharacterMoveGrappleData : ScriptableObject
    {
        [Header("Grapple Detection Settings")]
        [SerializeField] public float detectionDistance = 42f;                 // 포인트 구형 검색 최대 범위
        [SerializeField] public float detectionHorizontalPx = 300f;            // 검색 조건, 화면 중앙에서 수평방향 검색 범위(1920x1080 기준 픽셀값)
        [SerializeField] public float detectionBotPx = -100f;                  // 검색 조건, 화면 중앙에서 수직방향 검색 하단값(1920x1080 기준 픽셀값)
        [SerializeField] public float detectionValidObstacleDistance = 10f;    // 검색 조건, 유효한 장애물 거리
        [SerializeField] public float detectionValidObstacleSize = 2f;         // 검색 조건, 유효한 장애물 크기
        [SerializeField] public float detectionValidObstacleDepth = 5f;        // 검색 조건, 유효한 장애물 깊이 크기
        [SerializeField] public float detectionSphereCastCamAngle = 80f;       // 검색 조건, SphereCast를 실행하는 카메라 기준 앵글
        [SerializeField] public float detectionCharacterVerticalAngle = 20f;   // 시행 조건, 캐릭터 윗 방향과 포인트 간 각도 최소치
        [SerializeField] public float minGrappleDistance = 2f;                 // 시행 조건, 캐릭터와 최소 거리
        [SerializeField] public float maxGrappleDistance = 42f;                // 시행 조건, 캐릭터와 최대 거리
        [SerializeField] public LayerMask grapplePointLayer;
        [SerializeField] public LayerMask obstacleLayer;

        [Header("Grapple Movement Settings")]
        [SerializeField] public float nearDistanceThreshold = 15f;              // 근/원거리 판정값
        [SerializeField] public float nearMaxSpeed = 12f;                       // 근거리 그래플 최대 속도
        [SerializeField] public float farMaxSpeed = 20f;                        // 원거리 그래플 최대 속도
        [SerializeField] public float arrivalDistance = 0.5f;                   // 도착 판정 기준 거리
        [SerializeField] public float timeToMaxSpeed = 1f;                      // 최대 속도까지 가속하는 시간
        [SerializeField] public float rotationSpeed = 10f;                      // 그래플 시작 후 포인트로 회전하는 속도
        [SerializeField] public float obstacleAvoidCheckDistance = 20f;         // 그래플 이동시 장애물 회피 체크 거리
        [SerializeField] public float obstacleAvoidExtraClearance = 2f;         // 그래플 이동시 장애물 회피 거리
        [SerializeField] public float obstacleAvoidSteerWeight = 1f;            // 그래플 이동시 장애물 회피 가중치
        [SerializeField] public float obstacleAvoidStrength = 100f;             // 그래플 이동시 장애물 회피 세기
        [SerializeField] public int moveTimeCalculateSamples = 100;             // 그래플 이동 시간 계산 샘플수
        [SerializeField] public AnimationCurve speedCurve = AnimationCurve.EaseInOut(0, 0, 1, 1);  // 그래플 속도 그래프

        [Header("Launch Settings")]
        [SerializeField] public float launchInputWaiting = .2f;                // 그래플 완료 후 런치 입력 대기
        [SerializeField] public float launchLandingCheckDelay = .1f;           // 런치 후 착지 체크 딜레이 시간
        [SerializeField] public float launchHorizontalSpeed = 22f;             // 런치 수평방향 속도
        [SerializeField] public float launchVerticalSpeed = 10f;               // 런치 수직방향 속도
        [SerializeField] public float launchHorizontalTimeToMaxSpeed = 1f;     // 런치 수평방향 최대 속도까지 가속하는 시간
        [SerializeField] public float launchVerticalTimeToMaxSpeed = 1f;       // 런치 수직방향 최대 속도까지 가속하는 시간
        [SerializeField] public float launchVerticalAngle = 60f;               // 런치 수직방향 각도
        [SerializeField] public float launchHorizontalAngleLimit = 60f;        // 런치 수평방향 각도 제한
        [SerializeField] public float launchDeceleration = -10f;               // 런치 감속도
        [SerializeField] public AnimationCurve launchHorizontalSpeedCurve = AnimationCurve.EaseInOut(0, 0, 1, 1);  // 런치 수평방향 속도 그래프
        [SerializeField] public AnimationCurve launchVerticalSpeedCurve = AnimationCurve.EaseInOut(0, 0, 1, 1);    // 런치 수직방향 속도 그래프
        
        [Header("Launching")]
        [SerializeField] public float launchMoveMaxSpeed = 0.5f; // 런치 후 움직이지 못하는 시간
        [SerializeField] public float launchDecelAgainst = 0.5f; // 런치 후 움직이지 못하는 시간 이후 반대 방향일떼 최대 감속 비율 
        [SerializeField] public float launchMoveRotaionPower = 1.0f;

        [System.NonSerialized]public float launchDirTrackDegPerSec = 540; // 입력 방향을 따라가는 속도
        [System.NonSerialized]public float launchYawDegPerSec = 720; // 실제 회전 속도

        [Header("Landing")]
        [SerializeField] public float landingNormalMoveEnableDelay = 0.1f; 
        
        [Header("Debug")] 
        [SerializeField] public bool showDetectRays = false;
        [SerializeField] public bool showAngleRays = false;
        [SerializeField] public bool showLaunchRays = false;
        [SerializeField] public bool refreshDetectionScreenRegion = false;
        [SerializeField] public bool showGrapplePoint = true;
    }
}
