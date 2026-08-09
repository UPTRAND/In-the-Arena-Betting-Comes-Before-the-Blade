#if UNITY_6000_0_OR_NEWER
using System.Threading;
using DG.Tweening;
using InTheArena.UI;
using UnityEngine;

namespace InTheArena.MainGame
{
    /// <summary>
    /// 확정된 전투/베팅 정산 결과를 표시하고 다음 라운드 진행을 대기합니다.
    /// </summary>
    [DisallowMultipleComponent]
    public class ResultPhase : RoundPhaseBase
    {
        [Header("UI References")]
        [SerializeField] private UI_ResultPhase m_ResultUi;

        [Header("Animation")]
        [SerializeField] private float m_ResultDelay = 1f;

        private AwaitableCompletionSource m_PhaseCompletionSource;
        private bool m_IsWin;
        private int m_RewardCall;
        private BettingPhase m_BettingPhase;

#pragma warning disable 1998
        public override async Awaitable PreparePhaseAsync(CancellationToken token)
        {
            InitializeResult();

            if (m_ResultUi == null)
            {
                m_ResultUi = FindFirstObjectByType<UI_ResultPhase>(FindObjectsInactive.Include);
            }

            UnsubscribeEvents();
            SubscribeEvents();

            if (m_ResultUi != null)
            {
                m_ResultUi.Configure(Context.BetTicket, Context.CombatResult, Context.Settlement);
                if (!m_ResultUi.BIsOpened) m_ResultUi.Open();
                m_ResultUi.Enable();
                m_ResultUi.Refresh();
                if (m_ResultUi.CanvasGroup != null)
                {
                    m_ResultUi.CanvasGroup.alpha = 1f;
                    m_ResultUi.CanvasGroup.blocksRaycasts = true;
                    m_ResultUi.CanvasGroup.interactable = true;
                }
            }
        }
#pragma warning restore 1998

        public override async Awaitable EnterPhaseAsync(CancellationToken token)
        {
            if (m_ResultUi != null)
            {
                await Awaitable.WaitForSecondsAsync(m_ResultDelay);
                token.ThrowIfCancellationRequested();

                m_ResultUi.PlayResultAnimation();
            }
            else
            {
                Debug.LogWarning("[ResultPhase] UI_ResultPhase 참조가 없어 자동으로 페이즈를 완료합니다.");
                await Awaitable.WaitForSecondsAsync(m_ResultDelay);
                CompletePhase();
                return;
            }

            m_PhaseCompletionSource = new AwaitableCompletionSource();
            using (token.Register(() => m_PhaseCompletionSource?.TrySetResult()))
            {
                await m_PhaseCompletionSource.Awaitable;
            }
            token.ThrowIfCancellationRequested();
        }

        private void InitializeResult()
        {
            IsPhaseCompleted = false;
            m_IsWin = Context.Settlement != null && Context.Settlement.IsWin;
            m_RewardCall = Context.Settlement != null ? Context.Settlement.PayoutCall : 0;
        }

        private void SubscribeEvents()
        {
            if (m_ResultUi != null)
            {
                m_ResultUi.ContinueClicked += OnContinueClicked;
                m_ResultUi.PayoutRevealCompleted += OnPayoutRevealCompleted;
            }
            m_BettingPhase = FindFirstObjectByType<BettingPhase>(FindObjectsInactive.Include);
        }

        private void UnsubscribeEvents()
        {
            if (m_ResultUi != null)
            {
                m_ResultUi.ContinueClicked -= OnContinueClicked;
                m_ResultUi.PayoutRevealCompleted -= OnPayoutRevealCompleted;
            }
        }

        private void OnPayoutRevealCompleted()
        {
            if (m_BettingPhase == null || Context?.Settlement == null || !Context.Settlement.IsWin || Context.Settlement.PayoutCall <= 0) return;
            int from = Mathf.Max(0, Context.CurrentCall - Context.Settlement.PayoutCall);
            m_BettingPhase.PlayNowColRewardAnimation(m_ResultUi != null ? m_ResultUi.MyResultTransform : null, from, Context.CurrentCall);
        }

        private void OnContinueClicked()
        {
            if (IsPhaseCompleted) return;

            CompletePhase();
            m_PhaseCompletionSource?.TrySetResult();
        }

        public int GetRewardCall() => m_RewardCall;
        public bool IsWin() => m_IsWin;

        public override async Awaitable ExitPhaseAsync(CancellationToken token)
        {
            UnsubscribeEvents();

            if (m_ResultUi != null && m_ResultUi.BIsOpened)
            {
                m_ResultUi.CancelResultAnimation();
                if (m_ResultUi.CanvasGroup != null)
                {
                    m_ResultUi.CanvasGroup.DOKill();
                }
                m_ResultUi.Close();
            }

            transform.DOKill();
        }

        private void OnDestroy()
        {
            transform.DOKill();
            if (m_ResultUi != null && m_ResultUi.CanvasGroup != null)
                m_ResultUi.CanvasGroup.DOKill();
            UnsubscribeEvents();
        }
    }
}
#endif
