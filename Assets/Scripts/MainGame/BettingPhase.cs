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
    /// 베팅 페이즈
    /// 플레이어가 팀 A와 팀 B의 승률에 베팅하는 단계
    /// </summary>
    [DisallowMultipleComponent]
    public class BettingPhase : RoundPhaseBase
    {
        [Header("UI References")]
        [SerializeField] private CanvasGroup m_BettingUiCanvasGroup;
        [SerializeField] private Slider m_BetRatioSlider;
        [SerializeField] private Text m_TeamANameText;
        [SerializeField] private Text m_TeamBNameText;
        [SerializeField] private Text m_TeamARatioText;
        [SerializeField] private Text m_TeamBRatioText;
        [SerializeField] private Text m_CurrentCallText;
        [SerializeField] private Button m_ConfirmBetButton;
        [SerializeField] private Button m_ExtraBetButton;
        [SerializeField] private Button m_RearrangeButton;

        private AwaitableCompletionSource m_PhaseCompletionSource;
        private bool m_IsSliderInteractable = true;

        public override async Awaitable EnterPhaseAsync(CancellationToken token)
        {
            InitializePhaseData();
            SetupUI();
            SubscribeEvents();

            if (m_BettingUiCanvasGroup != null)
            {
                m_BettingUiCanvasGroup.gameObject.SetActive(true);
                m_BettingUiCanvasGroup.alpha = 0f;

                // [High Safety / Fix CS1061] DOTween v1.2.675+ supports AsyncWaitForCompletion API
                var tween = m_BettingUiCanvasGroup.DOFade(1f, 0.3f).SetEase(Ease.OutQuad);
                await AwaitTweenAsync(tween, token);
            }

            m_PhaseCompletionSource = new AwaitableCompletionSource();

            // 베팅 UI가 [확인] 버튼을 눌러 완료될 때까지 대기
            await m_PhaseCompletionSource.Awaitable;
        }

        private void InitializePhaseData()
        {
            IsPhaseCompleted = false;
            Context.ResetBettingData();

            // 기본 베팅 비율 설정
            m_BetRatioSlider.value = Context.TeamABetRatio / 100f;
            UpdateRatioTexts();
        }

        private void SetupUI()
        {
            // 팀 이름 설정 (유닛 데이터에서 가져오거나 기본값)
            if (m_TeamANameText != null && Context.TeamAUnitDatas.Count > 0)
            {
                m_TeamANameText.text = Context.TeamAUnitDatas[0].UnitName;
            }
            if (m_TeamBNameText != null && Context.TeamBUnitDatas.Count > 0)
            {
                m_TeamBNameText.text = Context.TeamBUnitDatas[0].UnitName;
            }

            // 현재 콜 표시
            if (m_CurrentCallText != null)
            {
                m_CurrentCallText.text = $"Call: {Context.CurrentCall}";
            }

            // 버튼 인터렉션 설정
            UpdateButtonInteractable();
        }

        private void SubscribeEvents()
        {
            if (m_BetRatioSlider != null)
            {
                m_BetRatioSlider.onValueChanged.AddListener(OnBetRatioChanged);
            }
            if (m_ConfirmBetButton != null)
            {
                m_ConfirmBetButton.onClick.AddListener(OnConfirmBetClicked);
            }
            if (m_ExtraBetButton != null)
            {
                m_ExtraBetButton.onClick.AddListener(OnExtraBetClicked);
            }
            if (m_RearrangeButton != null)
            {
                m_RearrangeButton.onClick.AddListener(OnRearrangeClicked);
            }
        }

        private void UnsubscribeEvents()
        {
            if (m_BetRatioSlider != null)
            {
                m_BetRatioSlider.onValueChanged.RemoveListener(OnBetRatioChanged);
            }
            if (m_ConfirmBetButton != null)
            {
                m_ConfirmBetButton.onClick.RemoveListener(OnConfirmBetClicked);
            }
            if (m_ExtraBetButton != null)
            {
                m_ExtraBetButton.onClick.RemoveListener(OnExtraBetClicked);
            }
            if (m_RearrangeButton != null)
            {
                m_RearrangeButton.onClick.RemoveListener(OnRearrangeClicked);
            }
        }

        private void OnBetRatioChanged(float value)
        {
            if (!m_IsSliderInteractable) return;

            int teamARatio = Mathf.RoundToInt(value * 100f);
            teamARatio = Mathf.Clamp(teamARatio, 0, 100);

            Context.TeamABetRatio = teamARatio;
            Context.TeamBBetRatio = 100 - teamARatio;

            UpdateRatioTexts();
            UpdateButtonInteractable();
        }

        private void UpdateRatioTexts()
        {
            if (m_TeamARatioText != null)
                m_TeamARatioText.text = $"{Context.TeamABetRatio}%";
            if (m_TeamBRatioText != null)
                m_TeamBRatioText.text = $"{Context.TeamBBetRatio}%";
        }

        private void UpdateButtonInteractable()
        {
            // 50:50 비율일 때 확인 버튼 비활성화
            bool canConfirm = Context.TeamABetRatio != 50;
            if (m_ConfirmBetButton != null)
                m_ConfirmBetButton.interactable = canConfirm;
        }

        /// <summary>
        /// 베팅 비율 직접 설정 (외부에서 호출 가능)
        /// </summary>
        public bool SetBettingRatio(int teamARatio)
        {
            teamARatio = Mathf.Clamp(teamARatio, 0, 100);

            if (teamARatio == 50)
            {
                Debug.LogWarning("[BettingPhase] 50:50 비율은 베팅할 수 없습니다.");
                return false;
            }

            Context.TeamABetRatio = teamARatio;
            Context.TeamBBetRatio = 100 - teamARatio;

            if (m_BetRatioSlider != null)
            {
                m_IsSliderInteractable = false;
                m_BetRatioSlider.value = teamARatio / 100f;
                m_IsSliderInteractable = true;
            }

            UpdateRatioTexts();
            UpdateButtonInteractable();
            Debug.Log($"[BettingPhase] 베팅 비율 설정 -> A: {Context.TeamABetRatio}% | B: {Context.TeamBBetRatio}%");
            return true;
        }

        /// <summary>
        /// 베팅 아이템 사용 1: 추가 베팅 콜 (+50)
        /// </summary>
        public void UseExtraBetItem()
        {
            if (IsPhaseCompleted) return;

            Context.ExtraBetCall += 50;
            Debug.Log($"[BettingPhase] 추가 베팅 아이템 사용! 추가 콜: +{Context.ExtraBetCall}");

            if (m_CurrentCallText != null)
            {
                m_CurrentCallText.text = $"Call: {Context.CurrentCall + Context.ExtraBetCall}";
            }
        }

        /// <summary>
        /// 베팅 아이템 사용 2: 유닛 재배치
        /// </summary>
        public void UseRearrangeItem()
        {
            if (IsPhaseCompleted) return;

            Debug.Log("[BettingPhase] 유닛 재배치 아이템 사용! 유닛 배치 변경 로직 필요");
            // TODO: 유닛 재배치 로직 구현
        }

        /// <summary>
        /// UI [확인] 버튼 클릭 시 호출
        /// </summary>
        private void OnConfirmBetClicked()
        {
            if (IsPhaseCompleted) return;

            // 50:50 베팅 방지
            if (Context.TeamABetRatio == 50)
            {
                Debug.LogError("[BettingPhase] 50:50 비율은 베팅할 수 없습니다.");
                return;
            }

            CompletePhase();
            m_PhaseCompletionSource?.TrySetResult();
        }

        private void OnExtraBetClicked()
        {
            UseExtraBetItem();
        }

        private void OnRearrangeClicked()
        {
            UseRearrangeItem();
        }

        public override async Awaitable ExitPhaseAsync(CancellationToken token)
        {
            UnsubscribeEvents();

            if (m_BettingUiCanvasGroup != null)
            {
                var tween = m_BettingUiCanvasGroup.DOFade(0f, 0.3f).SetEase(Ease.InQuad);
                await AwaitTweenAsync(tween, token);
                m_BettingUiCanvasGroup.gameObject.SetActive(false);
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
            if (m_BettingUiCanvasGroup != null)
            {
                m_BettingUiCanvasGroup.DOKill();
            }
            UnsubscribeEvents();
        }
    }
}
#endif