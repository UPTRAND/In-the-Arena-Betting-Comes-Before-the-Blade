#if UNITY_6000_0_OR_NEWER
using System;
using System.Threading;
using UnityEngine;
using DG.Tweening;

[DisallowMultipleComponent]
public class BettingPhase : RoundPhaseBase
{
    [Header("UI & Animation References")]
    [SerializeField] private CanvasGroup m_BettingUiCanvasGroup;

    private AwaitableCompletionSource m_PhaseCompletionSource;

    public override async Awaitable EnterPhaseAsync(CancellationToken token)
    {
        InitializePhaseData();

        if (m_BettingUiCanvasGroup != null)
        {
            m_BettingUiCanvasGroup.gameObject.SetActive(true);
            m_BettingUiCanvasGroup.alpha = 0f;

            // [High Safety / Fix CS1061] DOTween v1.2.675 공식 AsyncWaitForCompletion API 연동
            var tween = m_BettingUiCanvasGroup.DOFade(1f, 0.3f).SetEase(Ease.OutQuad);
            await AwaitTweenAsync(tween, token);
        }

        m_PhaseCompletionSource = new AwaitableCompletionSource();

        // 플레이어가 UI에서 [배팅 완료] 버튼을 누를 때까지 비동기 대기
        await m_PhaseCompletionSource.Awaitable;
    }

    private void InitializePhaseData()
    {
        IsPhaseCompleted = false;
        Context.ResetBettingData();
        Debug.Log($"[BettingPhase] 라운드 {Context.CurrentRound} 배팅 시작. 보유 콜: {Context.CurrentCall}");
    }

    /// <summary>
    /// 배팅 비율 변경 (UI 슬라이더 연동)
    /// 기획서 제약 조건: 50:50 배팅은 불가능합니다.
    /// </summary>
    public bool SetBettingRatio(int teamARatio)
    {
        teamARatio = Mathf.Clamp(teamARatio, 0, 100);

        if (teamARatio == 50)
        {
            Debug.LogWarning("[BettingPhase] 50:50 배팅은 불가능합니다.");
            return false;
        }

        Context.TeamABetRatio = teamARatio;
        Context.TeamBBetRatio = 100 - teamARatio;
        Debug.Log($"[BettingPhase] 배팅 비율 설정 완료 -> A: {Context.TeamABetRatio}% | B: {Context.TeamBBetRatio}%");
        return true;
    }

    /// <summary>
    /// 배팅 아이템 1: 추가 배팅권 사용 (+50 콜 추가 투입)
    /// </summary>
    public void UseExtraBetItem()
    {
        if (IsPhaseCompleted) return;

        Context.ExtraBetCall += 50;
        Debug.Log($"[BettingPhase] 추가 배팅권 사용! 추가 콜: +{Context.ExtraBetCall}");
    }

    /// <summary>
    /// 배팅 아이템 2: 유닛 재편성 사용 (팀 유닛 조합 재배치)
    /// </summary>
    public void UseRearrangeItem()
    {
        if (IsPhaseCompleted) return;

        Debug.Log("[BettingPhase] 팀 재편성 아이템 사용! 유닛 조합을 재배치합니다.");
    }

    /// <summary>
    /// UI [배팅 완료] 버튼 클릭 시 호출
    /// </summary>
    public void CompleteBetting()
    {
        if (IsPhaseCompleted) return;

        // 50:50 배팅 방어 검증
        if (Context.TeamABetRatio == 50)
        {
            Debug.LogError("[BettingPhase] 50:50 배팅 상태로는 완료할 수 없습니다.");
            return;
        }

        IsPhaseCompleted = true;
        m_PhaseCompletionSource?.TrySetResult();
    }

    public override async Awaitable ExitPhaseAsync(CancellationToken token)
    {
        if (m_BettingUiCanvasGroup != null)
        {
            // [High Safety / Fix CS1061] DOTween v1.2.675 공식 AsyncWaitForCompletion API 연동
            var tween = m_BettingUiCanvasGroup.DOFade(0f, 0.3f).SetEase(Ease.InQuad);
            await AwaitTweenAsync(tween, token);
            m_BettingUiCanvasGroup.gameObject.SetActive(false);
        }

        transform.DOKill();
    }

    /// <summary>
    /// [High Safety] DOTween v1.2.675 트윈을 CancellationToken과 함께 Unity 6 Awaitable로 대기하는 안전 래퍼
    /// </summary>
    private async Awaitable AwaitTweenAsync(Tween tween, CancellationToken token)
    {
        if (tween == null || !tween.IsActive()) return;

        using (token.Register(() =>
        {
            if (tween != null && tween.IsActive())
            {
                tween.Kill();
            }
        }))
        {
            await tween.AsyncWaitForCompletion();
        }
    }

    private void OnDestroy()
    {
        transform.DOKill();
        if (m_BettingUiCanvasGroup != null)
        {
            m_BettingUiCanvasGroup.DOKill();
        }
    }
}
#endif