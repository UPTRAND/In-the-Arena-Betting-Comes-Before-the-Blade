#if UNITY_6000_0_OR_NEWER
using UnityEngine;
using UnityEngine.EventSystems;
using TMPro;
using DG.Tweening;

namespace InTheArena.Scene
{
    [DisallowMultipleComponent]
    public class TitleScene : MonoBehaviour, IPointerClickHandler
    {
        [Header("UI References")]
        [SerializeField] private TextMeshProUGUI m_StartText;
        [SerializeField] private CanvasGroup m_TextCanvasGroup;
        [SerializeField] private RectTransform m_TitleLogo;
        [SerializeField] private CanvasGroup m_LogoCanvasGroup;

        [Header("Intro Animation")]
        [SerializeField, Min(0f)] private float m_IntroDelay = 0.1f;
        [SerializeField, Min(0.01f)] private float m_LogoIntroDuration = 0.5f;
        [SerializeField, Min(0.01f)] private float m_LogoFadeDuration = 0.18f;
        [SerializeField, Range(0.01f, 1f)] private float m_LogoStartScale = 0.65f;
        [Tooltip("로고가 원래 위치보다 아래에서 시작하는 거리입니다.")]
        [SerializeField, Min(0f)] private float m_LogoStartOffset = 80f;
        [SerializeField, Min(0.01f)] private float m_ImpactDuration = 0.15f;
        [SerializeField, Min(0f)] private float m_ImpactStrength = 6f;
        [SerializeField, Min(0.01f)] private float m_TextRevealDuration = 0.2f;

        [Header("Animation Settings")]
        [Tooltip("깜빡임 1회 주기에 걸리는 시간(초)")]
        [SerializeField] private float m_BlinkDuration = 1.2f;

        [Tooltip("깜빡임 최소 알파 값 (0.0 ~ 1.0)")]
        [SerializeField] private float m_MinAlpha = 0.2f;

        [Header("Scene Settings")]
        [Tooltip("터치 시 이동할 씬 이름")]
        [SerializeField] private string m_TargetSceneName = "Lobby";

        private bool m_IsTransitioning;
        private Vector2 m_LogoRestPosition;
        private Vector3 m_LogoRestScale;
        private Sequence m_IntroSequence;
        private Tween m_BlinkTween;

        private void Awake()
        {
            EnsureTextCanvasGroup();
            if (m_TitleLogo != null)
            {
                if (m_LogoCanvasGroup == null || m_LogoCanvasGroup.transform != m_TitleLogo)
                {
                    if (!m_TitleLogo.TryGetComponent(out m_LogoCanvasGroup))
                    {
                        m_LogoCanvasGroup = m_TitleLogo.gameObject.AddComponent<CanvasGroup>();
                    }
                }

                m_LogoRestPosition = m_TitleLogo.anchoredPosition;
                m_LogoRestScale = m_TitleLogo.localScale;
            }
        }

        private void OnEnable()
        {
            m_IsTransitioning = false;
            PlayIntro();
        }

        private void EnsureTextCanvasGroup()
        {
            if (m_StartText != null)
            {
                // CanvasGroup이 없거나 StartText와 다른 게임오브젝트(예: TouchPanel)를 가리키고 있는 경우 StartText전용 CanvasGroup 지정
                if (m_TextCanvasGroup == null || m_TextCanvasGroup.gameObject != m_StartText.gameObject)
                {
                    if (!m_StartText.TryGetComponent<CanvasGroup>(out m_TextCanvasGroup))
                    {
                        m_TextCanvasGroup = m_StartText.gameObject.AddComponent<CanvasGroup>();
                    }
                }
            }
        }

        private void PlayIntro()
        {
            StopAnimations();
            RestoreVisualState();

            bool hasLogo = m_TitleLogo != null && m_LogoCanvasGroup != null;
            if (!hasLogo && m_TextCanvasGroup == null) return;

            if (hasLogo)
            {
                m_TitleLogo.anchoredPosition = m_LogoRestPosition + Vector2.down * m_LogoStartOffset;
                m_TitleLogo.localScale = m_LogoRestScale * m_LogoStartScale;
                m_LogoCanvasGroup.alpha = 0f;
            }
            if (m_TextCanvasGroup != null)
            {
                m_TextCanvasGroup.alpha = 0f;
            }

            m_IntroSequence = DOTween.Sequence()
                .SetTarget(this)
                .SetUpdate(true)
                .SetLink(gameObject, LinkBehaviour.KillOnDestroy);
            m_IntroSequence.AppendInterval(Mathf.Max(0f, m_IntroDelay));

            if (hasLogo)
            {
                float logoDuration = Mathf.Max(0.01f, m_LogoIntroDuration);
                m_IntroSequence.Append(m_TitleLogo.DOScale(m_LogoRestScale, logoDuration)
                    .SetEase(Ease.OutBack));
                m_IntroSequence.Join(m_TitleLogo.DOAnchorPos(m_LogoRestPosition, logoDuration)
                    .SetEase(Ease.OutCubic));
                m_IntroSequence.Join(m_LogoCanvasGroup.DOFade(1f,
                    Mathf.Clamp(m_LogoFadeDuration, 0.01f, logoDuration)));
                m_IntroSequence.Append(m_TitleLogo.DOShakeAnchorPos(
                    Mathf.Max(0.01f, m_ImpactDuration), Mathf.Max(0f, m_ImpactStrength)));
            }

            if (m_TextCanvasGroup != null)
            {
                m_IntroSequence.Append(m_TextCanvasGroup.DOFade(1f,
                    Mathf.Max(0.01f, m_TextRevealDuration)));
            }

            m_IntroSequence.OnComplete(() =>
            {
                m_IntroSequence = null;
                RestoreVisualState();
                if (!m_IsTransitioning && isActiveAndEnabled)
                {
                    StartTextBlinking();
                }
            });
        }

        /// <summary>
        /// DOTween을 이용해 StartText만 알파 깜빡임(Yoyo Loop) 수행
        /// </summary>
        private void StartTextBlinking()
        {
            if (m_TextCanvasGroup == null) return;

            m_TextCanvasGroup.alpha = 1.0f;

            m_BlinkTween = m_TextCanvasGroup.DOFade(m_MinAlpha, m_BlinkDuration)
                .SetLoops(-1, LoopType.Yoyo)
                .SetEase(Ease.InOutSine)
                .SetUpdate(true)
                .SetLink(gameObject, LinkBehaviour.KillOnDestroy);
        }

        /// <summary>
        /// 화면(TouchArea) 클릭 시 씬 전환 처리
        /// </summary>
        public void OnPointerClick(PointerEventData eventData)
        {
            if (m_IsTransitioning || string.IsNullOrEmpty(m_TargetSceneName)) return;

            m_IsTransitioning = true;
            StopAnimations();
            RestoreVisualState();

            SoundManager.Instance?.PlaySfx(SfxIds.ButtonPositive);
            AsyncSceneLoader.LoadScene(m_TargetSceneName);
        }

        private void StopAnimations()
        {
            m_IntroSequence?.Kill();
            m_IntroSequence = null;
            m_BlinkTween?.Kill();
            m_BlinkTween = null;
        }

        private void RestoreVisualState()
        {
            if (m_TitleLogo != null)
            {
                m_TitleLogo.anchoredPosition = m_LogoRestPosition;
                m_TitleLogo.localScale = m_LogoRestScale;
            }
            if (m_LogoCanvasGroup != null)
            {
                m_LogoCanvasGroup.alpha = 1f;
            }
            if (m_TextCanvasGroup != null)
            {
                m_TextCanvasGroup.alpha = 1f;
            }
        }

        private void OnDisable()
        {
            StopAnimations();
            RestoreVisualState();
        }

        private void OnDestroy()
        {
            StopAnimations();
        }
    }
}
#endif
