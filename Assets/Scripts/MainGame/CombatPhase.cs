#if UNITY_6000_0_OR_NEWER
using System;
using System.Collections.Generic;
using System.Threading;
using UnityEngine;
using DG.Tweening;
using InTheArena.Unit;
using InTheArena.UI;
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

        [Header("Combat Settings")]
        [SerializeField] [Min(1f)] private float m_CombatTimeout = 30f;
        [SerializeField] private Transform m_TeamASpawnRoot;
        [SerializeField] private Transform m_TeamBSpawnRoot;

        [Header("Speed Control")]
        [SerializeField] private float m_NormalSpeed = 1f;
        [SerializeField] private float m_FastSpeed = 2f;
        [SerializeField] private UI_CombatHUD m_CombatHud;
        private float m_CurrentSpeed = 1f;

        private CancellationTokenSource m_CombatCts;
        private bool m_IsCombatEnded = false;
        private float m_RemainingCombatTime;
        private readonly Dictionary<UnitType, UnitSlotKey> m_UnitSlots = new Dictionary<UnitType, UnitSlotKey>();
        private readonly Dictionary<UnitType, Action<UnitType>> m_DeathHandlers = new Dictionary<UnitType, Action<UnitType>>();
        private int m_FirstEliminatedSlot = -1;

        public override async Awaitable EnterPhaseAsync(CancellationToken token)
        {
            InitializeCombat();

            m_CombatCts = CancellationTokenSource.CreateLinkedTokenSource(token);

            await ActivateUnitsAsync(m_CombatCts.Token);
            if (m_CombatCts.IsCancellationRequested) return;

            m_CombatHud?.BindAndShow(this);
            await RunCombatLoopAsync(m_CombatCts.Token);
        }

        private void InitializeCombat()
        {
            IsPhaseCompleted = false;
            m_IsCombatEnded = false;
            m_RemainingCombatTime = m_CombatTimeout;
            m_CurrentSpeed = m_NormalSpeed;
            Time.timeScale = m_CurrentSpeed;
            Context.CombatWinner = Team.None;
            Context.IsRoundCompleted = false;
            Context.CombatResult = null;
            m_UnitSlots.Clear();
            m_DeathHandlers.Clear();
            m_FirstEliminatedSlot = -1;

            // 컨텍스트에서 유닛 데이터로 런타임 유닛 생성
            CreateRuntimeUnitsFromData();
        }

        private void CreateRuntimeUnitsFromData()
        {
            Context.TeamAUnits.Clear();
            Context.TeamBUnits.Clear();

            PrewarmTeam(Context.TeamADeployments);
            PrewarmTeam(Context.TeamBDeployments);

            // BettingPhase에서 확정한 셀별 편성을 그대로 사용한다.
            SpawnTeamUnits(Context.TeamADeployments, Context.TeamAUnits, Team.Red, m_TeamASpawnRoot);
            SpawnTeamUnits(Context.TeamBDeployments, Context.TeamBUnits, Team.Blue, m_TeamBSpawnRoot);

            Debug.Log($"[CombatPhase] 런타임 유닛 생성 완료 - Red: {Context.TeamAUnits.Count}, Blue: {Context.TeamBUnits.Count}");
        }

        private static void PrewarmTeam(List<TeamUnitDeployment> deployments)
        {
            if (deployments == null) return;
            var counts = new Dictionary<UnitData, int>();
            foreach (var deployment in deployments)
            {
                if (deployment?.Units == null) continue;
                foreach (var data in deployment.Units)
                {
                    if (data == null) continue;
                    counts.TryGetValue(data, out int count);
                    counts[data] = count + 1;
                }
            }

            foreach (var pair in counts)
                UnitPoolService.Prewarm(pair.Key, pair.Value);
        }

        private void SpawnTeamUnits(List<TeamUnitDeployment> deployments, List<UnitType> runtimeUnits, Team team, Transform spawnRoot)
        {
            if (deployments == null)
            {
                Debug.LogError($"[CombatPhase] {team} 팀의 베팅 편성이 없습니다.");
                return;
            }

            foreach (var deployment in deployments)
            {
                if (deployment == null || deployment.Units == null || deployment.Units.Count == 0) continue;

                var spawnPos = GetGridCellCenterPosition(team, deployment.CellIndex);
                
                foreach (var unitData in deployment.Units)
                {
                    if (unitData == null) continue;

                    var finalPos = spawnPos + new Vector3(
                        UnityEngine.Random.Range(-0.3f, 0.3f),
                        0f,
                        UnityEngine.Random.Range(-0.3f, 0.3f)
                    );
                    var unit = UnitPoolService.Spawn(
                        unitData,
                        spawnRoot,
                        (int)team,
                        finalPos,
                        false);
                    if (unit != null)
                    {
                        if (unit.AI == null)
                        {
                            Debug.LogError($"[CombatPhase] {unitData.UnitName}에 런타임 AI가 없어 전투 행동을 할 수 없습니다.");
                        }

                        unit.SetAIActive(false);
                        runtimeUnits.Add(unit);
                        RegisterUnitSlot(unit, team, deployment.CellIndex);
                    }
                }
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
            if (m_FirstEliminatedSlot > 0 || deadUnit == null) return;

            foreach (var pair in m_UnitSlots)
            {
                if (pair.Value.Team == key.Team &&
                    pair.Value.CellIndex == key.CellIndex &&
                    pair.Key != null &&
                    !pair.Key.IsDead)
                {
                    return;
                }
            }

            // Red/Blue의 같은 인덱스는 공통 슬롯 번호 1~6으로 판정한다.
            m_FirstEliminatedSlot = key.CellIndex + 1;
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

        private async Awaitable ActivateUnitsAsync(CancellationToken token)
        {
            // 풀에서 준비된 유닛은 한 프레임에 활성화하고, 모두 배치된 후 동시에 AI를 시작합니다.
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

            // 모든 유닛이 배치된 뒤 동시에 AI 전투를 시작한다.
            foreach (var unit in Context.TeamAUnits)
            {
                if (unit != null && !unit.IsDead) unit.SetAIActive(true);
            }
            foreach (var unit in Context.TeamBUnits)
            {
                if (unit != null && !unit.IsDead) unit.SetAIActive(true);
            }
        }

        private async Awaitable RunCombatLoopAsync(CancellationToken token)
        {
            Debug.Log($"[CombatPhase] 전투 시작 - 제한 시간: {m_CombatTimeout:0.##}초");

            while (!m_IsCombatEnded && !token.IsCancellationRequested)
            {
                if (TryResolveElimination()) break;

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

        private bool TryResolveElimination()
        {
            bool redAlive = HasLivingUnit(Context.TeamAUnits);
            bool blueAlive = HasLivingUnit(Context.TeamBUnits);

            if (!redAlive && !blueAlive)
            {
                CompleteCombat(Team.None);
            }
            else if (!redAlive)
            {
                CompleteCombat(Team.Blue);
            }
            else if (!blueAlive)
            {
                CompleteCombat(Team.Red);
            }

            return m_IsCombatEnded;
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

            m_IsCombatEnded = true;
            Time.timeScale = 1f;

            Context.CombatWinner = winner;
            Context.IsRoundCompleted = true;
            Context.CombatResult = BuildCombatResult(winner);

            // 생존 유닛 정리
            foreach (var unit in Context.TeamAUnits)
            {
                if (unit != null) unit.SetAIActive(false);
            }
            foreach (var unit in Context.TeamBUnits)
            {
                if (unit != null) unit.SetAIActive(false);
            }

            string result = winner == Team.None ? "Draw" : $"{winner} Win";
            Debug.Log($"[CombatPhase] 전투 종료 - {result}, 남은 시간: {m_RemainingCombatTime:0.00}초");
            CompletePhase();
        }

        private CombatResultSnapshot BuildCombatResult(Team winner)
        {
            int redAlive = 0;
            int blueAlive = 0;
            var redSlots = new HashSet<int>();
            var blueSlots = new HashSet<int>();

            foreach (var pair in m_UnitSlots)
            {
                UnitType unit = pair.Key;
                if (unit == null || unit.IsDead) continue;

                int slot = pair.Value.CellIndex + 1;
                if (pair.Value.Team == Team.Red)
                {
                    redAlive++;
                    redSlots.Add(slot);
                }
                else if (pair.Value.Team == Team.Blue)
                {
                    blueAlive++;
                    blueSlots.Add(slot);
                }
            }

            return new CombatResultSnapshot(
                winner,
                m_RemainingCombatTime,
                redAlive,
                blueAlive,
                redSlots,
                blueSlots,
                m_FirstEliminatedSlot);
        }

        /// <summary>
        /// 전투 속도 토글 (2배속)
        /// </summary>
        public void ToggleCombatSpeed()
        {
            m_CurrentSpeed = (m_CurrentSpeed == m_NormalSpeed) ? m_FastSpeed : m_NormalSpeed;
            Time.timeScale = m_CurrentSpeed;
            InTheArena.Camera.CameraController.Instance?.SetSpeedBoost(m_CurrentSpeed > m_NormalSpeed);
            Debug.Log($"[CombatPhase] 전투 속도 변경: {m_CurrentSpeed}x");
        }

        /// <summary>
        /// 현재 전투 속도 반환
        /// </summary>
        public float CurrentSpeed => m_CurrentSpeed;
        public float RemainingCombatTime => m_RemainingCombatTime;
        public float CombatTimeout => m_CombatTimeout;
        public int RedAliveCount => CountLivingUnits(Context?.TeamAUnits);
        public int BlueAliveCount => CountLivingUnits(Context?.TeamBUnits);

        public override async Awaitable ExitPhaseAsync(CancellationToken token)
        {
            Time.timeScale = 1f;
            InTheArena.Camera.CameraController.Instance?.SetSpeedBoost(false);
            m_CombatHud?.UnbindAndHide();

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
                    UnitPoolService.Return(unit);
                }
            }
            foreach (var unit in Context.TeamBUnits)
            {
                if (unit != null && unit.gameObject != null)
                {
                    UnitPoolService.Return(unit);
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

        private void OnDestroy()
        {
            Time.timeScale = 1f;
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
