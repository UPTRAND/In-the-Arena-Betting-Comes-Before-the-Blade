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
        [SerializeField] private UI_CombatItemTeamSelectionController m_CombatItemTeamSelectionController;
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
        private bool m_IsSubscribed;
        private CancellationTokenSource m_TargetingLifetimeCancellation;
        private long m_ActiveTargetingRequestVersion = -1;
        private Tween m_DuplicateItemFeedbackTween;
        private Vector2 m_DuplicateItemFeedbackBasePosition;

        private const float LowerHudReferenceAspect = 1920f / 1080f;
        private RectTransform m_ItemAndSpeedArea;
        private RectTransform m_BottomArea;
        private Vector2 m_ItemAndSpeedOriginalPosition;
        private Vector2 m_BottomOriginalSize;
        private readonly List<LowerHudChildLayout> m_LowerHudChildren = new List<LowerHudChildLayout>();
        private bool m_HasLowerHudLayout;
        private bool m_HasAppliedLowerHudLayout;
        private Vector2 m_LowerHudViewportSize;

        private readonly struct LowerHudChildLayout
        {
            public readonly RectTransform Rect;
            public readonly Vector2 Position;
            public readonly float AnchorY;
            public readonly bool StretchVertically;
            public readonly bool KeepAtBottom;

            public LowerHudChildLayout(RectTransform rect)
            {
                Rect = rect;
                Position = rect.anchoredPosition;
                AnchorY = Mathf.Lerp(rect.anchorMin.y, rect.anchorMax.y, rect.pivot.y);
                StretchVertically = !Mathf.Approximately(rect.anchorMin.y, rect.anchorMax.y);
                KeepAtBottom = rect.name == "Deko_Bottom";
            }
        }

        protected override void Awake()
        {
            base.Awake();
            CacheLowerHudLayout();
            m_TargetingLifetimeCancellation = new CancellationTokenSource();
            EnsureCombatItemTeamSelectionController();
            EnsureDuplicateItemFeedback();
            ApplyItemIcons();
            ResolveItemPresenters();
            SetBattleView(true);
            ResetDisplay();
        }

        private void OnEnable()
        {
            Canvas.willRenderCanvases += RefreshLowerHudLayout;
            RefreshLowerHudLayout();
        }

        private void CacheLowerHudLayout()
        {
            if (m_HasLowerHudLayout) return;
            m_ItemAndSpeedArea = transform.Find("ItemAndSpeed_Area") as RectTransform;
            m_BottomArea = transform.Find("BottomArea") as RectTransform;
            if (m_ItemAndSpeedArea != null)
                m_ItemAndSpeedOriginalPosition = m_ItemAndSpeedArea.anchoredPosition;
            if (m_BottomArea != null)
            {
                m_BottomOriginalSize = m_BottomArea.sizeDelta;
                for (int i = 0; i < m_BottomArea.childCount; i++)
                    if (m_BottomArea.GetChild(i) is RectTransform child)
                        m_LowerHudChildren.Add(new LowerHudChildLayout(child));
            }
            m_HasLowerHudLayout = true;
        }

        private void RefreshLowerHudLayout()
        {
            CacheLowerHudLayout();
            RectTransform root = rectTransform;
            if (root == null || root.rect.width <= 0f || root.rect.height <= 0f) return;
            Vector2 size = root.rect.size;
            if (m_HasAppliedLowerHudLayout && (size - m_LowerHudViewportSize).sqrMagnitude < 0.0001f) return;
            m_HasAppliedLowerHudLayout = true;
            m_LowerHudViewportSize = size;
            float extraHeight = Mathf.Max(0f, size.y - size.x * LowerHudReferenceAspect);
            if (m_ItemAndSpeedArea != null)
                m_ItemAndSpeedArea.anchoredPosition = m_ItemAndSpeedOriginalPosition + Vector2.up * extraHeight;
            if (m_BottomArea == null) return;

            m_BottomArea.sizeDelta = m_BottomOriginalSize + Vector2.up * extraHeight;
            foreach (LowerHudChildLayout child in m_LowerHudChildren)
            {
                if (child.Rect == null || child.StretchVertically) continue;
                // Top content stays at the reference height; the bottom ornament stays at the screen edge.
                float offset = child.KeepAtBottom ? -child.AnchorY : 1f - child.AnchorY;
                child.Rect.anchoredPosition = child.Position + Vector2.up * (offset * extraHeight);
            }
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
            RefreshLowerHudLayout();
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

        private void OnDisable()
        {
            Canvas.willRenderCanvases -= RefreshLowerHudLayout;
            CancelTargetingRequest();
        }

        private int m_LastDisplayedSecond = -1;
        private BettingPhase m_BettingPhase;
        private bool m_LastSlowMotion;
        private bool m_LastCanAcceptInput;

        /// <summary>매 프레임에는 표시할 초의 변경만 확인합니다.</summary>
        private void Update()
        {
            if (m_CombatPhase != null && m_RoundContext != null)
            {
                bool slowMotion = m_CombatPhase.IsItemCastingSlowMotion;
                bool canAcceptInput = CanAcceptCombatInput();
                ItemPurchaseUseCoordinator coordinator = RoundManager.Instance?.ItemPurchaseUseCoordinator;
                if (!canAcceptInput || (m_ActiveTargetingRequestVersion >= 0 &&
                    (coordinator == null || coordinator.State != ItemPurchaseUseState.AwaitingTarget ||
                     coordinator.ActiveRequestVersion != m_ActiveTargetingRequestVersion)))
                    CancelTargetingRequest();
                if (slowMotion != m_LastSlowMotion || canAcceptInput != m_LastCanAcceptInput)
                {
                    m_LastSlowMotion = slowMotion;
                    m_LastCanAcceptInput = canAcceptInput;
                    RefreshCombatState();
                    RefreshItemButtons();
                }
                int seconds = Mathf.CeilToInt(m_CombatPhase.RemainingCombatTime);
                if (seconds != m_LastDisplayedSecond)
                {
                    m_LastDisplayedSecond = seconds;
                    if (m_BattleTimerText != null)
                    {
                        m_BattleTimerText.text = seconds.ToString();
                    }
                }
            }
        }





        private async void RequestTeamSelectionUse(ItemData itemData)
        {
            ItemPurchaseUseCoordinator coordinator = RoundManager.Instance?.ItemPurchaseUseCoordinator;
            UI_ItemPurchasePopupController popup = m_ItemPurchasePopup ??
                UIManager.Instance?.GetElement<UI_ItemPurchasePopupController>();
            if (coordinator == null || m_CombatPhase == null || coordinator.State != ItemPurchaseUseState.Idle)
                return;

            RenewItemRequestLifetime();
            long requestVersion = await coordinator.RequestTargetedUseAsync(
                itemData, popup, m_TargetingLifetimeCancellation.Token,
                ItemUseConfirmationPolicy.CombatClickUse);
            if (this == null || requestVersion < 0 || m_TargetingLifetimeCancellation.IsCancellationRequested ||
                coordinator.State != ItemPurchaseUseState.AwaitingTarget ||
                coordinator.ActiveRequestVersion != requestVersion)
            {
                if (this != null) RefreshItemButtons();
                return;
            }

            if (m_CombatItemTeamSelectionController == null ||
                !m_CombatItemTeamSelectionController.BeginSelection(itemData.ItemType, m_CombatPhase))
            {
                coordinator.CancelActiveRequest();
                RefreshItemButtons();
                return;
            }
            m_ActiveTargetingRequestVersion = requestVersion;
            m_ActiveTargetingItemData = itemData;
            ResolvePresenter(itemData)?.SetState(ItemSlotVisualState.Casting);
            RefreshItemButtons();
        }

        private void RenewItemRequestLifetime()
        {
            m_TargetingLifetimeCancellation?.Cancel();
            m_TargetingLifetimeCancellation?.Dispose();
            m_TargetingLifetimeCancellation = new CancellationTokenSource();
        }

        private IItemPurchaseUseExecutor CreateExecutor(ItemData itemData, Team team)
        {
            if (itemData.ItemType == ItemType.Meteor) return new CombatMeteorUseExecutor(m_CombatPhase, team);
            if (itemData.ItemType == ItemType.Mercenary) return new CombatMercenaryUseExecutor(m_CombatPhase, team);
            if (itemData.ItemType == ItemType.TimeExtension) return new CombatTimeExtensionUseExecutor(m_CombatPhase);
            return null;
        }

        private void ClearTargetingBinding()
        {
            m_ActiveTargetingRequestVersion = -1;
            m_ActiveTargetingItemData = null;
        }

        private void CancelTargetingRequest()
        {
            long requestVersion = m_ActiveTargetingRequestVersion;
            ItemData itemData = m_ActiveTargetingItemData;
            m_CombatItemTeamSelectionController?.AbortSelection();
            ResolvePresenter(itemData)?.SetState(ItemSlotVisualState.Normal);
            ClearTargetingBinding();
            ItemPurchaseUseCoordinator coordinator = RoundManager.Instance?.ItemPurchaseUseCoordinator;
            if (coordinator != null && coordinator.State == ItemPurchaseUseState.AwaitingTarget &&
                coordinator.ActiveRequestVersion == requestVersion)
                coordinator.CancelActiveRequest();
            m_TargetingLifetimeCancellation?.Cancel();
        }

        private void OnTeamConfirmed(Team team)
        {
            if (m_CombatPhase == null || m_ActiveTargetingItemData == null)
            {
                CancelTargetingRequest();
                return;
            }
            long requestVersion = m_ActiveTargetingRequestVersion;
            ItemData itemData = m_ActiveTargetingItemData;
            ItemPurchaseUseCoordinator coordinator = RoundManager.Instance?.ItemPurchaseUseCoordinator;
            if (coordinator == null || coordinator.State != ItemPurchaseUseState.AwaitingTarget ||
                coordinator.ActiveRequestVersion != requestVersion)
            {
                CancelTargetingRequest();
                return;
            }

            IItemPurchaseUseExecutor executor = CreateExecutor(itemData, team);
            ClearTargetingBinding();
            bool success = coordinator.TryCompleteTargetUse(
                requestVersion, executor, out _, out string message);
            Debug.Log($"[UI_BattlePhaseHUD] {message}");
            SoundManager.Instance?.PlaySfx(success ? SfxIds.ButtonPositive : SfxIds.ButtonNegative);
            ResolvePresenter(itemData)?.SetState(success ? ItemSlotVisualState.Used : ItemSlotVisualState.Normal);
            RefreshItemButtons();
        }

        private void OnTeamSelectionCanceled()
        {
            SoundManager.Instance?.PlaySfx(SfxIds.ButtonNegative);
            CancelTargetingRequest();
            RefreshItemButtons();
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
            ItemPurchaseUseCoordinator coordinator = RoundManager.Instance?.ItemPurchaseUseCoordinator;
            if (itemData == null || !CanAcceptCombatInput() || !IsCombatItem(itemData) ||
                coordinator == null || coordinator.State != ItemPurchaseUseState.Idle ||
                m_RoundContext?.BetTicket?.IsPlaced != true)
                return;

            if (m_RoundContext.RoundItemUsage.HasUsed(itemData.ItemType))
            {
                SoundManager.Instance?.PlaySfx(SfxIds.ButtonNegative);
                ShowDuplicateItemFeedback();
                return;
            }

            SoundManager.Instance?.PlaySfx(SfxIds.ButtonPositive);
            if (itemData.ItemType != ItemType.TimeExtension &&
                m_RoundContext.BetTicket.Faction == FactionPrediction.Draw)
                RequestTeamSelectionUse(itemData);
            else
                _ = RequestImmediateItemUseAsync(itemData, coordinator);
        }

        private static bool IsCombatItem(ItemData itemData) => itemData != null &&
            (itemData.ItemType == ItemType.Meteor || itemData.ItemType == ItemType.Mercenary ||
             itemData.ItemType == ItemType.TimeExtension);

        private void OnItemSlot1Clicked() => RequestItemUse(m_ItemSlot1Data);
        private void OnItemSlot2Clicked() => RequestItemUse(m_ItemSlot2Data);
        private void OnItemSlot3Clicked() => RequestItemUse(m_ItemSlot3Data);

        private async Awaitable RequestImmediateItemUseAsync(ItemData itemData, ItemPurchaseUseCoordinator coordinator)
        {
            RenewItemRequestLifetime();
            Team targetTeam = CombatItemTeamResolver.Resolve(m_RoundContext.BetTicket, itemData.ItemType);
            UI_ItemPurchasePopupController popup = m_ItemPurchasePopup ??
                UIManager.Instance?.GetElement<UI_ItemPurchasePopupController>();
            await coordinator.RequestImmediateUseAsync(
                itemData, popup, CreateExecutor(itemData, targetTeam),
                m_TargetingLifetimeCancellation.Token, ItemUseConfirmationPolicy.CombatClickUse);
            if (this == null) return;
            bool success = coordinator.LastResult == ItemPurchaseUseResult.UseSucceeded;
            SoundManager.Instance?.PlaySfx(success ? SfxIds.ButtonPositive : SfxIds.ButtonNegative);
            RefreshItemButtons();
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

            if (m_BettingPhase == null)
            {
                m_BettingPhase = FindFirstObjectByType<BettingPhase>(FindObjectsInactive.Include);
            }
            m_BettingPhase?.RefreshTopBar(m_RoundContext);

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
                    : $"{FormatFaction(ticket.Faction)} / {(ticket.OddEven == OddEvenPrediction.Odd ? "홀수" : "짝수")}";
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
            return m_RoundContext?.BetTicket?.CanSelectSpecialBets == true && m_RoundContext.ActiveSpecialBets.Contains(type);
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
            if (button == null) return;
            button.interactable = CanUseCombatItemFlow(itemData) && CanAcceptCombatInput() &&
                RoundManager.Instance.ItemPurchaseUseCoordinator.State == ItemPurchaseUseState.Idle;
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

        private bool CanUseCombatItemFlow(ItemData itemData)
        {
            return IsCombatItem(itemData) &&
                RoundManager.Instance?.ItemPurchaseUseCoordinator != null &&
                m_RoundContext?.BetTicket?.IsPlaced == true && m_PlayerState != null;
        }

        private bool CanAcceptCombatInput()
        {
            return m_CombatPhase != null &&
                   !m_CombatPhase.IsCombatEnded &&
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

        private void EnsureCombatItemTeamSelectionController()
        {
            m_CombatItemTeamSelectionController ??=
                GetComponentInChildren<UI_CombatItemTeamSelectionController>(true);
            m_CombatItemTeamSelectionController ??=
                UI_CombatItemTeamSelectionController.Create(transform,
                    m_DuplicateItemFeedbackFont ?? m_BattleTimerText?.font);
            m_CombatItemTeamSelectionController.TeamConfirmed -= OnTeamConfirmed;
            m_CombatItemTeamSelectionController.SelectionCanceled -= OnTeamSelectionCanceled;
            m_CombatItemTeamSelectionController.TeamConfirmed += OnTeamConfirmed;
            m_CombatItemTeamSelectionController.SelectionCanceled += OnTeamSelectionCanceled;
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
            Canvas.willRenderCanvases -= RefreshLowerHudLayout;
            m_TargetingLifetimeCancellation?.Cancel();
            if (m_CombatItemTeamSelectionController != null)
            {
                m_CombatItemTeamSelectionController.TeamConfirmed -= OnTeamConfirmed;
                m_CombatItemTeamSelectionController.SelectionCanceled -= OnTeamSelectionCanceled;
            }
            m_CombatItemTeamSelectionController?.AbortSelection();
            m_DuplicateItemFeedbackTween?.Kill();

            UnsubscribeEvents();
            ClearBindings();

            m_TargetingLifetimeCancellation?.Dispose();
            m_TargetingLifetimeCancellation = null;

            base.OnDestroy();
        }
    }
}
#endif
