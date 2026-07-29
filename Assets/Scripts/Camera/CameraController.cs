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
        [Header("State")]
        [SerializeField] private CameraPhase m_CurrentPhase = CameraPhase.Betting;
        [Header("Combat Framing")]
        [SerializeField] private float m_FramingPadding = 3f;
        [SerializeField] private float m_CombatPadding = 5f;
        [Header("Shake")]
        [SerializeField] private float m_ShakeStrength = 0.5f;
        [SerializeField] private float m_ShakeDuration = 0.3f;

        private CameraPose m_DefaultPose;
        private CameraPose m_TargetPose;
        private bool m_IsBoosted;
        private bool m_IsTransitioning;
        private float m_NextBoundsRefreshTime;
        private float m_ShakeTimer;
        private float m_ShakeIntensity;
        private float m_NextShakeAllowedTime;
        private Vector3 m_ShakeOffset;

        public CameraSettings Settings => m_CameraSettings;
        public UnityCamera MainCamera => m_MainCamera;
        public CameraPhase CurrentPhase => m_CurrentPhase;
        public bool IsBoosted => m_IsBoosted;

        private void Awake()
        {
            if (s_Instance != null && s_Instance != this)
            {
                Destroy(gameObject);
                return;
            }
            s_Instance = this;
            if (m_MainCamera == null) m_MainCamera = GetComponent<UnityCamera>();
        }

        private void Start()
        {
            ApplySettings();
            m_DefaultPose = BuildDefaultPose();
            m_TargetPose = m_DefaultPose;
            ApplyPose(m_DefaultPose);
            SetPhase(CameraPhase.Betting);
        }

        private void LateUpdate()
        {
            if (m_MainCamera == null || m_CameraSettings == null || m_IsTransitioning) return;

            if (m_CurrentPhase == CameraPhase.Combat &&
                Time.unscaledTime >= m_NextBoundsRefreshTime)
            {
                RefreshCombatTarget();
                m_NextBoundsRefreshTime = Time.unscaledTime + m_CameraSettings.BoundsRefreshInterval;
            }

            float followSpeed = m_IsBoosted
                ? m_CameraSettings.BoostFollowSpeed
                : m_CameraSettings.FollowLerpSpeed;
            float zoomSpeed = m_IsBoosted
                ? m_CameraSettings.BoostZoomSpeed
                : m_CameraSettings.ZoomLerpSpeed;
            float positionT = 1f - Mathf.Exp(-followSpeed * Time.unscaledDeltaTime);
            float zoomT = 1f - Mathf.Exp(-zoomSpeed * Time.unscaledDeltaTime);

            CameraPose current = CapturePose();
            current.Position = Vector3.Lerp(current.Position - m_ShakeOffset, m_TargetPose.Position, positionT);
            current.Rotation = Quaternion.Slerp(current.Rotation, m_TargetPose.Rotation, positionT);
            current.FieldOfView = Mathf.Lerp(current.FieldOfView, m_TargetPose.FieldOfView, zoomT);
            current.OrthographicSize = Mathf.Lerp(current.OrthographicSize, m_TargetPose.OrthographicSize, zoomT);

            UpdateShake();
            current.Position += m_ShakeOffset;
            ApplyPose(current);
        }

        public async Awaitable SetPhaseAsync(CameraPhase newPhase, CancellationToken token = default)
        {
            if (m_CurrentPhase == newPhase && !m_IsTransitioning) return;

            m_IsTransitioning = true;
            CameraPose target = GetPhasePose(newPhase);
            await MoveCameraAsync(target, 0.5f, token);
            m_CurrentPhase = newPhase;
            m_TargetPose = target;
            m_IsTransitioning = false;
            m_NextBoundsRefreshTime = 0f;
        }

        public void SetPhase(CameraPhase newPhase)
        {
            m_CurrentPhase = newPhase;
            m_TargetPose = GetPhasePose(newPhase);
            m_NextBoundsRefreshTime = 0f;
        }

        public void SetSpeedBoost(bool boosted)
        {
            m_IsBoosted = boosted;
        }

        public void ShakeCamera(float intensity = -1f, float duration = -1f)
        {
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
            CameraPose pose = m_TargetPose;
            pose.Position = PositionForTarget(position, zoom > 0f ? zoom : GetDistance(pose));
            if (m_MainCamera.orthographic && zoom > 0f) pose.OrthographicSize = zoom;
            await MoveCameraAsync(pose, duration, token);
            m_TargetPose = pose;
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
            m_CameraSettings.UsePerspective = usePerspective;
            m_MainCamera.orthographic = !usePerspective;
            m_DefaultPose = BuildDefaultPose();
            m_TargetPose = GetPhasePose(m_CurrentPhase);
        }

        private void ApplySettings()
        {
            if (m_CameraSettings == null || m_MainCamera == null) return;
            m_MainCamera.clearFlags = m_CameraSettings.ClearFlags;
            m_MainCamera.backgroundColor = m_CameraSettings.BackgroundColor;
            m_MainCamera.orthographic = !m_CameraSettings.UsePerspective;
            m_MainCamera.fieldOfView = m_CameraSettings.FieldOfView;
            m_MainCamera.orthographicSize = m_CameraSettings.OrthoSize;
            m_MainCamera.allowHDR = false;
        }

        private CameraPose GetPhasePose(CameraPhase phase)
        {
            if (phase == CameraPhase.Combat)
            {
                RefreshCombatTarget();
                return m_TargetPose;
            }

            if (phase == CameraPhase.Result &&
                UnitRegistry.TryCalculateLivingBounds(
                    m_CameraSettings != null ? m_CameraSettings.DefaultVisualRadius : 0.65f,
                    out Bounds resultBounds))
            {
                return CameraFramingCalculator.CalculatePose(
                    resultBounds,
                    m_MainCamera,
                    m_CameraSettings,
                    m_FramingPadding);
            }

            return m_DefaultPose;
        }

        private void RefreshCombatTarget()
        {
            if (m_CameraSettings == null || m_MainCamera == null) return;
            if (!UnitRegistry.TryCalculateLivingBounds(m_CameraSettings.DefaultVisualRadius, out Bounds bounds))
            {
                m_TargetPose = m_DefaultPose;
                return;
            }

            bounds.Expand(m_CombatPadding * 2f);
            m_TargetPose = CameraFramingCalculator.CalculatePose(
                bounds,
                m_MainCamera,
                m_CameraSettings,
                m_FramingPadding);
        }

        private CameraPose BuildDefaultPose()
        {
            if (m_CameraSettings == null) return CapturePose();

            var (position, rotation) = m_CameraSettings.CalculatePerspectiveTransform(
                m_CameraSettings.CombatAreaCenter);
            return new CameraPose(
                position,
                rotation,
                m_CameraSettings.FieldOfView,
                m_CameraSettings.OrthoSize);
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
                ApplyPose(CameraPose.Lerp(start, target, t));
                elapsed += Time.unscaledDeltaTime;
                await Awaitable.NextFrameAsync();
            }
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
            Vector3 center = m_CameraSettings != null ? m_CameraSettings.CombatAreaCenter : Vector3.zero;
            return Vector3.Distance(pose.Position, center);
        }

        private void UpdateShake()
        {
            if (m_ShakeTimer <= 0f)
            {
                m_ShakeOffset = Vector3.zero;
                return;
            }

            m_ShakeTimer -= Time.unscaledDeltaTime;
            float duration = Mathf.Max(0.01f, m_ShakeDuration);
            float fade = Mathf.Clamp01(m_ShakeTimer / duration);
            m_ShakeOffset = Random.insideUnitSphere * (m_ShakeIntensity * fade);
            m_ShakeOffset.y *= 0.5f;
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
        public static CameraPose CalculatePose(
            Bounds unitBounds,
            UnityCamera camera,
            CameraSettings settings,
            float padding = 3f)
        {
            Vector3 center = unitBounds.center;
            center.y = 0f;
            Vector3 size = unitBounds.size + new Vector3(padding * 2f, padding, padding * 2f);
            float safeWidth = Mathf.Max(0.1f, 1f - settings.SafeMarginHorizontal * 2f);
            float safeHeight = Mathf.Max(0.1f, 1f - settings.SafeMarginVertical * 2f);
            float aspect = Mathf.Max(0.1f, camera.aspect);
            Quaternion rotation = Quaternion.Euler(settings.CameraAngleX, settings.CameraAngleY, 0f);

            float verticalFov = settings.FieldOfView * Mathf.Deg2Rad;
            float horizontalFov = 2f * Mathf.Atan(Mathf.Tan(verticalFov * 0.5f) * aspect);
            float distanceByWidth = size.x / (2f * Mathf.Tan(horizontalFov * 0.5f) * safeWidth);
            float distanceByDepth = size.z / (2f * Mathf.Tan(verticalFov * 0.5f) * safeHeight);
            float distance = Mathf.Clamp(
                Mathf.Max(distanceByWidth, distanceByDepth),
                settings.MinZoom,
                settings.MaxZoom);

            float orthoSize = CalculateOrthoSize(
                unitBounds,
                padding,
                aspect * safeWidth / safeHeight);
            orthoSize = Mathf.Clamp(orthoSize, settings.MinZoom, settings.MaxZoom);
            Vector3 position = center - rotation * Vector3.forward * distance;

            return new CameraPose(position, rotation, settings.FieldOfView, orthoSize);
        }

        public static (Vector3 position, float zoom) CalculateFraming(
            Bounds unitBounds,
            UnityCamera camera,
            float padding = 3f)
        {
            Vector3 center = unitBounds.center;
            center.y = 0f;
            float fov = camera.fieldOfView * Mathf.Deg2Rad;
            float aspect = Mathf.Max(0.1f, camera.aspect);
            float width = unitBounds.size.x + padding * 2f;
            float depth = unitBounds.size.z + padding * 2f;
            float distance = Mathf.Max(
                width / (2f * Mathf.Tan(fov * 0.5f) * aspect),
                depth / (2f * Mathf.Tan(fov * 0.5f)));
            Quaternion rotation = Quaternion.Euler(45f, 0f, 0f);
            return (center - rotation * Vector3.forward * distance, distance);
        }

        public static float CalculateOrthoSize(Bounds unitBounds, float padding, float aspect)
        {
            float width = unitBounds.size.x + padding * 2f;
            float height = unitBounds.size.z + padding * 2f;
            return Mathf.Max(width / (2f * Mathf.Max(0.1f, aspect)), height * 0.5f);
        }
    }
}
#endif
