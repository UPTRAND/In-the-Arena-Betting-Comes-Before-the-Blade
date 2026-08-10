#if UNITY_6000_0_OR_NEWER
using System.Threading;
using DG.Tweening;
using InTheArena.MainGame;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace InTheArena.UI
{
    [DisallowMultipleComponent]
    public sealed class UI_StageIntro : MonoBehaviour
    {
        private const float LeadInDuration = 0.2f;
        private const float SwordMoveDuration = 0.5f;
        private const float EmblemViewportWidthRatio = 0.55f;
        private const float CompositeWidth = 244f;
        private const float CompositeHeight = 200f;
        private const float SwordSize = 196f;
        private const float ShieldSize = 128f;
        private const string CameraAreaReferenceName = "MainCameraArea_Spacer";

        [Header("Layout")]
        [SerializeField] private RectTransform m_VisualRoot;
        [SerializeField] private RectTransform m_CameraArea;
        [SerializeField] private RectTransform m_InfoArea;
        [SerializeField] private RectTransform m_EmblemRoot;

        [Header("Emblem")]
        [SerializeField] private RectTransform m_RedSwordRect;
        [SerializeField] private RectTransform m_BlueSwordRect;
        [SerializeField] private RectTransform m_ShieldRect;
        [Tooltip("합체 상태에서 각 칼 중심이 방패 중심으로부터 떨어지는 거리입니다. 낮을수록 중앙에 모입니다.")]
        [SerializeField, Min(0f)] private float m_SwordHorizontalOffset = 0f;

        [Header("Stage Information")]
        [SerializeField] private GameObject m_InfoRoot;
        [SerializeField] private TMP_Text m_StageText;
        [SerializeField] private TMP_Text m_RoundText;
        [SerializeField] private TMP_Text m_TargetCallText;

        [Header("Input Blocker")]
        [SerializeField] private GameObject m_InputBlocker;
        [SerializeField] private Button m_InputButton;

        private Sequence m_Sequence;
        private AwaitableCompletionSource m_TapCompletionSource;
        private Vector2 m_RedTargetPosition;
        private Vector2 m_BlueTargetPosition;
        private bool m_CanAcceptTap;
        private bool m_TapAccepted;

        private void Awake()
        {
            if (m_InputButton != null)
            {
                m_InputButton.onClick.AddListener(HandleIntroTapped);
            }
        }

        public void Prime(StageData stageData)
        {
            CancelPendingAnimation();
            ApplyStageCopy(stageData);

            if (m_VisualRoot != null)
            {
                m_VisualRoot.gameObject.SetActive(true);
            }

            if (m_InputBlocker != null)
            {
                m_InputBlocker.SetActive(true);
            }

            if (m_InputButton != null)
            {
                m_InputButton.interactable = false;
            }

            if (m_InfoRoot != null)
            {
                m_InfoRoot.SetActive(false);
            }

            m_CanAcceptTap = false;
            m_TapAccepted = false;

            Canvas.ForceUpdateCanvases();
            LayoutAgainstCameraViewport();
            PlaceSwordsOutsideViewport();
        }

        public async Awaitable PlayAsync(StageData stageData, CancellationToken token)
        {
            if (m_VisualRoot == null || !m_VisualRoot.gameObject.activeSelf)
            {
                Prime(stageData);
            }

            token.ThrowIfCancellationRequested();
            m_Sequence?.Kill();

            m_Sequence = DOTween.Sequence()
                .SetUpdate(true)
                .SetLink(gameObject, LinkBehaviour.KillOnDestroy);
            m_Sequence.AppendInterval(LeadInDuration);
            m_Sequence.Append(m_RedSwordRect.DOAnchorPos(m_RedTargetPosition, SwordMoveDuration)
                .SetEase(Ease.OutCubic));
            m_Sequence.Join(m_BlueSwordRect.DOAnchorPos(m_BlueTargetPosition, SwordMoveDuration)
                .SetEase(Ease.OutCubic));

            using (token.Register(CancelPendingAnimation))
            {
                await m_Sequence.AsyncWaitForCompletion();
            }

            token.ThrowIfCancellationRequested();
            m_Sequence = null;

            if (m_InfoRoot != null)
            {
                m_InfoRoot.SetActive(true);
            }

            m_CanAcceptTap = true;
            if (m_InputButton != null)
            {
                m_InputButton.interactable = true;
            }

            m_TapCompletionSource = new AwaitableCompletionSource();
            using (token.Register(() => m_TapCompletionSource?.TrySetResult()))
            {
                await m_TapCompletionSource.Awaitable;
            }

            token.ThrowIfCancellationRequested();
        }

        public void ReleaseAfterBettingReveal()
        {
            m_CanAcceptTap = false;
            m_TapAccepted = false;
            m_TapCompletionSource = null;

            if (m_InputButton != null)
            {
                m_InputButton.interactable = false;
            }

            if (m_InputBlocker != null)
            {
                m_InputBlocker.SetActive(false);
            }

            if (m_VisualRoot != null)
            {
                m_VisualRoot.gameObject.SetActive(false);
            }
        }

        public static void GetStageCopy(
            StageData stageData,
            out string stageText,
            out string roundText,
            out string targetCallText)
        {
            int stageNumber = stageData != null ? stageData.StageNum : 0;
            int roundCount = stageData != null ? stageData.TotalRounds : 0;
            int targetCall = stageData != null ? stageData.TargetCall : 0;

            stageText = $"스테이지 {stageNumber}";
            roundText = $"라운드 횟수 {roundCount}";
            targetCallText = $"목표 콜 {targetCall}";
        }

        private void ApplyStageCopy(StageData stageData)
        {
            GetStageCopy(stageData, out string stage, out string rounds, out string targetCall);
            if (m_StageText != null) m_StageText.text = stage;
            if (m_RoundText != null) m_RoundText.text = rounds;
            if (m_TargetCallText != null) m_TargetCallText.text = targetCall;
        }

        private void LayoutAgainstCameraViewport()
        {
            Rect viewport;
            if (!TryGetPlannedCameraViewport(out viewport))
            {
                viewport = UnityEngine.Camera.main != null
                    ? UnityEngine.Camera.main.rect
                    : new Rect(0f, 0.375f, 1f, 0.5f);
            }

            if (viewport.width <= 0f || viewport.height <= 0f)
            {
                viewport = new Rect(0f, 0.375f, 1f, 0.5f);
            }

            viewport.xMin = Mathf.Clamp01(viewport.xMin);
            viewport.xMax = Mathf.Clamp01(viewport.xMax);
            viewport.yMin = Mathf.Clamp01(viewport.yMin);
            viewport.yMax = Mathf.Clamp01(viewport.yMax);

            SetStretchAnchors(
                m_CameraArea,
                new Vector2(viewport.xMin, viewport.yMin),
                new Vector2(viewport.xMax, viewport.yMax));
            SetStretchAnchors(
                m_InfoArea,
                Vector2.zero,
                new Vector2(1f, viewport.yMin));

            Canvas.ForceUpdateCanvases();
            float emblemWidth = Mathf.Max(1f, m_CameraArea.rect.width * EmblemViewportWidthRatio);
            float scale = emblemWidth / CompositeWidth;
            m_EmblemRoot.sizeDelta = new Vector2(emblemWidth, CompositeHeight * scale);

            m_RedSwordRect.sizeDelta = Vector2.one * (SwordSize * scale);
            m_BlueSwordRect.sizeDelta = Vector2.one * (SwordSize * scale);
            m_ShieldRect.sizeDelta = Vector2.one * (ShieldSize * scale);

            m_RedTargetPosition = new Vector2(-m_SwordHorizontalOffset * scale, -2f * scale);
            m_BlueTargetPosition = new Vector2(m_SwordHorizontalOffset * scale, 0f);
            m_ShieldRect.anchoredPosition = Vector2.zero;
        }

        private static bool TryGetPlannedCameraViewport(out Rect viewport)
        {
            viewport = default;
            if (Screen.width <= 0 || Screen.height <= 0) return false;

            RectTransform[] rectTransforms = Resources.FindObjectsOfTypeAll<RectTransform>();
            for (int i = 0; i < rectTransforms.Length; i++)
            {
                RectTransform candidate = rectTransforms[i];
                if (candidate == null ||
                    candidate.name != CameraAreaReferenceName ||
                    !candidate.gameObject.scene.IsValid())
                {
                    continue;
                }

                Vector3[] corners = new Vector3[4];
                candidate.GetWorldCorners(corners);
                Vector2 min = RectTransformUtility.WorldToScreenPoint(null, corners[0]);
                Vector2 max = RectTransformUtility.WorldToScreenPoint(null, corners[2]);
                viewport = Rect.MinMaxRect(
                    min.x / Screen.width,
                    min.y / Screen.height,
                    max.x / Screen.width,
                    max.y / Screen.height);
                return viewport.width > 0f && viewport.height > 0f;
            }

            return false;
        }

        private void PlaceSwordsOutsideViewport()
        {
            float halfCameraWidth = m_CameraArea.rect.width * 0.5f;
            float halfSwordWidth = m_RedSwordRect.rect.width * 0.5f;
            m_RedSwordRect.anchoredPosition = new Vector2(
                -halfCameraWidth - halfSwordWidth,
                m_RedTargetPosition.y);
            m_BlueSwordRect.anchoredPosition = new Vector2(
                halfCameraWidth + halfSwordWidth,
                m_BlueTargetPosition.y);
        }

        private static void SetStretchAnchors(RectTransform rect, Vector2 min, Vector2 max)
        {
            if (rect == null) return;
            rect.anchorMin = min;
            rect.anchorMax = max;
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;
        }

        private void HandleIntroTapped()
        {
            if (!m_CanAcceptTap || m_TapAccepted) return;
            m_TapAccepted = true;
            m_CanAcceptTap = false;
            m_TapCompletionSource?.TrySetResult();
        }

        private void CancelPendingAnimation()
        {
            m_CanAcceptTap = false;
            if (m_Sequence != null && m_Sequence.IsActive())
            {
                m_Sequence.Kill();
            }
            m_Sequence = null;
            m_TapCompletionSource?.TrySetResult();
        }

        private void OnDestroy()
        {
            CancelPendingAnimation();
            if (m_InputButton != null)
            {
                m_InputButton.onClick.RemoveListener(HandleIntroTapped);
            }
        }
    }
}
#endif
