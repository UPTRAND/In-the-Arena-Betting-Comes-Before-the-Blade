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
            ShowFeedback("아이템은 더 이상 인벤토리에 보관되지 않으며 즉시 사용됩니다.");
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
                m_CurrentCountText.text = "0";
                m_CurrentCountText.gameObject.SetActive(false);
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
