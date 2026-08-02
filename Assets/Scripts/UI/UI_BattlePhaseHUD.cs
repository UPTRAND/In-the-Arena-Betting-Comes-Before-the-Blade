#if UNITY_6000_0_OR_NEWER
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
        [SerializeField] private TMP_Text m_RoundInfoText;
        [SerializeField] private TMP_Text m_TargetInfoText;
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

        private CombatPhase m_CombatPhase;
        private RoundContext m_RoundContext;
        private StagePlayerState m_PlayerState;
        private ItemInventoryService m_InventoryService;
        private ItemData m_DraggedItem;
        private bool m_IsSubscribed;

        protected override void Awake()
        {
            base.Awake();
            ApplyItemIcons();
            ResetDisplay();
        }

        public void BindAndShow(
            CombatPhase combatPhase,
            RoundContext roundContext,
            StagePlayerState playerState,
            ItemInventoryService inventoryService)
        {
            if (m_CombatPhase != null && m_CombatPhase != combatPhase)
            {
                UnsubscribeEvents();
            }

            m_CombatPhase = combatPhase;
            m_RoundContext = roundContext;
            m_PlayerState = playerState;
            m_InventoryService = inventoryService;

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
            UnsubscribeEvents();
            m_DraggedItem = null;
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

            if (!InputManager.Instance.RaycastGroundPosition(eventData.position, out Vector3 worldPosition))
            {
                Debug.Log("[UI_BattlePhaseHUD] 전장 영역에 아이템을 드롭해야 합니다.");
                return;
            }

            bool success = m_CombatPhase.UseCombatItem(itemData, worldPosition, out string message, out _);
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
                m_RedTeamSlider.value = GetSurvivalRatio(redAlive, m_CombatPhase.InitialRedUnitCount);
            }

            if (m_BlueTeamSlider != null)
            {
                m_BlueTeamSlider.value = GetSurvivalRatio(blueAlive, m_CombatPhase.InitialBlueUnitCount);
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

            if (m_RoundInfoText != null)
            {
                m_RoundInfoText.text = $"Round {m_RoundContext.CurrentRound} / {m_RoundContext.MaxRounds}";
            }

            if (m_TargetInfoText != null)
            {
                m_TargetInfoText.text = $"Target {m_RoundContext.TargetCall} COL";
            }

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

            int count = m_InventoryService != null && m_PlayerState != null && itemData != null
                ? m_InventoryService.GetStageItemCount(itemData, m_PlayerState)
                : 0;
            button.interactable = CanAcceptCombatInput() && count > 0;
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
            if (m_RoundInfoText != null) m_RoundInfoText.text = "Round - / -";
            if (m_TargetInfoText != null) m_TargetInfoText.text = "Target - COL";
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
            m_InventoryService = null;
            m_DraggedItem = null;
        }

        protected override void OnDestroy()
        {
            UnsubscribeEvents();
            ClearBindings();
            base.OnDestroy();
        }
    }
}
#endif
