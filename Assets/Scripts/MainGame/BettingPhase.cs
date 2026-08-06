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

namespace InTheArena.MainGame
{
    [DisallowMultipleComponent]
    public class BettingPhase : RoundPhaseBase
    {
        private const int WagerStepCall = 100;
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
        [SerializeField] private Button m_ConfirmBetButton;

        [Header("New Betting UI")]
        [SerializeField] private TMP_Dropdown m_WinningTeamDropdown;
        [SerializeField] private TMP_Dropdown m_GameEndTimeDropdown;
        [SerializeField] private TMP_Dropdown m_OddEvenDropdown;
        [SerializeField] private TMP_Dropdown m_FirstAnnihilatedDropdown;
        [SerializeField] private GameObject m_GameEndTimeDropdownRoot;
        [SerializeField] private GameObject m_OddEvenDropdownRoot;
        [SerializeField] private GameObject m_FirstAnnihilatedDropdownRoot;
        [SerializeField] private Button[] m_RedSurvivingSlotButtons = new Button[6];
        [SerializeField] private TMP_Text[] m_RedSurvivingSlotTexts = new TMP_Text[6];
        [SerializeField] private Image[] m_RedSurvivingSlotImages = new Image[6];
        [SerializeField] private Button[] m_BlueSurvivingSlotButtons = new Button[6];
        [SerializeField] private TMP_Text[] m_BlueSurvivingSlotTexts = new TMP_Text[6];
        [SerializeField] private Image[] m_BlueSurvivingSlotImages = new Image[6];
        [SerializeField] private TMP_Text m_NewRoundText;
        [SerializeField] private TMP_Text m_NewCurrentCallText;
        [SerializeField] private TMP_Text m_NewMultiplierText;

        private readonly HashSet<int> m_SelectedSurvivingSlots = new HashSet<int>();
        private AwaitableCompletionSource m_PhaseCompletionSource;
        private RoundBetTicket m_DraftTicket;

        // 아이템 상태 트래킹
        private SpecialBetType? m_OverriddenSpecialBet = null;

        public RoundBetTicket DraftTicket
        {
            get
            {
                return m_DraftTicket;
            }
        }

        public SpecialBetType? OverriddenSpecialBet
        {
            get
            {
                return m_OverriddenSpecialBet;
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
            using (token.Register(() => m_PhaseCompletionSource?.TrySetResult()))
            {
                await m_PhaseCompletionSource.Awaitable;
            }
            token.ThrowIfCancellationRequested();
        }

        private void InitializePhaseData()
        {
            IsPhaseCompleted = false;
            m_OverriddenSpecialBet = null;

            Context.AssignUnitsForBetting();
            PrewarmConfirmedPools(Context.TeamAUnitDatas, Context.TeamBUnitDatas);
            m_DraftTicket = new RoundBetTicket();
            m_DraftTicket.SetWager(GetMaximumWagerCall());
            m_SelectedSurvivingSlots.Clear();
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
            if (m_NewRoundText != null) m_NewRoundText.text = $"Round {Context.CurrentRound}";
            SetOptions(m_WinningTeamDropdown, "승리 진영 선택", "레드", "블루", "무승부");
            SetOptions(m_GameEndTimeDropdown, "종료 시간 선택", "0~5초", "5~10초", "10~15초", "15~20초", "20초 이상");
            SetOptions(m_OddEvenDropdown, "홀짝 선택", "홀", "짝");
            SetOptions(m_FirstAnnihilatedDropdown, "첫 전멸 슬롯 선택", "1번", "2번", "3번", "4번", "5번", "6번");
        }

        private static void SetOptions(TMP_Dropdown dropdown, params string[] options)
        {
            if (dropdown == null) return;
            dropdown.ClearOptions();
            dropdown.AddOptions(new List<string>(options));
            dropdown.SetValueWithoutNotify(0);
        }

        private bool HasSpecial(SpecialBetType type)
        {
            return Context != null && Context.ActiveSpecialBets.Contains(type);
        }

        private void RefreshSpecialBetAvailability()
        {
            SetActive(m_RemainingTimeRoot, HasSpecial(SpecialBetType.RemainingTime));
            SetActive(m_GameEndTimeDropdownRoot, HasSpecial(SpecialBetType.RemainingTime));
            SetActive(m_SurvivingSlotsRoot, HasSpecial(SpecialBetType.SurvivingSlots));
            SetActive(m_OddEvenRoot, HasSpecial(SpecialBetType.OddEven));
            SetActive(m_OddEvenDropdownRoot, HasSpecial(SpecialBetType.OddEven));
            SetActive(m_FirstEliminatedSlotRoot, HasSpecial(SpecialBetType.FirstEliminatedSlot));
            SetActive(m_FirstAnnihilatedDropdownRoot, HasSpecial(SpecialBetType.FirstEliminatedSlot));
        }

        private static void SetActive(GameObject target, bool active)
        {
            if (target != null) target.SetActive(active);
        }

        private void SubscribeEvents()
        {
            if (m_WagerSlider != null) m_WagerSlider.onValueChanged.AddListener(OnWagerChanged);
            if (m_WinningTeamDropdown != null) m_WinningTeamDropdown.onValueChanged.AddListener(OnWinningTeamChanged);
            if (m_GameEndTimeDropdown != null) m_GameEndTimeDropdown.onValueChanged.AddListener(OnGameEndTimeChanged);
            if (m_OddEvenDropdown != null) m_OddEvenDropdown.onValueChanged.AddListener(OnOddEvenChanged);
            if (m_FirstAnnihilatedDropdown != null) m_FirstAnnihilatedDropdown.onValueChanged.AddListener(OnFirstAnnihilatedChanged);
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
            for (int i = 0; i < m_FirstEliminatedSlotButtons.Length; i++)
            {
                int slot = i + 1;
                AddClick(m_FirstEliminatedSlotButtons[i], () => ToggleFirstEliminatedSlot(slot));
            }
            for (int i = 0; i < m_SurvivingSlotButtons.Length; i++)
            {
                int slot = i + 1;
                AddClick(m_SurvivingSlotButtons[i], () => ToggleSurvivingSlot(slot));
            }
            AddSurvivingSlotClicks(m_RedSurvivingSlotButtons);
            AddSurvivingSlotClicks(m_BlueSurvivingSlotButtons);
            if (m_ConfirmBetButton != null) m_ConfirmBetButton.onClick.AddListener(OnConfirmBetClicked);
        }

        private void AddSurvivingSlotClicks(Button[] buttons)
        {
            for (int i = 0; i < buttons.Length; i++)
            {
                int slot = i + 1;
                AddClick(buttons[i], () => ToggleSurvivingSlot(slot));
            }
        }

        private static void AddClick(Button button, UnityEngine.Events.UnityAction action)
        {
            if (button != null) button.onClick.AddListener(action);
        }

        private void UnsubscribeEvents()
        {
            if (m_WagerSlider != null) m_WagerSlider.onValueChanged.RemoveAllListeners();
            if (m_WinningTeamDropdown != null) m_WinningTeamDropdown.onValueChanged.RemoveListener(OnWinningTeamChanged);
            if (m_GameEndTimeDropdown != null) m_GameEndTimeDropdown.onValueChanged.RemoveListener(OnGameEndTimeChanged);
            if (m_OddEvenDropdown != null) m_OddEvenDropdown.onValueChanged.RemoveListener(OnOddEvenChanged);
            if (m_FirstAnnihilatedDropdown != null) m_FirstAnnihilatedDropdown.onValueChanged.RemoveListener(OnFirstAnnihilatedChanged);
            RemoveClicks(m_RedButton, m_BlueButton, m_DrawButton, m_ClearFactionButton, m_ConfirmBetButton);
            RemoveClicks(m_RemainingTimeButtons);
            RemoveClicks(m_OddEvenButtons);
            RemoveClicks(m_FirstEliminatedSlotButtons);
            RemoveClicks(m_SurvivingSlotButtons);
            RemoveClicks(m_RedSurvivingSlotButtons);
            RemoveClicks(m_BlueSurvivingSlotButtons);
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
            m_DraftTicket.SetWager(steps * WagerStepCall);
            RefreshBetSummary();
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
            m_DraftTicket.SetFirstEliminatedSlot(value == 0 ? null : value);
            RefreshBetSummary();
        }

        private void SetFaction(FactionPrediction prediction)
        {
            m_DraftTicket.SetFaction(prediction);
            if (prediction != FactionPrediction.Red && prediction != FactionPrediction.Blue)
            {
                m_SelectedSurvivingSlots.Clear();
                m_DraftTicket.ClearSurvivingSlots();
            }
            else if (m_DraftTicket.HasSurvivingSlotsPrediction)
            {
                m_DraftTicket.SetSurvivingSlots(GetSelectedTeam(), m_SelectedSurvivingSlots);
            }
            UpdateSurvivingSlotAvailability();
            RefreshBetSummary();
        }

        private Team GetSelectedTeam()
        {
            return m_DraftTicket.Faction == FactionPrediction.Red ? Team.Red :
                m_DraftTicket.Faction == FactionPrediction.Blue ? Team.Blue : Team.None;
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

        private void ToggleFirstEliminatedSlot(int slot)
        {
            m_DraftTicket.SetFirstEliminatedSlot(m_DraftTicket.FirstEliminatedSlot == slot ? null : slot);
            RefreshBetSummary();
        }

        private void ToggleSurvivingSlot(int slot)
        {
            Team team = GetSelectedTeam();
            if (team == Team.None || !IsOccupiedSlot(team, slot)) return;

            if (!m_SelectedSurvivingSlots.Add(slot)) m_SelectedSurvivingSlots.Remove(slot);
            if (m_SelectedSurvivingSlots.Count == 0)
                m_DraftTicket.ClearSurvivingSlots();
            else
                m_DraftTicket.SetSurvivingSlots(team, m_SelectedSurvivingSlots);
            RefreshBetSummary();
        }

        private void UpdateSurvivingSlotAvailability()
        {
            Team team = GetSelectedTeam();
            for (int i = 0; i < m_SurvivingSlotButtons.Length; i++)
            {
                Button button = m_SurvivingSlotButtons[i];
                if (button != null) button.interactable = team != Team.None && IsOccupiedSlot(team, i + 1);
            }
            UpdateNewSurvivingSlotAvailability(m_RedSurvivingSlotButtons, Team.Red);
            UpdateNewSurvivingSlotAvailability(m_BlueSurvivingSlotButtons, Team.Blue);
        }

        private void UpdateNewSurvivingSlotAvailability(Button[] buttons, Team buttonTeam)
        {
            Team selectedTeam = GetSelectedTeam();
            bool isAvailable = HasSpecial(SpecialBetType.SurvivingSlots);
            for (int i = 0; i < buttons.Length; i++)
            {
                if (buttons[i] != null)
                    buttons[i].interactable = isAvailable && selectedTeam == buttonTeam && IsOccupiedSlot(buttonTeam, i + 1);
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

        private bool IsOccupiedSlot(Team team, int slot)
        {
            List<TeamUnitDeployment> deployments = team == Team.Red
                ? Context.TeamADeployments
                : Context.TeamBDeployments;
            return deployments.Exists(item => item.CellIndex == slot - 1 && item.Units.Count > 0);
        }

        private void RefreshBetSummary()
        {
            RefreshSelectionVisuals();
            int wager = Mathf.Clamp(m_DraftTicket.WagerCall, WagerStepCall, GetMaximumWagerCall());
            m_DraftTicket.SetWager(wager);
            if (m_WagerSlider != null) m_WagerSlider.SetValueWithoutNotify(wager / WagerStepCall);
            int remainingCall = Mathf.Max(0, Context.CurrentCall - wager);
            if (m_CurrentCallText != null) m_CurrentCallText.text = $"{remainingCall} Call";
            if (m_NewCurrentCallText != null) m_NewCurrentCallText.text = $"{remainingCall} Call";
            if (m_WagerCallText != null) m_WagerCallText.text = $"{wager} Call";
            string multiplierLabel = m_DraftTicket.Multiplier > 0
                ? $"×{m_DraftTicket.Multiplier}"
                : "확인";
            if (m_NewMultiplierText != null) m_NewMultiplierText.text = multiplierLabel;
            if (m_MultiplierText != null) m_MultiplierText.text = multiplierLabel;
            if (m_EstimatedPayoutText != null)
            {
                m_EstimatedPayoutText.text = $"{wager * m_DraftTicket.Multiplier} Call";
            }

            bool valid = m_DraftTicket.Validate(Context.CurrentStageData, Context, Context.CurrentCall, out string error);
            if (m_ConfirmBetButton != null) m_ConfirmBetButton.interactable = valid;
            if (m_ValidationText != null) m_ValidationText.text = valid ? string.Empty : error;
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
            if (m_FirstAnnihilatedDropdown != null) m_FirstAnnihilatedDropdown.SetValueWithoutNotify(m_DraftTicket.FirstEliminatedSlot ?? 0);

            for (int i = 0; i < m_RemainingTimeButtons.Length; i++)
                SetSelected(m_RemainingTimeButtons[i], m_DraftTicket.RemainingTime == (RemainingTimePrediction)i);
            for (int i = 0; i < m_OddEvenButtons.Length; i++)
                SetSelected(m_OddEvenButtons[i], m_DraftTicket.OddEven == (OddEvenPrediction)i);
            for (int i = 0; i < m_FirstEliminatedSlotButtons.Length; i++)
                SetSelected(m_FirstEliminatedSlotButtons[i], m_DraftTicket.FirstEliminatedSlot == i + 1);
            for (int i = 0; i < m_SurvivingSlotButtons.Length; i++)
                SetSelected(m_SurvivingSlotButtons[i], m_SelectedSurvivingSlots.Contains(i + 1));
            RefreshNewSlotSelection(m_RedSurvivingSlotButtons);
            RefreshNewSlotSelection(m_BlueSurvivingSlotButtons);
        }

        private void RefreshNewSlotSelection(Button[] buttons)
        {
            for (int i = 0; i < buttons.Length; i++)
                SetSelected(buttons[i], m_SelectedSurvivingSlots.Contains(i + 1));
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

            if (itemData.ItemType == ItemType.AdditionalBetTicket)
            {
                message = "추가 배팅권을 사용했습니다. (+500 Call)";
                return true;
            }

            if (itemData.ItemType == ItemType.Insurance)
            {
                message = "보험을 사용했습니다. 패배 시 배팅 Call을 돌려받습니다.";
                return true;
            }

            if (itemData.ItemType != ItemType.RerollTicket)
            {
                message = "베팅 아이템이 아닙니다.";
                return false;
            }

            IReadOnlyList<SpecialBetType> specialBets = Context.CurrentStageData?.SpecialBetTypes;
            if (specialBets == null || specialBets.Count == 0)
            {
                message = "변경할 특수 베팅 규칙이 없습니다.";
                return false;
            }

            int randomIndex = UnityEngine.Random.Range(0, specialBets.Count);
            m_OverriddenSpecialBet = specialBets[randomIndex];
            Context.SetActiveSpecialBets(new[] { m_OverriddenSpecialBet.Value });
            RefreshSpecialBetAvailability();
            UpdateSurvivingSlotAvailability();
            RefreshBetSummary();

            message = $"특수 배팅이 {m_OverriddenSpecialBet}로 변경되었습니다.";
            return true;
        }

        internal void RestorePurchasedItemState(
            IReadOnlyList<SpecialBetType> previousSpecialBets,
            SpecialBetType? previousOverride)
        {
            m_OverriddenSpecialBet = previousOverride;
            Context?.SetActiveSpecialBets(previousSpecialBets);
            RefreshSpecialBetAvailability();
            UpdateSurvivingSlotAvailability();
            RefreshBetSummary();
        }

        internal IReadOnlyList<SpecialBetType> GetActiveSpecialBetsForItemUse()
        {
            return Context?.ActiveSpecialBets;
        }

        private void OnConfirmBetClicked()
        {
            if (IsPhaseCompleted) return;

            m_DraftTicket.SetItemUsages(
                Context.RoundItemUsage.HasUsed(ItemType.AdditionalBetTicket),
                Context.RoundItemUsage.HasUsed(ItemType.Insurance));
            if (!Context.StageSession.TryPlaceBet(m_DraftTicket, Context, out string error))
            {
                if (m_ValidationText != null) m_ValidationText.text = error;
                Debug.LogError($"[BettingPhase] {error}");
                return;
            }

            Context.BetTicket = m_DraftTicket;
            CompletePhase();
            m_PhaseCompletionSource?.TrySetResult();
        }

        public override async Awaitable ExitPhaseAsync(CancellationToken token)
        {
            UnsubscribeEvents();
            CanvasGroup canvasGroup = m_BettingCanvasGroup;
            if (canvasGroup != null)
            {
                await AwaitTweenAsync(canvasGroup.DOFade(0f, 0.3f).SetEase(Ease.InQuad), token);
                canvasGroup.gameObject.SetActive(false);
            }
            transform.DOKill();
        }

        private static async Awaitable AwaitTweenAsync(Tween tween, CancellationToken token)
        {
            if (tween == null || !tween.IsActive()) return;
            using (token.Register(() =>
            {
                if (tween.IsActive()) tween.Kill();
            }))
            {
                await tween.AsyncWaitForCompletion();
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
