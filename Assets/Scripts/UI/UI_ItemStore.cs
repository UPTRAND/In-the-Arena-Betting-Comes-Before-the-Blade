
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using InTheArena.MainGame;

namespace InTheArena.UI
{
    public class UI_ItemStore : UI_Base
    {
        public enum StoreType
        {
            Lobby,
            Stage
        }

        [Header("Store Settings")]
        [SerializeField] private StoreType m_StoreType = StoreType.Lobby;
        [SerializeField] private ItemData m_TargetItem;
        
        [Header("UI Elements")]
        [SerializeField] private Button m_BuyButton;
        [SerializeField] private TMP_Text m_PriceText;
        [SerializeField] private TMP_Text m_CurrentCountText;
        [SerializeField] private TMP_Text m_FeedbackText;

        protected override void Awake()
        {
            base.Awake();
            if (m_BuyButton != null)
            {
                m_BuyButton.onClick.AddListener(OnBuyClicked);
            }
        }

        private void OnEnable()
        {
            RefreshUI();
        }

        private void OnBuyClicked()
        {
            if (m_TargetItem == null) return;
            var inventory = SaveManager.Instance?.InventoryService;
            if (inventory == null)
            {
                ShowFeedback("?�이???�스?�을 찾을 ???�습?�다.");
                return;
            }

            bool success = false;
            if (m_StoreType == StoreType.Lobby)
            {
                success = inventory.TryBuyItemFromLobby(m_TargetItem);
            }
            else
            {
                var state = StageManager.Instance?.PlayerState;
                if (state != null)
                {
                    success = inventory.TryBuyItemFromStage(m_TargetItem, state);
                }
                else
                {
                    ShowFeedback("진행 중인 ?�테?��?가 ?�습?�다.");
                    return;
                }
            }

            if (success)
            {
                ShowFeedback($"{m_TargetItem.ItemName} 구매 ?�료!");
                RefreshUI();
            }
            else
            {
                ShowFeedback("골드가 부족합?�다.");
            }
        }

        public void RefreshUI()
        {
            if (m_TargetItem == null) return;
            
            if (m_PriceText != null)
            {
                m_PriceText.text = m_TargetItem.PriceGold.ToString();
            }

            if (m_CurrentCountText != null)
            {
                var inventory = SaveManager.Instance?.InventoryService;
                if (inventory != null)
                {
                    int count = 0;
                    if (m_StoreType == StoreType.Lobby)
                    {
                        count = inventory.GetLobbyItemCount(m_TargetItem);
                    }
                    else
                    {
                        var state = StageManager.Instance?.PlayerState;
                        if (state != null)
                        {
                            count = inventory.GetStageItemCount(m_TargetItem, state);
                        }
                    }
                    m_CurrentCountText.text = count.ToString();
                }
            }
        }

        private void ShowFeedback(string message)
        {
            if (m_FeedbackText != null)
            {
                m_FeedbackText.text = message;
            }
            Debug.Log($"[UI_ItemStore] {message}");
        }
    }
}

