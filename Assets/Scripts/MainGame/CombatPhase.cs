#if UNITY_6000_0_OR_NEWER
using System;
using System.Collections.Generic;
using System.Threading;
using UnityEngine;
using DG.Tweening;
using InTheArena.Unit;
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
        [Header("Combat Settings")]
        [SerializeField] private float m_CombatTimeout = 60f; // 전투 최대 시간
        [SerializeField] private float m_UnitSpawnInterval = 0.1f; // 유닛 소환 간격
        [SerializeField] private Transform m_TeamASpawnRoot;
        [SerializeField] private Transform m_TeamBSpawnRoot;

        [Header("Speed Control")]
        [SerializeField] private float m_NormalSpeed = 1f;
        [SerializeField] private float m_FastSpeed = 2f;
        private float m_CurrentSpeed = 1f;

        private AwaitableCompletionSource m_PhaseCompletionSource;
        private CancellationTokenSource m_CombatCts;
        private bool m_IsCombatEnded = false;

        public override async Awaitable EnterPhaseAsync(CancellationToken token)
        {
            InitializeCombat();

            m_CombatCts = CancellationTokenSource.CreateLinkedTokenSource(token);
            m_PhaseCompletionSource = new AwaitableCompletionSource();

            // 유닛 스폰 및 전투 시작
            await SpawnUnitsAndStartCombatAsync(m_CombatCts.Token);

            // 전투 종료 대기 (한 팀 전멸 또는 타임아웃)
            await m_PhaseCompletionSource.Awaitable;
        }

        private void InitializeCombat()
        {
            IsPhaseCompleted = false;
            m_IsCombatEnded = false;
            m_CurrentSpeed = m_NormalSpeed;
            Time.timeScale = m_CurrentSpeed;

            // 컨텍스트에서 유닛 데이터로 런타임 유닛 생성
            CreateRuntimeUnitsFromData();
        }

        private void CreateRuntimeUnitsFromData()
        {
            Context.TeamAUnits.Clear();
            Context.TeamBUnits.Clear();

            // Team A 유닛 생성 (왼쪽 2x3 그리드)
            SpawnTeamUnits(Context.TeamAUnitDatas, Context.TeamAUnits, Team.Ally, m_TeamASpawnRoot);

            // Team B 유닛 생성 (오른쪽 2x3 그리드)
            SpawnTeamUnits(Context.TeamBUnitDatas, Context.TeamBUnits, Team.Enemy, m_TeamBSpawnRoot);
        }

        private void SpawnTeamUnits(List<UnitData> unitDatas, List<UnitType> runtimeUnits, Team team, Transform spawnRoot)
        {
            // RoundData의 그리드 데이터 기반으로 유닛 생성
            var roundData = GetRoundDataForTeam(team);
            if (roundData == null) return;

            var grid = (team == Team.Ally) ? roundData.TeamAGrid : roundData.TeamBGrid;
            if (grid == null) return;

            // 2x3 그리드 (6칸) 순회
            for (int cellIndex = 0; cellIndex < 6; cellIndex++)
            {
                var cellData = grid[cellIndex];
                if (cellData == null || !cellData.IsValid()) continue;

                // 생성 확률 체크
                if (UnityEngine.Random.value > cellData.SpawnProbability) continue;

                // 해당 칸의 중앙 위치 계산
                var spawnPos = GetGridCellCenterPosition(team, cellIndex);
                
                // 해당 칸에 생성할 유닛 리스트 생성
                var unitsToSpawn = cellData.GenerateRuntimeUnits();
                
                foreach (var unitData in unitsToSpawn)
                {
                    var unit = unitData.CreateUnit(spawnRoot, (int)team);
                    if (unit != null)
                    {
                        // 칸 내부에서 약간 랜덤한 오프셋 적용 (겹침 방지)
                        var finalPos = spawnPos + new Vector3(
                            UnityEngine.Random.Range(-0.3f, 0.3f),
                            0f,
                            UnityEngine.Random.Range(-0.3f, 0.3f)
                        );
                        unit.transform.position = finalPos;
                        runtimeUnits.Add(unit);
                    }
                }
            }
        }

        private RoundData GetRoundDataForTeam(Team team)
        {
            if (Context.CurrentStageData == null || Context.CurrentRound <= 0) return null;
            
            int roundIndex = Context.CurrentRound - 1;
            if (roundIndex < 0 || roundIndex >= Context.CurrentStageData.RoundDatas.Count) return null;
            
            return Context.CurrentStageData.RoundDatas[roundIndex];
        }

        private Vector3 GetGridCellCenterPosition(Team team, int cellIndex)
        {
            // 2x3 그리드 (2행 3열)
            // cellIndex: 0~5 (row*3 + col)
            // Team.Ally (왼쪽): x = -4 ~ -2, z = -1 ~ 1 (간격 2)
            // Team.Enemy (오른쪽): x = 2 ~ 4, z = -1 ~ 1 (간격 2)
            // 각 칸 크기: 2x2, 칸 중심 좌표 계산

            int col = cellIndex % 3;
            int row = cellIndex / 3;

            float cellSize = 2f;
            float startX = (team == Team.Ally) ? -4f : 2f; // 왼쪽: -4, 오른쪽: 2
            float startZ = -1f;

            float centerX = startX + col * cellSize + cellSize * 0.5f;
            float centerZ = startZ + row * cellSize + cellSize * 0.5f;

            return new Vector3(centerX, 0f, centerZ);
        }

        private async Awaitable SpawnUnitsAndStartCombatAsync(CancellationToken token)
        {
            // 모든 유닛을 0.1초 간격으로 스폰
            var allUnits = new List<UnitType>();
            allUnits.AddRange(Context.TeamAUnits);
            allUnits.AddRange(Context.TeamBUnits);

            foreach (var unit in allUnits)
            {
                if (token.IsCancellationRequested) break;

                unit.gameObject.SetActive(true);
                await Awaitable.WaitForSecondsAsync(m_UnitSpawnInterval);
            }

            // 전투 시작
            StartCombatLoop(token);
        }

        private async void StartCombatLoop(CancellationToken token)
        {
            float elapsedTime = 0f;

            while (!m_IsCombatEnded && !token.IsCancellationRequested)
            {
                await Awaitable.NextFrameAsync();

                if (token.IsCancellationRequested) break;

                float deltaTime = Time.deltaTime * m_CurrentSpeed;
                elapsedTime += deltaTime;

                // AI 업데이트 (각 유닛의 AI가 스스로 판단하여 행동)
                UpdateUnitAI(Context.TeamAUnits, deltaTime);
                UpdateUnitAI(Context.TeamBUnits, deltaTime);

                // 승리 조건 체크
                CheckVictoryCondition();

                // 타임아웃 체크
                if (elapsedTime >= m_CombatTimeout)
                {
                    Debug.Log("[CombatPhase] 전투 타임아웃 - 무승부 처리");
                    ForceEndCombat(Team.None);
                    break;
                }
            }
        }

        private void UpdateUnitAI(List<UnitType> units, float deltaTime)
        {
            foreach (var unit in units)
            {
                if (unit != null && !unit.IsDead && unit.AI != null)
                {
                    unit.AI.UpdateAI(deltaTime);
                }
            }
        }

        private void CheckVictoryCondition()
        {
            bool teamAAlive = Context.TeamAUnits.Exists(u => u != null && !u.IsDead);
            bool teamBAlive = Context.TeamBUnits.Exists(u => u != null && !u.IsDead);

            if (!teamAAlive && !teamBAlive)
            {
                // 무승부
                ForceEndCombat(Team.None);
            }
            else if (!teamAAlive)
            {
                // Team B 승리
                ForceEndCombat(Team.Enemy);
            }
            else if (!teamBAlive)
            {
                // Team A 승리
                ForceEndCombat(Team.Ally);
            }
        }

        private void ForceEndCombat(Team winner)
        {
            if (m_IsCombatEnded) return;

            m_IsCombatEnded = true;
            Time.timeScale = 1f;

            Context.DidTeamAWin = (winner == Team.Ally);
            Context.IsRoundCompleted = true;

            // 생존 유닛 정리
            foreach (var unit in Context.TeamAUnits)
            {
                if (unit != null) unit.StopMovement();
            }
            foreach (var unit in Context.TeamBUnits)
            {
                if (unit != null) unit.StopMovement();
            }

            m_PhaseCompletionSource?.TrySetResult();
        }

        /// <summary>
        /// 전투 속도 토글 (2배속)
        /// </summary>
        public void ToggleCombatSpeed()
        {
            m_CurrentSpeed = (m_CurrentSpeed == m_NormalSpeed) ? m_FastSpeed : m_NormalSpeed;
            Time.timeScale = m_CurrentSpeed;
            Debug.Log($"[CombatPhase] 전투 속도 변경: {m_CurrentSpeed}x");
        }

        /// <summary>
        /// 현재 전투 속도 반환
        /// </summary>
        public float CurrentSpeed => m_CurrentSpeed;

        public override async Awaitable ExitPhaseAsync(CancellationToken token)
        {
            Time.timeScale = 1f;

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
            foreach (var unit in Context.TeamAUnits)
            {
                if (unit != null && unit.gameObject != null)
                {
                    UnityEngine.Object.Destroy(unit.gameObject);
                }
            }
            foreach (var unit in Context.TeamBUnits)
            {
                if (unit != null && unit.gameObject != null)
                {
                    UnityEngine.Object.Destroy(unit.gameObject);
                }
            }

            Context.TeamAUnits.Clear();
            Context.TeamBUnits.Clear();
        }

        private void OnDestroy()
        {
            Time.timeScale = 1f;
            transform.DOKill();
        }

#if UNITY_EDITOR
        /// <summary>
        /// 에디터에서 그리드 영역 시각화 (Scene 뷰에서만 표시)
        /// </summary>
        private void OnDrawGizmos()
        {
            if (!Application.isPlaying) return;
            if (Context.CurrentStageData == null) return;

            var roundData = GetRoundDataForTeam(Team.Ally);
            if (roundData == null) return;

            // Team A 그리드 (빨간색)
            DrawGridGizmos(roundData.TeamAGrid, Team.Ally, new Color(1f, 0.2f, 0.2f, 0.3f));
            
            // Team B 그리드 (파란색)
            roundData = GetRoundDataForTeam(Team.Enemy);
            if (roundData != null)
            {
                DrawGridGizmos(roundData.TeamBGrid, Team.Enemy, new Color(0.2f, 0.5f, 1f, 0.3f));
            }
        }

        private void DrawGridGizmos(GridCellData[] grid, Team team, Color color)
        {
            if (grid == null) return;

            Gizmos.color = color;
            
            for (int cellIndex = 0; cellIndex < 6; cellIndex++)
            {
                var cellData = grid[cellIndex];
                if (cellData == null || !cellData.IsValid()) continue;

                var center = GetGridCellCenterPosition(team, cellIndex);
                
                // 칸 경계 그리기 (2x2 정사각형)
                float halfSize = 1f; // cellSize * 0.5f = 1f
                Vector3[] corners = new Vector3[4]
                {
                    center + new Vector3(-halfSize, 0, -halfSize),
                    center + new Vector3(halfSize, 0, -halfSize),
                    center + new Vector3(halfSize, 0, halfSize),
                    center + new Vector3(-halfSize, 0, halfSize)
                };
                
                for (int i = 0; i < 4; i++)
                {
                    Gizmos.DrawLine(corners[i], corners[(i + 1) % 4]);
                }

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
        Ally = 0,   // Team A (Red)
        Enemy = 1   // Team B (Blue)
    }
}
#endif