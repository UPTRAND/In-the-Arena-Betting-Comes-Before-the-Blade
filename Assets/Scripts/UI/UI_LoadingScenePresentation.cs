#if UNITY_6000_0_OR_NEWER
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace InTheArena.UI
{
    [DisallowMultipleComponent]
    public sealed class UI_LoadingScenePresentation : MonoBehaviour
    {
        private static readonly string[] Tips =
        {
            "배팅 전, 유닛 조합을 한 번 더 확인하세요.",
            "전투가 불리할 땐 아이템으로 흐름을 바꿔보세요.",
            "스테이지를 클리어하면 다음 도전이 열립니다.",
            "같은 유닛도 배치에 따라 전혀 다르게 싸웁니다.",
            "마지막 한 콜이 승부를 바꿉니다."
        };

        [SerializeField, Min(0f)] private float m_LineSpeed = 420f;
        [SerializeField] private RuntimeAnimatorController[] m_WalkControllers;

        private const float LineSpacing = 626.5f;
        private RectTransform[] m_LineTracks;
        private float m_TrackStartX;
        private float m_TrackWidth;
        private SpriteRenderer m_AnimatedSpriteSource;
        private Image m_UnitImage;

        private void Awake()
        {
            Transform progressBar = transform.Find("ProgressBarArea");
            if (progressBar != null)
            {
                progressBar.gameObject.SetActive(false);
            }

            SetRandomTip();
            CreateMovingLines();
            CreateWalkingUnit();
        }

        private void Update()
        {
            if (m_LineTracks == null) return;

            foreach (RectTransform lineTrack in m_LineTracks)
            {
                lineTrack.anchoredPosition += Vector2.left * (m_LineSpeed * Time.unscaledDeltaTime);
                while (lineTrack.anchoredPosition.x <= m_TrackStartX - m_TrackWidth)
                {
                    lineTrack.anchoredPosition += Vector2.right * (m_TrackWidth * 2f);
                }
            }
        }

        private void LateUpdate()
        {
            if (m_UnitImage != null && m_AnimatedSpriteSource != null)
            {
                m_UnitImage.sprite = m_AnimatedSpriteSource.sprite;
            }
        }

        private void SetRandomTip()
        {
            Transform tipGroup = transform.Find("TipGroup");
            if (tipGroup != null)
            {
                RectTransform tipRect = tipGroup as RectTransform;
                tipRect.anchorMin = tipRect.anchorMax = new Vector2(0.5f, 0f);
                tipRect.anchoredPosition = new Vector2(0f, 140f);
                tipRect.sizeDelta = new Vector2(900f, 100f);
            }

            TMP_Text tipContent = transform.Find("TipGroup/TipContentText")?.GetComponent<TMP_Text>();
            if (tipContent != null)
            {
                tipContent.text = Tips[Random.Range(0, Tips.Length)];
            }
        }

        private void CreateMovingLines()
        {
            RectTransform lineGroup = FindLineGroup(transform);
            if (lineGroup == null)
            {
                Debug.LogWarning("[LoadingPresentation] Line_Group을 찾지 못했습니다.");
                return;
            }

            AlignLinePositions(lineGroup);
            m_TrackStartX = lineGroup.anchoredPosition.x;
            m_TrackWidth = GetTrackWidth(lineGroup);
            if (m_TrackWidth <= 0f)
            {
                Debug.LogWarning("[LoadingPresentation] Line_Group의 트랙 폭이 올바르지 않습니다.");
                return;
            }

            RectTransform duplicate = Instantiate(lineGroup, lineGroup.parent);
            duplicate.name = "Line_Group_Repeat";
            duplicate.anchoredPosition = lineGroup.anchoredPosition + Vector2.right * m_TrackWidth;
            duplicate.SetSiblingIndex(lineGroup.GetSiblingIndex() + 1);
            m_LineTracks = new[] { lineGroup, duplicate };
        }

        private static float GetTrackWidth(RectTransform track)
        {
            if (track.childCount > 1)
            {
                return LineSpacing * track.childCount;
            }

            return Mathf.Max(track.rect.width, 1f);
        }

        private static void AlignLinePositions(RectTransform lineGroup)
        {
            int childCount = lineGroup.childCount;
            if (childCount == 0) return;

            float centerOffset = (childCount - 1) * 0.5f;
            for (int i = 0; i < childCount; i++)
            {
                if (lineGroup.GetChild(i) is RectTransform child)
                {
                    child.anchoredPosition = new Vector2((i - centerOffset) * LineSpacing, child.anchoredPosition.y);
                }
            }
        }

        private static RectTransform FindLineGroup(Transform root)
        {
            if (root.name == "Line_Group")
            {
                return root as RectTransform;
            }

            for (int i = 0; i < root.childCount; i++)
            {
                RectTransform result = FindLineGroup(root.GetChild(i));
                if (result != null)
                {
                    return result;
                }
            }

            return null;
        }

        private void CreateWalkingUnit()
        {
            if (m_WalkControllers == null || m_WalkControllers.Length == 0)
            {
                return;
            }

            GameObject source = new("LoadingWalkAnimationSource", typeof(SpriteRenderer), typeof(Animator));
            source.hideFlags = HideFlags.HideAndDontSave;
            source.transform.SetParent(transform, false);

            m_AnimatedSpriteSource = source.GetComponent<SpriteRenderer>();
            m_AnimatedSpriteSource.color = Color.white;
            m_AnimatedSpriteSource.forceRenderingOff = true;

            Animator animator = source.GetComponent<Animator>();
            animator.runtimeAnimatorController = m_WalkControllers[Random.Range(0, m_WalkControllers.Length)];
            animator.SetBool("IsMoving", true);

            GameObject unit = new("LoadingUnitSilhouette", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
            RectTransform lineGroup = FindLineGroup(transform);
            Transform unitParent = lineGroup != null ? lineGroup.parent : transform;
            unit.transform.SetParent(unitParent, false);
            unit.transform.SetSiblingIndex(lineGroup != null ? lineGroup.GetSiblingIndex() + 2 : unitParent.childCount - 1);

            RectTransform unitRect = unit.GetComponent<RectTransform>();
            unitRect.anchorMin = unitRect.anchorMax = new Vector2(0.5f, 0.5f);
            unitRect.pivot = new Vector2(0.5f, 0f);
            unitRect.anchoredPosition = new Vector2(0f, 10f);
            unitRect.sizeDelta = new Vector2(280f, 280f);

            m_UnitImage = unit.GetComponent<Image>();
            m_UnitImage.color = Color.white;
            m_UnitImage.preserveAspect = true;
            m_UnitImage.raycastTarget = false;
        }
    }
}
#endif
