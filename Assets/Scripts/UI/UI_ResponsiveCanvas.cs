using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 기준 해상도 안의 UI를 안전 영역에 맞추고 남는 공간은 앵커로 확장합니다.
/// 기기별 해상도 목록 없이 화면 크기·회전·노치 변화에 대응합니다.
/// </summary>
[DisallowMultipleComponent]
[RequireComponent(typeof(Canvas))]
public sealed class UI_ResponsiveCanvas : CanvasScaler
{
    [Header("Safe Area")]
    [Tooltip("화면 UI의 직계 자식에 안전 영역을 적용합니다. 월드 좌표 표시용 Canvas에서는 끕니다.")]
    [SerializeField] private bool m_ApplySafeAreaToChildren = true;

    private readonly Dictionary<RectTransform, Rect> m_OriginalAnchors =
        new Dictionary<RectTransform, Rect>();

    private Canvas m_TargetCanvas;

    private readonly List<RectTransform> m_RemovedChildren = new List<RectTransform>();

    /// <summary>스케일과 안전 영역 정책을 설정합니다. 기준 크기는 UI의 제작 해상도입니다.</summary>
    public void Configure(Vector2 designResolution, bool applySafeArea)
    {
        RestoreAnchors();
        referenceResolution = designResolution;
        uiScaleMode = ScaleMode.ScaleWithScreenSize;
        screenMatchMode = ScreenMatchMode.Expand;
        m_ApplySafeAreaToChildren = applySafeArea;
        Handle();
    }

    /// <summary>안전 영역에 제작 화면 전체가 들어가는 균일 배율을 계산합니다.</summary>
    public static float CalculateScale(Vector2 designResolution, Rect availableArea)
    {
        float width = Mathf.Max(1f, designResolution.x);
        float height = Mathf.Max(1f, designResolution.y);

        return Mathf.Max(0.0001f, Mathf.Min(
            availableArea.width / width,
            availableArea.height / height));
    }

    /// <summary>일시적으로 잘못된 안전 영역이 들어와도 화면 내부의 유효 영역을 반환합니다.</summary>
    public static Rect ClampSafeArea(Vector2 screenSize, Rect safeArea)
    {
        Rect screen = new Rect(0f, 0f, Mathf.Max(1f, screenSize.x), Mathf.Max(1f, screenSize.y));
        float left = Mathf.Clamp(safeArea.xMin, 0f, screen.width);
        float right = Mathf.Clamp(safeArea.xMax, left, screen.width);
        float bottom = Mathf.Clamp(safeArea.yMin, 0f, screen.height);
        float top = Mathf.Clamp(safeArea.yMax, bottom, screen.height);

        if (right <= left || top <= bottom ||
            float.IsNaN(left + right + bottom + top))
        {
            return screen;
        }

        return Rect.MinMaxRect(left, bottom, right, top);
    }

    /// <summary>Canvas의 렌더 직전에 배율과 앵커를 함께 갱신합니다.</summary>
    protected override void HandleScaleWithScreenSize()
    {
        if (m_TargetCanvas == null)
        {
            m_TargetCanvas = GetComponent<Canvas>();
        }

        Vector2 screenSize = m_TargetCanvas.renderingDisplaySize;
        Rect safeArea = new Rect(0f, 0f, screenSize.x, screenSize.y);

        // 보조 디스플레이와 에디터 편집 화면에는 주 디스플레이의 노치를 적용하지 않습니다.
        if (Application.isPlaying && m_TargetCanvas.targetDisplay == 0)
        {
            Rect physicalSafeArea = Screen.safeArea;
            Vector2 physicalScale = new Vector2(
                screenSize.x / Mathf.Max(1, Screen.width),
                screenSize.y / Mathf.Max(1, Screen.height));
            safeArea = ClampSafeArea(screenSize, new Rect(
                Vector2.Scale(physicalSafeArea.position, physicalScale),
                Vector2.Scale(physicalSafeArea.size, physicalScale)));
        }

        SetScaleFactor(CalculateScale(referenceResolution, safeArea));
        SetReferencePixelsPerUnit(referencePixelsPerUnit);

        if (Application.isPlaying && m_ApplySafeAreaToChildren)
        {
            ApplySafeArea(screenSize, safeArea);
        }
    }

    /// <summary>기존 앵커의 의미를 유지한 채 안전 영역의 정규화 좌표로 변환합니다.</summary>
    private void ApplySafeArea(Vector2 screenSize, Rect safeArea)
    {
        RemoveDetachedChildren();

        Vector2 minimum = new Vector2(
            safeArea.xMin / Mathf.Max(1f, screenSize.x),
            safeArea.yMin / Mathf.Max(1f, screenSize.y));
        Vector2 size = new Vector2(
            safeArea.width / Mathf.Max(1f, screenSize.x),
            safeArea.height / Mathf.Max(1f, screenSize.y));

        for (int i = 0; i < transform.childCount; i++)
        {
            RectTransform child = transform.GetChild(i) as RectTransform;
            if (child == null)
            {
                continue;
            }

            if (!m_OriginalAnchors.TryGetValue(child, out Rect anchors))
            {
                anchors = Rect.MinMaxRect(
                    child.anchorMin.x, child.anchorMin.y,
                    child.anchorMax.x, child.anchorMax.y);
                m_OriginalAnchors.Add(child, anchors);
            }

            child.anchorMin = minimum + Vector2.Scale(anchors.min, size);
            child.anchorMax = minimum + Vector2.Scale(anchors.max, size);
        }
    }

    /// <summary>풀로 반환되거나 부모가 바뀐 UI의 앵커를 복구하고 캐시에서 제거합니다.</summary>
    private void RemoveDetachedChildren()
    {
        m_RemovedChildren.Clear();

        foreach (KeyValuePair<RectTransform, Rect> entry in m_OriginalAnchors)
        {
            if (entry.Key != null && entry.Key.parent == transform)
            {
                continue;
            }

            if (entry.Key != null)
            {
                entry.Key.anchorMin = entry.Value.min;
                entry.Key.anchorMax = entry.Value.max;
            }

            m_RemovedChildren.Add(entry.Key);
        }

        for (int i = 0; i < m_RemovedChildren.Count; i++)
        {
            m_OriginalAnchors.Remove(m_RemovedChildren[i]);
        }
    }

    /// <summary>다른 캔버스가 캐시하기 전에 분리된 자식의 원래 앵커를 복원합니다.</summary>
    private void OnTransformChildrenChanged()
    {
        RemoveDetachedChildren();
    }

    /// <summary>컴포넌트 해제나 정책 변경 시 제작 시점의 앵커로 되돌립니다.</summary>
    private void RestoreAnchors()
    {
        foreach (KeyValuePair<RectTransform, Rect> entry in m_OriginalAnchors)
        {
            if (entry.Key != null && entry.Key.parent == transform)
            {
                entry.Key.anchorMin = entry.Value.min;
                entry.Key.anchorMax = entry.Value.max;
            }
        }

        m_OriginalAnchors.Clear();
    }

    /// <summary>안전 영역 변경을 되돌리고 CanvasScaler의 렌더 이벤트를 해제합니다.</summary>
    protected override void OnDisable()
    {
        RestoreAnchors();
        base.OnDisable();
    }
}
