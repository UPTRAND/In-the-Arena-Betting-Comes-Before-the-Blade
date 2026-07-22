#if UNITY_6000_0_OR_NEWER
using System.Collections.Generic;

public class RoundContext
{
    // 스테이지 정보
    public int CurrentStageId { get; set; }
    public int CurrentRound { get; set; }
    public int MaxRounds { get; set; } = 5; // 일반: 5, 하드: 7
    public int TargetCall { get; set; } = 200; // 목표 자금
    public int CurrentCall { get; set; } = 100; // 시작 자금 (기본 100콜)

    // 팀 및 유닛 정보
    public List<object> TeamAUnits { get; set; } = new List<object>();
    public List<object> TeamBUnits { get; set; } = new List<object>();

    // 배팅 페이즈 결과 데이터
    public int TeamABetRatio { get; set; } = 60; // 기본 60:40
    public int TeamBBetRatio { get; set; } = 40;
    public int ExtraBetCall { get; set; } = 0; // 추가 배팅권으로 투입된 콜

    // 전투 페이즈 결과 데이터
    public bool DidTeamAWin { get; set; }

    public void ResetBettingData()
    {
        TeamABetRatio = 60;
        TeamBBetRatio = 40;
        ExtraBetCall = 0;
    }
}
#endif