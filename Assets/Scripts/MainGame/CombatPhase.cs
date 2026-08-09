#if UNITY_6000_0_OR_NEWER
using System;
using System.Collections.Generic;
using System.Threading;
using UnityEngine;
using DG.Tweening;
using InTheArena.Unit;
using InTheArena.UI;
using InTheArena.Battlefield;
using UnitType = InTheArena.Unit.Unit;

namespace InTheArena.MainGame
{
    /// <summary>
    /// 전투 페이즈
    /// 양 팀 유닛이 자동 전투를 진행하는 단계
    /// </summary>
    [DisallowMultipleComponent]
    public class CombatPhase : RoundPhaseBase
    {
        private const int BattlefieldColumnCount = 7;
        private const int BattlefieldRowCount = 3;
        private const int TeamGridColumnCount = 2;
        private const int TeamGridCellCount = TeamGridColumnCount * BattlefieldRowCount;
        private const float GridCellSize = 2f;
        private const float BattlefieldMinX = -(BattlefieldColumnCount * GridCellSize) * 0.5f;
        private const float BattlefieldMinZ = -(BattlefieldRowCount * GridCellSize) * 0.5f;
        private const float ItemCastingTimeScale = 0.25f;
        private const float MeteorEffectRadius = 2f;
        private const float MercenaryFormationHorizontalOffset = 0.5f;
        private const float MercenaryFormationDepthOffset = 0.5f;

        [Header("Combat Settings")]
        [SerializeField] [Min(1f)] private float m_CombatTimeout = 30f;
        [SerializeField] private Transform m_TeamASpawnRoot;
        [SerializeField] private Transform m_TeamBSpawnRoot;

        [Header("Speed Control")]
        [SerializeField] private float m_NormalSpeed = 1f;
        [SerializeField] private float m_FastSpeed = 2f;
        [SerializeField] private UI_BattlePhaseHUD m_CombatHud;
        private float m_CurrentSpeed = 1f;

        [Header("Final Elimination")]
        [SerializeField] [Range(0.01f, 1f)] private float m_FinalEliminationTimeScale = 0.25f;

        [Header("Item Effect References")]
        [SerializeField] private UnitData m_MercenaryKnightData;
        [SerializeField] private UnitData m_MercenaryArcherData;
        [SerializeField] private UnitData m_MercenaryWizardData;
        [SerializeField] private InTheArena.Unit.StatusEffectData m_MeteorStunEffect;
        [SerializeField] [Min(0f)] private float m_FinalEliminationDuration = 1.5f;

        private CancellationTokenSource m_CombatCts;
        private bool m_IsCombatEnded = false;
        private float m_RemainingCombatTime;
        private int m_InitialRedUnitCount;
        private int m_InitialBlueUnitCount;
        private int m_RedParticipantCount;
        private int m_BlueParticipantCount;
        private readonly Dictionary<UnitType, UnitSlotKey> m_UnitSlots = new Dictionary<UnitType, UnitSlotKey>();
        private readonly Dictionary<UnitType, Action<UnitType>> m_DeathHandlers = new Dictionary<UnitType, Action<UnitType>>();
        private readonly List<UnitType> m_FinalDeathPresentationUnits = new List<UnitType>(2);
        private FirstEliminatedColumnPrediction? m_FirstEliminatedColumn;
        private bool m_IsFinalEliminationPlaying;
        private bool m_IsItemCastingSlowMotion;

        public override async Awaitable PreparePhaseAsync(CancellationToken token)
        {
            InitializeCombat();

            m_CombatCts = CancellationTokenSource.CreateLinkedTokenSource(token);

            await PrepareUnitsAsync(m_CombatCts.Token);
            if (m_CombatCts.IsCancellationRequested) return;

            m_InitialRedUnitCount = RedAliveCount;
            m_InitialBlueUnitCount = BlueAliveCount;
            m_RedParticipantCount = m_InitialRedUnitCount;
            m_BlueParticipantCount = m_InitialBlueUnitCount;
            m_CombatHud?.BindAndShow(
                this,
                Context,
                StageManager.Instance?.PlayerState);
            OnCombatStateChanged?.Invoke();
        }

        public override async Awaitable EnterPhaseAsync(CancellationToken token)
        {
            StartUnitAI();
            await RunCombatLoopAsync(m_CombatCts.Token);
        }

        private void InitializeCombat()
        {
            IsPhaseCompleted = false;
            m_IsCombatEnded = false;
            m_RemainingCombatTime = m_CombatTimeout;
            m_InitialRedUnitCount = 0;
            m_InitialBlueUnitCount = 0;
            m_RedParticipantCount = 0;
            m_BlueParticipantCount = 0;
            m_CurrentSpeed = m_NormalSpeed;
            Time.timeScale = m_CurrentSpeed;
            Context.CombatWinner = Team.None;
            Context.IsRoundCompleted = false;
            Context.CombatResult = null;
            m_UnitSlots.Clear();
            m_DeathHandlers.Clear();
            m_FinalDeathPresentationUnits.Clear();
            m_FirstEliminatedColumn = null;
            m_IsFinalEliminationPlaying = false;
            m_IsItemCastingSlowMotion = false;

            // 컨텍스트에서 유닛 데이터로 런타임 유닛 생성
            CreateRuntimeUnitsFromData();
        }

        private void CreateRuntimeUnitsFromData()
        {
            Context.TeamAUnits.Clear();
            Context.TeamBUnits.Clear();

            // BettingPhase에서 확정한 셀별 편성을 그대로 사용한다.
            SpawnBattleConfig(BuildBattleConfig());

            Debug.Log($"[CombatPhase] 런타임 유닛 생성 완료 - Red: {Context.TeamAUnits.Count}, Blue: {Context.TeamBUnits.Count}");
        }

        private BattleConfig BuildBattleConfig()
        {
            var plans = new List<SpawnPlan>(UnitSpatialIndex.MaxUnits);
            AddTeamPlans(Context.TeamADeployments, Team.Red, plans);
            AddTeamPlans(Context.TeamBDeployments, Team.Blue, plans);
            return new BattleConfig(plans.ToArray(), Context.CurrentRoundRule);
        }

        private void AddTeamPlans(
            List<TeamUnitDeployment> deployments,
            Team team,
            List<SpawnPlan> plans)
        {
            if (deployments == null) return;
            for (int deploymentIndex = 0; deploymentIndex < deployments.Count; deploymentIndex++)
            {
                TeamUnitDeployment deployment = deployments[deploymentIndex];
                if (deployment?.Units == null) continue;

                Vector3 center = GetGridCellCenterPosition(team, deployment.CellIndex);
                for (int unitIndex = 0; unitIndex < deployment.Units.Count; unitIndex++)
                {
                    UnitData unitData = deployment.Units[unitIndex];
                    if (unitData == null) continue;
                    Vector3 position = center + new Vector3(
                        UnityEngine.Random.Range(-0.3f, 0.3f),
                        0f,
                        UnityEngine.Random.Range(-0.3f, 0.3f));
                    plans.Add(new SpawnPlan(
                        unitData,
                        team,
                        deployment.CellIndex,
                        unitIndex + 1,
                        position));
                }
            }
        }

        private void SpawnBattleConfig(BattleConfig config)
        {
            ReadOnlySpan<SpawnPlan> plans = config.SpawnPlans;
            for (int i = 0; i < plans.Length; i++)
            {
                SpawnPlan plan = plans[i];
                Transform root = plan.Team == Team.Red ? m_TeamASpawnRoot : m_TeamBSpawnRoot;
                List<UnitType> runtimeUnits =
                    plan.Team == Team.Red ? Context.TeamAUnits : Context.TeamBUnits;
                UnitType unit = PoolManager.Require().Units.Spawn(
                    plan.UnitData,
                    root,
                    (int)plan.Team,
                    plan.Position,
                    false);
                if (unit == null) continue;

                unit.AssignCombatLogNumber(plan.UnitNumber);
                if (unit.AI == null)
                    Debug.LogError($"[CombatPhase] {plan.UnitData.UnitName} has no runtime AI.");

                unit.SetAIActive(false);
                runtimeUnits.Add(unit);
                RegisterUnitSlot(unit, plan.Team, plan.CellIndex);
            }
        }

        private void RegisterUnitSlot(UnitType unit, Team team, int cellIndex)
        {
            var key = new UnitSlotKey(team, cellIndex);
            m_UnitSlots[unit] = key;
            Action<UnitType> handler = _ => OnUnitDied(unit, key);
            m_DeathHandlers[unit] = handler;
            unit.OnDied += handler;
        }

        private void OnUnitDied(UnitType deadUnit, UnitSlotKey key)
        {
            if (deadUnit == null) return;

            Team team = key.Team;
            Debug.Log($"[Phase1C Test] 유닛 사망. Team: {team}, RuntimeListCount: {(team == Team.Red ? Context.TeamAUnits.Count : Context.TeamBUnits.Count)}, RedAlive: {RedAliveCount}/{m_RedParticipantCount}, BlueAlive: {BlueAliveCount}/{m_BlueParticipantCount}");

            if (!m_FirstEliminatedColumn.HasValue)
            {
                bool columnHasLivingUnit = false;
                foreach (var pair in m_UnitSlots)
                {
                    if (pair.Value.Team == key.Team &&
                        pair.Value.CellIndex % TeamGridColumnCount == key.CellIndex % TeamGridColumnCount &&
                        pair.Key != null &&
                        !pair.Key.IsDead)
                    {
                        columnHasLivingUnit = true;
                        break;
                    }
                }

                if (!columnHasLivingUnit)
                    m_FirstEliminatedColumn = GetColumnPrediction(key.Team, key.CellIndex);
            }

            bool teamEliminated = key.Team == Team.Red
                ? UnitRegistry.RedAliveCount == 0
                : UnitRegistry.BlueAliveCount == 0;
            OnCombatStateChanged?.Invoke();
            if (!teamEliminated || m_FinalDeathPresentationUnits.Contains(deadUnit)) return;

            deadUnit.HoldDeathPresentation();
            m_FinalDeathPresentationUnits.Add(deadUnit);
        }

        internal static FirstEliminatedColumnPrediction GetColumnPrediction(Team team, int cellIndex)
        {
            bool isLeftColumn = cellIndex % TeamGridColumnCount == 0;
            return team == Team.Red
                ? isLeftColumn ? FirstEliminatedColumnPrediction.RedBack : FirstEliminatedColumnPrediction.RedFront
                : isLeftColumn ? FirstEliminatedColumnPrediction.BlueFront : FirstEliminatedColumnPrediction.BlueBack;
        }

        internal static SurvivingRowPrediction GetRowPrediction(Team team, int cellIndex)
        {
            int row = cellIndex / TeamGridColumnCount;
            return (SurvivingRowPrediction)(row + (team == Team.Red ? 0 : 3));
        }

        private RoundData GetCurrentRoundData()
        {
            if (Context == null || Context.CurrentStageData == null || Context.CurrentRound <= 0) return null;

            int roundIndex = Context.CurrentRound - 1;
            if (roundIndex < 0 || roundIndex >= Context.CurrentStageData.RoundDatas.Count) return null;

            return Context.CurrentStageData.RoundDatas[roundIndex];
        }

        private Vector3 GetGridCellCenterPosition(Team team, int cellIndex)
        {
            // 전체 전장은 7열 x 3행이다.
            // Red는 왼쪽 2열, Blue는 오른쪽 2열을 사용하고 중앙 3열은 중립 전투 영역이다.
            // cellIndex: 0~5 (row * 2 + col)
            int col = cellIndex % TeamGridColumnCount;
            int row = cellIndex / TeamGridColumnCount;

            int battlefieldCol = team == Team.Red
                ? col
                : BattlefieldColumnCount - TeamGridColumnCount + col;

            float centerX = BattlefieldMinX + (battlefieldCol + 0.5f) * GridCellSize;
            float centerZ = BattlefieldMinZ + (row + 0.5f) * GridCellSize;

            return new Vector3(centerX, 0f, centerZ);
        }

        private async Awaitable PrepareUnitsAsync(CancellationToken token)
        {
            // 풀에서 준비된 유닛은 한 프레임에 활성화합니다.
            foreach (var unit in Context.TeamAUnits)
            {
                if (token.IsCancellationRequested) break;
                if (unit != null) unit.gameObject.SetActive(true);
            }
            foreach (var unit in Context.TeamBUnits)
            {
                if (token.IsCancellationRequested) break;
                if (unit != null) unit.gameObject.SetActive(true);
            }
            await Awaitable.NextFrameAsync();
            if (token.IsCancellationRequested) return;

            var cameraController = InTheArena.Camera.CameraController.Instance;
            if (cameraController != null)
                await cameraController.SetPhaseAsync(
                    InTheArena.Camera.CameraPhase.Combat,
                    token);
            if (token.IsCancellationRequested) return;
        }

        private void StartUnitAI()
        {
            // 모든 유닛이 배치되고 화면이 밝아진 뒤 동시에 AI 전투를 시작한다.
            foreach (var unit in Context.TeamAUnits)
            {
                if (unit == null || unit.IsDead) continue;
                unit.NotifyBattleStarted();
                unit.SetAIActive(true);
            }
            foreach (var unit in Context.TeamBUnits)
            {
                if (unit == null || unit.IsDead) continue;
                unit.NotifyBattleStarted();
                unit.SetAIActive(true);
            }
        }

        private async Awaitable RunCombatLoopAsync(CancellationToken token)
        {
            Debug.Log($"[CombatPhase] 전투 시작 - 제한 시간: {m_CombatTimeout:0.##}초");

            while (!m_IsCombatEnded && !token.IsCancellationRequested)
            {
                if (TryGetEliminationWinner(out Team eliminationWinner))
                {
                    await CompleteEliminationAsync(eliminationWinner, token);
                    break;
                }

                if (m_RemainingCombatTime <= 0f)
                {
                    Debug.Log("[CombatPhase] 전투 타임아웃 - 무승부 처리");
                    CompleteCombat(Team.None);
                    break;
                }

                await Awaitable.NextFrameAsync();
                if (token.IsCancellationRequested) break;

                // Unit.Update에서 각 유닛의 런타임 AI가 행동한다.
                // Time.deltaTime은 timeScale이 반영된 전투 시간이다.
                m_RemainingCombatTime = Mathf.Max(0f, m_RemainingCombatTime - Time.deltaTime);
            }
        }

        private bool TryGetEliminationWinner(out Team winner)
        {
            bool redAlive = HasLivingUnit(Context.TeamAUnits);
            bool blueAlive = HasLivingUnit(Context.TeamBUnits);

            if (!redAlive && !blueAlive)
            {
                winner = Team.None;
                return true;
            }
            if (!redAlive)
            {
                winner = Team.Blue;
                return true;
            }
            if (!blueAlive)
            {
                winner = Team.Red;
                return true;
            }

            winner = Team.None;
            return false;
        }

        private static bool HasLivingUnit(List<UnitType> units)
        {
            if (units == null) return false;
            for (int i = 0; i < units.Count; i++)
            {
                UnitType unit = units[i];
                if (unit != null && !unit.IsDead) return true;
            }
            return false;
        }

        private static int CountLivingUnits(List<UnitType> units)
        {
            if (units == null) return 0;

            int count = 0;
            for (int i = 0; i < units.Count; i++)
            {
                UnitType unit = units[i];
                if (unit != null && !unit.IsDead) count++;
            }
            return count;
        }

        private void CompleteCombat(Team winner)
        {
            if (m_IsCombatEnded) return;

            FreezeCombatOutcome(winner);
            m_IsCombatEnded = true;
            m_IsItemCastingSlowMotion = false;
            Time.timeScale = 1f;
            InTheArena.Camera.CameraController.Instance?.SetPhase(
                InTheArena.Camera.CameraPhase.Result);
            CompletePhase();
        }

        private async Awaitable CompleteEliminationAsync(Team winner, CancellationToken token)
        {
            if (m_IsCombatEnded) return;

            FreezeCombatOutcome(winner);
            Context.CombatWinner = winner;
            m_IsFinalEliminationPlaying = true;
            float cinematicStartedAt = Time.unscaledTime;
            var cameraController = InTheArena.Camera.CameraController.Instance;

            try
            {
                m_IsItemCastingSlowMotion = false;
                Time.timeScale = m_FinalEliminationTimeScale;

                if (winner == Team.None)
                {
                    cameraController?.HoldCurrentPoseForFinalElimination();
                }
                else if (cameraController != null &&
                         TryBuildFinalDeathBounds(winner, out Bounds focusBounds))
                {
                    await cameraController.FocusFinalEliminationAsync(focusBounds, token);
                }
                else
                {
                    cameraController?.HoldCurrentPoseForFinalElimination();
                }

                float remainingDuration = m_FinalEliminationDuration -
                                          (Time.unscaledTime - cinematicStartedAt);
                await WaitForUnscaledSecondsAsync(remainingDuration, token);
            }
            finally
            {
                Time.timeScale = 1f;
                CompleteFinalDeathPresentations();
                if (cameraController != null)
                {
                    cameraController.EndFinalEliminationFocus();
                    if (!token.IsCancellationRequested)
                        cameraController.SetPhase(InTheArena.Camera.CameraPhase.Result);
                }
                m_IsFinalEliminationPlaying = false;
            }

            token.ThrowIfCancellationRequested();
            CompletePhase();
        }

        private void FreezeCombatOutcome(Team winner)
        {
            m_IsCombatEnded = true;
            Context.CombatWinner = winner;
            Context.IsRoundCompleted = true;
            Context.CombatResult = BuildCombatResult(winner);

            foreach (var unit in Context.TeamAUnits)
            {
                if (unit == null) continue;
                unit.NotifyBattleEnded();
                unit.SetAIActive(false);
            }
            foreach (var unit in Context.TeamBUnits)
            {
                if (unit == null) continue;
                unit.NotifyBattleEnded();
                unit.SetAIActive(false);
            }

            string result = winner == Team.None ? "Draw" : $"{winner} Win";
            Debug.Log($"[CombatPhase] 전투 종료 - {result}, 남은 시간: {m_RemainingCombatTime:0.00}초");
        }

        private bool TryBuildFinalDeathBounds(Team winner, out Bounds bounds)
        {
            bounds = default;
            bool initialized = false;
            int losingTeam = winner == Team.Red ? (int)Team.Blue : (int)Team.Red;

            for (int i = 0; i < m_FinalDeathPresentationUnits.Count; i++)
            {
                UnitType unit = m_FinalDeathPresentationUnits[i];
                if (unit == null || unit.Team != losingTeam || !unit.gameObject.activeSelf) continue;

                Bounds unitBounds = new Bounds(unit.GroundPosition, Vector3.one * 0.2f);
                unitBounds.Encapsulate(unit.HitPosition);
                if (!initialized)
                {
                    bounds = unitBounds;
                    initialized = true;
                }
                else
                {
                    bounds.Encapsulate(unitBounds);
                }
            }

            return initialized;
        }

        private static async Awaitable WaitForUnscaledSecondsAsync(
            float duration,
            CancellationToken token)
        {
            float remaining = Mathf.Max(0f, duration);
            while (remaining > 0f)
            {
                token.ThrowIfCancellationRequested();
                await Awaitable.NextFrameAsync();
                remaining -= Time.unscaledDeltaTime;
            }
        }

        private void CompleteFinalDeathPresentations()
        {
            for (int i = 0; i < m_FinalDeathPresentationUnits.Count; i++)
            {
                UnitType unit = m_FinalDeathPresentationUnits[i];
                if (unit != null) unit.CompleteDeathPresentation();
            }
            m_FinalDeathPresentationUnits.Clear();
        }

        private CombatResultSnapshot BuildCombatResult(Team winner)
        {
            int redAlive = RedAliveCount;
            int blueAlive = BlueAliveCount;
            var survivingRows = new HashSet<SurvivingRowPrediction>();

            foreach (var pair in m_UnitSlots)
            {
                UnitType unit = pair.Key;
                if (unit == null || unit.IsDead) continue;

                survivingRows.Add(GetRowPrediction(pair.Value.Team, pair.Value.CellIndex));
            }

            return new CombatResultSnapshot(
                winner,
                m_RemainingCombatTime,
                redAlive,
                blueAlive,
                survivingRows,
                m_FirstEliminatedColumn);
        }

        /// <summary>
        /// 전투 속도 토글 (2배속)
        /// </summary>
        public void ToggleCombatSpeed()
        {
            if (m_IsFinalEliminationPlaying ||
                m_IsCombatEnded ||
                m_IsItemCastingSlowMotion)
            {
                return;
            }

            m_CurrentSpeed = Mathf.Approximately(m_CurrentSpeed, 1f) ? 2f
                : Mathf.Approximately(m_CurrentSpeed, 2f) ? 3f
                : Mathf.Approximately(m_CurrentSpeed, 3f) ? 0.5f
                : 1f;
            Time.timeScale = m_CurrentSpeed;
            InTheArena.Camera.CameraController.Instance?.SetSpeedBoost(m_CurrentSpeed > m_NormalSpeed);
            Debug.Log($"[CombatPhase] 전투 속도 변경: {m_CurrentSpeed}x");
        }

        /// <summary>
        /// 현재 전투 속도 반환
        /// </summary>
        public float CurrentSpeed => m_CurrentSpeed;
        public float DisplaySpeed => m_IsItemCastingSlowMotion
            ? ItemCastingTimeScale
            : m_CurrentSpeed;
        public bool IsItemCastingSlowMotion => m_IsItemCastingSlowMotion;
        public bool IsFinalEliminationPlaying => m_IsFinalEliminationPlaying;
        public float RemainingCombatTime => m_RemainingCombatTime;
        public float CombatTimeout => m_CombatTimeout;
        public int RedAliveCount => CountLivingUnits(Context?.TeamAUnits);
        public int BlueAliveCount => CountLivingUnits(Context?.TeamBUnits);
        public int InitialRedUnitCount => m_InitialRedUnitCount;
        public int InitialBlueUnitCount => m_InitialBlueUnitCount;
        public int RedParticipantCount => m_RedParticipantCount;
        public int BlueParticipantCount => m_BlueParticipantCount;
        public bool IsCombatEnded => m_IsCombatEnded || IsPhaseCompleted;

        public bool BeginItemCastingSlowMotion()
        {
            if (m_IsItemCastingSlowMotion)
            {
                return true;
            }

            if (IsCombatEnded || m_IsFinalEliminationPlaying)
            {
                return false;
            }

            m_IsItemCastingSlowMotion = true;
            Time.timeScale = ItemCastingTimeScale;
            InTheArena.Camera.CameraController.Instance?.SetSpeedBoost(false);
            return true;
        }

        public void EndItemCastingSlowMotion()
        {
            if (!m_IsItemCastingSlowMotion)
            {
                return;
            }

            m_IsItemCastingSlowMotion = false;

            if (IsCombatEnded ||
                m_IsFinalEliminationPlaying ||
                IsPhaseCompleted)
            {
                Time.timeScale = 1f;
                return;
            }

            Time.timeScale = m_CurrentSpeed;
            InTheArena.Camera.CameraController.Instance?.SetSpeedBoost(
                m_CurrentSpeed > m_NormalSpeed);
        }

        public bool CanCommitGroundTargetItem()
        {
            return Context != null &&
                   !IsCombatEnded &&
                   !m_IsFinalEliminationPlaying &&
                   m_RemainingCombatTime > 0f &&
                   HasLivingUnit(Context.TeamAUnits) &&
                   HasLivingUnit(Context.TeamBUnits);
        }

        public float MeteorTargetRadius => MeteorEffectRadius;

        public float MercenaryFormationPadding =>
            MercenaryFormationHorizontalOffset + GetMercenaryVisualRadius();

        public Vector2 MercenaryFormationPreviewSize
        {
            get
            {
                float radius = GetMercenaryVisualRadius();
                return new Vector2(
                    MercenaryFormationHorizontalOffset * 2f + radius * 2f,
                    MercenaryFormationDepthOffset + radius * 2f);
            }
        }

        public int GetAliveCount(Team team, int cellIndex)
        {
            int count = 0;
            foreach (var pair in m_UnitSlots)
            {
                if (pair.Value.Team == team && pair.Value.CellIndex == cellIndex && pair.Key != null && !pair.Key.IsDead)
                    count++;
            }
            return count;
        }

        public Sprite GetSlotPortrait(Team team, int cellIndex)
        {
            foreach (var pair in m_UnitSlots)
            {
                if (pair.Value.Team == team && pair.Value.CellIndex == cellIndex && pair.Key?.UnitData != null)
                    return pair.Key.UnitData.GetPortrait(team);
            }
            return null;
        }

        public event Action OnCombatStateChanged;

        public override async Awaitable ExitPhaseAsync(CancellationToken token)
        {
            EndItemCastingSlowMotion();
            Time.timeScale = 1f;
            m_IsFinalEliminationPlaying = false;
            CompleteFinalDeathPresentations();
            InTheArena.Camera.CameraController.Instance?.EndFinalEliminationFocus();
            InTheArena.Camera.CameraController.Instance?.SetSpeedBoost(false);
            m_CombatHud?.UnbindAndHide();
            InTheArena.Unit.SkillVfxPresenter.ClearAllActive();

            if (m_CombatCts != null)
            {
                m_CombatCts.Cancel();
                m_CombatCts.Dispose();
                m_CombatCts = null;
            }

            // 유닛 정리
            CleanupUnits();
            transform.DOKill();
        }

        private void CleanupUnits()
        {
            UnsubscribeDeathEvents();
            foreach (var unit in Context.TeamAUnits)
            {
                if (unit != null && unit.gameObject != null)
                {
                    PoolManager.Require().Units.Return(unit);
                }
            }
            foreach (var unit in Context.TeamBUnits)
            {
                if (unit != null && unit.gameObject != null)
                {
                    PoolManager.Require().Units.Return(unit);
                }
            }

            Context.TeamAUnits.Clear();
            Context.TeamBUnits.Clear();
            m_UnitSlots.Clear();
        }

        private void UnsubscribeDeathEvents()
        {
            foreach (var pair in m_DeathHandlers)
            {
                if (pair.Key != null) pair.Key.OnDied -= pair.Value;
            }
            m_DeathHandlers.Clear();
        }

        public event System.Action<ItemData> OnItemUsed;

        internal bool TryApplyPurchasedItemEffect(ItemData itemData, out string message)
        {
            message = string.Empty;

            if (itemData == null)
            {
                message = "유효하지 않은 전투 아이템입니다.";
                return false;
            }

            if (itemData.ItemType != ItemType.TimeExtension)
            {
                message = "Phase 1-B에서 지원하는 전투 아이템이 아닙니다.";
                return false;
            }

            m_RemainingCombatTime += 5f;
            OnItemUsed?.Invoke(itemData);
            message = "전투 시간이 5초 연장되었습니다.";
            return true;
        }

        internal void RollbackPurchasedTimeExtension()
        {
            m_RemainingCombatTime = Mathf.Max(0f, m_RemainingCombatTime - 5f);
        }



        private void OnMercenaryDied(UnitType deadUnit)
        {
            if (deadUnit == null) return;

            Team team = (Team)deadUnit.Team;
            Debug.Log($"[Phase1C Test] 용병 사망. Team: {team}, RuntimeListCount: {(team == Team.Red ? Context.TeamAUnits.Count : Context.TeamBUnits.Count)}, RedAlive: {RedAliveCount}/{m_RedParticipantCount}, BlueAlive: {BlueAliveCount}/{m_BlueParticipantCount}");

            bool teamEliminated = deadUnit.Team == (int)Team.Red
                ? UnitRegistry.RedAliveCount == 0
                : UnitRegistry.BlueAliveCount == 0;

            if (!teamEliminated || m_FinalDeathPresentationUnits.Contains(deadUnit)) return;

            deadUnit.HoldDeathPresentation();
            m_FinalDeathPresentationUnits.Add(deadUnit);
        }

        internal bool TrySpawnMercenaries(Vector3 dropPosition, out string message)
        {
            message = string.Empty;

            if (!CanCommitGroundTargetItem())
            {
                message = "유효하지 않은 전투 상태입니다.";
                return false;
            }

            if (m_MercenaryKnightData == null ||
                m_MercenaryArcherData == null ||
                m_MercenaryWizardData == null)
            {
                message = "용병 데이터가 없습니다.";
                return false;
            }

            BattlefieldArea area = BattlefieldArea.Active;
            if (area == null)
            {
                message = "전장 영역이 설정되지 않았습니다.";
                return false;
            }

            dropPosition = area.ClampPosition(
                dropPosition,
                MercenaryFormationPadding);

            // 기존 팀 결정 로직을 그대로 유지한다.
            Team team = Team.Red;

            if (UnityEngine.Camera.main != null)
            {
                Vector3 viewportPos =
                    UnityEngine.Camera.main.WorldToViewportPoint(dropPosition);

                if (viewportPos.x >= 0.5f)
                {
                    team = Team.Blue;
                }
            }

            // 생성 위치를 한 번만 계산한다.
            Vector3 knightPosition = dropPosition;

            Vector3 archerPosition =
                dropPosition + new Vector3(
                    MercenaryFormationHorizontalOffset,
                    0f,
                    -MercenaryFormationDepthOffset);

            Vector3 wizardPosition =
                dropPosition + new Vector3(
                    -MercenaryFormationHorizontalOffset,
                    0f,
                    -MercenaryFormationDepthOffset);

            // 어떤 유닛도 생성하기 전에 세 위치를 모두 검증한다.
            if (!ValidateMercenarySpawnPosition(
                    m_MercenaryKnightData,
                    knightPosition,
                    out message) ||
                !ValidateMercenarySpawnPosition(
                    m_MercenaryArcherData,
                    archerPosition,
                    out message) ||
                !ValidateMercenarySpawnPosition(
                    m_MercenaryWizardData,
                    wizardPosition,
                    out message))
            {
                return false;
            }

            List<UnitType> spawnedUnits = new List<UnitType>(3);

            try
            {
                UnitType knight = SpawnMercenary(
                    m_MercenaryKnightData,
                    team,
                    knightPosition,
                    spawnedUnits);

                if (knight == null)
                {
                    throw new Exception("기사 소환 실패");
                }

                UnitType archer = SpawnMercenary(
                    m_MercenaryArcherData,
                    team,
                    archerPosition,
                    spawnedUnits);

                if (archer == null)
                {
                    throw new Exception("궁수 소환 실패");
                }

                UnitType wizard = SpawnMercenary(
                    m_MercenaryWizardData,
                    team,
                    wizardPosition,
                    spawnedUnits);

                if (wizard == null)
                {
                    throw new Exception("마법사 소환 실패");
                }

                for (int i = 0; i < spawnedUnits.Count; i++)
                {
                    spawnedUnits[i].gameObject.SetActive(true);
                }

                for (int i = 0; i < spawnedUnits.Count; i++)
                {
                    spawnedUnits[i].NotifyBattleStarted();
                    spawnedUnits[i].SetAIActive(true);
                }

                if (team == Team.Red)
                {
                    m_RedParticipantCount += spawnedUnits.Count;
                }
                else
                {
                    m_BlueParticipantCount += spawnedUnits.Count;
                }

                Debug.Log(
                    $"[Phase1C Test] 용병 고용 성공. " +
                    $"Team: {team}, " +
                    $"RuntimeListCount: " +
                    $"{(team == Team.Red ? Context.TeamAUnits.Count : Context.TeamBUnits.Count)}, " +
                    $"RedAlive: {RedAliveCount}/{m_RedParticipantCount}, " +
                    $"BlueAlive: {BlueAliveCount}/{m_BlueParticipantCount}");

                message = "용병을 고용했습니다.";
                return true;
            }
            catch (Exception ex)
            {
                Debug.LogError(
                    $"[CombatPhase] 용병 소환 실패 및 롤백: {ex.Message}");

                // 기존 시그니처와 기존 팀 변수를 그대로 사용한다.
                RollbackMercenaries(spawnedUnits, team);

                message = "용병 소환에 실패했습니다.";
                return false;
            }
        }

        private void RollbackMercenaries(List<UnitType> units, Team team)
        {
            List<UnitType> runtimeUnits = (team == Team.Red) ? Context.TeamAUnits : Context.TeamBUnits;
            foreach (var unit in units)
            {
                if (unit == null) continue;

                if (m_DeathHandlers.TryGetValue(unit, out var handler))
                {
                    unit.OnDied -= handler;
                    m_DeathHandlers.Remove(unit);
                }

                runtimeUnits.Remove(unit);
                PoolManager.Require().Units.Return(unit);
            }
        }

        private bool ValidateMercenarySpawnPosition(UnitData unitData, Vector3 position, out string message)
        {
            if (unitData == null)
            {
                message = "용병 데이터가 없습니다.";
                return false;
            }

            BattlefieldArea area = BattlefieldArea.Active;

            if (area == null)
            {
                message = "전장 영역이 설정되지 않았습니다.";
                return false;
            }

            float radius = Mathf.Max(0f, unitData.VisualRadius);

            if (!area.ContainsPosition(position, radius))
            {
                message =
                    "세 용병을 모두 배치할 수 있도록 전장 안쪽을 선택해야 합니다.";

                return false;
            }

            message = string.Empty;
            return true;
        }

        private UnitType SpawnMercenary(UnitData unitData, Team team, Vector3 position, List<UnitType> spawnedUnits)
        {
            if (unitData == null || Context == null) return null;

            UnitType unit = PoolManager.Require().Units.Spawn(
                unitData,
                team == Team.Red ? m_TeamASpawnRoot : m_TeamBSpawnRoot,
                (int)team,
                position,
                false
            );

            if (unit != null)
            {
                spawnedUnits.Add(unit);

                List<UnitType> runtimeUnits = (team == Team.Red) ? Context.TeamAUnits : Context.TeamBUnits;
                runtimeUnits.Add(unit);

                System.Action<UnitType> handler = _ => OnMercenaryDied(unit);
                m_DeathHandlers[unit] = handler;
                unit.OnDied += handler;
            }
            return unit;
        }

        internal bool TryApplyMeteorEffect(Vector3 center, out string message)
        {
            message = string.Empty;

            if (!CanCommitGroundTargetItem())
            {
                message = "유효하지 않은 전투 상태입니다.";
                return false;
            }

            if (!ValidateBattlefieldTarget(center, 0f, out message))
            {
                return false;
            }

            if (m_MeteorStunEffect == null)
            {
                message = "메테오 데이터가 없습니다.";
                return false;
            }

            float radius = MeteorEffectRadius;
            float stunDuration = 3f;
            float radiusSqr = radius * radius;

            ApplyStun(Context.TeamAUnits, center, radiusSqr, stunDuration);
            ApplyStun(Context.TeamBUnits, center, radiusSqr, stunDuration);

            message = "메테오를 사용했습니다.";
            return true;
        }

        private float GetMercenaryVisualRadius()
        {
            return Mathf.Max(
                0.5f,
                Mathf.Max(
                    m_MercenaryKnightData != null
                        ? m_MercenaryKnightData.VisualRadius
                        : 0f,
                    Mathf.Max(
                        m_MercenaryArcherData != null
                            ? m_MercenaryArcherData.VisualRadius
                            : 0f,
                        m_MercenaryWizardData != null
                            ? m_MercenaryWizardData.VisualRadius
                            : 0f)));
        }

        private void ApplyStun(List<InTheArena.Unit.Unit> units, Vector3 center, float radiusSqr, float stunDuration)
        {
            if (units == null)
            {
                return;
            }

            for (int i = 0; i < units.Count; i++)
            {
                var unit = units[i];
                if (unit != null)
                {
                    if (unit.IsDead == false)
                    {
                        float distSqr = (unit.GroundPosition - center).sqrMagnitude;
                        if (distSqr <= radiusSqr)
                        {
                            if (m_MeteorStunEffect != null)
                            {
                                unit.ApplyStatusEffect(m_MeteorStunEffect, null, stunDuration);
                            }
                        }
                    }
                }
            }
        }

        private static bool ValidateBattlefieldTarget(Vector3 position, float padding, out string message)
        {
            BattlefieldArea area = BattlefieldArea.Active;

            if (area == null)
            {
                message = "전장 영역이 설정되지 않았습니다.";
                return false;
            }

            if (!area.ContainsPosition(position, padding))
            {
                message = "전장 안쪽에서 사용해야 합니다.";
                return false;
            }

            message = string.Empty;
            return true;
        }

        private void OnDestroy()
        {
            m_IsItemCastingSlowMotion = false;
            Time.timeScale = 1f;
            m_IsFinalEliminationPlaying = false;
            CompleteFinalDeathPresentations();
            InTheArena.Camera.CameraController.Instance?.EndFinalEliminationFocus();
            UnsubscribeDeathEvents();
            transform.DOKill();
        }

#if UNITY_EDITOR
        /// <summary>
        /// 에디터에서 그리드 영역 시각화 (Scene 뷰에서만 표시)
        /// </summary>
        private void OnDrawGizmos()
        {
            DrawBattlefieldGizmos();

            if (!Application.isPlaying) return;
            if (Context == null) return;
            if (Context.CurrentStageData == null) return;

            var roundData = GetCurrentRoundData();
            if (roundData == null) return;

            // Team A 배치 정보 (빨간색)
            DrawGridContentGizmos(roundData.TeamAGrid, Team.Red, new Color(1f, 0.2f, 0.2f, 0.8f));

            // Team B 배치 정보 (파란색)
            DrawGridContentGizmos(roundData.TeamBGrid, Team.Blue, new Color(0.2f, 0.5f, 1f, 0.8f));
        }

        private void DrawBattlefieldGizmos()
        {
            Color redFill = new Color(1f, 0.2f, 0.2f, 0.18f);
            Color blueFill = new Color(0.2f, 0.7f, 1f, 0.18f);
            Color neutralLine = new Color(1f, 1f, 1f, 0.65f);

            for (int row = 0; row < BattlefieldRowCount; row++)
            {
                for (int col = 0; col < BattlefieldColumnCount; col++)
                {
                    Vector3 center = new Vector3(
                        BattlefieldMinX + (col + 0.5f) * GridCellSize,
                        0f,
                        BattlefieldMinZ + (row + 0.5f) * GridCellSize
                    );

                    bool isRedZone = col < TeamGridColumnCount;
                    bool isBlueZone = col >= BattlefieldColumnCount - TeamGridColumnCount;

                    if (isRedZone || isBlueZone)
                    {
                        Gizmos.color = isRedZone ? redFill : blueFill;
                        Gizmos.DrawCube(
                            center + Vector3.down * 0.01f,
                            new Vector3(GridCellSize, 0.02f, GridCellSize)
                        );
                    }

                    Gizmos.color = isRedZone
                        ? new Color(1f, 0.25f, 0.25f, 0.9f)
                        : isBlueZone
                            ? new Color(0.25f, 0.65f, 1f, 0.9f)
                            : neutralLine;
                    Gizmos.DrawWireCube(center, new Vector3(GridCellSize, 0f, GridCellSize));
                }
            }
        }

        private void DrawGridContentGizmos(GridCellData[] grid, Team team, Color color)
        {
            if (grid == null) return;

            Gizmos.color = color;

            int cellCount = Mathf.Min(grid.Length, TeamGridCellCount);
            for (int cellIndex = 0; cellIndex < cellCount; cellIndex++)
            {
                var cellData = grid[cellIndex];
                if (cellData == null || !cellData.IsValid()) continue;

                var center = GetGridCellCenterPosition(team, cellIndex);

                // 칸 번호 표시
                Gizmos.color = Color.yellow;
                Gizmos.DrawSphere(center, 0.15f);
                Gizmos.color = color;

                // 고정/가변 표시
                if (cellData.IsFixed)
                {
                    Gizmos.color = Color.green;
                    Gizmos.DrawWireCube(center + Vector3.up * 0.5f, new Vector3(0.5f, 0.5f, 0.5f));
                }
                else
                {
                    Gizmos.color = Color.cyan;
                    Gizmos.DrawWireSphere(center + Vector3.up * 0.5f, 0.25f);
                }
                Gizmos.color = color;
            }
        }
#endif
    }

    /// <summary>
    /// 팀 구분 열거형
    /// </summary>
    public enum Team
    {
        None = -1,
        Red = 0,   // Team A (Red)
        Blue = 1   // Team B (Blue)
    }

    internal readonly struct UnitSlotKey
    {
        public Team Team { get; }
        public int CellIndex { get; }

        public UnitSlotKey(Team team, int cellIndex)
        {
            Team = team;
            CellIndex = cellIndex;
        }
    }
}
#endif
