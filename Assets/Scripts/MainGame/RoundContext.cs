#if UNITY_6000_0_OR_NEWER
using UnityEngine;
using System.Collections.Generic;
using InTheArena.Unit;
using UnitType = InTheArena.Unit.Unit;

namespace InTheArena.MainGame
{
    /// <summary>
    /// 라운드 진행 중 필요한 컨텍스트 데이터
    /// 라운드 단위로 초기화되고 라운드가 끝나면 리셋됨
    /// </summary>
    public class RoundContext
    {
        // 라운드 기본 정보
        public int CurrentStageId { get; set; }
        public int CurrentRound { get; set; }
        public int MaxRounds { get; set; } = 5;
        public StageData CurrentStageData { get; set; }
        public StageSession StageSession { get; } = new StageSession();
        public int TargetCall => CurrentStageData != null ? CurrentStageData.TargetCall : 0;
        public int CurrentCall => StageSession.CurrentCall;

        // 베팅 페이즈에서 확정된 팀 유닛 데이터 (UI/결과 표시용 평탄화 목록)
        public List<UnitData> TeamAUnitDatas { get; set; } = new List<UnitData>();
        public List<UnitData> TeamBUnitDatas { get; set; } = new List<UnitData>();

        // 베팅 페이즈에서 확정된 셀별 배치. CombatPhase는 RoundData를 다시 랜덤 생성하지 않고 이 값을 사용한다.
        public List<TeamUnitDeployment> TeamADeployments { get; } = new List<TeamUnitDeployment>();
        public List<TeamUnitDeployment> TeamBDeployments { get; } = new List<TeamUnitDeployment>();

        // 런타임 유닛 리스트 (실제 전투에서 사용)
        public List<UnitType> TeamAUnits { get; set; } = new List<UnitType>();
        public List<UnitType> TeamBUnits { get; set; } = new List<UnitType>();

        // 베팅 및 전투 결과 데이터
        public RoundBetTicket BetTicket { get; set; }
        public CombatResultSnapshot CombatResult { get; set; }
        public BetSettlement Settlement { get; set; }

        // 라운드 결과
        public bool IsRoundCompleted { get; set; }
        public Team CombatWinner { get; set; } = Team.None;
        public bool IsCombatDraw => CombatWinner == Team.None;

        // 특별 규칙
        public RoundRule CurrentRoundRule { get; set; } = RoundRule.None;

        public List<SpecialBetType> ActiveSpecialBets { get; } = new List<SpecialBetType>();
        public event System.Action OnSpecialBetChanged;

        public void SetActiveSpecialBets(IEnumerable<SpecialBetType> bets)
        {
            ActiveSpecialBets.Clear();
            if (bets != null)
            {
                ActiveSpecialBets.AddRange(bets);
            }
            OnSpecialBetChanged?.Invoke();
        }

        /// <summary>
        /// 스테이지 런타임 상태를 한 번 초기화합니다.
        /// </summary>
        public void InitializeStage(StageData stageData)
        {
            if (stageData == null)
            {
                Debug.LogError("[RoundContext] StageData가 없습니다.");
                return;
            }

            CurrentStageData = stageData;
            CurrentStageId = stageData.StageId;
            MaxRounds = stageData.TotalRounds;
            StageSession.Initialize(stageData);
            CurrentRound = 0;
            ResetRoundState();
        }

        /// <summary>
        /// 라운드 데이터 설정 (StageData에서 복사)
        /// </summary>
        public void SetRoundData(StageData stageData, int roundIndex)
        {
            if (stageData == null) return;
            if (CurrentStageData != stageData || StageSession.StageData != stageData)
            {
                InitializeStage(stageData);
            }

            CurrentStageId = stageData.StageId;
            MaxRounds = stageData.TotalRounds;
            CurrentRound = roundIndex + 1;
            CurrentStageData = stageData;
            ResetRoundState();

            if (roundIndex < stageData.RoundDatas.Count)
            {
                var roundData = stageData.RoundDatas[roundIndex];
                CurrentRoundRule = roundData.SpecialRule;
            }
        }

        public void ResetRoundState()
        {
            ClearUnitAssignments();
            TeamAUnits.Clear();
            TeamBUnits.Clear();
            BetTicket = null;
            CombatResult = null;
            Settlement = null;
            CombatWinner = Team.None;
            IsRoundCompleted = false;
            CurrentRoundRule = RoundRule.None;

            ActiveSpecialBets.Clear();
            if (CurrentStageData != null && CurrentStageData.SpecialBetTypes != null)
            {
                ActiveSpecialBets.AddRange(CurrentStageData.SpecialBetTypes);
            }
        }

        /// <summary>
        /// BettingPhase 시작 시 RoundData의 고정/가변 칸을 한 번만 해석하여 이번 라운드의 편성을 확정한다.
        /// </summary>
        public void AssignUnitsForBetting()
        {
            ClearUnitAssignments();

            if (CurrentStageData == null || CurrentRound <= 0)
            {
                Debug.LogError("[RoundContext] 현재 라운드 데이터가 없어 유닛 편성을 확정할 수 없습니다.");
                return;
            }

            int roundIndex = CurrentRound - 1;
            if (roundIndex >= CurrentStageData.RoundDatas.Count || CurrentStageData.RoundDatas[roundIndex] == null)
            {
                Debug.LogError("[RoundContext] 현재 라운드 인덱스에 해당하는 RoundData가 없습니다.");
                return;
            }

            var roundData = CurrentStageData.RoundDatas[roundIndex];
            BuildTeamDeployments(roundData.TeamAGrid, TeamADeployments, TeamAUnitDatas);
            BuildTeamDeployments(roundData.TeamBGrid, TeamBDeployments, TeamBUnitDatas);
        }

        private static void BuildTeamDeployments(
            GridCellData[] grid,
            List<TeamUnitDeployment> deployments,
            List<UnitData> flatUnits)
        {
            if (grid == null) return;

            for (int cellIndex = 0; cellIndex < grid.Length; cellIndex++)
            {
                var cellData = grid[cellIndex];
                if (cellData == null || !cellData.IsValid()) continue;

                var units = cellData.GenerateRuntimeUnits();
                if (units.Count == 0) continue;

                deployments.Add(new TeamUnitDeployment(cellIndex, units));
                flatUnits.AddRange(units);
            }
        }

        private void ClearUnitAssignments()
        {
            TeamAUnitDatas.Clear();
            TeamBUnitDatas.Clear();
            TeamADeployments.Clear();
            TeamBDeployments.Clear();
        }

        /// <summary>
        /// 모든 데이터 초기화
        /// </summary>
        public void Clear()
        {
            CurrentStageId = 0;
            CurrentRound = 0;
            MaxRounds = 0;
            CurrentStageData = null;
            StageSession.Clear();
            ClearUnitAssignments();
            TeamAUnits.Clear();
            TeamBUnits.Clear();
            BetTicket = null;
            CombatResult = null;
            Settlement = null;
            IsRoundCompleted = false;
            CombatWinner = Team.None;
            CurrentRoundRule = RoundRule.None;
            ActiveSpecialBets.Clear();
            OnSpecialBetChanged = null;
        }
    }

    /// <summary>
    /// 베팅 페이즈에서 확정된 한 그리드 칸의 유닛 배치 결과입니다.
    /// </summary>
    public sealed class TeamUnitDeployment
    {
        public int CellIndex { get; }
        public List<UnitData> Units { get; }

        public TeamUnitDeployment(int cellIndex, List<UnitData> units)
        {
            CellIndex = cellIndex;
            Units = new List<UnitData>(units);
        }
    }
}
#endif
