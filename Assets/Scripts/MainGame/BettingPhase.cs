#if UNITY_6000_0_OR_NEWER
using System;
using System.Threading;
using UnityEngine;
using UnityEngine.UI;
using DG.Tweening;
using InTheArena.Unit;
using UnitType = InTheArena.Unit.Unit;
using TMPro;

namespace InTheArena.MainGame
{
    /// <summary>
    /// 베팅 페이즈 - 이미지 기반 UI 구성
    /// 1. 상단: 현재 라운드 텍스트
    /// 2. 중간: 팀A/팀B 유닛 정보 (좌표 + 유닛명 텍스트)
    /// 3. 하단: 진영 배팅 슬라이더
    /// 4. 최하단: 초록색 확인 버튼 (×2)
    /// </summary>
    [DisallowMultipleComponent]
    public class BettingPhase : RoundPhaseBase
    {
        [Header("UI References - Top")]
        [SerializeField] private TMP_Text m_RoundText;           // "Round 3"

        [Header("UI References - Team Info (Middle)")]
        [SerializeField] private TMP_Text m_TeamANameText;       // 팀A 이름
        [SerializeField] private TMP_Text m_TeamAUnitInfoText;    // 팀A 유닛 정보 (좌표 + 유닛명)
        [SerializeField] private TMP_Text m_TeamBNameText;       // 팀B 이름
        [SerializeField] private TMP_Text m_TeamBUnitInfoText;    // 팀B 유닛 정보 (좌표 + 유닛명)

        [Header("UI References - Betting (Bottom)")]
        [SerializeField] private Slider m_BetRatioSlider;         // 진영 배팅 슬라이더
        [SerializeField] private TMP_Text m_TeamARatioText;       // 팀A 비율 텍스트
        [SerializeField] private TMP_Text m_TeamBRatioText;       // 팀B 비율 텍스트
        [SerializeField] private Button m_ConfirmBetButton;       // 초록색 확인 버튼 (×2)

        private AwaitableCompletionSource m_PhaseCompletionSource;
        private bool m_IsSliderInteractable = true;

        public override async Awaitable EnterPhaseAsync(CancellationToken token)
        {
            InitializePhaseData();
            SetupUI();
            SubscribeEvents();

            // 페이드 인
            var canvasGroup = GetComponent<CanvasGroup>();
            if (canvasGroup != null)
            {
                canvasGroup.gameObject.SetActive(true);
                canvasGroup.alpha = 0f;
                var tween = canvasGroup.DOFade(1f, 0.3f).SetEase(Ease.OutQuad);
                await AwaitTweenAsync(tween, token);
            }

            m_PhaseCompletionSource = new AwaitableCompletionSource();
            await m_PhaseCompletionSource.Awaitable;
        }

        private void InitializePhaseData()
        {
            IsPhaseCompleted = false;
            Context.ResetBettingData();
            m_BetRatioSlider.value = Context.TeamABetRatio / 100f;
            UpdateRatioTexts();
        }

        private void SetupUI()
        {
            // 1. 라운드 텍스트
            if (m_RoundText != null)
            {
                m_RoundText.text = $"Round {Context.CurrentRound}";
            }

            // 2. 팀 유닛 정보 텍스트 (좌표 + 유닛명)
            UpdateTeamUnitInfo();

            // 3. 베팅 슬라이더 초기값
            m_BetRatioSlider.value = Context.TeamABetRatio / 100f;
            UpdateRatioTexts();
            UpdateButtonInteractable();
        }

        private void UpdateTeamUnitInfo()
        {
            // 팀A: 좌측 그리드 (Red 팀)
            if (m_TeamAUnitInfoText != null && Context.TeamAUnitDatas.Count > 0)
            {
                var lines = new System.Text.StringBuilder();
                for (int i = 0; i < Context.TeamAUnitDatas.Count; i++)
                {
                    var unit = Context.TeamAUnitDatas[i];
                    // 2x3 그리드 좌표 계산 (0~5)
                    int col = i % 3;
                    int row = i / 3;
                    lines.Append($"({col},{row}) {unit.UnitName}");
                    if (i < Context.TeamAUnitDatas.Count - 1) lines.Append("\n");
                }
                m_TeamAUnitInfoText.text = lines.ToString();
            }

            // 팀B: 우측 그리드 (Blue 팀)
            if (m_TeamBUnitInfoText != null && Context.TeamBUnitDatas.Count > 0)
            {
                var lines = new System.Text.StringBuilder();
                for (int i = 0; i < Context.TeamBUnitDatas.Count; i++)
                {
                    var unit = Context.TeamBUnitDatas[i];
                    int col = i % 3;
                    int row = i / 3;
                    lines.Append($"({col},{row}) {unit.UnitName}");
                    if (i < Context.TeamBUnitDatas.Count - 1) lines.Append("\n");
                }
                m_TeamBUnitInfoText.text = lines.ToString();
            }
        }

        private void SubscribeEvents()
        {
            if (m_BetRatioSlider != null)
                m_BetRatioSlider.onValueChanged.AddListener(OnBetRatioChanged);
            if (m_ConfirmBetButton != null)
                m_ConfirmBetButton.onClick.AddListener(OnConfirmBetClicked);
        }

        private void UnsubscribeEvents()
        {
            if (m_BetRatioSlider != null)
                m_BetRatioSlider.onValueChanged.RemoveListener(OnBetRatioChanged);
            if (m_ConfirmBetButton != null)
                m_ConfirmBetButton.onClick.RemoveListener(OnConfirmBetClicked);
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
        /// UI [확인] 버튼 클릭 시 호출 (×2 버튼)
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

        public override async Awaitable ExitPhaseAsync(CancellationToken token)
        {
            UnsubscribeEvents();

            var canvasGroup = GetComponent<CanvasGroup>();
            if (canvasGroup != null)
            {
                var tween = canvasGroup.DOFade(0f, 0.3f).SetEase(Ease.InQuad);
                await AwaitTweenAsync(tween, token);
                canvasGroup.gameObject.SetActive(false);
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
            UnsubscribeEvents();
        }
    }
}
#endif