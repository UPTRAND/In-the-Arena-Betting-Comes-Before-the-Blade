#if UNITY_6000_0_OR_NEWER
using System;
using UnityEngine;
using UnityEngine.EventSystems;
using TMPro;
using DG.Tweening;

[DisallowMultipleComponent]
public class TitleScene : MonoBehaviour, IPointerClickHandler
{
    [Header("UI References")]
    [SerializeField] private TextMeshProUGUI m_StartText;
    [SerializeField] private CanvasGroup m_TextCanvasGroup;

    [Header("Animation Settings")]
    [Tooltip("깜빡임 1회 주기에 걸리는 시간(초)")]
    [SerializeField] private float m_BlinkDuration = 1.2f;

    [Tooltip("최저 투명도 목표값 (0.0 ~ 1.0)")]
    [SerializeField] private float m_MinAlpha = 0.2f;

    [Header("Scene Settings")]
    [Tooltip("터치 시 이동할 목적지 씬 이름")]
    [SerializeField] private string m_TargetSceneName = "Stage";

    private bool m_IsTransitioning;
    private Tween m_BlinkTween;

    private void Awake()
    {
        // [High Safety] CanvasGroup 컴포넌트 자동 할당 및 방어 코드
        if (m_TextCanvasGroup == null && m_StartText != null)
        {
            if (!m_StartText.TryGetComponent<CanvasGroup>(out m_TextCanvasGroup))
            {
                m_TextCanvasGroup = m_StartText.gameObject.AddComponent<CanvasGroup>();
            }
        }
    }

    private void Start()
    {
        StartTextBlinking();
    }

    /// <summary>
    /// DOTween을 이용한 텍스트 깜빡임(Yoyo Loop) 연출 시작
    /// </summary>
    private void StartTextBlinking()
    {
        if (m_TextCanvasGroup == null) return;

        m_TextCanvasGroup.alpha = 1.0f;

        // CanvasGroup Alpha를 이용한 가볍고 부드러운 UI 알파 트위닝
        m_BlinkTween = m_TextCanvasGroup.DOFade(m_MinAlpha, m_BlinkDuration)
            .SetLoops(-1, LoopType.Yoyo)
            .SetEase(Ease.InOutSine);
    }

    /// <summary>
    /// 화면 전체 터치 입력 이벤트 감지 (uGUI IPointerClickHandler)
    /// </summary>
    public void OnPointerClick(PointerEventData eventData)
    {
        OnTitleTouched();
    }

    private void OnTitleTouched()
    {
        // 중복 터치 및 연타로 인한 씬 중복 전환 방지
        if (m_IsTransitioning) return;
        m_IsTransitioning = true;

        // 깜빡임 트윈 정지 및 강조 고정 연출
        if (m_BlinkTween != null && m_BlinkTween.IsActive())
        {
            m_BlinkTween.Kill();
        }

        if (m_TextCanvasGroup != null)
        {
            m_TextCanvasGroup.DOKill();
            m_TextCanvasGroup.alpha = 1.0f;
        }

        // 글로벌 비동기 씬 로더를 통해 "Stage" 씬으로 이동
        AsyncSceneLoader.LoadScene(m_TargetSceneName);
    }

    private void OnDestroy()
    {
        // [High Safety] DOTween: 컴포넌트 파괴 및 씬 전환 시 트윈 인스턴스 안전 정지
        if (m_BlinkTween != null && m_BlinkTween.IsActive())
        {
            m_BlinkTween.Kill();
        }

        if (m_TextCanvasGroup != null)
        {
            m_TextCanvasGroup.DOKill();
        }

        transform.DOKill();
    }
}
#endif