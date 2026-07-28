#if UNITY_6000_0_OR_NEWER
using System;
using System.Threading;
using UnityEngine;
using UnityEngine.UI;
using DG.Tweening;
using InTheArena.Unit;
using UnitType = InTheArena.Unit.Unit;

namespace InTheArena.MainGame
{
    /// <summary>
    /// 결과 페이즈
    /// 라운드 전투 결과 표시 및 보상/페널티 처리
    /// </summary>
    [DisallowMultipleComponent]
    public class ResultPhase : RoundPhaseBase
    {
        [Header("UI References")]
        [SerializeField] private CanvasGroup m_ResultUiCanvasGroup;
        [SerializeField] private Text m_ResultTitleText;
        [SerializeField] private Text m_TeamANameText;
        [SerializeField] private Text m_TeamBNameText;
        [SerializeField] private Text m_TeamACallText;
        [SerializeField] private Text m_TeamBCallText;
        [SerializeField] private Text m_RewardCallText;
        [SerializeField] private Text m_RoundProgressText;
        [SerializeField] private Button m_ContinueButton;
        [SerializeField] private Image m_VictoryEffectImage;
        [SerializeField] private Image m_DefeatEffectImage;

        [Header("Animation")]
        [SerializeField] private float m_ResultDelay = 1f; // 결과 표시 전 대기 시간
        [SerializeField] private float m_FadeDuration = 0.5f;

        private AwaitableCompletionSource m_PhaseCompletionSource;
        private bool m_IsWin = false;
        private int m_RewardCall = 0;

        public override async Awaitable EnterPhaseAsync(CancellationToken token)
        {
            InitializeResult();

            if (m_ResultUiCanvasGroup != null)
            {
                m_ResultUiCanvasGroup.gameObject.SetActive(true);
                m_ResultUiCanvasGroup.alpha = 0f;
                await Awaitable.WaitForSecondsAsync(m_ResultDelay);
                
                var tween = m_ResultUiCanvasGroup.DOFade(1f, m_FadeDuration).SetEase(Ease.OutQuad);
                await AwaitTweenAsync(tween, token);
            }

            SetupResultUI();
            SubscribeEvents();

            m_PhaseCompletionSource = new AwaitableCompletionSource();
            await m_PhaseCompletionSource.Awaitable;
        }

        private void InitializeResult()
        {
            IsPhaseCompleted = false;
            m_IsWin = Context.Settlement != null && Context.Settlement.IsWin;
            m_RewardCall = Context.Settlement != null ? Context.Settlement.PayoutCall : 0;
        }

        private void SetupResultUI()
        {
            // 팀 이름
            if (m_TeamANameText != null) m_TeamANameText.text = "Red Team";
            if (m_TeamBNameText != null) m_TeamBNameText.text = "Blue Team";

            // 결과 타이틀
            if (m_ResultTitleText != null)
            {
                m_ResultTitleText.text = m_IsWin ? "BET WIN" : "BET LOSE";
                m_ResultTitleText.color = m_IsWin ? Color.green : Color.red;
            }

            if (m_TeamACallText != null)
                m_TeamACallText.text = $"Alive: {Context.CombatResult?.RedAliveCount ?? 0}";
            if (m_TeamBCallText != null)
                m_TeamBCallText.text = $"Alive: {Context.CombatResult?.BlueAliveCount ?? 0}";

            // 보상/페널티 콜
            if (m_RewardCallText != null)
            {
                if (m_IsWin)
                {
                    m_RewardCallText.text =
                        $"×{Context.Settlement.Multiplier} / {m_RewardCall} Call 지급\n보유: {Context.CurrentCall} Call";
                    m_RewardCallText.color = Color.green;
                }
                else
                {
                    m_RewardCallText.text =
                        $"-{Context.Settlement?.WagerCall ?? 0} Call\n보유: {Context.CurrentCall} Call";
                    m_RewardCallText.color = Color.red;
                }
            }

            // 라운드 진행도
            if (m_RoundProgressText != null)
            {
                m_RoundProgressText.text = $"Round {Context.CurrentRound} / {Context.MaxRounds}";
            }

            // 이펙트 표시
            if (m_VictoryEffectImage != null) m_VictoryEffectImage.gameObject.SetActive(m_IsWin);
            if (m_DefeatEffectImage != null) m_DefeatEffectImage.gameObject.SetActive(!m_IsWin);
        }

        private void SubscribeEvents()
        {
            if (m_ContinueButton != null)
            {
                m_ContinueButton.onClick.AddListener(OnContinueClicked);
            }
        }

        private void UnsubscribeEvents()
        {
            if (m_ContinueButton != null)
            {
                m_ContinueButton.onClick.RemoveListener(OnContinueClicked);
            }
        }

        private void OnContinueClicked()
        {
            if (IsPhaseCompleted) return;
            
            CompletePhase();
            m_PhaseCompletionSource?.TrySetResult();
        }

        /// <summary>
        /// 계산된 보상 콜 반환 (RoundManager에서 호출)
        /// </summary>
        public int GetRewardCall()
        {
            return m_RewardCall;
        }

        /// <summary>
        /// 승리 여부 반환
        /// </summary>
        public bool IsWin()
        {
            return m_IsWin;
        }

        public override async Awaitable ExitPhaseAsync(CancellationToken token)
        {
            UnsubscribeEvents();

            if (m_ResultUiCanvasGroup != null)
            {
                var tween = m_ResultUiCanvasGroup.DOFade(0f, m_FadeDuration).SetEase(Ease.InQuad);
                await AwaitTweenAsync(tween, token);
                m_ResultUiCanvasGroup.gameObject.SetActive(false);
            }

            transform.DOKill();
        }

        /// <summary>
        /// [High Safety] DOTween v1.2.675+ CancellationToken 지원 Unity 6 Awaitable로 래핑
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
            if (m_ResultUiCanvasGroup != null)
            {
                m_ResultUiCanvasGroup.DOKill();
            }
            UnsubscribeEvents();
        }
    }
}
#endif
