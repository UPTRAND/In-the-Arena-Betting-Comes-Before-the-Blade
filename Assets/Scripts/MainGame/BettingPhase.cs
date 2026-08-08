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

        private static readonly Color AgreeTextColor = Color.black;
        private static readonly Color AgreeWarningColor = new Color(0.783f, 0.084f, 0.070f, 1f);
        private AwaitableCompletionSource m_PhaseCompletionSource;
        private RoundBetTicket m_DraftTicket;

        // 아이템 상태 트래킹

        private void Awake()
        {
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
            Transform root = (m_SurvivingRowDropdownRoot != null
                ? m_SurvivingRowDropdownRoot
                : m_SurvivingSlotsRoot)?.transform;
            if (root == null || m_SurvivingRowDropdown != null) return;

            m_SurvivingRowDropdown = root.GetComponentInChildren<TMP_Dropdown>(true);
            if (m_SurvivingRowDropdown != null || m_FirstAnnihilatedDropdown == null) return;

            GameObject clone = Instantiate(m_FirstAnnihilatedDropdown.gameObject, root);
            clone.name = "SurvivingRow_Dropdown";
            m_SurvivingRowDropdown = clone.GetComponent<TMP_Dropdown>();

            Transform guide = root.Find("Guide_Text");
            if (guide != null) guide.gameObject.SetActive(false);
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

            bool valid = m_DraftTicket.Validate(Context.CurrentStageData, Context, Context.CurrentCall, out _);
            if (m_ConfirmBetButton != null) m_ConfirmBetButton.interactable = valid;
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
            return true;
        }

        internal void RestorePurchasedItemState(IReadOnlyList<SpecialBetType> previousSpecialBetOrder)
        {
            Context?.RestoreSpecialBetOrder(previousSpecialBetOrder);
            RefreshSpecialBetAvailability();
            UpdateSurvivingSlotAvailability();
            RefreshBetSummary();
        }

        internal IReadOnlyList<SpecialBetType> GetSpecialBetOrderForItemUse()
        {
            return Context?.SpecialBetOrder;
        }

        private void OnConfirmBetClicked()
        {
            if (IsPhaseCompleted) return;

            m_DraftTicket.SetItemUsages(
                Context.RoundItemUsage.HasUsed(ItemType.AdditionalBetTicket),
                Context.RoundItemUsage.HasUsed(ItemType.Insurance));
            if (!Context.StageSession.TryPlaceBet(m_DraftTicket, Context, out string error))
            {
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
            if (canvasGroup != null && canvasGroup.gameObject != null)
            {
                await AwaitTweenAsync(canvasGroup.DOFade(0f, 0.3f).SetEase(Ease.InQuad), token);
                if (canvasGroup != null && canvasGroup.gameObject != null)
                    canvasGroup.gameObject.SetActive(false);
            }
            if (this != null && transform != null)
                transform.DOKill();
        }

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
