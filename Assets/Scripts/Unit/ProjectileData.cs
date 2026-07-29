#if UNITY_6000_0_OR_NEWER
using UnityEngine;

namespace InTheArena.Unit
{
    public enum ProjectileOrientationMode
    {
        FaceVelocity = 0,
        FullBillboard = 1,
        Fixed = 2
    }

    [CreateAssetMenu(
        fileName = "ProjectileData_",
        menuName = "In The Arena/Unit/Basic Attack/Projectile Data",
        order = 1)]
    public sealed class ProjectileData : ScriptableObject
    {
        [Header("프리팹")]
        [SerializeField] private GameObject m_Prefab;

        [Header("이동")]
        [SerializeField, Min(0.1f)] private float m_Speed = 12f;
        [SerializeField, Min(0.1f)] private float m_Lifetime = 5f;
        [SerializeField, Min(0.01f)] private float m_HitDistance = 0.2f;
        [SerializeField] private ProjectileOrientationMode m_OrientationMode =
            ProjectileOrientationMode.FaceVelocity;

        [Header("선택적 애니메이션")]
        [Tooltip("비어 있거나 Animator가 없으면 재생하지 않습니다.")]
        [SerializeField] private string m_FlightStateName;
        [Tooltip("비어 있거나 Animator가 없으면 적중 즉시 반환합니다.")]
        [SerializeField] private string m_ImpactStateName;
        [SerializeField, Min(0f)] private float m_ImpactPresentationDuration;

        public GameObject Prefab => m_Prefab;
        public float Speed => Mathf.Max(0.1f, m_Speed);
        public float Lifetime => Mathf.Max(0.1f, m_Lifetime);
        public float HitDistance => Mathf.Max(0.01f, m_HitDistance);
        public ProjectileOrientationMode OrientationMode => m_OrientationMode;
        public string FlightStateName => m_FlightStateName;
        public string ImpactStateName => m_ImpactStateName;
        public float ImpactPresentationDuration => Mathf.Max(0f, m_ImpactPresentationDuration);

        public bool IsValid()
        {
            bool valid = true;
            if (m_Prefab == null)
            {
                Debug.LogError($"[ProjectileData] {name}: 투사체 프리팹이 없습니다.", this);
                return false;
            }
            if (m_Prefab.GetComponent<Projectile>() == null)
            {
                Debug.LogError($"[ProjectileData] {name}: 프리팹에 Projectile 컴포넌트가 없습니다.", this);
                valid = false;
            }
            if (m_Prefab.GetComponent<PoolMember>() == null)
            {
                Debug.LogError($"[ProjectileData] {name}: 프리팹에 PoolMember 컴포넌트가 없습니다.", this);
                valid = false;
            }

            Animator animator = m_Prefab.GetComponentInChildren<Animator>(true);
            if (animator == null &&
                (!string.IsNullOrWhiteSpace(m_FlightStateName) ||
                 !string.IsNullOrWhiteSpace(m_ImpactStateName)))
            {
                Debug.LogWarning(
                    $"[ProjectileData] {name}: 애니메이션 상태가 설정됐지만 Animator가 없습니다.",
                    this);
            }
            return valid;
        }

#if UNITY_EDITOR
        public void ConfigureForEditor(
            GameObject prefab,
            float speed,
            float lifetime,
            float hitDistance,
            ProjectileOrientationMode orientationMode,
            string flightStateName = null,
            string impactStateName = null,
            float impactPresentationDuration = 0f)
        {
            m_Prefab = prefab;
            m_Speed = speed;
            m_Lifetime = lifetime;
            m_HitDistance = hitDistance;
            m_OrientationMode = orientationMode;
            m_FlightStateName = flightStateName;
            m_ImpactStateName = impactStateName;
            m_ImpactPresentationDuration = impactPresentationDuration;
        }

        private void OnValidate()
        {
            m_Speed = Mathf.Max(0.1f, m_Speed);
            m_Lifetime = Mathf.Max(0.1f, m_Lifetime);
            m_HitDistance = Mathf.Max(0.01f, m_HitDistance);
            m_ImpactPresentationDuration = Mathf.Max(0f, m_ImpactPresentationDuration);
        }
#endif
    }
}
#endif
