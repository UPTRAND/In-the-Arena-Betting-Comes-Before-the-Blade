#if UNITY_6000_0_OR_NEWER
using System.Threading;
using InTheArena.Unit;
using UnityEngine;
using UnityCamera = UnityEngine.Camera;

namespace InTheArena.Camera
{
    [System.Serializable]
    public struct CameraPose
    {
        public Vector3 Position;
        public Quaternion Rotation;
        public float FieldOfView;
        public float OrthographicSize;

        public CameraPose(Vector3 position, Quaternion rotation, float fieldOfView, float orthographicSize)
        {
            Position = position;
            Rotation = rotation;
            FieldOfView = fieldOfView;
            OrthographicSize = orthographicSize;
        }

        public static CameraPose Lerp(CameraPose from, CameraPose to, float t)
        {
            return new CameraPose(
                Vector3.Lerp(from.Position, to.Position, t),
                Quaternion.Slerp(from.Rotation, to.Rotation, t),
                Mathf.Lerp(from.FieldOfView, to.FieldOfView, t),
                Mathf.Lerp(from.OrthographicSize, to.OrthographicSize, t));
        }
    }

    /// <summary>
    /// 45도 카메라를 유지하면서 양 팀 생존 유닛 전체를 모바일 안전 영역에 프레이밍합니다.
    /// V6: Orthographic 지원 및 CameraBackgroundConstraint 연동
    /// </summary>
    [DefaultExecutionOrder(0)]
    [DisallowMultipleComponent]
    [RequireComponent(typeof(UnityCamera))]
    public sealed class CameraController : MonoBehaviour
    {
        private static CameraController s_Instance;
        public static CameraController Instance => s_Instance != null ? s_Instance : null;

        [Header("Settings")]
        [SerializeField] private CameraSettings m_CameraSettings;
        [Header("References")]
        [SerializeField] private UnityCamera m_MainCamera;
        [SerializeField] private BackgroundCameraSafeArea m_SafeArea;
        [SerializeField] private CameraViewportProvider m_ViewportProvider;
        [Header("State")]
        [SerializeField] private CameraPhase m_CurrentPhase = CameraPhase.Betting;
        [Header("Shake")]
        [SerializeField] private float m_ShakeStrength = 0.5f;
        [SerializeField] private float m_ShakeDuration = 0.3f;

        private CameraPose m_DefaultPose;
        private CameraPose m_TargetPose;
        private bool m_IsBoosted;
        private bool m_IsTransitioning;
        private bool m_IsCinematicFocus;
        private float m_NextBoundsRefreshTime;
        private float m_ShakeTimer;
        private float m_ShakeIntensity;
        private float m_NextShakeAllowedTime;
        private Vector3 m_ShakeOffset;

        private CameraBackgroundConstraint m_Constraint = new CameraBackgroundConstraint();

        public CameraSettings Settings => m_CameraSettings;
        public UnityCamera MainCamera => m_MainCamera;
        public CameraPhase CurrentPhase => m_CurrentPhase;
        public bool IsBoosted => m_IsBoosted;
        public bool IsCinematicFocus => m_IsCinematicFocus;

        private static readonly Rect FullViewportRect = new Rect(0f, 0f, 1f, 1f);

        private Rect GetEffectiveViewportRect()
        {
            return m_ViewportProvider != null ? m_ViewportProvider.GetEffectiveViewportRect() : FullViewportRect;
        }

        private void Awake()
        {
            if (s_Instance != null && s_Instance != this)
            {
                Destroy(gameObject);
                return;
            }
            s_Instance = this;
            if (m_MainCamera == null) m_MainCamera = GetComponent<UnityCamera>();
            if (m_ViewportProvider == null) m_ViewportProvider = GetComponent<CameraViewportProvider>();
        }

        private void Start()
        {
            m_Constraint.ResetState();
            ApplySettings();
            SyncViewportRect();
            m_DefaultPose = BuildDefaultPose();
            m_TargetPose = m_DefaultPose;
            ApplyPose(m_DefaultPose);
            SetPhase(CameraPhase.Betting);
        }

        private void LateUpdate()
        {
            if (m_MainCamera == null) return;

            SyncViewportRect();
            if (m_CameraSettings == null || m_IsTransitioning) return;

            if (m_CurrentPhase == CameraPhase.Combat &&
                m_CameraSettings.EnableAutoFraming &&
                !m_IsCinematicFocus &&
                m_ShakeTimer <= 0f &&
                Time.unscaledTime >= m_NextBoundsRefreshTime)
            {
                RefreshCombatTarget();
                m_NextBoundsRefreshTime = Time.unscaledTime + m_CameraSettings.BoundsRefreshInterval;
            }

            float followSpeed = m_IsBoosted ? m_CameraSettings.BoostFollowSpeed : m_CameraSettings.FollowLerpSpeed;
            float zoomSpeed = m_IsBoosted ? m_CameraSettings.BoostZoomSpeed : m_CameraSettings.ZoomLerpSpeed;

            if (m_CurrentPhase == CameraPhase.Combat && !m_IsCinematicFocus)
            {
                if (m_CameraSettings.ProjMode == ProjectionMode.Orthographic)
                {
                    zoomSpeed = m_TargetPose.OrthographicSize > m_MainCamera.orthographicSize
                        ? m_CameraSettings.AutoZoomOutSpeed
                        : m_CameraSettings.AutoZoomInSpeed;
                }
                else
                {
                    float currentDistance = GetPoseDistance(CapturePose());
                    float targetDistance = GetPoseDistance(m_TargetPose);
                    zoomSpeed = targetDistance > currentDistance
                        ? m_CameraSettings.AutoZoomOutSpeed
                        : m_CameraSettings.AutoZoomInSpeed;
                }
                if (m_IsBoosted) zoomSpeed = Mathf.Max(zoomSpeed, m_CameraSettings.BoostZoomSpeed);
            }

            float positionT = 1f - Mathf.Exp(-followSpeed * Time.unscaledDeltaTime);
            float zoomT = 1f - Mathf.Exp(-zoomSpeed * Time.unscaledDeltaTime);

            if (m_CurrentPhase == CameraPhase.Combat && m_ShakeTimer > 0f)
            {
                positionT = 0f;
                zoomT = 0f;
            }

            CameraPose current = CapturePose();
            current.Position -= m_ShakeOffset;

            current.Position = Vector3.Lerp(current.Position, m_TargetPose.Position, positionT);
            current.Rotation = Quaternion.Slerp(current.Rotation, m_TargetPose.Rotation, positionT);
            current.FieldOfView = Mathf.Lerp(current.FieldOfView, m_TargetPose.FieldOfView, zoomT);
            current.OrthographicSize = Mathf.Lerp(current.OrthographicSize, m_TargetPose.OrthographicSize, zoomT);

            UpdateShake();

            Rect vpRect = GetEffectiveViewportRect();
            Vector2 shakePadding = m_SafeArea != null ? GetSafeAreaLocalShakePadding(m_SafeArea.transform) : Vector2.zero;
            current = m_Constraint.ConstrainPose(current, m_MainCamera, m_SafeArea, vpRect, m_CameraSettings, shakePadding);

            current.Position += m_ShakeOffset;

            ApplyPose(current);
        }

        public async Awaitable SetPhaseAsync(CameraPhase newPhase, CancellationToken token = default)
        {
            SyncViewportRect();
            if (m_CurrentPhase == newPhase && !m_IsTransitioning && !m_IsCinematicFocus) return;

            m_IsCinematicFocus = false;
            m_IsTransitioning = true;
            CameraPose target = GetPhasePose(newPhase);
            try
            {
                await MoveCameraAsync(target, 0.5f, token);
                m_CurrentPhase = newPhase;
                m_TargetPose = target;
                m_NextBoundsRefreshTime = 0f;
            }
            finally
            {
                m_IsTransitioning = false;
            }
        }

        public void SetPhase(CameraPhase newPhase)
        {
            SyncViewportRect();
            m_IsCinematicFocus = false;
            m_CurrentPhase = newPhase;
            m_TargetPose = GetPhasePose(newPhase);
            m_NextBoundsRefreshTime = 0f;
        }

        private void SyncViewportRect()
        {
            if (m_MainCamera == null ||
                m_ViewportProvider == null ||
                !m_ViewportProvider.TryGetTargetCameraRect(out Rect targetRect))
            {
                return;
            }

            Rect currentRect = m_MainCamera.rect;
            if (Mathf.Abs(currentRect.x - targetRect.x) <= 0.0001f &&
                Mathf.Abs(currentRect.y - targetRect.y) <= 0.0001f &&
                Mathf.Abs(currentRect.width - targetRect.width) <= 0.0001f &&
                Mathf.Abs(currentRect.height - targetRect.height) <= 0.0001f)
            {
                return;
            }

            m_MainCamera.rect = targetRect;
            m_Constraint.ResetState();
            m_NextBoundsRefreshTime = 0f;
        }

        public void SetSpeedBoost(bool boosted)
        {
            m_IsBoosted = boosted;
        }

        public async Awaitable FocusFinalEliminationAsync(
            Bounds focusBounds,
            CancellationToken token = default)
        {
            if (m_MainCamera == null || m_CameraSettings == null) return;

            m_IsCinematicFocus = true;
            m_ShakeTimer = 0f;
            m_ShakeOffset = Vector3.zero;

            Rect vpRect = GetEffectiveViewportRect();

            CameraPose targetPose = CameraFramingCalculator.CalculateFinalEliminationPose(
                focusBounds,
                m_MainCamera,
                vpRect,
                m_CameraSettings);

            targetPose = m_Constraint.ConstrainPose(targetPose, m_MainCamera, m_SafeArea, vpRect, m_CameraSettings, Vector2.zero);

            m_TargetPose = targetPose;
            m_IsTransitioning = true;
            try
            {
                await MoveCameraAsync(
                    targetPose,
                    m_CameraSettings.FinalEliminationFocusDuration,
                    token);
            }
            finally
            {
                m_IsTransitioning = false;
            }
        }

        public void HoldCurrentPoseForFinalElimination()
        {
            m_IsCinematicFocus = true;
            m_IsTransitioning = false;
            m_ShakeTimer = 0f;
            CameraPose pose = CapturePose();
            pose.Position -= m_ShakeOffset;
            m_ShakeOffset = Vector3.zero;
            m_TargetPose = pose;
            ApplyPose(pose);
        }

        public void EndFinalEliminationFocus()
        {
            m_IsCinematicFocus = false;
            m_IsTransitioning = false;
            m_ShakeTimer = 0f;
            m_ShakeOffset = Vector3.zero;
            m_NextBoundsRefreshTime = 0f;
        }

        public void ShakeCamera(float intensity = -1f, float duration = -1f)
        {
            if (m_IsCinematicFocus) return;
            if (Time.unscaledTime < m_NextShakeAllowedTime) return;
            float cooldown = m_CameraSettings != null ? m_CameraSettings.ShakeCooldown : 0.15f;
            m_NextShakeAllowedTime = Time.unscaledTime + cooldown;
            m_ShakeIntensity = Mathf.Clamp(
                intensity > 0f ? intensity : m_CameraSettings?.DefaultShakeIntensity ?? m_ShakeStrength,
                0f,
                2f);
            m_ShakeDuration = duration > 0f
                ? duration
                : m_CameraSettings?.DefaultShakeDuration ?? m_ShakeDuration;
            m_ShakeTimer = m_ShakeDuration;
        }

        public async Awaitable FocusOnPositionAsync(
            Vector3 position,
            float zoom = -1f,
            float duration = 0.5f,
            CancellationToken token = default)
        {
            m_IsCinematicFocus = true;
            m_IsTransitioning = true;
            CameraPose pose = m_TargetPose;
            if (m_CameraSettings.ProjMode == ProjectionMode.Orthographic)
            {
                pose.Position = PositionForTarget(position, m_CameraSettings.CameraDistance);
                if (zoom > 0f) pose.OrthographicSize = zoom;
            }
            else
            {
                pose.Position = PositionForTarget(position, zoom > 0f ? zoom : GetDistance(pose));
            }

            Rect vpRect = GetEffectiveViewportRect();
            pose = m_Constraint.ConstrainPose(pose, m_MainCamera, m_SafeArea, vpRect, m_CameraSettings, Vector2.zero);

            try
            {
                await MoveCameraAsync(pose, duration, token);
                m_TargetPose = pose;
            }
            finally
            {
                m_IsTransitioning = false;
                m_IsCinematicFocus = false;
                m_NextBoundsRefreshTime = 0f;
            }
        }

        public async Awaitable ResetToDefaultAsync(
            float duration = 1f,
            CancellationToken token = default)
        {
            await MoveCameraAsync(m_DefaultPose, duration, token);
            m_TargetPose = m_DefaultPose;
        }

        public void SetProjectionMode(bool usePerspective)
        {
            if (m_CameraSettings == null || m_MainCamera == null) return;
            m_CameraSettings.ProjMode = usePerspective ? ProjectionMode.PerspectiveLegacy : ProjectionMode.Orthographic;
            m_MainCamera.orthographic = !usePerspective;
            m_Constraint.ResetState();
            m_DefaultPose = BuildDefaultPose();
            m_TargetPose = GetPhasePose(m_CurrentPhase);
        }

        private void ApplySettings()
        {
            if (m_CameraSettings == null || m_MainCamera == null) return;
            m_MainCamera.clearFlags = m_CameraSettings.ClearFlags;
            m_MainCamera.backgroundColor = m_CameraSettings.BackgroundColor;
            m_MainCamera.orthographic = m_CameraSettings.ProjMode == ProjectionMode.Orthographic;
            m_MainCamera.fieldOfView = m_CameraSettings.FieldOfView;
            m_MainCamera.orthographicSize = m_CameraSettings.OrthoSize;
            m_MainCamera.allowHDR = false;
        }

        private CameraPose GetPhasePose(CameraPhase phase)
        {
            CameraPose desiredPose = m_DefaultPose;
            if (phase == CameraPhase.Combat)
            {
                if (m_CameraSettings != null && m_CameraSettings.EnableAutoFraming)
                {
                    RefreshCombatTarget();
                    return m_TargetPose;
                }
            }
            else if (phase == CameraPhase.Result &&
                UnitRegistry.TryCalculateLivingBounds(
                    m_CameraSettings != null ? m_CameraSettings.DefaultVisualRadius : 0.65f,
                    out Bounds resultBounds))
            {
                Rect vpRect = GetEffectiveViewportRect();
                desiredPose = CameraFramingCalculator.CalculatePose(
                    resultBounds,
                    m_MainCamera,
                    vpRect,
                    m_CameraSettings,
                    m_CameraSettings.FramingPadding);
            }

            Rect vpRectOuter = GetEffectiveViewportRect();
            return m_Constraint.ConstrainPose(desiredPose, m_MainCamera, m_SafeArea, vpRectOuter, m_CameraSettings, Vector2.zero);
        }

        private void RefreshCombatTarget()
        {
            if (m_CameraSettings == null || m_MainCamera == null) return;
            if (!UnitRegistry.TryCalculateLivingBounds(m_CameraSettings.DefaultVisualRadius, out Bounds bounds))
            {
                return;
            }

            Rect vpRect = GetEffectiveViewportRect();

            CameraPose candidate = CameraFramingCalculator.CalculatePose(
                bounds,
                m_MainCamera,
                vpRect,
                m_CameraSettings,
                m_CameraSettings.FramingPadding);

            candidate = m_Constraint.ConstrainPose(candidate, m_MainCamera, m_SafeArea, vpRect, m_CameraSettings, Vector2.zero);

            Vector3 previousCenter = GetGroundTarget(m_TargetPose);
            Vector3 candidateCenter = GetGroundTarget(candidate);

            if ((candidateCenter - previousCenter).sqrMagnitude <
                m_CameraSettings.CenterDeadZone * m_CameraSettings.CenterDeadZone)
            {
                candidateCenter = previousCenter;
            }

            if (m_CameraSettings.ProjMode == ProjectionMode.Orthographic)
            {
                if (Mathf.Abs(candidate.OrthographicSize - m_TargetPose.OrthographicSize) < m_CameraSettings.DistanceDeadZone)
                {
                    candidate.OrthographicSize = m_TargetPose.OrthographicSize;
                }
                candidate.Position = candidateCenter - candidate.Rotation * Vector3.forward * m_CameraSettings.CameraDistance;
            }
            else
            {
                float previousDistance = GetPoseDistance(m_TargetPose);
                float candidateDistance = GetPoseDistance(candidate);
                if (Mathf.Abs(candidateDistance - previousDistance) < m_CameraSettings.DistanceDeadZone)
                {
                    candidateDistance = previousDistance;
                }
                candidate.Position = candidateCenter - candidate.Rotation * Vector3.forward * candidateDistance;
            }

            m_TargetPose = m_Constraint.ConstrainPose(candidate, m_MainCamera, m_SafeArea, vpRect, m_CameraSettings, Vector2.zero);
        }

        private CameraPose BuildDefaultPose()
        {
            if (m_CameraSettings == null) return CapturePose();

            var (position, rotation) = m_CameraSettings.ProjMode == ProjectionMode.PerspectiveLegacy
                ? m_CameraSettings.CalculatePerspectiveTransform(m_CameraSettings.CombatAreaCenter)
                : m_CameraSettings.CalculateOrthographicTransform(m_CameraSettings.CombatAreaCenter);

            CameraPose pose = new CameraPose(
                position,
                rotation,
                m_CameraSettings.FieldOfView,
                m_CameraSettings.OrthoSize);

            Rect vpRect = GetEffectiveViewportRect();
            return m_Constraint.ConstrainPose(pose, m_MainCamera, m_SafeArea, vpRect, m_CameraSettings, Vector2.zero);
        }

        private async Awaitable MoveCameraAsync(
            CameraPose target,
            float duration,
            CancellationToken token)
        {
            CameraPose start = CapturePose();
            float elapsed = 0f;
            while (elapsed < duration)
            {
                token.ThrowIfCancellationRequested();
                float t = duration <= 0f ? 1f : elapsed / duration;
                t = 1f - Mathf.Pow(1f - t, 3f);
                CameraPose lerped = CameraPose.Lerp(start, target, t);
                Vector2 shakePadding = m_SafeArea != null ? GetSafeAreaLocalShakePadding(m_SafeArea.transform) : Vector2.zero;
                Rect vpRect = GetEffectiveViewportRect();
                lerped = m_Constraint.ConstrainPose(lerped, m_MainCamera, m_SafeArea, vpRect, m_CameraSettings, shakePadding);
                ApplyPose(lerped);
                elapsed += Time.unscaledDeltaTime;
                await Awaitable.NextFrameAsync();
            }

            Vector2 finalShakePadding = m_SafeArea != null ? GetSafeAreaLocalShakePadding(m_SafeArea.transform) : Vector2.zero;
            Rect finalVpRect = GetEffectiveViewportRect();
            target = m_Constraint.ConstrainPose(target, m_MainCamera, m_SafeArea, finalVpRect, m_CameraSettings, finalShakePadding);
            ApplyPose(target);
        }

        private CameraPose CapturePose()
        {
            if (m_MainCamera == null) return default;
            return new CameraPose(
                transform.position,
                transform.rotation,
                m_MainCamera.fieldOfView,
                m_MainCamera.orthographicSize);
        }

        private void ApplyPose(CameraPose pose)
        {
            transform.SetPositionAndRotation(pose.Position, pose.Rotation);
            if (m_MainCamera == null) return;
            m_MainCamera.fieldOfView = pose.FieldOfView;
            m_MainCamera.orthographicSize = pose.OrthographicSize;
        }

        private Vector3 PositionForTarget(Vector3 target, float distance)
        {
            Quaternion rotation = m_CameraSettings != null
                ? Quaternion.Euler(m_CameraSettings.CameraAngleX, m_CameraSettings.CameraAngleY, 0f)
                : transform.rotation;
            target.y = 0f;
            return target - rotation * Vector3.forward * distance;
        }

        private float GetDistance(CameraPose pose)
        {
            return GetPoseDistance(pose);
        }

        private static Vector3 GetGroundTarget(CameraPose pose)
        {
            Vector3 forward = pose.Rotation * Vector3.forward;
            if (Mathf.Abs(forward.y) < 0.0001f)
            {
                Vector3 fallback = pose.Position + forward * 10f;
                fallback.y = 0f;
                return fallback;
            }

            float distance = -pose.Position.y / forward.y;
            Vector3 target = pose.Position + forward * Mathf.Max(0f, distance);
            target.y = 0f;
            return target;
        }

        private static float GetPoseDistance(CameraPose pose)
        {
            Vector3 forward = pose.Rotation * Vector3.forward;
            if (Mathf.Abs(forward.y) < 0.0001f) return 0f;
            return Mathf.Max(0f, -pose.Position.y / forward.y);
        }

        private void UpdateShake()
        {
            if (m_ShakeTimer <= 0f)
            {
                m_ShakeOffset = Vector3.zero;
                return;
            }

            m_ShakeTimer -= Time.unscaledDeltaTime;
            if (m_ShakeTimer <= 0f)
            {
                m_ShakeTimer = 0f;
                m_ShakeOffset = Vector3.zero;
                m_NextBoundsRefreshTime = 0f;
                return;
            }

            float duration = Mathf.Max(0.01f, m_ShakeDuration);
            float fade = Mathf.Clamp01(m_ShakeTimer / duration);
            m_ShakeOffset = Random.insideUnitSphere * (m_ShakeIntensity * fade);
            m_ShakeOffset.y *= 0.5f;
        }

        public Vector2 GetSafeAreaLocalShakePadding(Transform safeAreaTransform)
        {
            if (m_ShakeIntensity <= 0f || m_ShakeTimer <= 0f) return Vector2.zero;

            float intensity = m_ShakeIntensity;

            // 정밀 상한 투영: 타원체의 Safe Area 축 투영 최대 상한
            Vector3 localRight = safeAreaTransform.right;
            Vector3 localUp = safeAreaTransform.up;

            float paddingX = intensity * Mathf.Sqrt(localRight.x * localRight.x + 0.25f * localRight.y * localRight.y + localRight.z * localRight.z);
            float paddingY = intensity * Mathf.Sqrt(localUp.x * localUp.x + 0.25f * localUp.y * localUp.y + localUp.z * localUp.z);

            return new Vector2(paddingX, paddingY);
        }

        private void OnDestroy()
        {
            if (s_Instance == this) s_Instance = null;
        }

#if UNITY_EDITOR
        private void OnDrawGizmos()
        {
            if (m_CameraSettings == null) return;
            Gizmos.color = new Color(0f, 1f, 0f, 0.25f);
            Gizmos.DrawWireCube(
                m_CameraSettings.CombatAreaCenter,
                m_CameraSettings.CombatAreaSize);
        }
#endif
    }

    public enum CameraPhase
    {
        Betting = 0,
        Combat = 1,
        Result = 2
    }

    public static class CameraFramingCalculator
    {
        public static CameraPose CalculateFinalEliminationPose(
            Bounds focusBounds,
            UnityCamera camera,
            Rect viewportRect,
            CameraSettings settings)
        {
            if (settings.ProjMode == ProjectionMode.Orthographic)
            {
                Vector3 center = focusBounds.center;
                center.y = 0f;
                Quaternion rotation = Quaternion.Euler(settings.CameraAngleX, settings.CameraAngleY, 0f);
                Vector3 fixedCamPos = center - rotation * Vector3.forward * settings.CameraDistance;

                float orthoSize = settings.FinalEliminationOrthographicSize;

                float camAspect = camera.aspect;
                float viewWidth = 2f * orthoSize * camAspect;
                float viewHeight = 2f * orthoSize;

                float viewCenterX = (viewportRect.center.x - 0.5f) * viewWidth;
                float viewCenterY = (viewportRect.center.y - 0.5f) * viewHeight;

                Vector3 shiftWorld = rotation * new Vector3(-viewCenterX, -viewCenterY, 0f);
                fixedCamPos += shiftWorld;

                return new CameraPose(fixedCamPos, rotation, settings.FieldOfView, orthoSize);
            }
            else
            {
                Vector3 center = focusBounds.center;
                center.y = 0f;
                Quaternion rotation = Quaternion.Euler(
                    settings.CameraAngleX,
                    settings.CameraAngleY,
                    0f);
                float distance = Mathf.Min(
                    settings.FinalEliminationDistance,
                    settings.MinFramingDistance - 0.01f);

                return new CameraPose(
                    center - rotation * Vector3.forward * Mathf.Max(0.1f, distance),
                    rotation,
                    settings.FieldOfView,
                    settings.MinOrthographicSize);
            }
        }

        public static CameraPose CalculatePose(
            Bounds unitBounds,
            UnityCamera camera,
            Rect viewportRect,
            CameraSettings settings,
            float padding = 3f)
        {
            if (settings.ProjMode == ProjectionMode.Orthographic)
            {
                Vector3 center = unitBounds.center;
                center.y = 0f;
                Quaternion rotation = Quaternion.Euler(settings.CameraAngleX, settings.CameraAngleY, 0f);
                Vector3 fixedCamPos = center - rotation * Vector3.forward * settings.CameraDistance;

                Matrix4x4 viewMatrix = Matrix4x4.TRS(fixedCamPos, rotation, Vector3.one).inverse;

                Vector3 minBounds = unitBounds.min;
                Vector3 maxBounds = unitBounds.max;

                float minX = float.MaxValue, maxX = float.MinValue;
                float minY = float.MaxValue, maxY = float.MinValue;

                for (int x = 0; x < 2; x++)
                {
                    for (int y = 0; y < 2; y++)
                    {
                        for (int z = 0; z < 2; z++)
                        {
                            Vector3 corner = new Vector3(
                                x == 0 ? minBounds.x : maxBounds.x,
                                y == 0 ? minBounds.y : maxBounds.y,
                                z == 0 ? minBounds.z : maxBounds.z);

                            Vector3 viewPt = viewMatrix.MultiplyPoint3x4(corner);
                            minX = Mathf.Min(minX, viewPt.x);
                            maxX = Mathf.Max(maxX, viewPt.x);
                            minY = Mathf.Min(minY, viewPt.y);
                            maxY = Mathf.Max(maxY, viewPt.y);
                        }
                    }
                }

                float viewPadding = Mathf.Max(0f, padding);
                minX -= viewPadding;
                maxX += viewPadding;
                minY -= viewPadding;
                maxY += viewPadding;

                float safeWidth = Mathf.Max(0.1f, 1f - settings.SafeMarginHorizontal * 2f);
                float safeHeight = Mathf.Max(0.1f, 1f - settings.SafeMarginVertical * 2f);

                float requiredWidth = (maxX - minX) / safeWidth;
                float requiredHeight = (maxY - minY) / safeHeight;

                float requiredByHeight = requiredHeight / (2f * Mathf.Max(0.001f, viewportRect.height));
                float requiredByWidth = requiredWidth / (2f * camera.aspect * Mathf.Max(0.001f, viewportRect.width));

                float orthoSize = Mathf.Max(requiredByHeight, requiredByWidth);
                orthoSize = Mathf.Clamp(orthoSize, settings.MinOrthographicSize, settings.MaxOrthographicSize);

                float camAspect = camera.aspect;
                float viewWidth = 2f * orthoSize * camAspect;
                float viewHeight = 2f * orthoSize;

                float viewCenterX = (viewportRect.center.x - 0.5f) * viewWidth;
                float viewCenterY = (viewportRect.center.y - 0.5f) * viewHeight;

                float shiftX = ((minX + maxX) * 0.5f) - viewCenterX;
                float shiftY = ((minY + maxY) * 0.5f) - viewCenterY;

                Vector3 shiftWorld = rotation * new Vector3(shiftX, shiftY, 0f);
                fixedCamPos += shiftWorld;
                fixedCamPos += new Vector3(settings.FramingCenterOffset.x, 0f, settings.FramingCenterOffset.y);

                return new CameraPose(fixedCamPos, rotation, settings.FieldOfView, orthoSize);
            }
            else
            {
                // Legacy Perspective Calculation
                Vector3 center = unitBounds.center;
                center.y = 0f;
                float safeWidth = Mathf.Max(0.1f, 1f - settings.SafeMarginHorizontal * 2f);
                float safeHeight = Mathf.Max(0.1f, 1f - settings.SafeMarginVertical * 2f);
                float aspect = settings.FramingAspect;
                Quaternion rotation = Quaternion.Euler(settings.CameraAngleX, settings.CameraAngleY, 0f);
                Quaternion inverseRotation = Quaternion.Inverse(rotation);
                float tanVertical = Mathf.Tan(settings.FieldOfView * Mathf.Deg2Rad * 0.5f) * safeHeight;
                float tanHorizontal = tanVertical * aspect * safeWidth / safeHeight;

                Vector3 min = unitBounds.min - new Vector3(padding, padding * 0.5f, padding);
                Vector3 max = unitBounds.max + new Vector3(padding, padding * 0.5f, padding);
                float requiredDistance = 0f;
                float requiredOrthoSize = 0f;

                for (int x = 0; x < 2; x++)
                {
                    for (int y = 0; y < 2; y++)
                    {
                        for (int z = 0; z < 2; z++)
                        {
                            Vector3 corner = new Vector3(
                                x == 0 ? min.x : max.x,
                                y == 0 ? min.y : max.y,
                                z == 0 ? min.z : max.z);
                            Vector3 local = inverseRotation * (corner - center);

                            float horizontalDistance =
                                Mathf.Abs(local.x) / Mathf.Max(0.0001f, tanHorizontal) - local.z;
                            float verticalDistance =
                                Mathf.Abs(local.y) / Mathf.Max(0.0001f, tanVertical) - local.z;
                            requiredDistance = Mathf.Max(
                                requiredDistance,
                                Mathf.Max(horizontalDistance, verticalDistance));

                            float horizontalOrtho =
                                Mathf.Abs(local.x) / Mathf.Max(0.0001f, aspect * safeWidth);
                            float verticalOrtho = Mathf.Abs(local.y) / safeHeight;
                            requiredOrthoSize = Mathf.Max(
                                requiredOrthoSize,
                                Mathf.Max(horizontalOrtho, verticalOrtho));
                        }
                    }
                }

                float distance = Mathf.Clamp(
                    requiredDistance,
                    settings.MinFramingDistance,
                    settings.MaxFramingDistance);
                float orthoSize = Mathf.Clamp(
                    requiredOrthoSize,
                    settings.MinOrthographicSize,
                    settings.MaxOrthographicSize);
                Vector3 targetCenter = center + new Vector3(settings.FramingCenterOffset.x, 0f, settings.FramingCenterOffset.y);
                Vector3 position = targetCenter - rotation * Vector3.forward * distance;

                return new CameraPose(position, rotation, settings.FieldOfView, orthoSize);
            }
        }
    }
}
#endif
