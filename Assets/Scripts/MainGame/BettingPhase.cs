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
    /// <summary>
    /// 라운드 편성을 확정하고 단일 복합 베팅 티켓을 입력받는 페이즈입니다.
    /// </summary>
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

        private readonly HashSet<int> m_SelectedSurvivingSlots = new HashSet<int>();
        private AwaitableCompletionSource m_PhaseCompletionSource;
        private RoundBetTicket m_DraftTicket;

        public override async Awaitable EnterPhaseAsync(CancellationToken token)
        {
            var cameraController = InTheArena.Camera.CameraController.Instance;
            if (cameraController != null)
                await cameraController.SetPhaseAsync(
                    InTheArena.Camera.CameraPhase.Betting,
                    token);

            InitializePhaseData();
            SetupUI();
            SubscribeEvents();

            CanvasGroup canvasGroup = m_BettingCanvasGroup;
            if (canvasGroup != null)
            {
                canvasGroup.gameObject.SetActive(true);
                canvasGroup.alpha = 0f;
                await AwaitTweenAsync(canvasGroup.DOFade(1f, 0.3f).SetEase(Ease.OutQuad), token);
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
                manager.Projectiles.Prewarm(pair.Key, Mathf.Clamp(pair.Value * 2, 16, 128));
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

        private static void CountProjectiles(
            Dictionary<GameObject, int> counts,
            List<GameObject> projectilePrefabs,
            UnitData unitData,
            int unitCount)
        {
            IReadOnlyList<SkillData> skills = unitData?.SkillDatas;
            if (skills == null) return;

            for (int i = 0; i < skills.Count; i++)
            {
                SkillData skill = skills[i];
                if (skill == null) continue;
                projectilePrefabs.Clear();
                skill.CollectProjectilePrefabs(projectilePrefabs);
                for (int j = 0; j < projectilePrefabs.Count; j++)
                {
                    GameObject prefab = projectilePrefabs[j];
                    counts.TryGetValue(prefab, out int current);
                    counts[prefab] = current + unitCount;
                }
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

            StageData stageData = Context.CurrentStageData;
            SetActive(m_FactionBetRoot, stageData != null && stageData.EnableFactionBet);
            SetActive(m_RemainingTimeRoot, HasSpecial(SpecialBetType.RemainingTime));
            SetActive(m_SurvivingSlotsRoot, HasSpecial(SpecialBetType.SurvivingSlots));
            SetActive(m_OddEvenRoot, HasSpecial(SpecialBetType.OddEven));
            SetActive(m_FirstEliminatedSlotRoot, HasSpecial(SpecialBetType.FirstEliminatedSlot));
            UpdateSurvivingSlotAvailability();
            RefreshBetSummary();
        }

        private bool HasSpecial(SpecialBetType type)
        {
            return Context.CurrentStageData != null && Context.CurrentStageData.HasSpecialBet(type);
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
            if (m_ConfirmBetButton != null) m_ConfirmBetButton.onClick.AddListener(OnConfirmBetClicked);
        }

        private static void AddClick(Button button, UnityEngine.Events.UnityAction action)
        {
            if (button != null) button.onClick.AddListener(action);
        }

        private void UnsubscribeEvents()
        {
            if (m_WagerSlider != null) m_WagerSlider.onValueChanged.RemoveAllListeners();
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
            m_DraftTicket.SetWager(Mathf.Clamp(Mathf.RoundToInt(value), 1, Mathf.Max(1, Context.CurrentCall)));
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
            if (m_WagerCallText != null) m_WagerCallText.text = $"{wager} Call";
            if (m_MultiplierText != null) m_MultiplierText.text = $"×{m_DraftTicket.Multiplier}";
            if (m_EstimatedPayoutText != null)
            {
                m_EstimatedPayoutText.text = $"{wager * m_DraftTicket.Multiplier} Call";
            }

            bool valid = m_DraftTicket.Validate(Context.CurrentStageData, Context.CurrentCall, out string error);
            if (m_ConfirmBetButton != null) m_ConfirmBetButton.interactable = valid;
            if (m_ValidationText != null) m_ValidationText.text = valid ? string.Empty : error;
        }

        private void RefreshSelectionVisuals()
        {
            SetSelected(m_RedButton, m_DraftTicket.Faction == FactionPrediction.Red);
            SetSelected(m_BlueButton, m_DraftTicket.Faction == FactionPrediction.Blue);
            SetSelected(m_DrawButton, m_DraftTicket.Faction == FactionPrediction.Draw);

            for (int i = 0; i < m_RemainingTimeButtons.Length; i++)
                SetSelected(m_RemainingTimeButtons[i], m_DraftTicket.RemainingTime == (RemainingTimePrediction)i);
            for (int i = 0; i < m_OddEvenButtons.Length; i++)
                SetSelected(m_OddEvenButtons[i], m_DraftTicket.OddEven == (OddEvenPrediction)i);
            for (int i = 0; i < m_FirstEliminatedSlotButtons.Length; i++)
                SetSelected(m_FirstEliminatedSlotButtons[i], m_DraftTicket.FirstEliminatedSlot == i + 1);
            for (int i = 0; i < m_SurvivingSlotButtons.Length; i++)
                SetSelected(m_SurvivingSlotButtons[i], m_SelectedSurvivingSlots.Contains(i + 1));
        }

        private static void SetSelected(Button button, bool selected)
        {
            if (button == null || button.image == null) return;
            button.image.color = selected
                ? new Color(0.95f, 0.72f, 0.18f)
                : new Color(0.2f, 0.24f, 0.3f);
        }

        private void OnConfirmBetClicked()
        {
            if (IsPhaseCompleted) return;
            if (!Context.StageSession.TryPlaceBet(m_DraftTicket, out string error))
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
