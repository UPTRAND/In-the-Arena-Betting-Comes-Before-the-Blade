#if UNITY_6000_0_OR_NEWER
using System;
using System.Collections.Generic;
using System.Text;
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

        [Header("New Betting UI (optional)")]
        [SerializeField] private TMP_InputField m_WagerInput;
        [SerializeField] private TMP_Dropdown m_WinningTeamDropdown;
        [SerializeField] private TMP_Dropdown m_GameEndTimeDropdown;
        [SerializeField] private TMP_Dropdown m_OddEvenDropdown;
        [SerializeField] private TMP_Dropdown m_FirstAnnihilatedDropdown;
        [SerializeField] private GameObject m_GameEndTimeDropdownRoot;
        [SerializeField] private GameObject m_OddEvenDropdownRoot;
        [SerializeField] private GameObject m_FirstAnnihilatedDropdownRoot;
        [SerializeField] private Button[] m_RedSurvivingSlotButtons = new Button[6];
        [SerializeField] private TMP_Text[] m_RedSurvivingSlotTexts = new TMP_Text[6];
        [SerializeField] private Button[] m_BlueSurvivingSlotButtons = new Button[6];
        [SerializeField] private TMP_Text[] m_BlueSurvivingSlotTexts = new TMP_Text[6];
        [SerializeField] private TMP_Text m_NewRoundText;
        [SerializeField] private TMP_Text m_NewCurrentCallText;
        [SerializeField] private TMP_Text m_NewMultiplierText;

        private readonly HashSet<int> m_SelectedSurvivingSlots = new HashSet<int>();
        private AwaitableCompletionSource m_PhaseCompletionSource;
        private RoundBetTicket m_DraftTicket;
        
        // 아이템 상태 트래킹
        private bool m_UsedAdditionalBetTicket = false;
        private bool m_UsedInsurance = false;
        private bool m_UsedRerollTicket = false;
        private SpecialBetType? m_OverriddenSpecialBet = null;
        private bool m_HasRerolledThisRound = false;

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
                return m_UsedAdditionalBetTicket; 
            } 
        }

        public bool UsedInsurance 
        { 
            get 
            { 
                return m_UsedInsurance; 
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
            m_UsedAdditionalBetTicket = false;
            m_UsedInsurance = false;
            m_UsedRerollTicket = false;
            m_OverriddenSpecialBet = null;
            m_HasRerolledThisRound = false;

            Context.AssignUnitsForBetting();
            PrewarmConfirmedPools(Context.TeamAUnitDatas, Context.TeamBUnitDatas);
            m_DraftTicket = new RoundBetTicket();
            m_DraftTicket.SetWager(Mathf.Max(1, Context.CurrentCall));
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
            SetTeamUnitInfo(m_TeamAUnitInfoText, Context.TeamADeployments);
            SetTeamUnitInfo(m_TeamBUnitInfoText, Context.TeamBDeployments);

            if (m_WagerSlider != null)
            {
                m_WagerSlider.wholeNumbers = true;
                m_WagerSlider.minValue = 1f;
                m_WagerSlider.maxValue = Mathf.Max(1, Context.CurrentCall);
                m_WagerSlider.value = Mathf.Max(1, Context.CurrentCall);
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
            if (m_WagerInput != null)
            {
                m_WagerInput.contentType = TMP_InputField.ContentType.IntegerNumber;
                m_WagerInput.SetTextWithoutNotify(m_DraftTicket.WagerCall.ToString());
            }

            SetOptions(m_WinningTeamDropdown, "Select winning team", "Red", "Blue", "Draw");
            SetOptions(m_GameEndTimeDropdown, "Select game end time", "0-5 sec", "5-10 sec", "10-15 sec", "15-20 sec", "20+ sec");
            SetOptions(m_OddEvenDropdown, "Select odd / even", "Odd", "Even");
            SetOptions(m_FirstAnnihilatedDropdown, "Select first annihilated", "Slot 1", "Slot 2", "Slot 3", "Slot 4", "Slot 5", "Slot 6");
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

        private static void SetTeamUnitInfo(TMP_Text infoText, List<TeamUnitDeployment> deployments)
        {
            if (infoText == null) return;
            if (deployments == null || deployments.Count == 0)
            {
                infoText.text = "No units deployed";
                return;
            }

            var lines = new StringBuilder();
            for (int i = 0; i < deployments.Count; i++)
            {
                TeamUnitDeployment deployment = deployments[i];
                int col = deployment.CellIndex % 2;
                int row = deployment.CellIndex / 2;
                lines.Append($"({col},{row}) {DescribeUnits(deployment.Units)}");
                if (i < deployments.Count - 1) lines.Append('\n');
            }
            infoText.text = lines.ToString();
        }

        private static string DescribeUnits(List<UnitData> units)
        {
            if (units == null || units.Count == 0) return "Empty";
            UnitData first = units[0];
            bool sameType = first != null;
            for (int i = 1; i < units.Count; i++)
            {
                if (units[i] != first)
                {
                    sameType = false;
                    break;
                }
            }
            return sameType
                ? $"{first.UnitName} x{units.Count}"
                : string.Join(", ", units.ConvertAll(unit => unit != null ? unit.UnitName : "Unknown"));
        }

        private void SubscribeEvents()
        {
            if (m_WagerSlider != null) m_WagerSlider.onValueChanged.AddListener(OnWagerChanged);
            if (m_WagerInput != null) m_WagerInput.onEndEdit.AddListener(OnWagerInputEnded);
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
            if (m_WagerInput != null) m_WagerInput.onEndEdit.RemoveListener(OnWagerInputEnded);
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
            m_DraftTicket.SetWager(Mathf.Clamp(Mathf.RoundToInt(value), 1, Mathf.Max(1, Context.CurrentCall)));
            RefreshBetSummary();
        }

        private void OnWagerInputEnded(string value)
        {
            if (!int.TryParse(value, out int wager)) wager = 1;
            m_DraftTicket.SetWager(Mathf.Clamp(wager, 1, Mathf.Max(1, Context.CurrentCall)));
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
            SetUnitSlotTexts(m_RedSurvivingSlotTexts, Context.TeamADeployments);
            SetUnitSlotTexts(m_BlueSurvivingSlotTexts, Context.TeamBDeployments);
        }

        private static void SetUnitSlotTexts(TMP_Text[] texts, List<TeamUnitDeployment> deployments)
        {
            for (int i = 0; i < texts.Length; i++)
            {
                if (texts[i] == null) continue;
                TeamUnitDeployment deployment = deployments.Find(item => item.CellIndex == i);
                texts[i].text = deployment == null ? "-" : DescribeUnits(deployment.Units);
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
            int wager = Mathf.Clamp(m_DraftTicket.WagerCall, 1, Mathf.Max(1, Context.CurrentCall));
            if (m_CurrentCallText != null) m_CurrentCallText.text = $"{Context.CurrentCall} Call";
            if (m_NewCurrentCallText != null) m_NewCurrentCallText.text = $"{Context.CurrentCall} Call";
            if (m_WagerCallText != null) m_WagerCallText.text = $"{wager} Call";
            if (m_WagerInput != null) m_WagerInput.SetTextWithoutNotify(wager.ToString());
            if (m_NewMultiplierText != null) m_NewMultiplierText.text = $"x{m_DraftTicket.Multiplier}";
            if (m_MultiplierText != null) m_MultiplierText.text = $"×{m_DraftTicket.Multiplier}";
            if (m_EstimatedPayoutText != null)
            {
                m_EstimatedPayoutText.text = $"{wager * m_DraftTicket.Multiplier} Call";
            }

            bool valid = m_DraftTicket.Validate(Context.CurrentStageData, Context, Context.CurrentCall, out string error);
            if (m_ConfirmBetButton != null) m_ConfirmBetButton.interactable = valid;
            if (m_ValidationText != null) m_ValidationText.text = valid ? string.Empty : error;
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

        // 아이템 사용
        public bool UseBettingItem(ItemData itemData, out string message, out int remainingCount)
        {
            remainingCount = 0;
            message = "";

            if (itemData == null)
            {
                message = "유효하지 않은 아이템입니다.";
                return false;
            }

            var inventoryService = SaveManager.Instance?.InventoryService;
            var playerState = StageManager.Instance?.PlayerState;
            
            if (inventoryService == null || playerState == null)
            {
                message = "아이템 시스템을 불러올 수 없습니다.";
                return false;
            }

            if (inventoryService.GetStageItemCount(itemData, playerState) <= 0)
            {
                message = "보유한 아이템이 없습니다.";
                return false;
            }

            bool success = false;

            if (itemData.ItemType == ItemType.AdditionalBetTicket)
            {
                if (m_UsedAdditionalBetTicket)
                {
                    message = "추가 배팅권은 라운드당 1회만 사용 가능합니다.";
                }
                else
                {
                    m_UsedAdditionalBetTicket = true;
                    success = true;
                    message = "추가 배팅권(+500 Call)을 사용했습니다.";
                }
            }
            else if (itemData.ItemType == ItemType.Insurance)
            {
                if (m_UsedInsurance)
                {
                    message = "보험은 라운드당 1회만 사용 가능합니다.";
                }
                else
                {
                    m_UsedInsurance = true;
                    success = true;
                    message = "패배 시 원금이 반환되는 보험을 사용했습니다.";
                }
            }
            else if (itemData.ItemType == ItemType.RerollTicket)
            {
                if (m_UsedRerollTicket || m_HasRerolledThisRound)
                {
                    message = "리롤권은 라운드당 1회만 사용 가능합니다.";
                }
                else
                {
                    var specialBets = Context.CurrentStageData.SpecialBetTypes;
                    if (specialBets != null && specialBets.Count > 0)
                    {
                        int randomIndex = UnityEngine.Random.Range(0, specialBets.Count);
                        m_OverriddenSpecialBet = specialBets[randomIndex];
                        m_HasRerolledThisRound = true;
                        m_UsedRerollTicket = true;
                        Context.SetActiveSpecialBets(new[] { m_OverriddenSpecialBet.Value });
                        RefreshSpecialBetAvailability();
                        UpdateSurvivingSlotAvailability();
                        RefreshBetSummary();

                        success = true;
                        message = $"특수 배팅이 {m_OverriddenSpecialBet}로 변경되었습니다.";
                    }
                    else
                    {
                        message = "변경할 특수 베팅 룰이 없습니다.";
                    }
                }
            }

            if (success)
            {
                inventoryService.TryUseItemFromStage(itemData, playerState);
            }

            remainingCount = inventoryService.GetStageItemCount(itemData, playerState);
            return success;
        }

        private void OnConfirmBetClicked()
        {
            if (IsPhaseCompleted) return;

            m_DraftTicket.SetItemUsages(m_UsedAdditionalBetTicket, m_UsedInsurance);
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
