#if UNITY_6000_0_OR_NEWER
using System.Threading;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using InTheArena.MainGame;

namespace InTheArena.UI
{
    public class UI_BettingPhase : UI_Base
    {
        [Header("Betting Item Buttons")]
        [SerializeField] private Button m_AdditionalBetButton;
        [SerializeField] private Button m_InsuranceButton;
        [SerializeField] private Button m_RerollButton;

        [Header("Betting Item Data")]
        [SerializeField] private ItemData m_AdditionalBetData;
        [SerializeField] private ItemData m_InsuranceData;
        [SerializeField] private ItemData m_RerollData;
        [SerializeField] private UI_ItemPurchasePopupController m_ItemPurchasePopup;

        [Header("UI Feedback")]
        [SerializeField] private TMP_Text m_FeedbackText;
        [SerializeField] private TMP_Text m_AdditionalBetCountText;
        [SerializeField] private TMP_Text m_InsuranceCountText;
        [SerializeField] private TMP_Text m_RerollCountText;

        private CancellationTokenSource m_ItemUseLifetimeCancellation;

        protected override void Awake()
        {
            base.Awake();
            m_ItemUseLifetimeCancellation = new CancellationTokenSource();
            RefreshItemCounts();

            if (m_AdditionalBetButton != null)
            {
                m_AdditionalBetButton.onClick.RemoveAllListeners();
                m_AdditionalBetButton.onClick.AddListener(OnAdditionalBetClicked);
            }

            if (m_InsuranceButton != null)
            {
                m_InsuranceButton.onClick.RemoveAllListeners();
                m_InsuranceButton.onClick.AddListener(OnInsuranceClicked);
            }

            if (m_RerollButton != null)
            {
                m_RerollButton.onClick.RemoveAllListeners();
                m_RerollButton.onClick.AddListener(OnRerollClicked);
            }
        }

        private void OnAdditionalBetClicked()
        {
            TryUseItem(m_AdditionalBetData, m_AdditionalBetCountText);
        }

        private void OnInsuranceClicked()
        {
            TryUseItem(m_InsuranceData, m_InsuranceCountText);
        }

        private void OnRerollClicked()
        {
            TryUseItem(m_RerollData, m_RerollCountText);
        }

        private void TryUseItem(ItemData itemData, TMP_Text countText)
        {
            if (RoundManager.Instance == null)
            {
                ShowFeedback("라운드 매니저를 찾을 수 없습니다.");
                return;
            }

            if (RoundManager.Instance.BettingPhase == null)
            {
                ShowFeedback("현재 배팅 페이즈가 아닙니다.");
                return;
            }

            UI_ItemPurchasePopupController popup = m_ItemPurchasePopup ??
                UIManager.Instance?.GetElement<UI_ItemPurchasePopupController>();
            ItemPurchaseUseCoordinator coordinator = RoundManager.Instance.ItemPurchaseUseCoordinator;
            if (itemData != null && popup != null && popup.ParentRoot != null && coordinator != null)
            {
                _ = TryUseItemThroughPurchaseFlowAsync(itemData, coordinator, popup);
                return;
            }

            ShowFeedback("아이템 구매 팝업 또는 코디네이터가 없어 사용 불가합니다.");
        }

        private async Awaitable TryUseItemThroughPurchaseFlowAsync(
            ItemData itemData,
            ItemPurchaseUseCoordinator coordinator,
            UI_ItemPurchasePopupController popup)
        {
            await coordinator.RequestImmediateUseAsync(
                itemData,
                popup,
                new BettingItemUseExecutor(RoundManager.Instance.BettingPhase),
                m_ItemUseLifetimeCancellation?.Token ?? CancellationToken.None);

            ShowFeedback(coordinator.LastResult == ItemPurchaseUseResult.UseSucceeded
                ? "베팅 아이템을 사용했습니다."
                : "베팅 아이템을 사용하지 못했습니다.");
        }

        private void ShowFeedback(string message)
        {
            if (m_FeedbackText != null)
            {
                m_FeedbackText.text = message;
            }
            Debug.Log("[UI_BettingPhase] " + message);
        }

        private void OnEnable()
        {
            if (RoundManager.Instance != null && RoundManager.Instance.Context != null)
            {
                RoundManager.Instance.Context.OnSpecialBetChanged += HandleSpecialBetChanged;
            }
        }

        private void OnDisable()
        {
            if (RoundManager.Instance != null && RoundManager.Instance.Context != null)
            {
                RoundManager.Instance.Context.OnSpecialBetChanged -= HandleSpecialBetChanged;
            }
        }

        protected override void OnDestroy()
        {
            base.OnDestroy();
            m_ItemUseLifetimeCancellation?.Cancel();
            m_ItemUseLifetimeCancellation?.Dispose();
            m_ItemUseLifetimeCancellation = null;
        }

        private void HandleSpecialBetChanged()
        {
            ShowFeedback("특수 배팅 룰이 갱신되었습니다!");
        }

        public void RefreshItemCounts()
        {
            UpdateCountText(m_AdditionalBetData, m_AdditionalBetCountText);
            UpdateCountText(m_InsuranceData, m_InsuranceCountText);
            UpdateCountText(m_RerollData, m_RerollCountText);
        }

        private void UpdateCountText(ItemData itemData, TMP_Text countText)
        {
            if (itemData != null && countText != null)
            {
                countText.text = string.Empty;
            }
        }
    }
}
#endif
