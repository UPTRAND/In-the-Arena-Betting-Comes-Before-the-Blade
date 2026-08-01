#if UNITY_6000_0_OR_NEWER
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

        [Header("UI Feedback")]
        [SerializeField] private TMP_Text m_FeedbackText;
        [SerializeField] private TMP_Text m_AdditionalBetCountText;
        [SerializeField] private TMP_Text m_InsuranceCountText;
        [SerializeField] private TMP_Text m_RerollCountText;

        protected override void Awake()
        {
            base.Awake();
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

            string message;
            int remain;
            bool success = RoundManager.Instance.BettingPhase.UseBettingItem(itemData, out message, out remain);

            ShowFeedback(message);

            if (success)
            {
                if (countText != null)
                {
                    countText.text = remain.ToString();
                }
            }
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
                var inventory = SaveManager.Instance?.InventoryService;
                var state = StageManager.Instance?.PlayerState;
                if (inventory != null && state != null)
                {
                    int count = inventory.GetStageItemCount(itemData, state);
                    countText.text = count.ToString();
                }
            }
        }
    }
}
#endif
