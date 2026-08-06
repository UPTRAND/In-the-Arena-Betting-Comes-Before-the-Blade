using UnityEngine;

namespace InTheArena.Battlefield
{
    [DisallowMultipleComponent]
    [RequireComponent(typeof(BoxCollider))]
    public sealed class BattlefieldArea : MonoBehaviour
    {
        public static BattlefieldArea Active { get; private set; }

        [SerializeField] private BoxCollider m_AreaCollider;

        private void Reset()
        {
            m_AreaCollider = GetComponent<BoxCollider>();

            if (m_AreaCollider != null)
            {
                m_AreaCollider.isTrigger = true;
            }
        }

        private void Awake()
        {
            if (m_AreaCollider == null)
            {
                m_AreaCollider = GetComponent<BoxCollider>();
            }
        }

        private void OnEnable()
        {
            if (Active != null && Active != this)
            {
                Debug.LogError(
                    "[BattlefieldArea] 활성화된 전장 영역이 둘 이상입니다.",
                    this);

                enabled = false;
                return;
            }

            Active = this;
        }

        private void OnDisable()
        {
            if (Active == this)
            {
                Active = null;
            }
        }

        public Vector3 ClampPosition(
            Vector3 worldPosition,
            float worldRadius)
        {
            if (m_AreaCollider == null)
            {
                return worldPosition;
            }

            Transform areaTransform = m_AreaCollider.transform;
            Vector3 localPosition =
                areaTransform.InverseTransformPoint(worldPosition);

            Vector3 center = m_AreaCollider.center;
            Vector3 extents = m_AreaCollider.size * 0.5f;
            Vector3 scale = areaTransform.lossyScale;

            float safeRadius = Mathf.Max(0f, worldRadius);

            float localRadiusX =
                safeRadius / Mathf.Max(Mathf.Abs(scale.x), 0.0001f);

            float localRadiusZ =
                safeRadius / Mathf.Max(Mathf.Abs(scale.z), 0.0001f);

            float allowedHalfX =
                Mathf.Max(0f, extents.x - localRadiusX);

            float allowedHalfZ =
                Mathf.Max(0f, extents.z - localRadiusZ);

            localPosition.x = Mathf.Clamp(
                localPosition.x,
                center.x - allowedHalfX,
                center.x + allowedHalfX);

            localPosition.z = Mathf.Clamp(
                localPosition.z,
                center.z - allowedHalfZ,
                center.z + allowedHalfZ);

            Vector3 result =
                areaTransform.TransformPoint(localPosition);

            // 현재 전장이 수평면이라는 전제
            result.y = worldPosition.y;

            return result;
        }

#if UNITY_EDITOR
        private void OnDrawGizmosSelected()
        {
            if (m_AreaCollider == null)
            {
                m_AreaCollider = GetComponent<BoxCollider>();
            }

            if (m_AreaCollider == null)
            {
                return;
            }

            Gizmos.matrix = m_AreaCollider.transform.localToWorldMatrix;
            Gizmos.DrawWireCube(
                m_AreaCollider.center,
                m_AreaCollider.size);
        }
#endif
    }
}