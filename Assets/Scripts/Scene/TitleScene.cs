#if UNITY_6000_0_OR_NEWER
using System;
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

        [Header("Animation Settings")]
        [Tooltip("깜빡임 1회 주기에 걸리는 시간(초)")]
        [SerializeField] private float m_BlinkDuration = 1.2f;

        [Tooltip("깜빡임 최소 알파 값 (0.0 ~ 1.0)")]
        [SerializeField] private float m_MinAlpha = 0.2f;

        [Header("Scene Settings")]
        [Tooltip("터치 시 이동할 씬 이름")]
        [SerializeField] private string m_TargetSceneName = "Lobby";

        private bool m_IsTransitioning;
        private Tween m_BlinkTween;

        private void Awake()
        {
            EnsureTextCanvasGroup();
        }

        private void Start()
        {
            EnsureTextCanvasGroup();
            StartTextBlinking();
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

        /// <summary>
        /// DOTween을 이용해 StartText만 알파 깜빡임(Yoyo Loop) 수행
        /// </summary>
        private void StartTextBlinking()
        {
            if (m_TextCanvasGroup == null) return;

            m_TextCanvasGroup.alpha = 1.0f;

            m_BlinkTween = m_TextCanvasGroup.DOFade(m_MinAlpha, m_BlinkDuration)
                .SetLoops(-1, LoopType.Yoyo)
                .SetEase(Ease.InOutSine);
        }

        /// <summary>
        /// 화면(TouchArea) 클릭 시 씬 전환 처리
        /// </summary>
        public void OnPointerClick(PointerEventData eventData)
        {
            if (m_IsTransitioning) return;

            m_IsTransitioning = true;
            StopTextBlinking();

            if (!string.IsNullOrEmpty(m_TargetSceneName))
            {
                AsyncSceneLoader.LoadScene(m_TargetSceneName);
            }
        }

        private void StopTextBlinking()
        {
            if (m_BlinkTween != null && m_BlinkTween.IsActive())
            {
                m_BlinkTween.Kill();
                m_BlinkTween = null;
            }

            if (m_TextCanvasGroup != null)
            {
                m_TextCanvasGroup.DOKill();
                m_TextCanvasGroup.alpha = 1.0f;
            }
        }

        private void OnDestroy()
        {
            StopTextBlinking();
            transform.DOKill();
        }
    }
}
#endif