#if UNITY_6000_0_OR_NEWER
using UnityEngine;
using System.Collections.Generic;
using System.Threading;
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
        public int TargetCall { get; set; } = 200;
        public int CurrentCall { get; set; } = 100;
        public int CurrentCoin { get; set; } = 100;
        public StageData CurrentStageData { get; set; }

        // 팀 유닛 데이터 (에디터 설정용 - 런타임에는 Unit 리스트로 변환)
        public List<UnitData> TeamAUnitDatas { get; set; } = new List<UnitData>();
        public List<UnitData> TeamBUnitDatas { get; set; } = new List<UnitData>();

        // 런타임 유닛 리스트 (실제 전투에서 사용)
        public List<UnitType> TeamAUnits { get; set; } = new List<UnitType>();
        public List<UnitType> TeamBUnits { get; set; } = new List<UnitType>();

        // 베팅 데이터
        public int TeamABetRatio { get; set; } = 50;
        public int TeamBBetRatio { get; set; } = 50;
        public int ExtraBetCall { get; set; } = 0;

        // 라운드 결과
        public bool DidTeamAWin { get; set; }
        public bool IsRoundCompleted { get; set; }

        // 특별 규칙
        public RoundRule CurrentRoundRule { get; set; } = RoundRule.None;

        /// <summary>
        /// 베팅 데이터 초기화
        /// </summary>
        public void ResetBettingData()
        {
            TeamABetRatio = 50;
            TeamBBetRatio = 50;
            ExtraBetCall = 0;
        }

        /// <summary>
        /// 라운드 데이터 설정 (StageData에서 복사)
        /// </summary>
        public void SetRoundData(StageData stageData, int roundIndex)
        {
            CurrentStageId = stageData.StageId;
            MaxRounds = stageData.TotalRounds;
            TargetCall = stageData.TargetCall;
            CurrentCall = stageData.InitialCoin;
            CurrentCoin = stageData.InitialCoin;
            CurrentRound = roundIndex + 1;
            CurrentStageData = stageData;

            if (roundIndex < stageData.RoundDatas.Count)
            {
                var roundData = stageData.RoundDatas[roundIndex];
                TeamAUnitDatas = new List<UnitData>(roundData.GetTeamAUnits());
                TeamBUnitDatas = new List<UnitData>(roundData.GetTeamBUnits());
                TeamABetRatio = Mathf.RoundToInt(roundData.DefaultBetRatioA);
                TeamBBetRatio = Mathf.RoundToInt(roundData.DefaultBetRatioB);
                CurrentRoundRule = roundData.SpecialRule;
            }
        }

        /// <summary>
        /// 모든 데이터 초기화
        /// </summary>
        public void Clear()
        {
            CurrentStageId = 0;
            CurrentRound = 0;
            MaxRounds = 0;
            TargetCall = 0;
            CurrentCall = 0;
            CurrentCoin = 0;
            TeamAUnitDatas.Clear();
            TeamBUnitDatas.Clear();
            TeamAUnits.Clear();
            TeamBUnits.Clear();
            ResetBettingData();
            DidTeamAWin = false;
            IsRoundCompleted = false;
            CurrentRoundRule = RoundRule.None;
        }
    }
}
#endif