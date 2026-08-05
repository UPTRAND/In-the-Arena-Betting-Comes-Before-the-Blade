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
        [SerializeField] private float m_FadeDuration = 0.5f;

        private AwaitableCompletionSource m_PhaseCompletionSource;
        private bool m_IsWin;
        private int m_RewardCall;

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
                m_ResultUi.Configure(Context.CombatResult, Context.Settlement, Context.CurrentCall);
                if (!m_ResultUi.BIsOpened) m_ResultUi.Open();
                m_ResultUi.Enable();
                if (m_ResultUi.CanvasGroup != null)
                {
                    m_ResultUi.CanvasGroup.alpha = 0f;
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

                m_ResultUi.Refresh();
                var tween = m_ResultUi.CanvasGroup != null
                    ? m_ResultUi.CanvasGroup.DOFade(1f, m_FadeDuration).SetEase(Ease.OutQuad)
                    : null;
                await AwaitTweenAsync(tween, token);
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
            if (m_ResultUi != null) m_ResultUi.ContinueClicked += OnContinueClicked;
        }

        private void UnsubscribeEvents()
        {
            if (m_ResultUi != null) m_ResultUi.ContinueClicked -= OnContinueClicked;
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
                if (m_ResultUi.CanvasGroup != null)
                {
                    m_ResultUi.CanvasGroup.DOKill();
                }
                m_ResultUi.Close();
            }

            transform.DOKill();
        }

        private static async Awaitable AwaitTweenAsync(Tween tween, CancellationToken token)
        {
            if (tween == null || !tween.IsActive()) return;

            using (token.Register(() =>
            {
                if (tween != null && tween.IsActive()) tween.Kill();
            }))
            {
                await tween.AsyncWaitForCompletion();
            }
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
