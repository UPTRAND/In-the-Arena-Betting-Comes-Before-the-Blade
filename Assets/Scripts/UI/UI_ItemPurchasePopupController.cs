#if UNITY_6000_0_OR_NEWER
using System.Threading;
using InTheArena.MainGame;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace InTheArena.UI
{
    [DisallowMultipleComponent]
    public sealed class UI_ItemPurchasePopupController : UI_Base, IItemPurchaseConfirmationView, IPointerClickHandler
    {
        [SerializeField] private Button m_BuyButton;
        [SerializeField] private TMP_Text m_ItemInfoText;
        [SerializeField] private TMP_Text m_SecondaryInfoText;
        [SerializeField] private TMP_Text m_GoldText;
        [SerializeField] private TMP_Text m_PriceText;
        [SerializeField] private RectTransform m_PopupPanel;

        private AwaitableCompletionSource m_CompletionSource;
        private Button m_PopupPanelButton;
        private ItemData m_ActiveItem;
        private ItemConfirmationMode m_Mode;
        private int m_CurrentGold;
        private int m_OwnedCount;
        private bool m_IsShowing;
        private bool m_IsRequestCompleted;
        private bool m_ListenersRegistered;
        private ItemPurchaseDecision m_LastDecision = ItemPurchaseDecision.Cancelled;

        public ItemPurchaseDecision LastDecision => m_LastDecision;

        protected override void Awake()
        {
            base.Awake();
            ResolveReferences();
            RegisterListeners();
        }

        public async Awaitable ShowAsync(
            ItemData itemData,
            ItemConfirmationMode mode,
            int currentGold,
            int ownedCount,
            CancellationToken token)
        {
            if (m_IsShowing || itemData == null)
            {
                return;
            }

            ResolveReferences();
            RegisterListeners();
            SetDisplay(itemData, mode, currentGold, ownedCount);

            var source = new AwaitableCompletionSource();
            m_LastDecision = ItemPurchaseDecision.Cancelled;
            m_IsRequestCompleted = false;
            m_CompletionSource = source;
            m_IsShowing = true;

            try
            {
                if (UIManager.Instance == null || !UIManager.Instance.OpenControl(this))
                {
                    Complete(ItemPurchaseDecision.Cancelled);
                    return;
                }

                using (CancellationTokenRegistration registration = token.Register(
                    static state => ((UI_ItemPurchasePopupController)state).Complete(
                        ItemPurchaseDecision.Cancelled),
                    this))
                {
                    await source.Awaitable;
                    // Keep the completed request alive until the next frame so a
                    // re-entrant ShowAsync call cannot replace its decision before
                    // popup cleanup has finished.
                    await Awaitable.NextFrameAsync();
                }
            }
            finally
            {
                if (ReferenceEquals(m_CompletionSource, source))
                {
                    m_CompletionSource = null;
                    m_IsShowing = false;
                    m_IsRequestCompleted = false;
                }

                if (UIManager.Instance != null)
                {
                    UIManager.Instance.CloseControl(this);
                }
            }
        }

        public Awaitable ShowAsync(
            ItemData itemData,
            int currentGold,
            CancellationToken token)
        {
            return ShowAsync(
                itemData,
                ItemConfirmationMode.Purchase,
                currentGold,
                0,
                token);
        }

        public void Cancel()
        {
            Complete(ItemPurchaseDecision.Cancelled);
        }

        private void OnBuyClicked()
        {
            if (m_ActiveItem == null)
            {
                return;
            }

            if (m_Mode == ItemConfirmationMode.Purchase &&
                m_CurrentGold < m_ActiveItem.PriceGold)
            {
                ShowInsufficientGold();
                return;
            }

            Complete(ItemPurchaseDecision.Confirmed);
        }

        private void OnPopupPanelClicked()
        {
            Complete(ItemPurchaseDecision.Cancelled);
        }

        private void Complete(ItemPurchaseDecision decision)
        {
            AwaitableCompletionSource source = m_CompletionSource;
            if (!m_IsShowing || m_IsRequestCompleted || source == null)
            {
                return;
            }

            m_IsRequestCompleted = true;
            m_LastDecision = decision;
            source.TrySetResult();
        }

        private void ResolveReferences()
        {
            m_BuyButton ??= FindButton("Btn_Buy");

            Transform innerPanel = transform.Find("PopupPanel/InnerPanel");
            m_ItemInfoText ??= innerPanel?.Find("Text_Info1")?.GetComponent<TMP_Text>();
            m_SecondaryInfoText ??= innerPanel?.Find("Text_Info2")?.GetComponent<TMP_Text>();
            m_GoldText ??= innerPanel?.Find("Gold_Box/Gold_text")?.GetComponent<TMP_Text>();
            m_PriceText ??= innerPanel?.Find("Text")?.GetComponent<TMP_Text>()
                ?? innerPanel?.Find("Btn_Buy/InnerButton/Text")?.GetComponent<TMP_Text>();
            m_PopupPanel ??= transform.Find("PopupPanel") as RectTransform;

            if (m_PopupPanel != null && m_PopupPanelButton == null)
            {
                m_PopupPanelButton = m_PopupPanel.GetComponent<Button>() ??
                    m_PopupPanel.gameObject.AddComponent<Button>();
                m_PopupPanelButton.targetGraphic = m_PopupPanel.GetComponent<Graphic>();
                m_PopupPanelButton.transition = Selectable.Transition.None;
            }
        }

        private void RegisterListeners()
        {
            if (m_ListenersRegistered)
            {
                return;
            }

            if (m_BuyButton != null)
            {
                m_BuyButton.onClick.AddListener(OnBuyClicked);
            }

            if (m_PopupPanelButton != null)
            {
                m_PopupPanelButton.onClick.AddListener(OnPopupPanelClicked);
            }

            m_ListenersRegistered = true;
        }

        private Button FindButton(string objectName)
        {
            Transform target = transform.Find($"PopupPanel/InnerPanel/{objectName}");
            return target != null ? target.GetComponent<Button>() : null;
        }

        private void SetDisplay(
            ItemData itemData,
            ItemConfirmationMode mode,
            int currentGold,
            int ownedCount)
        {
            m_ActiveItem = itemData;
            m_Mode = mode;
            m_CurrentGold = Mathf.Max(0, currentGold);
            m_OwnedCount = Mathf.Max(0, ownedCount);

            if (m_ItemInfoText != null)
            {
                m_ItemInfoText.text = mode == ItemConfirmationMode.Purchase
                    ? "아이템이 부족합니다."
                    : string.IsNullOrEmpty(itemData.ItemName) ? "아이템" : itemData.ItemName;
            }

            if (m_SecondaryInfoText != null)
            {
                m_SecondaryInfoText.text = mode == ItemConfirmationMode.Purchase
                    ? "구매하시겠습니까?"
                    : $"사용하시겠습니까? (보유 x{m_OwnedCount})";
            }

            if (m_GoldText != null)
            {
                m_GoldText.text = $"{m_CurrentGold} G";
                m_GoldText.gameObject.SetActive(mode == ItemConfirmationMode.Purchase);
            }

            if (m_PriceText != null)
            {
                m_PriceText.text = mode == ItemConfirmationMode.Purchase
                    ? $"{itemData.PriceGold} G"
                    : "사용";
            }

            if (mode == ItemConfirmationMode.Purchase &&
                m_CurrentGold < itemData.PriceGold)
            {
                ShowInsufficientGold();
            }
        }

        private void ShowInsufficientGold()
        {
            if (m_ItemInfoText != null)
            {
                m_ItemInfoText.text = "골드가 부족합니다.";
            }

            if (m_SecondaryInfoText != null)
            {
                int shortage = m_ActiveItem == null ? 0 : Mathf.Max(0, m_ActiveItem.PriceGold - m_CurrentGold);
                m_SecondaryInfoText.text = shortage > 0
                    ? $"{shortage} G가 더 필요합니다."
                    : "구매할 수 없습니다.";
            }
        }

        public void OnPointerClick(PointerEventData eventData)
        {
            if (!m_IsShowing ||
                m_IsRequestCompleted ||
                eventData == null ||
                m_PopupPanel == null)
            {
                return;
            }

            bool isInsidePopup = RectTransformUtility.RectangleContainsScreenPoint(
                m_PopupPanel,
                eventData.position,
                eventData.pressEventCamera);

            if (!isInsidePopup)
            {
                Complete(ItemPurchaseDecision.Cancelled);
            }
        }

        private void OnDisable()
        {
            Complete(ItemPurchaseDecision.Cancelled);
        }

        protected override void OnDestroy()
        {
            if (m_ListenersRegistered && m_BuyButton != null)
            {
                m_BuyButton.onClick.RemoveListener(OnBuyClicked);
            }

            if (m_ListenersRegistered && m_PopupPanelButton != null)
            {
                m_PopupPanelButton.onClick.RemoveListener(OnPopupPanelClicked);
            }

            Complete(ItemPurchaseDecision.Cancelled);
            base.OnDestroy();
        }
    }
}
#endif
