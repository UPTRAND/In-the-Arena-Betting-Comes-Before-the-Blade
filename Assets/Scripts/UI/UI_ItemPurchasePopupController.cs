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
        [SerializeField] private TMP_Text m_PriceText;
        [SerializeField] private RectTransform m_PopupPanel;

        private AwaitableCompletionSource m_CompletionSource;
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
            int currentGold,
            CancellationToken token)
        {
            if (m_IsShowing || itemData == null)
            {
                return;
            }

            ResolveReferences();
            RegisterListeners();
            SetDisplay(itemData, currentGold);

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

        public void Cancel()
        {
            Complete(ItemPurchaseDecision.Cancelled);
        }

        private void OnBuyClicked()
        {
            Complete(ItemPurchaseDecision.Confirmed);
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
            m_PriceText ??= innerPanel?.Find("Text")?.GetComponent<TMP_Text>()
                ?? innerPanel?.Find("Btn_Buy/InnerButton/Text")?.GetComponent<TMP_Text>();
            m_PopupPanel ??= transform.Find("PopupPanel") as RectTransform;
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

            m_ListenersRegistered = true;
        }

        private Button FindButton(string objectName)
        {
            Transform target = transform.Find($"PopupPanel/InnerPanel/{objectName}");
            return target != null ? target.GetComponent<Button>() : null;
        }

        private void SetDisplay(ItemData itemData, int currentGold)
        {
            if (m_ItemInfoText != null)
            {
                m_ItemInfoText.text = $"{itemData.ItemName}을(를) 구매할까요?\n보유 골드: {currentGold}";
            }

            if (m_PriceText != null)
            {
                m_PriceText.text = $"{itemData.PriceGold} G";
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

            Complete(ItemPurchaseDecision.Cancelled);
            base.OnDestroy();
        }
    }
}
#endif
