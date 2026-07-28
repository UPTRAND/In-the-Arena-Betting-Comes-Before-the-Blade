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
            m_IsWin = Context.DidTeamAWin;
            m_RewardCall = CalculateRewardCall();
        }

        private void SetupResultUI()
        {
            // 팀 이름
            if (m_TeamANameText != null && Context.TeamAUnitDatas.Count > 0)
                m_TeamANameText.text = Context.TeamAUnitDatas[0].UnitName;
            if (m_TeamBNameText != null && Context.TeamBUnitDatas.Count > 0)
                m_TeamBNameText.text = Context.TeamBUnitDatas[0].UnitName;

            // 결과 타이틀
            if (m_ResultTitleText != null)
            {
                m_ResultTitleText.text = m_IsWin ? "VICTORY" : "DEFEAT";
                m_ResultTitleText.color = m_IsWin ? Color.green : Color.red;
            }

            // 팀별 콜 표시
            int teamACall = m_IsWin ? Context.CurrentCall + Context.ExtraBetCall : 0;
            int teamBCall = !m_IsWin ? Context.CurrentCall + Context.ExtraBetCall : 0;

            if (m_TeamACallText != null)
                m_TeamACallText.text = $"{Context.TeamABetRatio}% : {teamACall}";
            if (m_TeamBCallText != null)
                m_TeamBCallText.text = $"{Context.TeamBBetRatio}% : {teamBCall}";

            // 보상/페널티 콜
            if (m_RewardCallText != null)
            {
                if (m_IsWin)
                {
                    m_RewardCallText.text = $"+{m_RewardCall} Call 획득!";
                    m_RewardCallText.color = Color.green;
                }
                else
                {
                    m_RewardCallText.text = $"-{Context.CurrentCall} Call 잃음";
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
        /// 보상 콜 계산
        /// </summary>
        private int CalculateRewardCall()
        {
            if (!m_IsWin) return 0;

            // 기본 콜 * 베팅 비율 보너스
            float ratioBonus = Context.TeamABetRatio / 50f; // 50% 기준 1.0, 80%면 1.6
            int baseReward = Context.CurrentCall + Context.ExtraBetCall;
            
            return Mathf.RoundToInt(baseReward * ratioBonus);
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