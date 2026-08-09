#if UNITY_6000_0_OR_NEWER
using UnityEngine;
using UnityEngine.UI;

namespace InTheArena.UI
{
    public enum CombatItemRangeShape
    {
        Circle,
        Rectangle
    }

    /// <summary>
    /// 코드로 그리는 전투 아이템 범위 표시입니다. 입력을 가로채지 않도록
    /// MaskableGraphic을 사용하되 raycastTarget은 항상 비활성화합니다.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class UI_CombatItemRangeIndicator : MaskableGraphic
    {
        private const int CircleSegments = 64;

        [SerializeField] private CombatItemRangeShape m_Shape = CombatItemRangeShape.Circle;
        [SerializeField] private Color m_FillColor = new Color(0.2f, 0.9f, 0.35f, 0.18f);
        [SerializeField] private Color m_OutlineColor = new Color(0.35f, 1f, 0.55f, 0.85f);
        [SerializeField] [Min(0.5f)] private float m_OutlineThickness = 3f;

        protected override void Awake()
        {
            base.Awake();
            raycastTarget = false;
        }

        public void Configure(
            CombatItemRangeShape shape,
            Color fillColor,
            Color outlineColor,
            float outlineThickness = 3f)
        {
            m_Shape = shape;
            m_FillColor = fillColor;
            m_OutlineColor = outlineColor;
            m_OutlineThickness = Mathf.Max(0.5f, outlineThickness);
            raycastTarget = false;
            SetVerticesDirty();
        }

        protected override void OnPopulateMesh(VertexHelper vertexHelper)
        {
            vertexHelper.Clear();

            Rect rect = rectTransform.rect;
            if (rect.width <= 0f || rect.height <= 0f)
            {
                return;
            }

            if (m_Shape == CombatItemRangeShape.Circle)
            {
                PopulateCircle(vertexHelper, rect);
            }
            else
            {
                PopulateRectangle(vertexHelper, rect);
            }
        }

        private void PopulateCircle(VertexHelper vertexHelper, Rect rect)
        {
            Vector2 center = rect.center;
            float radiusX = rect.width * 0.5f;
            float radiusY = rect.height * 0.5f;
            float innerRadiusX = Mathf.Max(0f, radiusX - m_OutlineThickness);
            float innerRadiusY = Mathf.Max(0f, radiusY - m_OutlineThickness);

            AddVertex(vertexHelper, center, m_FillColor);
            for (int i = 0; i <= CircleSegments; i++)
            {
                AddVertex(
                    vertexHelper,
                    center + PointOnEllipse(radiusX, radiusY, i),
                    m_FillColor);
            }

            for (int i = 0; i < CircleSegments; i++)
            {
                int current = 1 + i;
                int next = current + 1;
                vertexHelper.AddTriangle(0, current, next);
            }

            int ringStart = vertexHelper.currentVertCount;
            for (int i = 0; i <= CircleSegments; i++)
            {
                Vector2 direction = PointOnEllipse(1f, 1f, i);
                AddVertex(
                    vertexHelper,
                    center + new Vector2(direction.x * innerRadiusX, direction.y * innerRadiusY),
                    m_OutlineColor);
                AddVertex(
                    vertexHelper,
                    center + new Vector2(direction.x * radiusX, direction.y * radiusY),
                    m_OutlineColor);
            }

            for (int i = 0; i < CircleSegments; i++)
            {
                int current = ringStart + i * 2;
                int next = current + 2;
                AddQuad(vertexHelper, current, next, next + 1, current + 1);
            }
        }

        private void PopulateRectangle(VertexHelper vertexHelper, Rect rect)
        {
            AddQuad(
                vertexHelper,
                rect.min,
                new Vector2(rect.xMax, rect.yMin),
                rect.max,
                new Vector2(rect.xMin, rect.yMax),
                m_FillColor);

            float thickness = Mathf.Min(
                m_OutlineThickness,
                Mathf.Min(rect.width, rect.height) * 0.5f);
            Rect inner = new Rect(
                rect.xMin + thickness,
                rect.yMin + thickness,
                Mathf.Max(0f, rect.width - thickness * 2f),
                Mathf.Max(0f, rect.height - thickness * 2f));

            AddQuad(vertexHelper,
                new Vector2(rect.xMin, rect.yMin),
                new Vector2(rect.xMax, rect.yMin),
                new Vector2(inner.xMax, inner.yMin),
                new Vector2(inner.xMin, inner.yMin),
                m_OutlineColor);
            AddQuad(vertexHelper,
                new Vector2(inner.xMin, inner.yMax),
                new Vector2(inner.xMax, inner.yMax),
                new Vector2(rect.xMax, rect.yMax),
                new Vector2(rect.xMin, rect.yMax),
                m_OutlineColor);
            AddQuad(vertexHelper,
                new Vector2(rect.xMin, rect.yMin),
                new Vector2(inner.xMin, inner.yMin),
                new Vector2(inner.xMin, inner.yMax),
                new Vector2(rect.xMin, rect.yMax),
                m_OutlineColor);
            AddQuad(vertexHelper,
                new Vector2(inner.xMax, inner.yMin),
                new Vector2(rect.xMax, rect.yMin),
                new Vector2(rect.xMax, rect.yMax),
                new Vector2(inner.xMax, inner.yMax),
                m_OutlineColor);
        }

        private static Vector2 PointOnEllipse(float radiusX, float radiusY, int index)
        {
            float angle = index * Mathf.PI * 2f / CircleSegments;
            return new Vector2(
                Mathf.Cos(angle) * radiusX,
                Mathf.Sin(angle) * radiusY);
        }

        private static void AddVertex(VertexHelper vertexHelper, Vector2 position, Color color)
        {
            UIVertex vertex = UIVertex.simpleVert;
            vertex.position = position;
            vertex.color = color;
            vertexHelper.AddVert(vertex);
        }

        private static void AddQuad(
            VertexHelper vertexHelper,
            int bottomLeft,
            int bottomRight,
            int topRight,
            int topLeft)
        {
            vertexHelper.AddTriangle(bottomLeft, bottomRight, topRight);
            vertexHelper.AddTriangle(topRight, topLeft, bottomLeft);
        }

        private static void AddQuad(
            VertexHelper vertexHelper,
            Vector2 bottomLeft,
            Vector2 bottomRight,
            Vector2 topRight,
            Vector2 topLeft,
            Color color)
        {
            int start = vertexHelper.currentVertCount;
            AddVertex(vertexHelper, bottomLeft, color);
            AddVertex(vertexHelper, bottomRight, color);
            AddVertex(vertexHelper, topRight, color);
            AddVertex(vertexHelper, topLeft, color);
            vertexHelper.AddTriangle(start, start + 1, start + 2);
            vertexHelper.AddTriangle(start + 2, start + 3, start);
        }
    }
}
#endif
