#if UNITY_6000_0_OR_NEWER
using UnityEngine;

namespace InTheArena.Camera
{
    /// <summary>
    /// 카메라 설정 데이터 ScriptableObject
    /// 45도 탑다운 뷰를 위한 카메라 설정 데이터 관리
    /// </summary>
    [CreateAssetMenu(fileName = "CameraSettings_", menuName = "In The Arena/Camera/Camera Settings", order = 0)]
    public class CameraSettings : ScriptableObject
    {
        [Header("카메라 투영 설정")]
        [Tooltip("투영 모드: true = Perspective(45도), false = Orthographic")]
        [SerializeField] private bool m_UsePerspective = true;

        [Tooltip("카메라 클리어 플래그")]
        [SerializeField] private CameraClearFlags m_ClearFlags = CameraClearFlags.SolidColor;

        [Tooltip("배경 색상")]
        [SerializeField] private Color m_BackgroundColor = new Color(0.1f, 0.1f, 0.12f, 1f);

        [Header("45도 Perspective 뷰 설정")]
        [Tooltip("시야각 (FOV): 45도 느낌을 위한 좁은 FOV")]
        [SerializeField] [Range(10f, 60f)] private float m_FieldOfView = 30f;

        [Tooltip("카메라-타겟 거리")]
        [SerializeField] [Range(5f, 50f)] private float m_CameraDistance = 18f;

        [Tooltip("카메라 높이 (Y축)")]
        [SerializeField] [Range(5f, 30f)] private float m_CameraHeight = 15f;

        [Tooltip("X축 회전 각도 (45도 내려다보기)")]
        [SerializeField] [Range(30f, 60f)] private float m_CameraAngleX = 45f;

        [Tooltip("Y축 회전 각도 (정면)")]
        [SerializeField] [Range(-180f, 180f)] private float m_CameraAngleY = 0f;

        [Header("Orthographic Fallback (Low-end 지원)")]
        [Tooltip("Orthographic 크기")]
        [SerializeField] [Range(5f, 30f)] private float m_OrthoSize = 12f;

        [Tooltip("Orthographic 종횡비")]
        [SerializeField] private float m_OrthoAspect = 16f / 9f;

        [Header("전투 영역 바운딩")]
        [Tooltip("전투 영역 중심 좌표")]
        [SerializeField] private Vector3 m_CombatAreaCenter = Vector3.zero;

        [Tooltip("전투 영역 크기 (7x3 그리드 기준)")]
        [SerializeField] private Vector3 m_CombatAreaSize = new Vector3(28f, 0f, 12f);

        [Header("모바일 안전 프레이밍")]
        [Tooltip("화면 좌우 안전 여백 비율")]
        [SerializeField] [Range(0f, 0.25f)] private float m_SafeMarginHorizontal = 0.05f;

        [Tooltip("화면 상하 안전 여백 비율")]
        [SerializeField] [Range(0f, 0.3f)] private float m_SafeMarginVertical = 0.12f;

        [Tooltip("생존 유닛 Bounds를 다시 계산하는 주기")]
        [SerializeField] [Range(0.05f, 0.5f)] private float m_BoundsRefreshInterval = 0.1f;

        [Tooltip("카메라 프레이밍에 사용하는 기본 유닛 시각 반경")]
        [SerializeField] [Range(0.1f, 2f)] private float m_DefaultVisualRadius = 0.65f;

        [Header("줌/이동 제한")]
        [Tooltip("최소 줌 거리")]
        [SerializeField] [Range(5f, 20f)] private float m_MinZoom = 8f;

        [Tooltip("최대 줌 거리")]
        [SerializeField] [Range(20f, 50f)] private float m_MaxZoom = 30f;

        [Tooltip("최소 높이")]
        [SerializeField] [Range(5f, 20f)] private float m_MinHeight = 8f;

        [Tooltip("최대 높이")]
        [SerializeField] [Range(20f, 40f)] private float m_MaxHeight = 25f;

        [Header("이동/줌 스무딩")]
        [Tooltip("일반 이동 Lerp 속도")]
        [SerializeField] [Range(1f, 20f)] private float m_FollowLerpSpeed = 8f;

        [Tooltip("부스트(2배속) 시 이동 Lerp 속도")]
        [SerializeField] [Range(5f, 30f)] private float m_BoostFollowSpeed = 15f;

        [Tooltip("일반 줌 Lerp 속도")]
        [SerializeField] [Range(1f, 20f)] private float m_ZoomLerpSpeed = 10f;

        [Tooltip("부스트 시 줌 Lerp 속도")]
        [SerializeField] [Range(5f, 30f)] private float m_BoostZoomSpeed = 20f;

        [Tooltip("데드존 반경 (이동 무시 거리)")]
        [SerializeField] [Range(0f, 2f)] private float m_DeadZoneRadius = 0.1f;

        [Header("카메라 쉐이크")]
        [Tooltip("기본 쉐이크 강도")]
        [SerializeField] [Range(0f, 2f)] private float m_DefaultShakeIntensity = 0.5f;

        [Tooltip("기본 쉐이크 지속 시간")]
        [SerializeField] [Range(0.1f, 1f)] private float m_DefaultShakeDuration = 0.3f;

        [Tooltip("쉐이크가 다시 발생할 수 있는 최소 간격")]
        [SerializeField] [Range(0f, 1f)] private float m_ShakeCooldown = 0.15f;

        /// <summary> Perspective 모드 사용 여부 </summary>
        public bool UsePerspective
        {
            get => m_UsePerspective;
            set => m_UsePerspective = value;
        }

        /// <summary> 카메라 클리어 플래그 </summary>
        public CameraClearFlags ClearFlags => m_ClearFlags;

        /// <summary> 배경 색상 </summary>
        public Color BackgroundColor => m_BackgroundColor;

        /// <summary> 시야각 (FOV) </summary>
        public float FieldOfView => m_FieldOfView;

        /// <summary> 카메라 기본 거리 </summary>
        public float CameraDistance => m_CameraDistance;

        /// <summary> 카메라 높이 </summary>
        public float CameraHeight => m_CameraHeight;

        /// <summary> X축 회전 각도 </summary>
        public float CameraAngleX => m_CameraAngleX;

        /// <summary> Y축 회전 각도 </summary>
        public float CameraAngleY => m_CameraAngleY;

        /// <summary> Orthographic 크기 </summary>
        public float OrthoSize => m_OrthoSize;

        /// <summary> Orthographic 종횡비 </summary>
        public float OrthoAspect => m_OrthoAspect;

        /// <summary> 전투 영역 중심 </summary>
        public Vector3 CombatAreaCenter => m_CombatAreaCenter;

        /// <summary> 전투 영역 크기 </summary>
        public Vector3 CombatAreaSize => m_CombatAreaSize;
        public float SafeMarginHorizontal => m_SafeMarginHorizontal;
        public float SafeMarginVertical => m_SafeMarginVertical;
        public float BoundsRefreshInterval => m_BoundsRefreshInterval;
        public float DefaultVisualRadius => m_DefaultVisualRadius;

        /// <summary> 최소 줌 </summary>
        public float MinZoom => m_MinZoom;

        /// <summary> 최대 줌 </summary>
        public float MaxZoom => m_MaxZoom;

        /// <summary> 최소 높이 </summary>
        public float MinHeight => m_MinHeight;

        /// <summary> 최대 높이 </summary>
        public float MaxHeight => m_MaxHeight;

        /// <summary> 일반 이동 Lerp 속도 </summary>
        public float FollowLerpSpeed => m_FollowLerpSpeed;

        /// <summary> 부스트 이동 Lerp 속도 </summary>
        public float BoostFollowSpeed => m_BoostFollowSpeed;

        /// <summary> 일반 줌 Lerp 속도 </summary>
        public float ZoomLerpSpeed => m_ZoomLerpSpeed;

        /// <summary> 부스트 줌 Lerp 속도 </summary>
        public float BoostZoomSpeed => m_BoostZoomSpeed;

        /// <summary> 데드존 반경 </summary>
        public float DeadZoneRadius => m_DeadZoneRadius;

        /// <summary> 기본 쉐이크 강도 </summary>
        public float DefaultShakeIntensity => m_DefaultShakeIntensity;

        /// <summary> 기본 쉐이크 지속 시간 </summary>
        public float DefaultShakeDuration => m_DefaultShakeDuration;
        public float ShakeCooldown => m_ShakeCooldown;

        /// <summary>
        /// Perspective 모드용 카메라 Transform 계산
        /// </summary>
        /// <param name="targetPosition">타겟 위치</param>
        /// <returns>카메라 위치와 회전</returns>
        public (Vector3 position, Quaternion rotation) CalculatePerspectiveTransform(Vector3 targetPosition)
        {
            // 타겟 위치에서 높이 보정
            Vector3 targetPos = targetPosition;
            targetPos.y = 0f; // 지면 기준

            // 카메라 회전 (X축 45도, Y축 0도)
            Quaternion rotation = Quaternion.Euler(m_CameraAngleX, m_CameraAngleY, 0f);

            // 카메라 위치: 타겟 뒤쪽 + 위쪽
            Vector3 forward = rotation * Vector3.forward;
            Vector3 cameraPos = targetPos - forward * m_CameraDistance;
            cameraPos.y = m_CameraHeight;

            return (cameraPos, rotation);
        }

        /// <summary>
        /// Orthographic 모드용 카메라 Transform 계산
        /// </summary>
        public (Vector3 position, Quaternion rotation) CalculateOrthographicTransform(Vector3 targetPosition)
        {
            Vector3 targetPos = targetPosition;
            targetPos.y = 0f;

            Quaternion rotation = Quaternion.Euler(90f, 0f, 0f); // Top-down
            Vector3 cameraPos = targetPos + new Vector3(0f, m_CameraHeight, 0f);

            return (cameraPos, rotation);
        }

        /// <summary>
        /// 데이터 유효성 검사
        /// </summary>
        public bool IsValid()
        {
            bool isValid = true;

            if (m_CameraDistance <= 0f)
            {
                Debug.LogError($"[CameraSettings] {name}: CameraDistance는 0보다 커야 합니다.");
                isValid = false;
            }

            if (m_CameraHeight <= 0f)
            {
                Debug.LogError($"[CameraSettings] {name}: CameraHeight는 0보다 커야 합니다.");
                isValid = false;
            }

            if (m_MinZoom >= m_MaxZoom)
            {
                Debug.LogError($"[CameraSettings] {name}: MinZoom은 MaxZoom보다 작아야 합니다.");
                isValid = false;
            }

            if (m_MinHeight >= m_MaxHeight)
            {
                Debug.LogError($"[CameraSettings] {name}: MinHeight는 MaxHeight보다 작아야 합니다.");
                isValid = false;
            }

            if (m_FieldOfView <= 0f || m_FieldOfView >= 179f)
            {
                Debug.LogError($"[CameraSettings] {name}: FieldOfView는 0~179 사이여야 합니다.");
                isValid = false;
            }

            return isValid;
        }

#if UNITY_EDITOR
        private void OnValidate()
        {
            IsValid();
        }
#endif
    }
}
#endif
