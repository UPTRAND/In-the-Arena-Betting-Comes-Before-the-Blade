#if UNITY_6000_0_OR_NEWER
using UnityEngine;
using UnityCamera = UnityEngine.Camera;

namespace InTheArena.Camera
{
    /// <summary>
    /// 실제 게임 전투 영역이 노출되는 뷰포트 Rect를 제공합니다.
    /// UI가 화면 일부를 가릴 경우, 그 부분을 제외한 실제 노출 영역을 카메라의 Normalized 좌표(0~1)로 변환하여 제공합니다.
    /// 계산된 Rect는 이전 값과 Epsilon 비교를 통해 불필요한 캐시 무효화를 방지합니다.
    /// </summary>
    public class CameraViewportProvider : MonoBehaviour
    {
        [Tooltip("실제 전투가 보여지는 UI 영역 (비워두면 카메라 전체 화면 사용)")]
        [SerializeField] private RectTransform m_UIRect;

        [SerializeField] private UnityCamera m_Camera;

        private Rect m_CachedViewport = new Rect(0, 0, 1, 1);
        private Vector3[] m_CornersCache = new Vector3[4];

        private const float ViewportEpsilon = 0.001f;
        private bool m_HasReportedError = false;

        private void Awake()
        {
            if (m_Camera == null)
                m_Camera = GetComponent<UnityCamera>();
        }

#if UNITY_EDITOR
        private void Reset()
        {
            m_Camera = GetComponent<UnityCamera>();
        }
#endif

        public Rect GetEffectiveViewportRect()
        {
            if (m_Camera == null || m_UIRect == null)
                return new Rect(0, 0, 1, 1);

            Rect newViewport = CalculateViewport();

            if (Mathf.Abs(m_CachedViewport.x - newViewport.x) > ViewportEpsilon ||
                Mathf.Abs(m_CachedViewport.y - newViewport.y) > ViewportEpsilon ||
                Mathf.Abs(m_CachedViewport.width - newViewport.width) > ViewportEpsilon ||
                Mathf.Abs(m_CachedViewport.height - newViewport.height) > ViewportEpsilon)
            {
                m_CachedViewport = newViewport;
            }

            return m_CachedViewport;
        }

        private Rect CalculateViewport()
        {
            Canvas canvas = m_UIRect.GetComponentInParent<Canvas>();
            if (canvas == null)
                return new Rect(0, 0, 1, 1);

            m_UIRect.GetWorldCorners(m_CornersCache);

            Vector2 minScreen = new Vector2(float.MaxValue, float.MaxValue);
            Vector2 maxScreen = new Vector2(float.MinValue, float.MinValue);

            for (int i = 0; i < 4; i++)
            {
                Vector3 screenPt;
                if (canvas.renderMode == RenderMode.ScreenSpaceOverlay)
                {
                    screenPt = m_CornersCache[i];
                }
                else
                {
                    UnityCamera uiCam = canvas.worldCamera != null ? canvas.worldCamera : m_Camera;
                    screenPt = uiCam.WorldToScreenPoint(m_CornersCache[i]);
                }

                minScreen.x = Mathf.Min(minScreen.x, screenPt.x);
                maxScreen.x = Mathf.Max(maxScreen.x, screenPt.x);
                minScreen.y = Mathf.Min(minScreen.y, screenPt.y);
                maxScreen.y = Mathf.Max(maxScreen.y, screenPt.y);
            }

            return NormalizeScreenRect(minScreen, maxScreen, m_Camera.pixelRect);
        }

        public static Rect NormalizeScreenRect(Vector2 minScreen, Vector2 maxScreen, Rect pixelRect)
        {
            if (pixelRect.width <= 0f || pixelRect.height <= 0f ||
                float.IsNaN(pixelRect.width) || float.IsInfinity(pixelRect.width) ||
                float.IsNaN(pixelRect.height) || float.IsInfinity(pixelRect.height) ||
                float.IsNaN(minScreen.x) || float.IsInfinity(minScreen.x) ||
                float.IsNaN(minScreen.y) || float.IsInfinity(minScreen.y) ||
                float.IsNaN(maxScreen.x) || float.IsInfinity(maxScreen.x) ||
                float.IsNaN(maxScreen.y) || float.IsInfinity(maxScreen.y))
            {
                return new Rect(0, 0, 1, 1);
            }

            // UI 화면 좌표를 카메라의 normalized viewport 좌표로 변환
            float nXMin = (minScreen.x - pixelRect.xMin) / pixelRect.width;
            float nXMax = (maxScreen.x - pixelRect.xMin) / pixelRect.width;
            float nYMin = (minScreen.y - pixelRect.yMin) / pixelRect.height;
            float nYMax = (maxScreen.y - pixelRect.yMin) / pixelRect.height;

            // 카메라 렌더 영역과의 교차 영역으로 클램핑
            nXMin = Mathf.Clamp01(nXMin);
            nXMax = Mathf.Clamp01(nXMax);
            nYMin = Mathf.Clamp01(nYMin);
            nYMax = Mathf.Clamp01(nYMax);

            Rect result = new Rect(nXMin, nYMin, nXMax - nXMin, nYMax - nYMin);

            if (result.width <= 0 || result.height <= 0 || float.IsNaN(result.width) || float.IsNaN(result.height))
            {
                return new Rect(0, 0, 1, 1);
            }

            return result;
        }
    }
}
#endif
