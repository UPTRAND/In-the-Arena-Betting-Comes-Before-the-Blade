using UnityEngine;
using UnityEngine.UI;

namespace InTheArena.UI
{
    public sealed class LobbyNpcView : MonoBehaviour
    {
        public string NpcId { get; private set; }
        public LobbyNpcMotion Motion { get; private set; }
        public Vector3 HeadWorldPosition
        {
            get
            {
                if (m_Image == null)
                {
                    return transform.position;
                }

                // 스프라이트의 피벗과 좌우 반전 여부에 관계없이 실제 상단 중앙을 사용합니다.
                m_Image.rectTransform.GetWorldCorners(m_WorldCorners);
                return (m_WorldCorners[1] + m_WorldCorners[2]) * 0.5f;
            }
        }

        private readonly Vector3[] m_WorldCorners = new Vector3[4];

        private LobbyNpcVisualData m_Visual;
        private RectTransform m_Root;
        private Image m_Image;
        private float m_AnimationTime;
        private bool m_Walking;

        public void Initialize(LobbyNpcDefinition definition, LobbyNpcMotion motion)
        {
            NpcId = definition.Id;
            Motion = motion;
            m_Visual = definition.Visual;
            m_Root = (RectTransform)transform;
            m_Root.anchorMin = m_Root.anchorMax = new Vector2(.5f, .5f);
            m_Root.sizeDelta = Vector2.zero;
            var visual = new GameObject("Visual", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
            visual.transform.SetParent(transform, false);
            m_Image = visual.GetComponent<Image>();
            m_Image.raycastTarget = false;
            m_Image.rectTransform.pivot = Vector2.zero;
            m_AnimationTime = UnityEngine.Random.value * m_Visual.Idle.Duration;
            Present(0f);
        }

        public void Present(float dt)
        {
            if (m_Walking != Motion.Walking) { m_Walking = Motion.Walking; m_AnimationTime = 0; }
            var animation = m_Walking ? m_Visual.Walk : m_Visual.Idle;
            m_AnimationTime = Mathf.Repeat(m_AnimationTime + dt, animation.Duration);
            Sprite sprite = animation.Sample(m_AnimationTime);
            m_Image.sprite = sprite;
            float flip = Motion.FacingRight == m_Visual.FacesRight ? 1f : -1f;
            m_Image.rectTransform.sizeDelta = sprite.rect.size * m_Visual.PixelScale;
            m_Image.rectTransform.anchoredPosition = new Vector2(-m_Visual.FootPixel.x * flip, -m_Visual.FootPixel.y) * m_Visual.PixelScale;
            m_Image.rectTransform.localScale = new Vector3(flip, 1f, 1f);
            m_Root.anchoredPosition = Motion.Position;
        }
    }
}
