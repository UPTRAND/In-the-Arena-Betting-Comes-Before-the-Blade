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
        [SerializeField] private Button m_SettingsButton;
        [SerializeField] private UI_OptionsPopup m_OptionsPopupPrefab;

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

        private BettingPhase m_BettingPhase;
        private RoundContext m_RoundContext;
        private StagePlayerState m_PlayerState;
        private Image m_AdditionalBetIcon;
        private Image m_InsuranceIcon;
        private Image m_RerollIcon;
        private UI_ItemSlotPresenter m_AdditionalBetPresenter;
        private UI_ItemSlotPresenter m_InsurancePresenter;
        private UI_ItemSlotPresenter m_RerollPresenter;
        private bool m_IsSubscribed;
        private CancellationTokenSource m_ItemUseLifetimeCancellation;

        protected override void Awake()
        {
            base.Awake();
            m_ItemUseLifetimeCancellation = new CancellationTokenSource();
            ResolveItemIcons();
            ApplyItemIcons();
            ResolveItemPresenters();
            RefreshItemCounts();
            m_SettingsButton ??= FindDescendant(transform, "Settings_Button")?.GetComponent<Button>();

            if (m_SettingsButton != null)
            {
                m_SettingsButton.onClick.AddListener(OpenOptionsPopup);
            }

            if (m_AdditionalBetButton != null)
            {
                m_AdditionalBetButton.onClick.AddListener(OnAdditionalBetClicked);
            }

            if (m_InsuranceButton != null)
            {
                m_InsuranceButton.onClick.AddListener(OnInsuranceClicked);
            }

            if (m_RerollButton != null)
            {
                m_RerollButton.onClick.AddListener(OnRerollClicked);
            }
        }

        public void BindAndShow(
            BettingPhase bettingPhase,
            RoundContext roundContext,
            StagePlayerState playerState)
        {
            if ((m_BettingPhase != null && m_BettingPhase != bettingPhase) ||
                (m_RoundContext != null && m_RoundContext != roundContext))
            {
                UnsubscribeEvents();
            }

            m_BettingPhase = bettingPhase;
            m_RoundContext = roundContext;
            m_PlayerState = playerState;

            ResolveItemIcons();
            ApplyItemIcons();
            ResolveItemPresenters();

            if (!BIsOpened)
            {
                Open();
            }
            else
            {
                SubscribeEvents();
            }

            Enable();
            RefreshDisplay();
        }

        private void OnAdditionalBetClicked()
        {
            TryUseItem(m_AdditionalBetData);
        }

        private void OnInsuranceClicked()
        {
            TryUseItem(m_InsuranceData);
        }

        private void OnRerollClicked()
        {
            TryUseItem(m_RerollData);
        }

        private void TryUseItem(ItemData itemData)
        {
            if (RoundManager.Instance == null)
            {
                SoundManager.Instance?.PlaySfx(SfxIds.ButtonNegative);
                ShowFeedback("라운드 매니저를 찾을 수 없습니다.");
                return;
            }

            if (RoundManager.Instance.BettingPhase == null)
            {
                SoundManager.Instance?.PlaySfx(SfxIds.ButtonNegative);
                ShowFeedback("현재 배팅 페이즈가 아닙니다.");
                return;
            }

            BettingPhase bettingPhase = m_BettingPhase ?? RoundManager.Instance.BettingPhase;
            RoundContext roundContext = m_RoundContext ?? RoundManager.Instance.Context;
            StagePlayerState playerState = m_PlayerState ?? StageManager.Instance?.PlayerState;
            UI_ItemPurchasePopupController popup = m_ItemPurchasePopup ??
                UIManager.Instance?.GetElement<UI_ItemPurchasePopupController>();
            ItemPurchaseUseCoordinator coordinator = RoundManager.Instance.ItemPurchaseUseCoordinator;

            if (itemData == null || bettingPhase == null || roundContext == null || playerState == null ||
                popup == null || popup.ParentRoot == null || coordinator == null)
            {
                SoundManager.Instance?.PlaySfx(SfxIds.ButtonNegative);
                ShowFeedback("아이템 구매 팝업 또는 배팅 페이즈가 없어 사용 불가합니다.");
                return;
            }

            if (coordinator.State != ItemPurchaseUseState.Idle)
            {
                RefreshItemButtons();
                return;
            }

            if (itemData.PriceGold < 0)
            {
                SoundManager.Instance?.PlaySfx(SfxIds.ButtonNegative);
                ShowFeedback("아이템 가격이 잘못되었습니다.");
                return;
            }

            m_BettingPhase = bettingPhase;
            m_RoundContext = roundContext;
            m_PlayerState = playerState;
            SoundManager.Instance?.PlaySfx(SfxIds.ButtonPositive);
            _ = TryUseItemThroughPurchaseFlowAsync(itemData, bettingPhase, coordinator, popup);
            RefreshItemButtons();
        }

        private async Awaitable TryUseItemThroughPurchaseFlowAsync(
            ItemData itemData,
            BettingPhase bettingPhase,
            ItemPurchaseUseCoordinator coordinator,
            UI_ItemPurchasePopupController popup)
        {
            await coordinator.RequestImmediateUseAsync(
                itemData,
                popup,
                new BettingItemUseExecutor(bettingPhase),
                m_ItemUseLifetimeCancellation?.Token ?? CancellationToken.None);

            RefreshItemButtons();
            RefreshItemCounts();
            switch (coordinator.LastResult)
            {
                case ItemPurchaseUseResult.PurchaseSucceeded:
                    SoundManager.Instance?.PlaySfx(SfxIds.ButtonPositive);
                    ShowFeedback("베팅 아이템을 구매했습니다.");
                    break;
                case ItemPurchaseUseResult.UseSucceeded:
                    SoundManager.Instance?.PlaySfx(SfxIds.ButtonPositive);
                    ShowFeedback("베팅 아이템을 사용했습니다.");
                    break;
                case ItemPurchaseUseResult.Rejected:
                    SoundManager.Instance?.PlaySfx(SfxIds.ButtonNegative);
                    ShowFeedback("이번 라운드에 이미 사용한 아이템입니다.");
                    break;
                case ItemPurchaseUseResult.Failed:
                    SoundManager.Instance?.PlaySfx(SfxIds.ButtonNegative);
                    ShowFeedback("베팅 아이템을 처리하지 못했습니다.");
                    break;
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

        public override void OnOpened()
        {
            base.OnOpened();
            SubscribeEvents();
            RefreshDisplay();
        }

        public override void OnClosed()
        {
            UnsubscribeEvents();
            base.OnClosed();
        }

        private void OnDisable()
        {
            UnsubscribeEvents();
        }

        protected override void OnDestroy()
        {
            UnsubscribeEvents();

            if (m_SettingsButton != null)
            {
                m_SettingsButton.onClick.RemoveListener(OpenOptionsPopup);
            }

            if (m_AdditionalBetButton != null)
            {
                m_AdditionalBetButton.onClick.RemoveListener(OnAdditionalBetClicked);
            }

            if (m_InsuranceButton != null)
            {
                m_InsuranceButton.onClick.RemoveListener(OnInsuranceClicked);
            }

            if (m_RerollButton != null)
            {
                m_RerollButton.onClick.RemoveListener(OnRerollClicked);
            }

            base.OnDestroy();
            m_ItemUseLifetimeCancellation?.Cancel();
            m_ItemUseLifetimeCancellation?.Dispose();
            m_ItemUseLifetimeCancellation = null;
        }

        private void HandleSpecialBetChanged()
        {
            ShowFeedback("특수 배팅 룰이 갱신되었습니다!");
            RefreshItemButtons();
        }

        private void HandleBettingItemUsed(ItemData itemData)
        {
            RefreshItemCounts();
            RefreshItemButtons();
        }

        private void SubscribeEvents()
        {
            if (m_IsSubscribed)
            {
                return;
            }

            if (m_BettingPhase != null)
            {
                m_BettingPhase.OnItemUsed += HandleBettingItemUsed;
            }

            if (m_RoundContext != null)
            {
                m_RoundContext.OnSpecialBetChanged += HandleSpecialBetChanged;
            }

            m_IsSubscribed = true;
        }

        private void UnsubscribeEvents()
        {
            if (!m_IsSubscribed)
            {
                return;
            }

            if (m_BettingPhase != null)
            {
                m_BettingPhase.OnItemUsed -= HandleBettingItemUsed;
            }

            if (m_RoundContext != null)
            {
                m_RoundContext.OnSpecialBetChanged -= HandleSpecialBetChanged;
            }

            m_IsSubscribed = false;
        }

        private void RefreshDisplay()
        {
            ResolveItemIcons();
            ApplyItemIcons();
            RefreshItemCounts();
            RefreshItemButtons();
        }

        public void RefreshItemCounts()
        {
            UpdateCountText(m_AdditionalBetData, m_AdditionalBetCountText);
            UpdateCountText(m_InsuranceData, m_InsuranceCountText);
            UpdateCountText(m_RerollData, m_RerollCountText);
            RefreshPresenter(
                m_AdditionalBetPresenter,
                m_AdditionalBetData);
            RefreshPresenter(
                m_InsurancePresenter,
                m_InsuranceData);
            RefreshPresenter(
                m_RerollPresenter,
                m_RerollData);
        }

        private void UpdateCountText(ItemData itemData, TMP_Text countText)
        {
            if (itemData != null && countText != null)
            {
                int count = SaveManager.Instance != null
                    ? SaveManager.Instance.GetItemCount(itemData.ItemType)
                    : 0;
                countText.text = $"x{count}";
            }
        }

        private void RefreshItemButtons()
        {
            SetItemButtonState(m_AdditionalBetButton, m_AdditionalBetData);
            SetItemButtonState(m_InsuranceButton, m_InsuranceData);
            SetItemButtonState(m_RerollButton, m_RerollData);
        }

        private void SetItemButtonState(Button button, ItemData itemData)
        {
            if (button == null)
            {
                return;
            }

            StagePlayerState playerState = m_PlayerState ?? StageManager.Instance?.PlayerState;
            ItemPurchaseUseCoordinator coordinator = RoundManager.Instance?.ItemPurchaseUseCoordinator;
            bool canRequest = itemData != null &&
                          itemData.ItemType != ItemType.None &&
                          itemData.PriceGold >= 0 &&
                          m_BettingPhase != null &&
                          !m_BettingPhase.IsPhaseCompleted &&
                           m_RoundContext != null &&
                           playerState != null &&
                           coordinator != null &&
                           coordinator.State == ItemPurchaseUseState.Idle;

            button.interactable = canRequest;
        }

        private void ResolveItemPresenters()
        {
            m_AdditionalBetPresenter = ResolvePresenter(
                m_AdditionalBetButton,
                m_AdditionalBetPresenter);
            m_InsurancePresenter = ResolvePresenter(
                m_InsuranceButton,
                m_InsurancePresenter);
            m_RerollPresenter = ResolvePresenter(
                m_RerollButton,
                m_RerollPresenter);
        }

        private static UI_ItemSlotPresenter ResolvePresenter(
            Button button,
            UI_ItemSlotPresenter presenter)
        {
            if (presenter != null || button == null)
            {
                return presenter;
            }

            return button.GetComponent<UI_ItemSlotPresenter>() ??
                button.gameObject.AddComponent<UI_ItemSlotPresenter>();
        }

        private void RefreshPresenter(
            UI_ItemSlotPresenter presenter,
            ItemData itemData)
        {
            if (presenter == null)
            {
                return;
            }

            presenter.Bind(itemData);
            bool used = m_RoundContext != null && itemData != null &&
                m_RoundContext.RoundItemUsage.HasUsed(itemData.ItemType);
            presenter.SetState(
                used ? ItemSlotVisualState.Used : ItemSlotVisualState.Normal);
        }

        private void ResolveItemIcons()
        {
            m_AdditionalBetIcon ??= FindChildIcon(m_AdditionalBetButton);
            m_InsuranceIcon ??= FindChildIcon(m_InsuranceButton);
            m_RerollIcon ??= FindChildIcon(m_RerollButton);
        }

        private void ApplyItemIcons()
        {
            SetItemIcon(m_AdditionalBetIcon, m_AdditionalBetData);
            SetItemIcon(m_InsuranceIcon, m_InsuranceData);
            SetItemIcon(m_RerollIcon, m_RerollData);
        }

        private static Image FindChildIcon(Button button)
        {
            if (button == null)
            {
                return null;
            }

            Image frameImage = button.image;
            Image[] childImages = button.GetComponentsInChildren<Image>(true);
            for (int i = 0; i < childImages.Length; i++)
            {
                Image image = childImages[i];
                if (image != null && image != frameImage)
                {
                    return image;
                }
            }

            return null;
        }

        private static void SetItemIcon(Image image, ItemData itemData)
        {
            if (image == null)
            {
                return;
            }

            image.sprite = itemData?.Icon;
            image.gameObject.SetActive(image.sprite != null);
        }

        private static Transform FindDescendant(Transform root, string objectName)
        {
            if (root == null) return null;
            if (root.name == objectName) return root;

            for (int i = 0; i < root.childCount; i++)
            {
                Transform result = FindDescendant(root.GetChild(i), objectName);
                if (result != null) return result;
            }

            return null;
        }

        private void OpenOptionsPopup()
        {
            SoundManager.Instance?.PlaySfx(SfxIds.ButtonPositive);
            UI_OptionsPopup.Show(m_OptionsPopupPrefab, GetComponentInParent<UI_Root>());
        }
    }
}
#endif
