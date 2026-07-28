#if UNITY_6000_0_OR_NEWER
using System;
using System.Threading;
using UnityEngine;
using DG.Tweening;
using InTheArena.MainGame;
using UnityCamera = UnityEngine.Camera;

namespace InTheArena.Camera
{
    /// <summary>
    /// 카메라 컨트롤러 - 페이즈별 카메라 동작 관리
    /// Betting: 정적 전체 보기 + 그리드 포커스
    /// Combat: 생존 유닛 자동 프레이밍 + 2배속 지원
    /// Result: 결과 포커스 + 유닛 상세 포커스
    /// </summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(UnityCamera))]
    public class CameraController : MonoBehaviour
    {
        private static CameraController _instance;

        /// <summary>싱글톤 인스턴스</summary>
        public static CameraController Instance
        {
            get
            {
                if (ReferenceEquals(_instance, null) || _instance == null)
                {
                    return null;
                }
                return _instance;
            }
        }

        [Header("Settings")]
        [SerializeField] private CameraSettings m_CameraSettings;

        [Header("References")]
        [SerializeField] private UnityCamera m_MainCamera;

        [Header("State")]
        [SerializeField] private CameraPhase m_CurrentPhase = CameraPhase.Betting;

        [Header("Combat Framing")]
        [SerializeField] private float m_FramingPadding = 3f;
        [SerializeField] private float m_CombatPadding = 5f;

        [Header("Shake")]
        [SerializeField] private float m_ShakeStrength = 0.5f;
        [SerializeField] private float m_ShakeDuration = 0.3f;

        // Runtime
        private CameraPhase m_TargetPhase;
        private bool m_IsBoosted = false;
        private Vector3 m_TargetPosition;
        private float m_TargetZoom;
        private bool m_IsTransitioning = false;
        private AwaitableCompletionSource m_TransitionCompletionSource;

        // 카메라 초기 설정 저장
        private Vector3 m_InitialPosition;
        private Quaternion m_InitialRotation;
        private float m_InitialFieldOfView;
        private float m_InitialOrthoSize;
        private bool m_WasOrthographic;

        // 셰이크 관련
        private Vector3 m_ShakeOffset;
        private float m_ShakeTimer;
        private float m_ShakeIntensity;

        public CameraSettings Settings => m_CameraSettings;
        public UnityCamera MainCamera => m_MainCamera;
        public CameraPhase CurrentPhase => m_CurrentPhase;
        public bool IsBoosted => m_IsBoosted;

        private void Awake()
        {
            if (!ReferenceEquals(_instance, null) && _instance != null && _instance != this)
            {
                Debug.LogWarning("[CameraController] 중복 인스턴스 감지 - 기존 인스턴스 파괴");
                Destroy(gameObject);
                return;
            }

            _instance = this;
            InitializeCamera();
        }

        private void Start()
        {
            // 초기 설정 저장
            m_InitialPosition = transform.position;
            m_InitialRotation = transform.rotation;
            m_InitialFieldOfView = m_MainCamera.fieldOfView;
            m_InitialOrthoSize = m_MainCamera.orthographicSize;
            m_WasOrthographic = m_MainCamera.orthographic;

            // 설정 적용
            ApplySettings();

            // 기본 페이즈 설정
            SetPhase(CameraPhase.Betting);
        }

        private void LateUpdate()
        {
            if (m_ShakeTimer > 0f)
            {
                UpdateShake();
            }

            // 페이즈별 카메라 업데이트
            switch (m_CurrentPhase)
            {
                case CameraPhase.Combat:
                    UpdateCombatCamera();
                    break;
                case CameraPhase.Betting:
                    UpdateBettingCamera();
                    break;
                case CameraPhase.Result:
                    UpdateResultCamera();
                    break;
            }

            // 쉐이크 오프셋 적용
            if (m_ShakeTimer > 0f)
            {
                transform.position += m_ShakeOffset;
            }
        }

        private void InitializeCamera()
        {
            if (m_MainCamera == null)
                m_MainCamera = GetComponent<UnityCamera>();

            if (m_CameraSettings == null)
            {
                Debug.LogWarning("[CameraController] CameraSettings가 설정되지 않았습니다. 기본값 사용.");
            }
        }

        private void ApplySettings()
        {
            if (m_CameraSettings == null) return;

            var settings = m_CameraSettings;

            m_MainCamera.clearFlags = settings.ClearFlags;
            m_MainCamera.backgroundColor = settings.BackgroundColor;
            m_MainCamera.orthographic = !settings.UsePerspective;

            if (settings.UsePerspective)
            {
                m_MainCamera.fieldOfView = settings.FieldOfView;
            }
            else
            {
                m_MainCamera.orthographicSize = settings.OrthoSize;
            }

            // 초기 Transform 설정
            var (pos, rot) = settings.CalculatePerspectiveTransform(settings.CombatAreaCenter);
            transform.position = pos;
            transform.rotation = rot;
        }

        /// <summary>
        /// 카메라 페이즈 변경 (비동기)
        /// </summary>
        public async Awaitable SetPhaseAsync(CameraPhase newPhase, CancellationToken token = default)
        {
            if (m_CurrentPhase == newPhase && !m_IsTransitioning) return;

            m_TargetPhase = newPhase;
            m_IsTransitioning = true;
            m_TransitionCompletionSource = new AwaitableCompletionSource();

            // 페이즈 전환 애니메이션
            await TransitionToPhase(newPhase, token);

            m_CurrentPhase = newPhase;
            m_IsTransitioning = false;
            m_TransitionCompletionSource?.TrySetResult();
        }

        /// <summary>
        /// 페이즈 즉시 변경 (애니메이션 없음)
        /// </summary>
        public void SetPhase(CameraPhase newPhase)
        {
            m_CurrentPhase = newPhase;
            OnPhaseEnter(newPhase);
        }

        private async Awaitable TransitionToPhase(CameraPhase newPhase, CancellationToken token)
        {
            // 페이즈 종료
            OnPhaseExit(m_CurrentPhase);

            // 전환 애니메이션 (DOTween)
            float duration = 0.5f;
            switch (newPhase)
            {
                case CameraPhase.Betting:
                    await AnimateToBetting(token);
                    break;
                case CameraPhase.Combat:
                    await AnimateToCombat(token);
                    break;
                case CameraPhase.Result:
                    await AnimateToResult(token);
                    break;
            }

            OnPhaseEnter(newPhase);
        }

        private void OnPhaseEnter(CameraPhase phase)
        {
            m_CurrentPhase = phase;
            Debug.Log($"[CameraController] 페이즈 진입: {phase}");
        }

        private void OnPhaseExit(CameraPhase phase)
        {
            Debug.Log($"[CameraController] 페이즈 종료: {phase}");
        }

        private async Awaitable AnimateToBetting(CancellationToken token)
        {
            // 전체 전투 영역이 보이도록 카메라 이동
            var settings = m_CameraSettings;
            if (settings == null) return;

            var (pos, rot) = settings.CalculatePerspectiveTransform(settings.CombatAreaCenter);
            pos.y = Mathf.Max(pos.y, settings.CameraHeight);

            await MoveCameraAsync(pos, settings.CameraDistance, 0.5f, token);
        }

        private async Awaitable AnimateToCombat(CancellationToken token)
        {
            // Combat는 첫 프레임에서 자동 프레이밍이 시작됨
            await Awaitable.NextFrameAsync();
        }

        private async Awaitable AnimateToResult(CancellationToken token)
        {
            // 결과 화면 중앙으로 이동
            var settings = m_CameraSettings;
            if (settings == null) return;

            var (pos, rot) = settings.CalculatePerspectiveTransform(settings.CombatAreaCenter);
            pos.y = Mathf.Max(pos.y, settings.CameraHeight * 1.2f);

            await MoveCameraAsync(pos, settings.CameraDistance * 1.2f, 0.5f, token);
        }

        private async Awaitable MoveCameraAsync(Vector3 targetPos, float targetZoom, float duration, CancellationToken token)
        {
            Vector3 startPos = transform.position;
            Quaternion startRot = transform.rotation;
            float startZoom = m_MainCamera.orthographic ? m_MainCamera.orthographicSize : m_MainCamera.fieldOfView;

            float elapsed = 0f;
            while (elapsed < duration)
            {
                token.ThrowIfCancellationRequested();
                float t = elapsed / duration;
                t = EaseOutCubic(t);

                transform.position = Vector3.Lerp(startPos, targetPos, t);
                float currentZoom = m_MainCamera.orthographic ? m_MainCamera.orthographicSize : m_MainCamera.fieldOfView;
                float newZoom = Mathf.Lerp(startZoom, targetZoom, t);

                if (m_MainCamera.orthographic)
                    m_MainCamera.orthographicSize = newZoom;
                else
                    m_MainCamera.fieldOfView = newZoom;

                elapsed += Time.deltaTime;
                await Awaitable.NextFrameAsync();
            }

            transform.position = targetPos;
            if (m_MainCamera.orthographic)
                m_MainCamera.orthographicSize = targetZoom;
            else
                m_MainCamera.fieldOfView = targetZoom;
        }

        private static float EaseOutCubic(float t)
        {
            return 1f - Mathf.Pow(1f - t, 3f);
        }

        private void UpdateBettingCamera()
        {
            // 베팅 페이즈: 정적 카메라, 사용자 입력으로 그리드 포커스 가능
            // 현재는 정적 유지
        }

        private void UpdateCombatCamera()
        {
            if (m_CameraSettings == null) return;

            // 생존 유닛 바운딩 박스 계산
            Bounds unitBounds = CalculateUnitsBounds();
            if (unitBounds.size == Vector3.zero) return;

            // 프레이밍 계산
            var (targetPos, targetZoom) = CameraFramingCalculator.CalculateFraming(
                unitBounds, m_MainCamera, m_FramingPadding);

            // 높이 제한
            targetPos.y = Mathf.Clamp(targetPos.y,
                m_CameraSettings.MinHeight, m_CameraSettings.MaxHeight);

            m_TargetPosition = targetPos;
            m_TargetZoom = Mathf.Clamp(targetZoom,
                m_CameraSettings.MinZoom, m_CameraSettings.MaxZoom);

            // 카메라 Transform 적용
            ApplyCameraTransform();
        }

        private void UpdateResultCamera()
        {
            // 결과 페이즈: 중앙 고정, 필요시 유닛 상세 포커스
        }

        private Bounds CalculateUnitsBounds()
        {
            var context = RoundManager.Instance?.Context;
            if (context == null) return new Bounds();

            bool hasUnits = false;
            Bounds bounds = new Bounds();

            // Team A 생존 유닛
            foreach (var unit in context.TeamAUnits)
            {
                if (unit != null && !unit.IsDead)
                {
                    if (!hasUnits)
                    {
                        bounds = unit.GetComponent<Collider>()?.bounds ??
                                 new Bounds(unit.transform.position, Vector3.one);
                        hasUnits = true;
                    }
                    else
                    {
                        var collider = unit.GetComponent<Collider>();
                        if (collider != null)
                            bounds.Encapsulate(collider.bounds);
                        else
                            bounds.Encapsulate(unit.transform.position);
                    }
                }
            }

            // Team B 생존 유닛
            foreach (var unit in context.TeamBUnits)
            {
                if (unit != null && !unit.IsDead)
                {
                    if (!hasUnits)
                    {
                        bounds = unit.GetComponent<Collider>()?.bounds ??
                                 new Bounds(unit.transform.position, Vector3.one);
                        hasUnits = true;
                    }
                    else
                    {
                        var collider = unit.GetComponent<Collider>();
                        if (collider != null)
                            bounds.Encapsulate(collider.bounds);
                        else
                            bounds.Encapsulate(unit.transform.position);
                    }
                }
            }

            // 패딩 적용
            if (hasUnits)
            {
                bounds.Expand(m_CombatPadding * 2f);
            }

            return bounds;
        }

        private void ApplyCameraTransform()
        {
            if (m_CameraSettings == null) return;

            float followSpeed = m_IsBoosted ? m_CameraSettings.BoostFollowSpeed : m_CameraSettings.FollowLerpSpeed;
            float zoomSpeed = m_IsBoosted ? m_CameraSettings.BoostZoomSpeed : m_CameraSettings.ZoomLerpSpeed;

            // 위치 Lerp (데드존 적용)
            Vector3 currentPos = transform.position;
            Vector3 targetPos = new Vector3(m_TargetPosition.x, currentPos.y, m_TargetPosition.z);

            float distance = Vector3.Distance(currentPos, targetPos);
            if (distance > m_CameraSettings.DeadZoneRadius)
            {
                float lerpFactor = 1f - Mathf.Exp(-followSpeed * Time.unscaledDeltaTime);
                transform.position = Vector3.Lerp(currentPos, targetPos, lerpFactor);
            }

            // 줌 Lerp
            if (m_MainCamera.orthographic)
            {
                m_MainCamera.orthographicSize = Mathf.Lerp(
                    m_MainCamera.orthographicSize, m_TargetZoom,
                    1f - Mathf.Exp(-zoomSpeed * Time.unscaledDeltaTime));
            }
            else
            {
                float currentDist = Vector3.Distance(transform.position, m_TargetPosition);
                float newDist = Mathf.Lerp(currentDist, m_TargetZoom,
                    1f - Mathf.Exp(-zoomSpeed * Time.unscaledDeltaTime));

                Vector3 dir = (transform.position - m_TargetPosition).normalized;
                transform.position = m_TargetPosition + dir * newDist;
            }
        }

        private void UpdateShake()
        {
            m_ShakeTimer -= Time.unscaledDeltaTime;
            float progress = 1f - m_ShakeTimer / m_ShakeDuration;
            float intensity = m_ShakeIntensity * (1f - progress);

            m_ShakeOffset = new Vector3(
                UnityEngine.Random.Range(-1f, 1f) * intensity,
                UnityEngine.Random.Range(-1f, 1f) * intensity * 0.5f,
                UnityEngine.Random.Range(-1f, 1f) * intensity * 0.3f
            );
        }

        /// <summary>
        /// 2배속 토글 (CombatPhase에서 호출)
        /// </summary>
        public void SetSpeedBoost(bool boosted)
        {
            m_IsBoosted = boosted;
        }

        /// <summary>
        /// 카메라 쉐이크 트리거
        /// </summary>
        public void ShakeCamera(float intensity = -1f, float duration = -1f)
        {
            m_ShakeIntensity = intensity > 0f ? intensity : (m_CameraSettings?.DefaultShakeIntensity ?? m_ShakeStrength);
            m_ShakeDuration = duration > 0f ? duration : (m_CameraSettings?.DefaultShakeDuration ?? m_ShakeDuration);
            m_ShakeTimer = m_ShakeDuration;
        }

        /// <summary>
        /// 특정 위치로 카메라 강제 이동 (베팅 그리드 포커스 등)
        /// </summary>
        public async Awaitable FocusOnPositionAsync(Vector3 position, float zoom = -1f, float duration = 0.5f, CancellationToken token = default)
        {
            float targetZoom = zoom > 0f ? zoom : m_TargetZoom;
            await MoveCameraAsync(position, targetZoom, duration, token);
        }

        /// <summary>
        /// 초기 카메라 위치로 복원
        /// </summary>
        public async Awaitable ResetToDefaultAsync(float duration = 1f, CancellationToken token = default)
        {
            await MoveCameraAsync(m_InitialPosition, m_InitialFieldOfView, duration, token);
            transform.rotation = m_InitialRotation;
            m_MainCamera.fieldOfView = m_InitialFieldOfView;
            m_MainCamera.orthographicSize = m_InitialOrthoSize;
            m_MainCamera.orthographic = m_WasOrthographic;
        }

        /// <summary>
        /// Perspective <-> Orthographic 전환
        /// </summary>
        public void SetProjectionMode(bool usePerspective)
        {
            if (m_CameraSettings == null) return;

            m_CameraSettings.UsePerspective = usePerspective;
            m_MainCamera.orthographic = !usePerspective;

            if (usePerspective)
            {
                m_MainCamera.fieldOfView = m_CameraSettings.FieldOfView;
            }
            else
            {
                m_MainCamera.orthographicSize = m_CameraSettings.OrthoSize;
            }
        }

        private void OnDrawGizmos()
        {
            if (m_CameraSettings == null) return;

            // 전투 영역 시각화
            Gizmos.color = new Color(0f, 1f, 0f, 0.2f);
            Gizmos.DrawWireCube(m_CameraSettings.CombatAreaCenter, m_CameraSettings.CombatAreaSize);

            // 카메라 위치/시야 표시
            Gizmos.color = Color.yellow;
            Gizmos.DrawWireSphere(transform.position, 0.5f);
        }
    }

    /// <summary>
    /// 카메라 페이즈 열거형
    /// </summary>
    public enum CameraPhase
    {
        Betting = 0,
        Combat = 1,
        Result = 2
    }

    /// <summary>
    /// 카메라 프레이밍 계산 유틸리티
    /// </summary>
    public static class CameraFramingCalculator
    {
        /// <summary>
        /// 주어진 유닛 바운딩을 화면에 꽉 채우는 카메라 파라미터 계산
        /// </summary>
        public static (Vector3 position, float zoom) CalculateFraming(
            Bounds unitBounds, UnityCamera camera, float padding = 3f)
        {
            Vector3 size = unitBounds.size + new Vector3(padding * 2f, 0f, padding * 2f);
            Vector3 center = unitBounds.center;
            center.y = 0f;

            float fovRad = camera.fieldOfView * Mathf.Deg2Rad;
            float aspect = camera.aspect;

            float distX = size.x / (2f * Mathf.Tan(fovRad * 0.5f) * aspect);
            float distZ = size.z / (2f * Mathf.Tan(fovRad * 0.5f));
            float distance = Mathf.Max(distX, distZ);

            Quaternion rot = Quaternion.Euler(45f, 0f, 0f);
            Vector3 forward = rot * Vector3.forward;
            Vector3 camPos = center - forward * distance;
            camPos.y = Mathf.Max(camPos.y, 10f);

            return (camPos, distance);
        }

        public static float CalculateOrthoSize(Bounds unitBounds, float padding, float aspect)
        {
            float width = unitBounds.size.x + padding * 2f;
            float height = unitBounds.size.z + padding * 2f;
            return Mathf.Max(width / (2f * aspect), height / 2f);
        }
    }
}
#endif