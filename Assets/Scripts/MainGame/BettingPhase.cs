#if UNITY_6000_0_OR_NEWER
using System;
using System.Collections.Generic;
using System.Threading;
using DG.Tweening;
using InTheArena.Unit;
using TMPro;
using UnityEngine;
using UnityEngine.Serialization;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using InTheArena.UI;

namespace InTheArena.MainGame
{
    internal sealed class BettingDraftSpecialPredictionState
    {
        public RemainingTimePrediction? RemainingTime { get; }
        public OddEvenPrediction? OddEven { get; }
        public FirstEliminatedColumnPrediction? FirstEliminatedColumn { get; }
        public SurvivingRowPrediction? SurvivingRow { get; }

        public BettingDraftSpecialPredictionState(RoundBetTicket ticket)
        {
            RemainingTime = ticket?.RemainingTime;
            OddEven = ticket?.OddEven;
            FirstEliminatedColumn = ticket?.FirstEliminatedColumn;
            SurvivingRow = ticket?.SurvivingRow;
        }

        public void Restore(RoundBetTicket ticket)
        {
            if (ticket == null) return;

            ticket.SetRemainingTime(RemainingTime);
            ticket.SetOddEven(OddEven);
            ticket.SetFirstEliminatedColumn(FirstEliminatedColumn);
            ticket.SetSurvivingRow(SurvivingRow);
        }
    }

    [DisallowMultipleComponent]
    public class BettingPhase : RoundPhaseBase
    {
        private const int WagerStepCall = 100;
        private const int AdditionalBetBonusCall = 500;
        private const float DropdownOptionHeight = 65f;
        [Header("Round / Team Info")]
        [SerializeField] private CanvasGroup m_BettingCanvasGroup;
        [SerializeField] private TMP_Text m_RoundText;
        [SerializeField] private TMP_Text m_TeamANameText;
        [SerializeField] private TMP_Text m_TeamAUnitInfoText;
        [SerializeField] private TMP_Text m_TeamBNameText;
        [SerializeField] private TMP_Text m_TeamBUnitInfoText;

        [Header("Wager")]
        [FormerlySerializedAs("m_BetRatioSlider")]
        [SerializeField] private Slider m_WagerSlider;
        [FormerlySerializedAs("m_TeamARatioText")]
        [SerializeField] private TMP_Text m_CurrentCallText;
        [FormerlySerializedAs("m_TeamBRatioText")]
        [SerializeField] private TMP_Text m_WagerCallText;
        [SerializeField] private TMP_Text m_MultiplierText;
        [SerializeField] private TMP_Text m_EstimatedPayoutText;

        [Header("Faction Bet")]
        [SerializeField] private GameObject m_FactionBetRoot;
        [SerializeField] private Button m_RedButton;
        [SerializeField] private Button m_BlueButton;
        [SerializeField] private Button m_DrawButton;
        [SerializeField] private Button m_ClearFactionButton;

        [Header("Special Bets")]
        [SerializeField] private GameObject m_RemainingTimeRoot;
        [SerializeField] private Button[] m_RemainingTimeButtons = new Button[5];
        [SerializeField] private GameObject m_SurvivingSlotsRoot;
        [SerializeField] private Button[] m_SurvivingSlotButtons = new Button[6];
        [SerializeField] private GameObject m_OddEvenRoot;
        [SerializeField] private Button[] m_OddEvenButtons = new Button[2];
        [SerializeField] private GameObject m_FirstEliminatedSlotRoot;
        [SerializeField] private Button[] m_FirstEliminatedSlotButtons = new Button[6];

        [Header("Confirm")]
        [SerializeField] private TMP_Text m_ValidationText;
        [SerializeField] private TMP_Text m_AgreeText;
        [SerializeField] private Button m_ConfirmBetButton;

        [Header("New Betting UI")]
        [SerializeField] private TMP_Dropdown m_WinningTeamDropdown;
        [SerializeField] private TMP_Dropdown m_GameEndTimeDropdown;
        [SerializeField] private TMP_Dropdown m_OddEvenDropdown;
        [SerializeField] private TMP_Dropdown m_FirstAnnihilatedDropdown;
        [SerializeField] private TMP_Dropdown m_SurvivingRowDropdown;
        [SerializeField] private GameObject m_GameEndTimeDropdownRoot;
        [SerializeField] private GameObject m_OddEvenDropdownRoot;
        [SerializeField] private GameObject m_FirstAnnihilatedDropdownRoot;
        [SerializeField] private GameObject m_SurvivingRowDropdownRoot;
        [SerializeField] private Button[] m_RedSurvivingSlotButtons = new Button[6];
        [SerializeField] private TMP_Text[] m_RedSurvivingSlotTexts = new TMP_Text[6];
        [SerializeField] private Image[] m_RedSurvivingSlotImages = new Image[6];
        [SerializeField] private Button[] m_BlueSurvivingSlotButtons = new Button[6];
        [SerializeField] private TMP_Text[] m_BlueSurvivingSlotTexts = new TMP_Text[6];
        [SerializeField] private Image[] m_BlueSurvivingSlotImages = new Image[6];
        [Header("Shared Top Bar")]
        [SerializeField] private TMP_Text m_RoundInfoText;
        [SerializeField] private TMP_Text m_TargetInfoText;
        [SerializeField] private TMP_Text m_NewCurrentCallText;
        [SerializeField] private TMP_Text m_NewMultiplierText;

        [Header("Shared UI References")]
        [SerializeField] private UI_BettingPhase m_BettingUi;
        [SerializeField] private CanvasGroup m_BettingContentCanvasGroup;
        [SerializeField] private UI_StageIntro m_StageIntroUi;
        [SerializeField] private TMP_Text m_NowColInfoText;
        [SerializeField] private Image m_NowColImage;
        [SerializeField] private EventTrigger m_SliderHandlePointerTrigger;
        private Tween m_SliderAttentionTween;
        private Tween m_ConfirmAttentionTween;
        private bool m_SliderTouched;
        private EventTrigger.Entry m_SliderPointerDownEntry;
        private Graphic m_ConfirmAttentionGraphic;
        private Color m_ConfirmAttentionGraphicColor;
        private bool m_HasConfirmAttentionGraphicColor;

        private static readonly Color AgreeTextColor = Color.black;
        private static readonly Color AgreeWarningColor = new Color(0.783f, 0.084f, 0.070f, 1f);
        private AwaitableCompletionSource m_PhaseCompletionSource;
        private RoundBetTicket m_DraftTicket;
        private bool m_StageIntroPending;
        private bool m_HasBettingContentRestingPosition;
        private Vector2 m_BettingContentRestingPosition;

        public event Action<ItemData> OnItemUsed;

        // 아이템 상태 트래킹

        private void Awake()
        {
            if (m_StageIntroUi == null)
            {
                m_StageIntroUi = FindAnyObjectByType<UI_StageIntro>(FindObjectsInactive.Include);
            }
            CacheBettingContentRestingPosition();
            SetBettingContentVisible(false);
            RefreshTopBar(null);
        }

        public RoundBetTicket DraftTicket
        {
            get
            {
                return m_DraftTicket;
            }
        }

        public bool UsedAdditionalBetTicket
        {
            get
            {
                return Context != null && Context.RoundItemUsage.HasUsed(ItemType.AdditionalBetTicket);
            }
        }

        public bool UsedInsurance
        {
            get
            {
                return Context != null && Context.RoundItemUsage.HasUsed(ItemType.Insurance);
            }
        }

        public override async Awaitable PreparePhaseAsync(CancellationToken token)
        {
            var cameraController = InTheArena.Camera.CameraController.Instance;
            if (cameraController != null)
            {
                await cameraController.SetPhaseAsync(InTheArena.Camera.CameraPhase.Betting, token);
            }

            InitializePhaseData();
            EnsureSharedTopBar();
            SetupUI();
            SubscribeEvents();

            CanvasGroup canvasGroup = m_BettingCanvasGroup;
            if (canvasGroup != null)
            {
                canvasGroup.gameObject.SetActive(true);
                canvasGroup.alpha = 1f;
                canvasGroup.interactable = false;
                canvasGroup.blocksRaycasts = false;
            }
            ResetBettingContentPosition();
            SetBettingContentVisible(!m_StageIntroPending);
            SetNowCol(Context.CurrentCall);
        }

        public void PrimeStageOpening(StageData stageData)
        {
            m_StageIntroPending = m_StageIntroUi != null;
            CacheBettingContentRestingPosition();
            ResetBettingContentPosition();
            SetBettingContentVisible(false);
            RefreshTopBar(Context);
            m_StageIntroUi?.Prime(stageData);
        }

        public async Awaitable PlayStageOpeningAsync(StageData stageData, CancellationToken token)
        {
            if (!m_StageIntroPending || m_StageIntroUi == null)
            {
                ResetBettingContentPosition();
                SetBettingContentVisible(true);
                return;
            }

            try
            {
                await m_StageIntroUi.PlayAsync(stageData, token);
                token.ThrowIfCancellationRequested();
                await RevealBettingContentFromBottomAsync(token);
                token.ThrowIfCancellationRequested();
                m_StageIntroPending = false;
            }
            finally
            {
                m_StageIntroUi.ReleaseAfterBettingReveal();
            }
        }

        public void LockInteractionForCombatPreparation()
        {
            CanvasGroup root = m_BettingCanvasGroup;
            if (root != null)
            {
                root.interactable = false;
                root.blocksRaycasts = false;
            }

            if (m_BettingContentCanvasGroup != null)
            {
                m_BettingContentCanvasGroup.interactable = false;
                m_BettingContentCanvasGroup.blocksRaycasts = false;
            }
        }

        public override async Awaitable EnterPhaseAsync(CancellationToken token)
        {
            CanvasGroup canvasGroup = m_BettingCanvasGroup;
            if (canvasGroup != null)
            {
                canvasGroup.DOKill();
                canvasGroup.interactable = true;
                canvasGroup.blocksRaycasts = true;
            }

            m_PhaseCompletionSource = new AwaitableCompletionSource();
            StartSliderAttention();
            using (token.Register(() => m_PhaseCompletionSource?.TrySetResult()))
            {
                await m_PhaseCompletionSource.Awaitable;
            }
            token.ThrowIfCancellationRequested();
        }

        private void InitializePhaseData()
        {
            IsPhaseCompleted = false;
            Context.AssignUnitsForBetting();
            PrewarmConfirmedPools(Context.TeamAUnitDatas, Context.TeamBUnitDatas);
            m_DraftTicket = new RoundBetTicket();
            m_DraftTicket.SetWager(WagerStepCall);
        }

        private static void PrewarmConfirmedPools(List<UnitData> red, List<UnitData> blue)
        {
            var counts = new Dictionary<UnitData, int>();
            CountUnits(red, counts);
            CountUnits(blue, counts);

            PoolManager manager = PoolManager.Require();
            var projectileCounts = new Dictionary<GameObject, int>();
            var projectilePrefabs = new List<GameObject>(4);
            foreach (KeyValuePair<UnitData, int> pair in counts)
            {
                manager.Units.Prewarm(pair.Key, pair.Value);
                CountProjectiles(projectileCounts, projectilePrefabs, pair.Key, pair.Value);
            }
            foreach (KeyValuePair<GameObject, int> pair in projectileCounts)
            {
                manager.Projectiles.Prewarm(pair.Key, Mathf.Clamp(pair.Value * 2, 16, 128));
            }
        }

        private static void CountUnits(List<UnitData> units, Dictionary<UnitData, int> counts)
        {
            if (units == null) return;
            for (int i = 0; i < units.Count; i++)
            {
                UnitData data = units[i];
                if (data == null) continue;
                counts.TryGetValue(data, out int count);
                counts[data] = count + 1;
            }
        }

        private static void CountProjectiles(Dictionary<GameObject, int> counts, List<GameObject> projectilePrefabs, UnitData unitData, int unitCount)
        {
            projectilePrefabs.Clear();
            if (unitData?.BasicAttackData != null)
            {
                unitData.BasicAttackData.CollectProjectilePrefabs(projectilePrefabs);
            }
            AddProjectileCounts(counts, projectilePrefabs, unitCount);

            IReadOnlyList<SkillData> skills = unitData?.SkillDatas;
            if (skills == null) return;

            for (int i = 0; i < skills.Count; i++)
            {
                SkillData skill = skills[i];
                if (skill == null) continue;
                projectilePrefabs.Clear();
                skill.CollectProjectilePrefabs(projectilePrefabs);
                AddProjectileCounts(counts, projectilePrefabs, unitCount);
            }
        }

        private static void AddProjectileCounts(Dictionary<GameObject, int> counts, List<GameObject> projectilePrefabs, int unitCount)
        {
            for (int i = 0; i < projectilePrefabs.Count; i++)
            {
                GameObject prefab = projectilePrefabs[i];
                if (prefab == null) continue;
                counts.TryGetValue(prefab, out int current);
                counts[prefab] = current + unitCount;
            }
        }

        private void SetupUI()
        {
            if (m_RoundText != null) m_RoundText.text = $"Round {Context.CurrentRound}";
            if (m_TeamANameText != null) m_TeamANameText.text = "Red Team";
            if (m_TeamBNameText != null) m_TeamBNameText.text = "Blue Team";

            if (m_WagerSlider != null)
            {
                m_WagerSlider.wholeNumbers = true;
                m_WagerSlider.minValue = 1f;
                m_WagerSlider.maxValue = Mathf.Max(1, GetMaximumWagerCall() / WagerStepCall);
                m_WagerSlider.SetValueWithoutNotify(m_DraftTicket.WagerCall / WagerStepCall);
            }

            SetupNewUi();

            StageData stageData = Context.CurrentStageData;
            SetActive(m_FactionBetRoot, stageData != null && stageData.EnableFactionBet);
            RefreshSpecialBetAvailability();
            UpdateSurvivingSlotAvailability();
            RefreshUnitSlotTexts();
            RefreshBetSummary();
        }

        private void SetupNewUi()
        {
            RefreshTopBar(Context);
            EnsureSurvivingRowDropdown();

            SetOptions(m_WinningTeamDropdown, "미선택", "레드", "블루", "무승부");
            SetOptions(m_GameEndTimeDropdown, "미선택", "0~5초", "5~10초", "10~15초", "15~20초", "20초 이상");
            SetOptions(m_OddEvenDropdown, "미선택", "홀", "짝");
            SetOptions(m_FirstAnnihilatedDropdown, "미선택", "레드 / 전열", "레드 / 후열", "블루 / 전열", "블루 / 후열");
            SetOptions(m_SurvivingRowDropdown, "미선택", "레드 / 1행", "레드 / 2행", "레드 / 3행", "블루 / 1행", "블루 / 2행", "블루 / 3행");
        }

        private void EnsureSurvivingRowDropdown()
        {
            // UI_BettingPhase serializes this reference. Do not clone or search a dropdown at runtime.
        }

        private static void SetOptions(TMP_Dropdown dropdown, params string[] options)
        {
            if (dropdown == null) return;
            dropdown.ClearOptions();
            dropdown.AddOptions(new List<string>(options));
            dropdown.SetValueWithoutNotify(0);
            ConfigureDropdownLayout(dropdown, options.Length);
        }

        public void RefreshTopBar(RoundContext context)
        {
            if (m_RoundInfoText != null)
            {
                m_RoundInfoText.text = context != null
                    ? $"{context.CurrentRound} / {context.MaxRounds}"
                    : "- / -";
            }

            if (m_TargetInfoText != null)
            {
                m_TargetInfoText.text = context != null
                    ? $"{context.TargetCall} Col"
                    : "- Col";
            }
        }

        private static void ConfigureDropdownLayout(TMP_Dropdown dropdown, int optionCount)
        {
            RectTransform template = dropdown.template;
            if (template == null) return;

            float height = Mathf.Max(1, optionCount) * DropdownOptionHeight;
            template.anchorMin = new Vector2(0.5f, 0f);
            template.anchorMax = new Vector2(0.5f, 0f);
            template.pivot = new Vector2(0.5f, 1f);
            template.anchoredPosition = Vector2.zero;
            template.sizeDelta = new Vector2(dropdown.GetComponent<RectTransform>().rect.width, height);

            ScrollRect scrollRect = template.GetComponent<ScrollRect>();
            if (scrollRect == null) return;

            scrollRect.horizontal = false;
            scrollRect.vertical = false;
            scrollRect.movementType = ScrollRect.MovementType.Clamped;

            RectTransform viewport = scrollRect.viewport;
            if (viewport != null)
            {
                viewport.anchorMin = Vector2.zero;
                viewport.anchorMax = Vector2.one;
                viewport.offsetMin = Vector2.zero;
                viewport.offsetMax = Vector2.zero;
            }

            RectTransform content = scrollRect.content;
            if (content == null) return;
            content.anchorMin = new Vector2(0f, 1f);
            content.anchorMax = new Vector2(1f, 1f);
            content.pivot = new Vector2(0.5f, 1f);
            content.anchoredPosition = Vector2.zero;
            content.sizeDelta = new Vector2(0f, height);
        }

        private bool HasSpecial(SpecialBetType type)
        {
            return Context != null && Context.ActiveSpecialBets.Contains(type);
        }

        private void RefreshSpecialBetAvailability()
        {
            SetActive(m_RemainingTimeRoot, HasSpecial(SpecialBetType.RemainingTime));
            SetActive(m_GameEndTimeDropdownRoot, HasSpecial(SpecialBetType.RemainingTime));
            SetActive(m_SurvivingSlotsRoot, HasSpecial(SpecialBetType.SurvivingRow));
            SetActive(m_SurvivingRowDropdownRoot, HasSpecial(SpecialBetType.SurvivingRow));
            SetActive(m_OddEvenRoot, HasSpecial(SpecialBetType.OddEven));
            SetActive(m_OddEvenDropdownRoot, HasSpecial(SpecialBetType.OddEven));
            SetActive(m_FirstEliminatedSlotRoot, HasSpecial(SpecialBetType.FirstEliminatedColumn));
            SetActive(m_FirstAnnihilatedDropdownRoot, HasSpecial(SpecialBetType.FirstEliminatedColumn));
        }

        private static void SetActive(GameObject target, bool active)
        {
            if (target != null) target.SetActive(active);
        }

        private void SubscribeEvents()
        {
            if (m_WagerSlider != null) m_WagerSlider.onValueChanged.AddListener(OnWagerChanged);
            if (m_SliderHandlePointerTrigger != null && m_SliderPointerDownEntry == null)
            {
                m_SliderHandlePointerTrigger.triggers ??= new List<EventTrigger.Entry>();
                m_SliderPointerDownEntry = new EventTrigger.Entry { eventID = EventTriggerType.PointerDown };
                m_SliderPointerDownEntry.callback.AddListener(_ => StopSliderAttention());
                m_SliderHandlePointerTrigger.triggers.Add(m_SliderPointerDownEntry);
            }
            if (m_WinningTeamDropdown != null) m_WinningTeamDropdown.onValueChanged.AddListener(OnWinningTeamChanged);
            if (m_GameEndTimeDropdown != null) m_GameEndTimeDropdown.onValueChanged.AddListener(OnGameEndTimeChanged);
            if (m_OddEvenDropdown != null) m_OddEvenDropdown.onValueChanged.AddListener(OnOddEvenChanged);
            if (m_FirstAnnihilatedDropdown != null) m_FirstAnnihilatedDropdown.onValueChanged.AddListener(OnFirstAnnihilatedChanged);
            if (m_SurvivingRowDropdown != null) m_SurvivingRowDropdown.onValueChanged.AddListener(OnSurvivingRowChanged);
            AddClick(m_RedButton, () => SetFaction(FactionPrediction.Red));
            AddClick(m_BlueButton, () => SetFaction(FactionPrediction.Blue));
            AddClick(m_DrawButton, () => SetFaction(FactionPrediction.Draw));
            AddClick(m_ClearFactionButton, () => SetFaction(FactionPrediction.NotSelected));

            for (int i = 0; i < m_RemainingTimeButtons.Length; i++)
            {
                int index = i;
                AddClick(m_RemainingTimeButtons[i], () => ToggleRemainingTime((RemainingTimePrediction)index));
            }
            for (int i = 0; i < m_OddEvenButtons.Length; i++)
            {
                int index = i;
                AddClick(m_OddEvenButtons[i], () => ToggleOddEven((OddEvenPrediction)index));
            }
            for (int i = 0; i < Mathf.Min(4, m_FirstEliminatedSlotButtons.Length); i++)
            {
                FirstEliminatedColumnPrediction prediction = (FirstEliminatedColumnPrediction)i;
                AddClick(m_FirstEliminatedSlotButtons[i], () => ToggleFirstEliminatedColumn(prediction));
            }
            if (m_ConfirmBetButton != null) m_ConfirmBetButton.onClick.AddListener(OnConfirmBetClicked);
        }

        private static void AddClick(Button button, UnityEngine.Events.UnityAction action)
        {
            if (button != null) button.onClick.AddListener(action);
        }

        private void UnsubscribeEvents()
        {
            if (m_WagerSlider != null) m_WagerSlider.onValueChanged.RemoveAllListeners();
            if (m_SliderHandlePointerTrigger != null && m_SliderPointerDownEntry != null)
            {
                m_SliderHandlePointerTrigger.triggers.Remove(m_SliderPointerDownEntry);
                m_SliderPointerDownEntry = null;
            }
            if (m_WinningTeamDropdown != null) m_WinningTeamDropdown.onValueChanged.RemoveListener(OnWinningTeamChanged);
            if (m_GameEndTimeDropdown != null) m_GameEndTimeDropdown.onValueChanged.RemoveListener(OnGameEndTimeChanged);
            if (m_OddEvenDropdown != null) m_OddEvenDropdown.onValueChanged.RemoveListener(OnOddEvenChanged);
            if (m_FirstAnnihilatedDropdown != null) m_FirstAnnihilatedDropdown.onValueChanged.RemoveListener(OnFirstAnnihilatedChanged);
            if (m_SurvivingRowDropdown != null) m_SurvivingRowDropdown.onValueChanged.RemoveListener(OnSurvivingRowChanged);
            RemoveClicks(m_RedButton, m_BlueButton, m_DrawButton, m_ClearFactionButton, m_ConfirmBetButton);
            RemoveClicks(m_RemainingTimeButtons);
            RemoveClicks(m_OddEvenButtons);
            RemoveClicks(m_FirstEliminatedSlotButtons);
            RemoveClicks(m_SurvivingSlotButtons);
        }

        private static void RemoveClicks(params Button[] buttons)
        {
            if (buttons == null) return;
            foreach (Button button in buttons)
            {
                if (button != null) button.onClick.RemoveAllListeners();
            }
        }

        private void OnWagerChanged(float value)
        {
            int steps = Mathf.Clamp(Mathf.RoundToInt(value), 1, GetMaximumWagerCall() / WagerStepCall);
            if (m_DraftTicket.WagerCall == steps * WagerStepCall) return;
            m_DraftTicket.SetWager(steps * WagerStepCall);
            RefreshWagerDisplay();
        }

        private void OnWinningTeamChanged(int value) => SetFaction(value switch
        {
            1 => FactionPrediction.Red,
            2 => FactionPrediction.Blue,
            3 => FactionPrediction.Draw,
            _ => FactionPrediction.NotSelected
        });

        private void OnGameEndTimeChanged(int value)
        {
            m_DraftTicket.SetRemainingTime(value == 0 ? null : (RemainingTimePrediction)(value - 1));
            RefreshBetSummary();
        }

        private void OnOddEvenChanged(int value)
        {
            m_DraftTicket.SetOddEven(value == 0 ? null : (OddEvenPrediction)(value - 1));
            RefreshBetSummary();
        }

        private void OnFirstAnnihilatedChanged(int value)
        {
            m_DraftTicket.SetFirstEliminatedColumn(
                value == 0 ? null : (FirstEliminatedColumnPrediction?)(value - 1));
            RefreshBetSummary();
        }

        private void OnSurvivingRowChanged(int value)
        {
            m_DraftTicket.SetSurvivingRow(value == 0 ? null : (SurvivingRowPrediction?)(value - 1));
            RefreshBetSummary();
        }

        private void SetFaction(FactionPrediction prediction)
        {
            m_DraftTicket.SetFaction(prediction);
            RefreshBetSummary();
        }

        private void ToggleRemainingTime(RemainingTimePrediction value)
        {
            m_DraftTicket.SetRemainingTime(m_DraftTicket.RemainingTime == value ? null : value);
            RefreshBetSummary();
        }

        private void ToggleOddEven(OddEvenPrediction value)
        {
            m_DraftTicket.SetOddEven(m_DraftTicket.OddEven == value ? null : value);
            RefreshBetSummary();
        }

        private void ToggleFirstEliminatedColumn(FirstEliminatedColumnPrediction prediction)
        {
            m_DraftTicket.SetFirstEliminatedColumn(
                m_DraftTicket.FirstEliminatedColumn == prediction ? null : prediction);
            RefreshBetSummary();
        }

        private void UpdateSurvivingSlotAvailability()
        {
            SetButtonsInteractable(m_SurvivingSlotButtons, false);
            SetButtonsInteractable(m_RedSurvivingSlotButtons, false);
            SetButtonsInteractable(m_BlueSurvivingSlotButtons, false);
        }

        private static void SetButtonsInteractable(Button[] buttons, bool interactable)
        {
            foreach (Button button in buttons)
            {
                if (button == null) continue;
                if (!interactable) button.transition = Selectable.Transition.None;
                button.interactable = interactable;
            }
        }

        private void RefreshUnitSlotTexts()
        {
            SetUnitSlots(m_RedSurvivingSlotTexts, m_RedSurvivingSlotImages, Context.TeamADeployments, Team.Red);
            SetUnitSlots(m_BlueSurvivingSlotTexts, m_BlueSurvivingSlotImages, Context.TeamBDeployments, Team.Blue);
        }

        private static void SetUnitSlots(TMP_Text[] texts, Image[] images, List<TeamUnitDeployment> deployments, Team team)
        {
            for (int i = 0; i < texts.Length; i++)
            {
                TeamUnitDeployment deployment = deployments.Find(item => item.CellIndex == i);
                UnitData representative = deployment?.Units?.Find(unit => unit != null);
                int count = deployment?.Units?.Count ?? 0;

                if (texts[i] != null)
                {
                    texts[i].gameObject.SetActive(count > 0);
                    texts[i].text = count > 0 ? $"×{count}" : string.Empty;
                }

                if (images == null || i >= images.Length || images[i] == null) continue;
                Sprite portrait = representative?.GetPortrait(team);
                images[i].gameObject.SetActive(portrait != null);
                images[i].sprite = portrait;
            }
        }

        private void RefreshBetSummary()
        {
            RefreshSelectionVisuals();
            int wager = Mathf.Clamp(m_DraftTicket.WagerCall, WagerStepCall, GetMaximumWagerCall());
            m_DraftTicket.SetWager(wager);
            if (m_WagerSlider != null) m_WagerSlider.SetValueWithoutNotify(wager / WagerStepCall);
            RefreshWagerDisplay();
        }

        private void RefreshWagerDisplay()
        {
            int wager = Mathf.Clamp(m_DraftTicket.WagerCall, WagerStepCall, GetMaximumWagerCall());
            m_DraftTicket.SetWager(wager);
            int remainingCall = Mathf.Max(0, Context.CurrentCall - wager);
            if (m_CurrentCallText != null) m_CurrentCallText.text = $"{remainingCall} Call";
            SetNowCol(Context.CurrentCall);
            if (m_WagerCallText != null) m_WagerCallText.text = $"{wager} Call";
            string multiplierLabel = m_DraftTicket.Multiplier > 0
                ? $"×{m_DraftTicket.Multiplier}"
                : "확인";
            if (m_NewMultiplierText != null) m_NewMultiplierText.text = multiplierLabel;
            if (m_MultiplierText != null) m_MultiplierText.text = multiplierLabel;
            if (m_EstimatedPayoutText != null)
            {
                int effectiveWager = wager;
                if (Context.RoundItemUsage.HasUsed(ItemType.AdditionalBetTicket))
                {
                    effectiveWager += AdditionalBetBonusCall;
                }

                m_EstimatedPayoutText.text = $"{effectiveWager * m_DraftTicket.Multiplier} Call";
            }

            bool valid = m_DraftTicket.Validate(Context.CurrentStageData, Context, Context.CurrentCall, out _);
            if (m_ConfirmBetButton != null) m_ConfirmBetButton.interactable = valid;
            RefreshConfirmAttention(valid);
            if (m_ValidationText != null)
            {
                m_ValidationText.text = string.Empty;
                m_ValidationText.gameObject.SetActive(false);
            }

            if (m_AgreeText != null)
            {
                bool hasSelection = m_DraftTicket.SelectedCategoryCount > 0;
                m_AgreeText.text = hasSelection
                    ? "위 배팅에 동의하십니까?"
                    : "최소 1개 이상의 배팅 내역을 선택해야합니다.";
                m_AgreeText.color = hasSelection ? AgreeTextColor : AgreeWarningColor;
            }
        }

        private int GetMaximumWagerCall()
        {
            int roundedCall = Mathf.FloorToInt(Mathf.Max(0, Context?.CurrentCall ?? 0) / (float)WagerStepCall) * WagerStepCall;
            return Mathf.Max(WagerStepCall, roundedCall);
        }

        private void RefreshSelectionVisuals()
        {
            SetSelected(m_RedButton, m_DraftTicket.Faction == FactionPrediction.Red);
            SetSelected(m_BlueButton, m_DraftTicket.Faction == FactionPrediction.Blue);
            SetSelected(m_DrawButton, m_DraftTicket.Faction == FactionPrediction.Draw);
            if (m_WinningTeamDropdown != null) m_WinningTeamDropdown.SetValueWithoutNotify(m_DraftTicket.Faction switch
            {
                FactionPrediction.Red => 1,
                FactionPrediction.Blue => 2,
                FactionPrediction.Draw => 3,
                _ => 0
            });
            if (m_GameEndTimeDropdown != null) m_GameEndTimeDropdown.SetValueWithoutNotify(m_DraftTicket.RemainingTime.HasValue ? (int)m_DraftTicket.RemainingTime.Value + 1 : 0);
            if (m_OddEvenDropdown != null) m_OddEvenDropdown.SetValueWithoutNotify(m_DraftTicket.OddEven.HasValue ? (int)m_DraftTicket.OddEven.Value + 1 : 0);
            if (m_FirstAnnihilatedDropdown != null) m_FirstAnnihilatedDropdown.SetValueWithoutNotify(
                m_DraftTicket.FirstEliminatedColumn.HasValue ? (int)m_DraftTicket.FirstEliminatedColumn.Value + 1 : 0);
            if (m_SurvivingRowDropdown != null) m_SurvivingRowDropdown.SetValueWithoutNotify(
                m_DraftTicket.SurvivingRow.HasValue ? (int)m_DraftTicket.SurvivingRow.Value + 1 : 0);

            for (int i = 0; i < m_RemainingTimeButtons.Length; i++)
                SetSelected(m_RemainingTimeButtons[i], m_DraftTicket.RemainingTime == (RemainingTimePrediction)i);
            for (int i = 0; i < m_OddEvenButtons.Length; i++)
                SetSelected(m_OddEvenButtons[i], m_DraftTicket.OddEven == (OddEvenPrediction)i);
            for (int i = 0; i < m_FirstEliminatedSlotButtons.Length; i++)
                SetSelected(m_FirstEliminatedSlotButtons[i], i < 4 && m_DraftTicket.FirstEliminatedColumn == (FirstEliminatedColumnPrediction)i);
        }

        private static void SetSelected(Button button, bool selected)
        {
            if (button == null || button.image == null) return;
            button.image.color = selected
                ? new Color(0.95f, 0.72f, 0.18f)
                : new Color(0.2f, 0.24f, 0.3f);
        }



        internal bool TryApplyPurchasedItemEffect(ItemData itemData, out string message)
        {
            message = string.Empty;

            if (itemData == null || Context == null)
            {
                message = "유효하지 않은 베팅 아이템입니다.";
                return false;
            }

            if (IsPhaseCompleted || (m_DraftTicket != null && m_DraftTicket.IsPlaced))
            {
                message = "이미 배팅이 확정되었습니다.";
                return false;
            }

            if (itemData.ItemType == ItemType.AdditionalBetTicket)
            {
                message = $"추가 배팅권을 사용했습니다. (+{AdditionalBetBonusCall} Call)";
                OnItemUsed?.Invoke(itemData);
                return true;
            }

            if (itemData.ItemType == ItemType.Insurance)
            {
                message = "보험을 사용했습니다. 패배 시 배팅 Call을 돌려받습니다.";
                OnItemUsed?.Invoke(itemData);
                return true;
            }

            if (itemData.ItemType != ItemType.RerollTicket)
            {
                message = "베팅 아이템이 아닙니다.";
                return false;
            }

            if (!Context.RerollSpecialBets())
            {
                message = "현재 라운드에는 리롤할 특수 베팅이 없습니다.";
                return false;
            }

            m_DraftTicket?.ClearSpecialPredictions();
            RefreshSpecialBetAvailability();
            UpdateSurvivingSlotAvailability();
            RefreshBetSummary();

            message = $"특수 배팅이 {string.Join(", ", Context.ActiveSpecialBets)}로 변경되었습니다.";
            OnItemUsed?.Invoke(itemData);
            return true;
        }

        internal void RestorePurchasedItemState(
            IReadOnlyList<SpecialBetType> previousSpecialBetOrder,
            BettingDraftSpecialPredictionState previousDraftSpecialPredictions = null)
        {
            Context?.RestoreSpecialBetOrder(previousSpecialBetOrder);
            previousDraftSpecialPredictions?.Restore(m_DraftTicket);
            RefreshSpecialBetAvailability();
            UpdateSurvivingSlotAvailability();
            if (m_DraftTicket != null)
            {
                RefreshBetSummary();
            }
        }

        internal BettingDraftSpecialPredictionState GetDraftSpecialPredictionsForItemUse()
        {
            return m_DraftTicket == null ? null : new BettingDraftSpecialPredictionState(m_DraftTicket);
        }

        internal IReadOnlyList<SpecialBetType> GetSpecialBetOrderForItemUse()
        {
            return Context?.SpecialBetOrder;
        }

        public void AnimateNowCol(int targetCall)
        {
            if (m_NowColInfoText == null) return;
            int from = Context != null ? Mathf.Max(0, Context.CurrentCall - (Context.Settlement?.PayoutCall ?? 0)) : 0;
            AnimateNowCol(from, targetCall);
        }

        public void AnimateNowCol(int fromCall, int targetCall) => DOTween.To(() => fromCall, SetNowCol, Mathf.Max(0, targetCall), 0.45f).SetEase(Ease.OutCubic).SetTarget(m_NowColInfoText);

        public void PlayNowColRewardAnimation(RectTransform source, int fromCall, int targetCall)
        {
            int reward = Mathf.Max(0, targetCall - fromCall);
            ResolveNowColImage();
            if (source == null || m_NowColImage == null || m_NowColImage.sprite == null || reward <= 0)
            {
                AnimateNowCol(fromCall, targetCall);
                return;
            }

            int received = 0;
            int displayed = fromCall;
            SetNowCol(fromCall);
            UI_FlyingRewardEffect.Play(source, m_NowColImage.rectTransform, m_NowColImage.sprite, reward, amount =>
            {
                received += amount;
                int nextValue = fromCall + received;
                m_NowColInfoText.DOKill();
                DOTween.To(() => displayed, value =>
                {
                    displayed = value;
                    SetNowCol(value);
                }, nextValue, 0.18f).SetEase(Ease.OutCubic).SetTarget(m_NowColInfoText).SetUpdate(true);
            }, () =>
            {
                m_NowColInfoText.DOKill();
                SetNowCol(targetCall);
            }, previewText: $"+{reward} Col");
        }

        private void EnsureSharedTopBar()
        {
            if (m_BettingUi != null)
            {
                m_BettingUi.BindAndShow(this, Context, StageManager.Instance?.PlayerState);
            }
        }

        private void ResolveNowColImage()
        {
            if (m_NowColImage != null) return;
            Transform root = m_BettingUi != null ? m_BettingUi.transform : transform;
            Transform icon = FindDescendant(root, "NowCol_Image");
            if (icon != null) m_NowColImage = icon.GetComponent<Image>();
        }

        private static Transform FindDescendant(Transform root, string objectName)
        {
            if (root == null) return null;
            if (root.name == objectName) return root;
            for (int i = 0; i < root.childCount; i++)
            {
                Transform found = FindDescendant(root.GetChild(i), objectName);
                if (found != null) return found;
            }
            return null;
        }

        private void SetBettingContentVisible(bool visible)
        {
            if (m_BettingContentCanvasGroup == null) return;
            m_BettingContentCanvasGroup.gameObject.SetActive(true);
            m_BettingContentCanvasGroup.alpha = visible ? 1f : 0f;
            m_BettingContentCanvasGroup.interactable = visible;
            m_BettingContentCanvasGroup.blocksRaycasts = visible;
        }

        private async Awaitable RevealBettingContentFromBottomAsync(CancellationToken token)
        {
            if (m_BettingContentCanvasGroup == null) return;

            RectTransform contentRect = m_BettingContentCanvasGroup.transform as RectTransform;
            if (contentRect == null) return;

            CacheBettingContentRestingPosition();
            RectTransform parentRect = contentRect.parent as RectTransform;
            float slideDistance = parentRect != null
                ? Mathf.Max(parentRect.rect.height, contentRect.rect.height)
                : Screen.height;

            contentRect.DOKill();
            contentRect.anchoredPosition = m_BettingContentRestingPosition + Vector2.down * slideDistance;
            m_BettingContentCanvasGroup.gameObject.SetActive(true);
            m_BettingContentCanvasGroup.alpha = 1f;
            m_BettingContentCanvasGroup.interactable = false;
            m_BettingContentCanvasGroup.blocksRaycasts = false;

            Tween slideTween = contentRect
                .DOAnchorPos(m_BettingContentRestingPosition, 0.5f)
                .SetEase(Ease.OutCubic)
                .SetUpdate(true);
            await AwaitTweenAsync(slideTween, token);
            token.ThrowIfCancellationRequested();
        }

        private void CacheBettingContentRestingPosition()
        {
            if (m_HasBettingContentRestingPosition || m_BettingContentCanvasGroup == null) return;
            if (m_BettingContentCanvasGroup.transform is not RectTransform contentRect) return;
            m_BettingContentRestingPosition = contentRect.anchoredPosition;
            m_HasBettingContentRestingPosition = true;
        }

        private void ResetBettingContentPosition()
        {
            CacheBettingContentRestingPosition();
            if (!m_HasBettingContentRestingPosition || m_BettingContentCanvasGroup == null) return;
            if (m_BettingContentCanvasGroup.transform is RectTransform contentRect)
            {
                contentRect.DOKill();
                contentRect.anchoredPosition = m_BettingContentRestingPosition;
            }
        }

        private void SetNowCol(int value)
        {
            if (m_NowColInfoText != null) m_NowColInfoText.text = $"{Mathf.Max(0, value)} Col";
        }

        private void StartSliderAttention()
        {
            m_SliderTouched = false;
            if (m_WagerSlider?.handleRect == null) return;
            Transform handle = m_WagerSlider.handleRect;
            handle.DOKill();
            m_SliderAttentionTween = handle.DOScale(1.12f, 0.5f).SetLoops(-1, LoopType.Yoyo).SetUpdate(true);
        }

        private void StopSliderAttention()
        {
            if (m_SliderTouched) return;
            m_SliderTouched = true;
            if (m_WagerSlider?.handleRect != null)
            {
                m_WagerSlider.handleRect.DOKill();
                m_WagerSlider.handleRect.localScale = Vector3.one;
            }
        }

        private void RefreshConfirmAttention(bool valid)
        {
            if (!valid || m_ConfirmBetButton == null)
            {
                m_ConfirmAttentionTween?.Kill();
                RestoreConfirmAttentionGraphic();
                return;
            }
            if (m_ConfirmAttentionTween != null && m_ConfirmAttentionTween.IsActive()) return;
            m_ConfirmAttentionGraphic = m_ConfirmBetButton.targetGraphic;
            if (m_ConfirmAttentionGraphic == null) return;
            if (!m_HasConfirmAttentionGraphicColor)
            {
                m_ConfirmAttentionGraphicColor = m_ConfirmAttentionGraphic.color;
                m_HasConfirmAttentionGraphicColor = true;
            }
            m_ConfirmAttentionGraphic.DOKill();
            m_ConfirmAttentionGraphic.color = m_ConfirmAttentionGraphicColor;
            m_ConfirmAttentionTween = m_ConfirmAttentionGraphic
                .DOFade(0.55f, 0.55f)
                .SetLoops(-1, LoopType.Yoyo)
                .SetUpdate(true);
        }

        private void StopAttention()
        {
            StopSliderAttention();
            m_ConfirmAttentionTween?.Kill();
            RestoreConfirmAttentionGraphic();
        }

        private void RestoreConfirmAttentionGraphic()
        {
            if (m_ConfirmAttentionGraphic == null || !m_HasConfirmAttentionGraphicColor) return;
            m_ConfirmAttentionGraphic.DOKill();
            m_ConfirmAttentionGraphic.color = m_ConfirmAttentionGraphicColor;
        }

        private void OnConfirmBetClicked()
        {
            if (IsPhaseCompleted) return;

            m_DraftTicket.SetItemUsages(
                Context.RoundItemUsage.HasUsed(ItemType.AdditionalBetTicket),
                Context.RoundItemUsage.HasUsed(ItemType.Insurance));
            int callBeforeBet = Context.CurrentCall;
            if (!Context.StageSession.TryPlaceBet(m_DraftTicket, Context, out string error))
            {
                Debug.LogError($"[BettingPhase] {error}");
                return;
            }

            Context.BetTicket = m_DraftTicket;
            StopAttention();
            AnimateNowCol(callBeforeBet, Context.CurrentCall);
            CompletePhase();
            m_PhaseCompletionSource?.TrySetResult();
        }

#pragma warning disable CS1998 // Phase API requires an Awaitable even though this transition is now immediate.
        public override async Awaitable ExitPhaseAsync(CancellationToken token)
        {
            UnsubscribeEvents();
            StopAttention();
            CanvasGroup canvasGroup = m_BettingContentCanvasGroup ?? m_BettingCanvasGroup;
            if (canvasGroup != null && canvasGroup.gameObject != null)
            {
                canvasGroup.DOKill();
                canvasGroup.alpha = 0f;
                canvasGroup.interactable = false;
                canvasGroup.blocksRaycasts = false;
                canvasGroup.gameObject.SetActive(false);
            }
            ResetBettingContentPosition();
            if (this != null && transform != null)
                transform.DOKill();
        }
#pragma warning restore CS1998

        private static async Awaitable AwaitTweenAsync(Tween tween, CancellationToken token)
        {
            if (tween == null || !tween.IsActive()) return;
            using (token.Register(() =>
            {
                if (tween != null && tween.IsActive()) tween.Kill();
            }))
            {
                try
                {
                    await tween.AsyncWaitForCompletion();
                }
                catch (MissingReferenceException)
                {
                    // The UI can be destroyed while the phase is being cancelled or the scene is unloading.
                }
            }
        }

        private void OnDestroy()
        {
            transform.DOKill();
            UnsubscribeEvents();
        }
    }
}
#endif
