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
    public sealed class UI_BattlePhaseHUD : UI_Base, IBeginDragHandler, IDragHandler, IEndDragHandler
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

        [Header("Stage Information")]
        [SerializeField] private TMP_Text m_MoneyText;
        [SerializeField] private TMP_Text m_WinningTeamHistoryText;
        [SerializeField] private TMP_Text m_GameEndTimeHistoryText;
        [SerializeField] private TMP_Text m_FirstAnnihilatedHistoryText;

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
        [SerializeField] private UI_ItemPurchasePopupController m_ItemPurchasePopup;

        private CombatPhase m_CombatPhase;
        private RoundContext m_RoundContext;
        private StagePlayerState m_PlayerState;

        private ItemData m_DraggedItem;
        private ItemData m_ActiveTargetingItemData;
        private bool m_IsSubscribed;
        private CancellationTokenSource m_ItemUseLifetimeCancellation;
        private CancellationTokenSource m_TargetingLifetimeCancellation;
        private long m_ActiveTargetingRequestVersion = -1;

        protected override void Awake()
        {
            base.Awake();
            m_ItemUseLifetimeCancellation = new CancellationTokenSource();
            m_TargetingLifetimeCancellation = new CancellationTokenSource();
            ApplyItemIcons();
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
            m_DraggedItem = null;
            m_ActiveTargetingItemData = null;
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

        private IItemPurchaseUseExecutor CreateExecutor(ItemData itemData, Vector3 worldPos)
        {
            if (itemData.ItemType == ItemType.Meteor) return new CombatMeteorUseExecutor(m_CombatPhase, worldPos);
            if (itemData.ItemType == ItemType.Mercenary) return new CombatMercenaryUseExecutor(m_CombatPhase, worldPos);
            return null;
        }

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
            RefreshCombatState();
        }

        private void RequestItemUse(ItemData itemData)
        {
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

            if (coordinator.LastResult == ItemPurchaseUseResult.UseSucceeded)
            {
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
                m_BattleTimerText.text = $"{totalSeconds / 60:00}:{totalSeconds % 60:00}";
            }

            if (m_SpeedMultiplierText != null)
            {
                m_SpeedMultiplierText.text = $"x{m_CombatPhase.CurrentSpeed:0.#}";
            }

            if (m_SpeedButton != null)
            {
                m_SpeedButton.interactable = CanAcceptCombatInput();
            }
        }

        private void RefreshStageState()
        {
            if (m_RoundContext == null)
            {
                return;
            }

            FindFirstObjectByType<BettingPhase>(FindObjectsInactive.Include)?.RefreshTopBar(m_RoundContext);

            if (m_MoneyText != null)
            {
                m_MoneyText.text = $"{(m_PlayerState != null ? m_PlayerState.Gold : 0)} COL";
            }
        }

        private void RefreshBetHistory()
        {
            RoundBetTicket ticket = m_RoundContext?.BetTicket;

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

            if (m_FirstAnnihilatedHistoryText != null)
            {
                m_FirstAnnihilatedHistoryText.text = ticket?.FirstEliminatedSlot == null
                    ? "-"
                    : $"Slot {ticket.FirstEliminatedSlot.Value}";
            }
        }

        private void RefreshItemButtons()
        {
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

            if (CanUseNewImmediateFlow(itemData) || CanUseNewTargetingFlow(itemData))
            {
                button.interactable = CanAcceptCombatInput() &&
                    !m_RoundContext.RoundItemUsage.HasUsed(itemData.ItemType) &&
                    itemData.PriceGold >= 0 &&
                    m_PlayerState.Gold >= itemData.PriceGold;
                return;
            }

            button.interactable = false;
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

        private void ResetDisplay()
        {
            if (m_RedTeamCountText != null) m_RedTeamCountText.text = "0";
            if (m_BlueTeamCountText != null) m_BlueTeamCountText.text = "0";
            if (m_RedTeamSlider != null) m_RedTeamSlider.value = 0f;
            if (m_BlueTeamSlider != null) m_BlueTeamSlider.value = 0f;
            if (m_BattleTimerText != null) m_BattleTimerText.text = "00:00";
            if (m_SpeedMultiplierText != null) m_SpeedMultiplierText.text = "x1";
            if (m_MoneyText != null) m_MoneyText.text = "0 COL";
            if (m_WinningTeamHistoryText != null) m_WinningTeamHistoryText.text = "-";
            if (m_GameEndTimeHistoryText != null) m_GameEndTimeHistoryText.text = "-";
            if (m_FirstAnnihilatedHistoryText != null) m_FirstAnnihilatedHistoryText.text = "-";
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
            m_DraggedItem = null;
            m_ActiveTargetingItemData = null;
        }

        protected override void OnDestroy()
        {
            m_ItemUseLifetimeCancellation?.Cancel();
            m_TargetingLifetimeCancellation?.Cancel();
            DetachTargetingInput();

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
