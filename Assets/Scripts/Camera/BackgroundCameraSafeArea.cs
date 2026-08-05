#if UNITY_6000_0_OR_NEWER
using UnityEngine;

namespace InTheArena.Camera
{
    public enum SafeAreaError
    {
        None,
        InvalidScale,
        InvalidSize,
        InvalidPadding,
        InvalidNumber
    }

    /// <summary>
    /// 로컬 XY 평면을 기준으로 한 카메라 안전 영역.
    /// 배경 3장이 공통으로 커버하는 보수적인 직사각형으로 수동 설정해야 합니다.
    /// 비균일 Scale은 로컬 오프셋 선형 계산을 파괴하므로 금지됩니다.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class BackgroundCameraSafeArea : MonoBehaviour
    {
        [Header("Safe Area (Local XY)")]
        [Tooltip("배경 평면 로컬 기준의 중심점")]
        [SerializeField] private Vector2 m_Center = Vector2.zero;

        [Tooltip("배경 평면 로컬 기준의 전체 크기")]
        [SerializeField] private Vector2 m_Size = new Vector2(30f, 15f);

        [Tooltip("안전을 위해 추가로 안쪽으로 밀어넣는 여백")]
        [SerializeField] private float m_InnerPadding = 0f;

        private SafeAreaError m_LastError = SafeAreaError.None;
        private bool m_HasLoggedError = false;

        public Plane BackgroundPlane => new Plane(transform.forward, transform.position);

        public Vector2 Center => m_Center;
        public Vector2 Size => m_Size;
        public float InnerPadding => m_InnerPadding;

        public bool IsValid { get; private set; } = true;

        public Rect GetEffectiveLocalRect(float extraPadding = 0f)
        {
            return GetEffectiveLocalRect(new Vector2(extraPadding, extraPadding));
        }

        public Rect GetEffectiveLocalRect(Vector2 extraPadding)
        {
            float paddingX = m_InnerPadding + extraPadding.x;
            float paddingY = m_InnerPadding + extraPadding.y;
            float w = Mathf.Max(0f, m_Size.x - paddingX * 2f);
            float h = Mathf.Max(0f, m_Size.y - paddingY * 2f);
            return new Rect(m_Center.x - w * 0.5f, m_Center.y - h * 0.5f, w, h);
        }

        public Vector2 GetLocalPoint(Vector3 worldPoint)
        {
            Vector3 local = transform.InverseTransformPoint(worldPoint);
            return new Vector2(local.x, local.y);
        }

        public Vector3 GetWorldPoint(Vector2 localPoint, float localZ = 0f)
        {
            return transform.TransformPoint(new Vector3(localPoint.x, localPoint.y, localZ));
        }

        private void Awake()
        {
            ValidateConfiguration(true);
        }

        private void OnEnable()
        {
            ValidateConfiguration(true);
        }

        public bool ValidateConfiguration(bool reportError)
        {
            SafeAreaError error = CheckForErrors();

            if (error != SafeAreaError.None)
            {
                IsValid = false;
                if (reportError && (!m_HasLoggedError || m_LastError != error))
                {
                    LogErrorForState(error);
                    m_HasLoggedError = true;
                    m_LastError = error;
                }
                return false;
            }

            IsValid = true;
            if (m_HasLoggedError)
            {
                m_HasLoggedError = false;
                m_LastError = SafeAreaError.None;
            }
            return true;
        }

        private SafeAreaError CheckForErrors()
        {
            if (float.IsNaN(m_Center.x) || float.IsInfinity(m_Center.x) ||
                float.IsNaN(m_Center.y) || float.IsInfinity(m_Center.y) ||
                float.IsNaN(m_Size.x) || float.IsInfinity(m_Size.x) ||
                float.IsNaN(m_Size.y) || float.IsInfinity(m_Size.y) ||
                float.IsNaN(m_InnerPadding) || float.IsInfinity(m_InnerPadding))
            {
                return SafeAreaError.InvalidNumber;
            }

            Vector3 scale = transform.lossyScale;
            if (float.IsNaN(scale.x) || float.IsInfinity(scale.x) ||
                float.IsNaN(scale.y) || float.IsInfinity(scale.y) ||
                float.IsNaN(scale.z) || float.IsInfinity(scale.z))
            {
                return SafeAreaError.InvalidNumber;
            }

            if (Mathf.Abs(scale.x - 1f) > 0.001f || Mathf.Abs(scale.y - 1f) > 0.001f || Mathf.Abs(scale.z - 1f) > 0.001f)
            {
                return SafeAreaError.InvalidScale;
            }

            if (m_Size.x <= 0f || m_Size.y <= 0f)
            {
                return SafeAreaError.InvalidSize;
            }

            if (m_InnerPadding < 0f || m_Size.x - m_InnerPadding * 2f <= 0f || m_Size.y - m_InnerPadding * 2f <= 0f)
            {
                return SafeAreaError.InvalidPadding;
            }

            return SafeAreaError.None;
        }

        private void LogErrorForState(SafeAreaError error)
        {
            switch (error)
            {
                case SafeAreaError.InvalidScale:
                    Debug.LogError($"[BackgroundCameraSafeArea] Safe Area와 부모 계층의 최종 lossyScale은 Epsilon 범위에서 (1,1,1)이어야 합니다. 현재: {transform.lossyScale}");
                    break;
                case SafeAreaError.InvalidSize:
                    Debug.LogError("[BackgroundCameraSafeArea] Size는 0보다 커야 합니다.");
                    break;
                case SafeAreaError.InvalidPadding:
                    Debug.LogError("[BackgroundCameraSafeArea] Inner Padding을 적용한 후의 영역 크기가 0 이하입니다.");
                    break;
                case SafeAreaError.InvalidNumber:
                    Debug.LogError("[BackgroundCameraSafeArea] Center, Size, InnerPadding 또는 Scale에 NaN이나 Infinity가 포함되어 있습니다.");
                    break;
            }
        }

#if UNITY_EDITOR
        private void OnValidate()
        {
            ValidateConfiguration(true);
        }

        private void OnDrawGizmosSelected()
        {
            Gizmos.matrix = transform.localToWorldMatrix;

            // Outer Rect
            Gizmos.color = new Color(0f, 1f, 1f, 0.8f);
            Vector3 center = new Vector3(m_Center.x, m_Center.y, 0f);
            Vector3 size = new Vector3(m_Size.x, m_Size.y, 0f);
            Gizmos.DrawWireCube(center, size);

            // Inner Rect (Padding applied)
            Gizmos.color = new Color(1f, 0f, 0f, 0.8f);
            float w = Mathf.Max(0f, m_Size.x - m_InnerPadding * 2f);
            float h = Mathf.Max(0f, m_Size.y - m_InnerPadding * 2f);
            Vector3 innerSize = new Vector3(w, h, 0f);
            Gizmos.DrawWireCube(center, innerSize);

            Gizmos.color = new Color(1f, 0f, 0f, 0.15f);
            Gizmos.DrawCube(center, innerSize);
        }
#endif
    }
}
#endif
