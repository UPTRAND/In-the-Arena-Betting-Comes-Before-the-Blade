#if UNITY_6000_0_OR_NEWER
using System.Collections.Generic;
using System.Threading;
using DG.Tweening;
using InTheArena.MainGame;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace InTheArena.UI
{
    [DisallowMultipleComponent]
    public sealed class UI_BattlePhaseHUD : UI_Base
    {
        [Header("Battle Information")]
        [SerializeField] private TMP_Text m_RedTeamCountText;
        [SerializeField] private Slider m_RedTeamSlider;
        [SerializeField] private TMP_Text m_BlueTeamCountText;
        [SerializeField] private Slider m_BlueTeamSlider;
        [SerializeField] private TMP_Text m_BattleTimerText;

        [Header("Speed Control")]
        [SerializeField] private Button m_SpeedButton;
        [SerializeField] private TMP_Text m_SpeedMultiplierText;

        [Header("View Switch")]
        [SerializeField] private GameObject m_BattleGroup;
        [SerializeField] private GameObject m_BettingGroup;
        [SerializeField] private Button m_BattleSwapButton;
        [SerializeField] private Button m_BettingSwapButton;
        [SerializeField] private Image[] m_RedUnitSlotImages = new Image[6];
        [SerializeField] private Image[] m_BlueUnitSlotImages = new Image[6];
        [SerializeField] private Image[] m_RedUnitPortraitImages = new Image[6];
        [SerializeField] private Image[] m_BlueUnitPortraitImages = new Image[6];
        [SerializeField] private TMP_Text[] m_RedUnitSlotTexts = new TMP_Text[6];
        [SerializeField] private TMP_Text[] m_BlueUnitSlotTexts = new TMP_Text[6];

        [Header("Stage Information")]
        [SerializeField] private GameObject m_WinningTeamHistoryRoot;
        [SerializeField] private TMP_Text m_WinningTeamHistoryText;
        [SerializeField] private GameObject m_GameEndTimeHistoryRoot;
        [SerializeField] private TMP_Text m_GameEndTimeHistoryText;
        [SerializeField] private GameObject m_OddEvenHistoryRoot;
        [SerializeField] private TMP_Text m_OddEvenHistoryText;
        [SerializeField] private GameObject m_FirstAnnihilatedHistoryRoot;
        [SerializeField] private TMP_Text m_FirstAnnihilatedHistoryText;
        [SerializeField] private GameObject m_SurvivingSlotsHistoryRoot;
        [SerializeField] private TMP_Text m_SurvivingSlotsHistoryText;

        [Header("Combat Items")]
        [SerializeField] private Button m_ItemSlot1Button;
        [SerializeField] private Image m_ItemSlot1Icon;
        [SerializeField] private ItemData m_ItemSlot1Data;
        [SerializeField] private Button m_ItemSlot2Button;
        [SerializeField] private Image m_ItemSlot2Icon;
        [SerializeField] private ItemData m_ItemSlot2Data;
        [SerializeField] private Button m_ItemSlot3Button;
        [SerializeField] private Image m_ItemSlot3Icon;
        [SerializeField] private ItemData m_ItemSlot3Data;
        private UI_ItemSlotPresenter m_ItemSlot1Presenter;
        private UI_ItemSlotPresenter m_ItemSlot2Presenter;
        private UI_ItemSlotPresenter m_ItemSlot3Presenter;
        [SerializeField] private UI_ItemPurchasePopupController m_ItemPurchasePopup;
        [SerializeField] private Sprite m_CancelOverlaySprite;
        [SerializeField] private TMP_FontAsset m_DuplicateItemFeedbackFont;
        [SerializeField] private UI_CombatItemTargetingController m_CombatItemTargetingController;
        [SerializeField] private Image m_ItemSlot1CancelOverlay;
        [SerializeField] private Image m_ItemSlot2CancelOverlay;
        [SerializeField] private Image m_ItemSlot3CancelOverlay;
        [SerializeField] private TMP_Text m_DuplicateItemFeedbackText;
        [SerializeField] private CanvasGroup m_DuplicateItemFeedbackCanvasGroup;
        [SerializeField] private RectTransform m_DuplicateItemFeedbackRect;

        private CombatPhase m_CombatPhase;
        private RoundContext m_RoundContext;
        private StagePlayerState m_PlayerState;

        private ItemData m_ActiveTargetingItemData;
        private Image m_ActiveTargetingCancelOverlay;
        private bool m_IsSubscribed;
        private CancellationTokenSource m_ItemUseLifetimeCancellation;
        private CancellationTokenSource m_TargetingLifetimeCancellation;
        private long m_ActiveTargetingRequestVersion = -1;
        private Tween m_DuplicateItemFeedbackTween;
        private Vector2 m_DuplicateItemFeedbackBasePosition;

        protected override void Awake()
        {
            base.Awake();
            m_ItemUseLifetimeCancellation = new CancellationTokenSource();
            m_TargetingLifetimeCancellation = new CancellationTokenSource();
            EnsureCombatItemTargetingController();
            EnsureCancelOverlays();
            EnsureDuplicateItemFeedback();
            ApplyItemIcons();
            ResolveItemPresenters();
            SetBattleView(true);
            ResetDisplay();
        }

        public void BindAndShow(
            CombatPhase combatPhase,
            RoundContext roundContext,
            StagePlayerState playerState)
        {
            if (m_CombatPhase != null && m_CombatPhase != combatPhase)
            {
                UnsubscribeEvents();
            }

            m_CombatPhase = combatPhase;
            m_RoundContext = roundContext;
            m_PlayerState = playerState;

            if (!BIsOpened)
            {
                Open();
            }
            else
            {
                SubscribeEvents();
            }

            Enable();
            ApplyItemIcons();
            ResolveItemPresenters();
            SetBattleView(true);
            Refresh();
        }

        public void UnbindAndHide()
        {
            m_TargetingLifetimeCancellation?.Cancel();

            if (BIsOpened)
            {
                Close();
            }
            else
            {
                UnsubscribeEvents();
                ResetDisplay();
            }

            ClearBindings();
        }

        public override void OnOpened()
        {
            base.OnOpened();
            SubscribeEvents();
            Refresh();
        }

        public override void OnClosed()
        {
            m_TargetingLifetimeCancellation?.Cancel();
            CancelTargetingRequest();
            UnsubscribeEvents();
            ResetDisplay();
            base.OnClosed();
        }

        private void Update()
        {
            if (m_CombatPhase != null && m_RoundContext != null)
            {
                Refresh();
            }
        }

        #if false
        public void OnBeginDrag(PointerEventData eventData)
        {
            m_DraggedItem = ResolveDraggedItem(eventData);
            if (m_DraggedItem == null) return;
        }

        public void OnDrag(PointerEventData eventData)
        {
            // InputManager owns the screen-to-world conversion. No HUD-local targeting state is required.
        }

        public void OnEndDrag(PointerEventData eventData)
        {
            ItemData itemData = m_DraggedItem;
            m_DraggedItem = null;

            if (itemData == null || m_CombatPhase == null || InputManager.Instance == null)
            {
                return;
            }

            if (CanUseNewImmediateFlow(itemData) && itemData.ItemType == ItemType.TimeExtension)
            {
                Debug.Log("[UI_BattlePhaseHUD] 시간 연장 아이템은 클릭으로 사용해야 합니다.");
                return;
            }

            if (CanUseNewTargetingFlow(itemData))
            {
                // 타기팅 기반 아이템 사용은 비동기 흐름과 InputManager.OnSkillDragEnded를 통해 커밋됨
                return;
            }

            if (!InputManager.Instance.RaycastGroundPosition(eventData.position, out Vector3 worldPosition))
            {
                Debug.Log("[UI_BattlePhaseHUD] 전장 영역에 아이템을 드롭해야 합니다.");
                return;
            }

            Debug.Log("[UI_BattlePhaseHUD] 인벤토리 기반 아이템 직접 사용은 제거되었습니다.");
        }

        #endif

        #if false
        private async void RequestTargetedItemUse(ItemData itemData)
        {
            ItemPurchaseUseCoordinator coordinator = RoundManager.Instance?.ItemPurchaseUseCoordinator;
            UI_ItemPurchasePopupController popup = m_ItemPurchasePopup ??
                UIManager.Instance?.GetElement<UI_ItemPurchasePopupController>();

            if (coordinator == null || popup == null) return;

            if (coordinator.State != ItemPurchaseUseState.Idle)
            {
                return;
            }

            m_TargetingLifetimeCancellation?.Cancel();
            m_TargetingLifetimeCancellation?.Dispose();
            m_TargetingLifetimeCancellation = new CancellationTokenSource();

            long requestVersion = await coordinator.RequestTargetedUseAsync(
                itemData,
                popup,
                m_TargetingLifetimeCancellation.Token);

            if (requestVersion != -1 && coordinator.State == ItemPurchaseUseState.AwaitingTarget && coordinator.ActiveRequestVersion == requestVersion)
            {
                m_ActiveTargetingRequestVersion = requestVersion;
                m_ActiveTargetingItemData = itemData;
                InputManager.Instance.ArmSkillTargeting(0, (int)requestVersion);
                InputManager.Instance.OnSkillDragEnded += OnSkillDragEnded;
            }
        }

        #endif

        private async void RequestTargetedItemUse(ItemData itemData)
        {
            ItemPurchaseUseCoordinator coordinator = RoundManager.Instance?.ItemPurchaseUseCoordinator;
            UI_ItemPurchasePopupController popup = m_ItemPurchasePopup ??
                UIManager.Instance?.GetElement<UI_ItemPurchasePopupController>();

            if (coordinator == null || popup == null || m_CombatPhase == null ||
                !CanUseNewTargetingFlow(itemData) || coordinator.State != ItemPurchaseUseState.Idle)
            {
                return;
            }

            m_TargetingLifetimeCancellation?.Cancel();
            m_TargetingLifetimeCancellation?.Dispose();
            m_TargetingLifetimeCancellation = new CancellationTokenSource();

            long requestVersion = await coordinator.RequestTargetedUseAsync(
                itemData,
                popup,
                m_TargetingLifetimeCancellation.Token);

            if (this == null || requestVersion == -1 ||
                m_TargetingLifetimeCancellation.IsCancellationRequested ||
                coordinator.State != ItemPurchaseUseState.AwaitingTarget ||
                coordinator.ActiveRequestVersion != requestVersion)
            {
                if (coordinator.LastResult == ItemPurchaseUseResult.PurchaseSucceeded)
                {
                    RefreshItemButtons();
                }
                return;
            }

            ResolveItemSlot(itemData, out RectTransform selectedSlot, out Image cancelOverlay);
            if (selectedSlot == null || m_CombatItemTargetingController == null ||
                !m_CombatItemTargetingController.BeginTargeting(
                    itemData.ItemType,
                    m_CombatPhase,
                    selectedSlot,
                    cancelOverlay))
            {
                coordinator.CancelActiveRequest();
                return;
            }

            m_ActiveTargetingRequestVersion = requestVersion;
            m_ActiveTargetingItemData = itemData;
            m_ActiveTargetingCancelOverlay = cancelOverlay;
            ResolvePresenter(itemData)?.SetState(ItemSlotVisualState.Casting);
        }

        private IItemPurchaseUseExecutor CreateExecutor(ItemData itemData, Vector3 worldPos)
        {
            if (itemData.ItemType == ItemType.Meteor) return new CombatMeteorUseExecutor(m_CombatPhase, worldPos);
            if (itemData.ItemType == ItemType.Mercenary) return new CombatMercenaryUseExecutor(m_CombatPhase, worldPos);
            return null;
        }

        #if false
        private void DetachTargetingInput()
        {
            if (InputManager.Instance != null)
            {
                InputManager.Instance.OnSkillDragEnded -= OnSkillDragEnded;
                InputManager.Instance.CancelSkillDrag();
            }
            m_ActiveTargetingRequestVersion = -1;
        }

        private void CancelTargetingRequest()
        {
            long requestVersion = m_ActiveTargetingRequestVersion;
            DetachTargetingInput();
            m_ActiveTargetingItemData = null;

            ItemPurchaseUseCoordinator coordinator = RoundManager.Instance?.ItemPurchaseUseCoordinator;
            if (coordinator != null && coordinator.State == ItemPurchaseUseState.AwaitingTarget && coordinator.ActiveRequestVersion == requestVersion)
            {
                coordinator.CancelActiveRequest();
            }

            m_TargetingLifetimeCancellation?.Cancel();
        }

        private void OnSkillDragEnded(int skillId, int sessionId, Vector2 screenPos, Vector3 worldPos, bool isCanceled, bool isValid)
        {
            if (sessionId != m_ActiveTargetingRequestVersion)
            {
                return;
            }

            if (m_CombatPhase == null || m_ActiveTargetingItemData == null)
            {
                CancelTargetingRequest();
                return;
            }

            long targetVersion = m_ActiveTargetingRequestVersion;
            ItemPurchaseUseCoordinator coordinator = RoundManager.Instance?.ItemPurchaseUseCoordinator;

            if (isCanceled || !isValid || coordinator == null || coordinator.ActiveRequestVersion != targetVersion)
            {
                CancelTargetingRequest();
                return;
            }

            DetachTargetingInput();

            IItemPurchaseUseExecutor executor = CreateExecutor(m_ActiveTargetingItemData, worldPos);
            m_ActiveTargetingItemData = null;

            bool success = coordinator.TryCompleteTargetUse(
                targetVersion,
                executor,
                out ItemPurchaseUseResult result,
                out string message);

            Debug.Log($"[UI_BattlePhaseHUD] {message}");
            if (success)
            {
                RefreshItemButtons();
            }
        }

        #endif

        private void ClearTargetingBinding()
        {
            m_ActiveTargetingRequestVersion = -1;
            m_ActiveTargetingItemData = null;
            m_ActiveTargetingCancelOverlay = null;
        }

        private void CancelTargetingRequest()
        {
            long requestVersion = m_ActiveTargetingRequestVersion;
            ItemData itemData = m_ActiveTargetingItemData;
            m_CombatItemTargetingController?.AbortTargeting();
            ResolvePresenter(itemData)?.SetState(ItemSlotVisualState.Normal);
            ClearTargetingBinding();

            ItemPurchaseUseCoordinator coordinator = RoundManager.Instance?.ItemPurchaseUseCoordinator;
            if (coordinator != null && coordinator.State == ItemPurchaseUseState.AwaitingTarget &&
                coordinator.ActiveRequestVersion == requestVersion)
            {
                coordinator.CancelActiveRequest();
            }

            m_TargetingLifetimeCancellation?.Cancel();
        }

        private void OnTargetConfirmed(Vector3 worldPosition)
        {
            if (m_CombatPhase == null || m_ActiveTargetingItemData == null)
            {
                CancelTargetingRequest();
                return;
            }

            long targetVersion = m_ActiveTargetingRequestVersion;
            ItemData itemData = m_ActiveTargetingItemData;
            ItemPurchaseUseCoordinator coordinator = RoundManager.Instance?.ItemPurchaseUseCoordinator;
            if (coordinator == null || coordinator.State != ItemPurchaseUseState.AwaitingTarget ||
                coordinator.ActiveRequestVersion != targetVersion)
            {
                CancelTargetingRequest();
                return;
            }

            IItemPurchaseUseExecutor executor = CreateExecutor(itemData, worldPosition);
            UI_ItemSlotPresenter presenter = ResolvePresenter(itemData);
            ClearTargetingBinding();

            bool success = coordinator.TryCompleteTargetUse(
                targetVersion,
                executor,
                out ItemPurchaseUseResult result,
                out string message);

            Debug.Log($"[UI_BattlePhaseHUD] {message}");
            if (success)
            {
                SoundManager.Instance?.PlaySfx(SfxIds.ButtonPositive);
                presenter?.SetState(ItemSlotVisualState.Used);
                RefreshItemButtons();
            }
            else
            {
                SoundManager.Instance?.PlaySfx(SfxIds.ButtonNegative);
                presenter?.SetState(ItemSlotVisualState.Normal);
            }
        }

        private void OnTargetCanceled()
        {
            SoundManager.Instance?.PlaySfx(SfxIds.ButtonNegative);
            CancelTargetingRequest();
        }

        private void SubscribeEvents()
        {
            if (m_IsSubscribed || m_CombatPhase == null)
            {
                return;
            }

            if (m_SpeedButton != null)
            {
                m_SpeedButton.onClick.AddListener(OnSpeedButtonClicked);
            }
            if (m_BattleSwapButton != null) m_BattleSwapButton.onClick.AddListener(OnBattleSwapClicked);
            if (m_BettingSwapButton != null) m_BettingSwapButton.onClick.AddListener(OnBettingSwapClicked);

            if (m_ItemSlot1Button != null)
            {
                m_ItemSlot1Button.onClick.AddListener(OnItemSlot1Clicked);
            }

            if (m_ItemSlot2Button != null)
            {
                m_ItemSlot2Button.onClick.AddListener(OnItemSlot2Clicked);
            }

            if (m_ItemSlot3Button != null)
            {
                m_ItemSlot3Button.onClick.AddListener(OnItemSlot3Clicked);
            }

            m_CombatPhase.OnItemUsed += OnCombatItemUsed;
            m_CombatPhase.OnCombatStateChanged += RefreshCombatState;
            m_IsSubscribed = true;
        }

        private void UnsubscribeEvents()
        {
            if (!m_IsSubscribed)
            {
                return;
            }

            if (m_SpeedButton != null)
            {
                m_SpeedButton.onClick.RemoveListener(OnSpeedButtonClicked);
            }
            if (m_BattleSwapButton != null) m_BattleSwapButton.onClick.RemoveListener(OnBattleSwapClicked);
            if (m_BettingSwapButton != null) m_BettingSwapButton.onClick.RemoveListener(OnBettingSwapClicked);

            if (m_ItemSlot1Button != null)
            {
                m_ItemSlot1Button.onClick.RemoveListener(OnItemSlot1Clicked);
            }

            if (m_ItemSlot2Button != null)
            {
                m_ItemSlot2Button.onClick.RemoveListener(OnItemSlot2Clicked);
            }

            if (m_ItemSlot3Button != null)
            {
                m_ItemSlot3Button.onClick.RemoveListener(OnItemSlot3Clicked);
            }

            if (m_CombatPhase != null)
            {
                m_CombatPhase.OnItemUsed -= OnCombatItemUsed;
                m_CombatPhase.OnCombatStateChanged -= RefreshCombatState;
            }

            m_IsSubscribed = false;
        }

        private void OnSpeedButtonClicked()
        {
            if (!CanAcceptCombatInput())
            {
                return;
            }

            m_CombatPhase.ToggleCombatSpeed();
            SoundManager.Instance?.PlaySfx(SfxIds.ButtonPositive);
            RefreshCombatState();
        }

        private void OnBattleSwapClicked()
        {
            SoundManager.Instance?.PlaySfx(SfxIds.ButtonPositive);
            SetBattleView(true);
        }

        private void OnBettingSwapClicked()
        {
            SoundManager.Instance?.PlaySfx(SfxIds.ButtonPositive);
            SetBattleView(false);
        }

        private void SetBattleView(bool showBattle)
        {
            SetActive(m_BattleGroup, showBattle);
            SetActive(m_BettingGroup, !showBattle);
        }

        private void RequestItemUse(ItemData itemData)
        {
            if (itemData == null || !CanAcceptCombatInput() || !IsCombatItem(itemData))
            {
                return;
            }

            SoundManager.Instance?.PlaySfx(SfxIds.ButtonPositive);
            if (CanUseNewImmediateFlow(itemData))
            {
                RequestImmediateItemUse(itemData);
                return;
            }

            if (CanUseNewTargetingFlow(itemData))
            {
                RequestTargetedItemUse(itemData);
            }
        }

        private static bool IsCombatItem(ItemData itemData)
        {
            return itemData != null &&
                   (itemData.ItemType == ItemType.Meteor ||
                    itemData.ItemType == ItemType.Mercenary ||
                    itemData.ItemType == ItemType.TimeExtension);
        }

        private void OnItemSlot1Clicked() => RequestItemUse(m_ItemSlot1Data);
        private void OnItemSlot2Clicked() => RequestItemUse(m_ItemSlot2Data);
        private void OnItemSlot3Clicked() => RequestItemUse(m_ItemSlot3Data);

        private void RequestImmediateItemUse(ItemData itemData)
        {
            if (!CanUseNewImmediateFlow(itemData) || itemData.ItemType != ItemType.TimeExtension)
            {
                return;
            }

            ItemPurchaseUseCoordinator coordinator = RoundManager.Instance?.ItemPurchaseUseCoordinator;
            UI_ItemPurchasePopupController popup = m_ItemPurchasePopup ??
                UIManager.Instance?.GetElement<UI_ItemPurchasePopupController>();
            if (coordinator == null || popup == null || popup.ParentRoot == null)
            {
                return;
            }

            _ = RequestImmediateItemUseAsync(itemData, coordinator, popup);
        }

        private async Awaitable RequestImmediateItemUseAsync(
            ItemData itemData,
            ItemPurchaseUseCoordinator coordinator,
            UI_ItemPurchasePopupController popup)
        {
            await coordinator.RequestImmediateUseAsync(
                itemData,
                popup,
                new CombatTimeExtensionUseExecutor(m_CombatPhase),
                m_ItemUseLifetimeCancellation?.Token ?? CancellationToken.None);

            if (coordinator.LastResult == ItemPurchaseUseResult.PurchaseSucceeded ||
                coordinator.LastResult == ItemPurchaseUseResult.UseSucceeded ||
                coordinator.LastResult == ItemPurchaseUseResult.Failed)
            {
                SoundManager.Instance?.PlaySfx(
                    coordinator.LastResult == ItemPurchaseUseResult.Failed
                        ? SfxIds.ButtonNegative
                        : SfxIds.ButtonPositive);
                RefreshItemButtons();
            }
        }

        private void OnCombatItemUsed(ItemData itemData)
        {
            RefreshItemButtons();
        }

        private void Refresh()
        {
            RefreshCombatState();
            RefreshStageState();
            RefreshBetHistory();
            RefreshItemButtons();
        }

        private void RefreshCombatState()
        {
            if (m_CombatPhase == null)
            {
                return;
            }

            if (m_ActiveTargetingRequestVersion != -1 &&
                !m_CombatPhase.CanCommitGroundTargetItem())
            {
                CancelTargetingRequest();
            }

            int redAlive = m_CombatPhase.RedAliveCount;
            int blueAlive = m_CombatPhase.BlueAliveCount;

            if (m_RedTeamCountText != null)
            {
                m_RedTeamCountText.text = redAlive.ToString();
            }

            if (m_BlueTeamCountText != null)
            {
                m_BlueTeamCountText.text = blueAlive.ToString();
            }

            if (m_RedTeamSlider != null)
            {
                m_RedTeamSlider.value = GetSurvivalRatio(redAlive, m_CombatPhase.RedParticipantCount);
            }

            if (m_BlueTeamSlider != null)
            {
                m_BlueTeamSlider.value = GetSurvivalRatio(blueAlive, m_CombatPhase.BlueParticipantCount);
            }

            if (m_BattleTimerText != null)
            {
                int totalSeconds = Mathf.CeilToInt(m_CombatPhase.RemainingCombatTime);
                m_BattleTimerText.text = totalSeconds.ToString();
            }

            if (m_SpeedMultiplierText != null)
            {
                m_SpeedMultiplierText.text = $"\u00D7{m_CombatPhase.DisplaySpeed:0.#}";
            }

            if (m_SpeedButton != null)
            {
                m_SpeedButton.interactable = CanAcceptCombatInput() &&
                    !m_CombatPhase.IsItemCastingSlowMotion;
            }
            RefreshUnitSlots();
        }

        private void RefreshUnitSlots()
        {
            RefreshTeamUnitSlots(Team.Red, m_RedUnitSlotImages, m_RedUnitPortraitImages, m_RedUnitSlotTexts, m_CombatPhase.RedAliveCount == 0);
            RefreshTeamUnitSlots(Team.Blue, m_BlueUnitSlotImages, m_BlueUnitPortraitImages, m_BlueUnitSlotTexts, m_CombatPhase.BlueAliveCount == 0);
        }

        private void RefreshTeamUnitSlots(Team team, Image[] backgrounds, Image[] portraits, TMP_Text[] texts, bool teamEliminated)
        {
            for (int i = 0; i < backgrounds.Length; i++)
            {
                int alive = m_CombatPhase.GetAliveCount(team, i);
                bool empty = teamEliminated || alive == 0;
                if (backgrounds[i] != null) backgrounds[i].color = empty ? Color.gray : Color.white;
                if (portraits != null && i < portraits.Length && portraits[i] != null)
                {
                    Sprite portrait = m_CombatPhase.GetSlotPortrait(team, i);
                    portraits[i].sprite = portrait;
                    portraits[i].gameObject.SetActive(portrait != null);
                    portraits[i].color = empty ? Color.gray : Color.white;
                }
                if (texts != null && i < texts.Length && texts[i] != null) texts[i].text = alive > 0 ? $"x{alive}" : string.Empty;
            }
        }

        private void RefreshStageState()
        {
            if (m_RoundContext == null)
            {
                return;
            }

            FindFirstObjectByType<BettingPhase>(FindObjectsInactive.Include)?.RefreshTopBar(m_RoundContext);

        }

        private void RefreshBetHistory()
        {
            RoundBetTicket ticket = m_RoundContext?.BetTicket;

            SetActive(m_WinningTeamHistoryRoot, m_RoundContext?.CurrentStageData?.EnableFactionBet == true);
            SetActive(m_GameEndTimeHistoryRoot, HasSpecial(SpecialBetType.RemainingTime));
            SetActive(m_OddEvenHistoryRoot, HasSpecial(SpecialBetType.OddEven));
            SetActive(m_FirstAnnihilatedHistoryRoot, HasSpecial(SpecialBetType.FirstEliminatedColumn));
            SetActive(m_SurvivingSlotsHistoryRoot, HasSpecial(SpecialBetType.SurvivingRow));

            if (m_WinningTeamHistoryText != null)
            {
                m_WinningTeamHistoryText.text = ticket == null ? "-" : FormatFaction(ticket.Faction);
            }

            if (m_GameEndTimeHistoryText != null)
            {
                m_GameEndTimeHistoryText.text = ticket?.RemainingTime == null
                    ? "-"
                    : FormatRemainingTime(ticket.RemainingTime.Value);
            }

            if (m_OddEvenHistoryText != null)
            {
                m_OddEvenHistoryText.text = ticket?.OddEven == null
                    ? "-"
                    : ticket.OddEven == OddEvenPrediction.Odd ? "홀수" : "짝수";
            }

            if (m_FirstAnnihilatedHistoryText != null)
            {
                m_FirstAnnihilatedHistoryText.text = ticket?.FirstEliminatedColumn == null
                    ? "-"
                    : FormatFirstEliminatedColumn(ticket.FirstEliminatedColumn.Value);
            }

            if (m_SurvivingSlotsHistoryText != null)
            {
                m_SurvivingSlotsHistoryText.text = ticket?.SurvivingRow == null
                    ? "-"
                    : FormatSurvivingRow(ticket.SurvivingRow.Value);
            }
        }

        private bool HasSpecial(SpecialBetType type)
        {
            return m_RoundContext != null && m_RoundContext.ActiveSpecialBets.Contains(type);
        }

        private static void SetActive(GameObject target, bool active)
        {
            if (target != null) target.SetActive(active);
        }

        private void RefreshItemButtons()
        {
            ResolveItemPresenters();
            RefreshItemPresenters();
            SetItemButtonState(m_ItemSlot1Button, m_ItemSlot1Data);
            SetItemButtonState(m_ItemSlot2Button, m_ItemSlot2Data);
            SetItemButtonState(m_ItemSlot3Button, m_ItemSlot3Data);
        }

        private void SetItemButtonState(Button button, ItemData itemData)
        {
            if (button == null)
            {
                return;
            }

            if (CanUseNewTargetingFlow(itemData))
            {
                button.interactable = CanAcceptCombatInput();
                return;
            }

            if (CanUseNewImmediateFlow(itemData))
            {
                button.interactable = CanAcceptCombatInput();
                return;
            }

            button.interactable = false;
        }

        private void ResolveItemPresenters()
        {
            m_ItemSlot1Presenter = ResolvePresenter(m_ItemSlot1Button, m_ItemSlot1Presenter);
            m_ItemSlot2Presenter = ResolvePresenter(m_ItemSlot2Button, m_ItemSlot2Presenter);
            m_ItemSlot3Presenter = ResolvePresenter(m_ItemSlot3Button, m_ItemSlot3Presenter);
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

        private UI_ItemSlotPresenter ResolvePresenter(ItemData itemData)
        {
            if (itemData == null)
            {
                return null;
            }

            if (ReferenceEquals(itemData, m_ItemSlot1Data)) return m_ItemSlot1Presenter;
            if (ReferenceEquals(itemData, m_ItemSlot2Data)) return m_ItemSlot2Presenter;
            if (ReferenceEquals(itemData, m_ItemSlot3Data)) return m_ItemSlot3Presenter;
            return null;
        }

        private void RefreshItemPresenters()
        {
            RefreshPresenter(m_ItemSlot1Presenter, m_ItemSlot1Data);
            RefreshPresenter(m_ItemSlot2Presenter, m_ItemSlot2Data);
            RefreshPresenter(m_ItemSlot3Presenter, m_ItemSlot3Data);
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
            if (ReferenceEquals(itemData, m_ActiveTargetingItemData) &&
                m_ActiveTargetingRequestVersion >= 0)
            {
                presenter.SetState(ItemSlotVisualState.Casting, false);
                return;
            }

            bool used = m_RoundContext != null && itemData != null &&
                m_RoundContext.RoundItemUsage.HasUsed(itemData.ItemType);
            presenter.SetState(
                used ? ItemSlotVisualState.Used : ItemSlotVisualState.Normal,
                false);
        }

        private bool CanUseNewImmediateFlow(ItemData itemData)
        {
            if (itemData == null || itemData.ItemType != ItemType.TimeExtension)
            {
                return false;
            }

            UI_ItemPurchasePopupController popup = m_ItemPurchasePopup ??
                UIManager.Instance?.GetElement<UI_ItemPurchasePopupController>();
            return RoundManager.Instance?.ItemPurchaseUseCoordinator != null &&
                popup != null &&
                popup.ParentRoot != null &&
                m_RoundContext != null &&
                m_PlayerState != null;
        }

        private bool CanUseNewTargetingFlow(ItemData itemData)
        {
            if (itemData == null || (itemData.ItemType != ItemType.Meteor && itemData.ItemType != ItemType.Mercenary))
            {
                return false;
            }

            UI_ItemPurchasePopupController popup = m_ItemPurchasePopup ??
                UIManager.Instance?.GetElement<UI_ItemPurchasePopupController>();
            return RoundManager.Instance?.ItemPurchaseUseCoordinator != null &&
                popup != null &&
                popup.ParentRoot != null &&
                m_RoundContext != null &&
                m_PlayerState != null;
        }

        #if false
        private ItemData ResolveDraggedItem(PointerEventData eventData)
        {
            if (!CanAcceptCombatInput() || eventData == null)
            {
                return null;
            }

            GameObject pressedObject = eventData.pointerPressRaycast.gameObject ?? eventData.pointerPress;
            if (pressedObject == null)
            {
                return null;
            }

            Transform pressedTransform = pressedObject.transform;
            if (IsButtonTarget(pressedTransform, m_ItemSlot1Button)) return m_ItemSlot1Data;
            if (IsButtonTarget(pressedTransform, m_ItemSlot2Button)) return m_ItemSlot2Data;
            if (IsButtonTarget(pressedTransform, m_ItemSlot3Button)) return m_ItemSlot3Data;
            return null;
        }

        private static bool IsButtonTarget(Transform pressedTransform, Button button)
        {
            return button != null && button.interactable &&
                   (pressedTransform == button.transform || pressedTransform.IsChildOf(button.transform));
        }

        #endif

        private bool CanAcceptCombatInput()
        {
            return m_CombatPhase != null &&
                   !m_CombatPhase.IsPhaseCompleted &&
                   !m_CombatPhase.IsFinalEliminationPlaying;
        }

        private void ApplyItemIcons()
        {
            SetItemIcon(m_ItemSlot1Icon, m_ItemSlot1Data);
            SetItemIcon(m_ItemSlot2Icon, m_ItemSlot2Data);
            SetItemIcon(m_ItemSlot3Icon, m_ItemSlot3Data);
        }

        private static void SetItemIcon(Image image, ItemData itemData)
        {
            if (image != null && itemData != null)
            {
                image.sprite = itemData.Icon;
            }
        }

        private static float GetSurvivalRatio(int aliveCount, int initialCount)
        {
            return initialCount > 0 ? Mathf.Clamp01((float)aliveCount / initialCount) : 0f;
        }

        private static string FormatFaction(FactionPrediction faction)
        {
            return faction switch
            {
                FactionPrediction.Red => "Red",
                FactionPrediction.Blue => "Blue",
                FactionPrediction.Draw => "Draw",
                _ => "-"
            };
        }

        private static string FormatRemainingTime(RemainingTimePrediction prediction)
        {
            return prediction switch
            {
                RemainingTimePrediction.Seconds0To5 => "0-5 sec",
                RemainingTimePrediction.Seconds5To10 => "5-10 sec",
                RemainingTimePrediction.Seconds10To15 => "10-15 sec",
                RemainingTimePrediction.Seconds15To20 => "15-20 sec",
                RemainingTimePrediction.Seconds20OrMore => "20+ sec",
                _ => "-"
            };
        }

        private static string FormatFirstEliminatedColumn(FirstEliminatedColumnPrediction prediction)
        {
            return prediction switch
            {
                FirstEliminatedColumnPrediction.RedFront => "레드 / 전열",
                FirstEliminatedColumnPrediction.RedBack => "레드 / 후열",
                FirstEliminatedColumnPrediction.BlueFront => "블루 / 전열",
                FirstEliminatedColumnPrediction.BlueBack => "블루 / 후열",
                _ => "-"
            };
        }

        private static string FormatSurvivingRow(SurvivingRowPrediction prediction)
        {
            return prediction switch
            {
                SurvivingRowPrediction.RedRow1 => "레드 / 1행",
                SurvivingRowPrediction.RedRow2 => "레드 / 2행",
                SurvivingRowPrediction.RedRow3 => "레드 / 3행",
                SurvivingRowPrediction.BlueRow1 => "블루 / 1행",
                SurvivingRowPrediction.BlueRow2 => "블루 / 2행",
                SurvivingRowPrediction.BlueRow3 => "블루 / 3행",
                _ => "-"
            };
        }

        private void EnsureCombatItemTargetingController()
        {
            if (m_CombatItemTargetingController == null)
            {
                GameObject inputObject = new GameObject(
                    "CombatItemTargetingInput",
                    typeof(RectTransform),
                    typeof(Canvas),
                    typeof(GraphicRaycaster),
                    typeof(Image));
                inputObject.transform.SetParent(transform, false);

                RectTransform inputRect = (RectTransform)inputObject.transform;
                inputRect.anchorMin = Vector2.zero;
                inputRect.anchorMax = Vector2.one;
                inputRect.offsetMin = Vector2.zero;
                inputRect.offsetMax = Vector2.zero;

                Canvas inputCanvas = inputObject.GetComponent<Canvas>();
                inputCanvas.overrideSorting = true;
                inputCanvas.sortingOrder = 1000;

                Image inputImage = inputObject.GetComponent<Image>();
                inputImage.color = Color.clear;
                inputImage.raycastTarget = true;
                m_CombatItemTargetingController =
                    inputObject.AddComponent<UI_CombatItemTargetingController>();
            }

            if (m_CombatItemTargetingController != null)
            {
                m_CombatItemTargetingController.TargetConfirmed -= OnTargetConfirmed;
                m_CombatItemTargetingController.TargetCanceled -= OnTargetCanceled;
                m_CombatItemTargetingController.TargetConfirmed += OnTargetConfirmed;
                m_CombatItemTargetingController.TargetCanceled += OnTargetCanceled;
                m_CombatItemTargetingController.gameObject.SetActive(false);
            }
        }

        private void EnsureCancelOverlays()
        {
            m_ItemSlot1CancelOverlay ??= CreateCancelOverlay(m_ItemSlot1Button);
            m_ItemSlot2CancelOverlay ??= CreateCancelOverlay(m_ItemSlot2Button);
            m_ItemSlot3CancelOverlay ??= CreateCancelOverlay(m_ItemSlot3Button);
        }

        private Image CreateCancelOverlay(Button button)
        {
            if (button == null)
            {
                return null;
            }

            Transform existing = button.transform.Find("TargetingCancelOverlay");
            GameObject overlayObject = existing != null
                ? existing.gameObject
                : new GameObject("TargetingCancelOverlay", typeof(RectTransform), typeof(Image));
            if (existing == null)
            {
                overlayObject.transform.SetParent(button.transform, false);
            }

            RectTransform overlayRect = (RectTransform)overlayObject.transform;
            overlayRect.anchorMin = new Vector2(0.5f, 0.5f);
            overlayRect.anchorMax = new Vector2(0.5f, 0.5f);
            overlayRect.pivot = new Vector2(0.5f, 0.5f);
            overlayRect.anchoredPosition = Vector2.zero;
            overlayRect.sizeDelta = new Vector2(115f, 110f);
            overlayObject.transform.SetAsLastSibling();

            Image overlay = overlayObject.GetComponent<Image>();
            overlay.sprite = m_CancelOverlaySprite;
            overlay.preserveAspect = true;
            overlay.raycastTarget = false;
            overlay.enabled = false;
            return overlay;
        }

        private void EnsureDuplicateItemFeedback()
        {
            if (m_DuplicateItemFeedbackText == null)
            {
                GameObject feedbackObject = new GameObject(
                    "DuplicateItemFeedback",
                    typeof(RectTransform),
                    typeof(CanvasGroup),
                    typeof(TextMeshProUGUI));
                feedbackObject.transform.SetParent(transform, false);

                m_DuplicateItemFeedbackText = feedbackObject.GetComponent<TextMeshProUGUI>();
                m_DuplicateItemFeedbackCanvasGroup = feedbackObject.GetComponent<CanvasGroup>();
                m_DuplicateItemFeedbackRect = (RectTransform)feedbackObject.transform;
                m_DuplicateItemFeedbackText.alignment = TextAlignmentOptions.Center;
                m_DuplicateItemFeedbackText.fontSize = 24f;
                m_DuplicateItemFeedbackText.color = Color.white;
                m_DuplicateItemFeedbackText.enableWordWrapping = false;
                m_DuplicateItemFeedbackText.raycastTarget = false;
                m_DuplicateItemFeedbackRect.anchorMin = new Vector2(0.5f, 0.5f);
                m_DuplicateItemFeedbackRect.anchorMax = new Vector2(0.5f, 0.5f);
                m_DuplicateItemFeedbackRect.pivot = new Vector2(0.5f, 0.5f);
                m_DuplicateItemFeedbackRect.anchoredPosition = new Vector2(0f, 180f);
                m_DuplicateItemFeedbackRect.sizeDelta = new Vector2(620f, 48f);
            }

            if (m_DuplicateItemFeedbackText != null &&
                m_DuplicateItemFeedbackFont != null)
            {
                m_DuplicateItemFeedbackText.font = m_DuplicateItemFeedbackFont;
            }

            if (m_DuplicateItemFeedbackCanvasGroup == null &&
                m_DuplicateItemFeedbackText != null)
            {
                m_DuplicateItemFeedbackCanvasGroup =
                    m_DuplicateItemFeedbackText.GetComponent<CanvasGroup>() ??
                    m_DuplicateItemFeedbackText.gameObject.AddComponent<CanvasGroup>();
            }

            if (m_DuplicateItemFeedbackRect == null && m_DuplicateItemFeedbackText != null)
            {
                m_DuplicateItemFeedbackRect = m_DuplicateItemFeedbackText.rectTransform;
            }

            if (m_DuplicateItemFeedbackRect != null)
            {
                m_DuplicateItemFeedbackBasePosition =
                    m_DuplicateItemFeedbackRect.anchoredPosition;
            }

            if (m_DuplicateItemFeedbackCanvasGroup != null)
            {
                m_DuplicateItemFeedbackCanvasGroup.alpha = 0f;
                m_DuplicateItemFeedbackCanvasGroup.blocksRaycasts = false;
                m_DuplicateItemFeedbackCanvasGroup.interactable = false;
            }
        }

        private void ShowDuplicateItemFeedback()
        {
            EnsureDuplicateItemFeedback();
            if (m_DuplicateItemFeedbackText == null ||
                m_DuplicateItemFeedbackCanvasGroup == null ||
                m_DuplicateItemFeedbackRect == null)
            {
                return;
            }

            m_DuplicateItemFeedbackText.text = "이미 이번 라운드에는 해당 아이템을 사용했습니다.";
            m_DuplicateItemFeedbackTween?.Kill();
            m_DuplicateItemFeedbackCanvasGroup.DOKill();
            m_DuplicateItemFeedbackRect.DOKill();
            m_DuplicateItemFeedbackRect.anchoredPosition = m_DuplicateItemFeedbackBasePosition;
            m_DuplicateItemFeedbackCanvasGroup.alpha = 0f;

            m_DuplicateItemFeedbackTween = DOTween.Sequence()
                .Append(m_DuplicateItemFeedbackCanvasGroup.DOFade(1f, 0.5f))
                .Join(m_DuplicateItemFeedbackRect.DOAnchorPos(
                    m_DuplicateItemFeedbackBasePosition + Vector2.up * 24f,
                    1f))
                .Append(m_DuplicateItemFeedbackCanvasGroup.DOFade(0f, 0.5f))
                .SetUpdate(true)
                .OnComplete(() =>
                {
                    if (m_DuplicateItemFeedbackRect != null)
                    {
                        m_DuplicateItemFeedbackRect.anchoredPosition =
                            m_DuplicateItemFeedbackBasePosition;
                    }
                });
        }

        private void ResolveItemSlot(
            ItemData itemData,
            out RectTransform selectedSlot,
            out Image cancelOverlay)
        {
            selectedSlot = null;
            cancelOverlay = null;

            if (itemData == null)
            {
                return;
            }

            if (itemData == m_ItemSlot1Data)
            {
                selectedSlot = m_ItemSlot1Button?.transform as RectTransform;
                cancelOverlay = m_ItemSlot1CancelOverlay;
            }
            else if (itemData == m_ItemSlot2Data)
            {
                selectedSlot = m_ItemSlot2Button?.transform as RectTransform;
                cancelOverlay = m_ItemSlot2CancelOverlay;
            }
            else if (itemData == m_ItemSlot3Data)
            {
                selectedSlot = m_ItemSlot3Button?.transform as RectTransform;
                cancelOverlay = m_ItemSlot3CancelOverlay;
            }
        }

        private void ResetDisplay()
        {
            if (m_RedTeamCountText != null) m_RedTeamCountText.text = "0";
            if (m_BlueTeamCountText != null) m_BlueTeamCountText.text = "0";
            if (m_RedTeamSlider != null) m_RedTeamSlider.value = 0f;
            if (m_BlueTeamSlider != null) m_BlueTeamSlider.value = 0f;
            if (m_BattleTimerText != null) m_BattleTimerText.text = "0";
            if (m_SpeedMultiplierText != null) m_SpeedMultiplierText.text = "×1";
            SetActive(m_WinningTeamHistoryRoot, true);
            SetActive(m_GameEndTimeHistoryRoot, false);
            SetActive(m_OddEvenHistoryRoot, false);
            SetActive(m_FirstAnnihilatedHistoryRoot, false);
            SetActive(m_SurvivingSlotsHistoryRoot, false);
            if (m_WinningTeamHistoryText != null) m_WinningTeamHistoryText.text = "-";
            if (m_GameEndTimeHistoryText != null) m_GameEndTimeHistoryText.text = "-";
            if (m_OddEvenHistoryText != null) m_OddEvenHistoryText.text = "-";
            if (m_FirstAnnihilatedHistoryText != null) m_FirstAnnihilatedHistoryText.text = "-";
            if (m_SurvivingSlotsHistoryText != null) m_SurvivingSlotsHistoryText.text = "-";
            if (m_SpeedButton != null) m_SpeedButton.interactable = false;
            if (m_ItemSlot1Button != null) m_ItemSlot1Button.interactable = false;
            if (m_ItemSlot2Button != null) m_ItemSlot2Button.interactable = false;
            if (m_ItemSlot3Button != null) m_ItemSlot3Button.interactable = false;
        }

        private void ClearBindings()
        {
            m_CombatPhase = null;
            m_RoundContext = null;
            m_PlayerState = null;
            ClearTargetingBinding();
        }

        protected override void OnDestroy()
        {
            m_ItemUseLifetimeCancellation?.Cancel();
            m_TargetingLifetimeCancellation?.Cancel();
            if (m_CombatItemTargetingController != null)
            {
                m_CombatItemTargetingController.TargetConfirmed -= OnTargetConfirmed;
                m_CombatItemTargetingController.TargetCanceled -= OnTargetCanceled;
            }
            m_CombatItemTargetingController?.AbortTargeting();
            m_DuplicateItemFeedbackTween?.Kill();

            UnsubscribeEvents();
            ClearBindings();

            m_ItemUseLifetimeCancellation?.Dispose();
            m_ItemUseLifetimeCancellation = null;
            m_TargetingLifetimeCancellation?.Dispose();
            m_TargetingLifetimeCancellation = null;

            base.OnDestroy();
        }
    }
}
#endif
