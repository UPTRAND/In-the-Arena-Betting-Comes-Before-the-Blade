#if UNITY_6000_0_OR_NEWER
using System;
using System.Linq;
using System.Collections.Generic;

namespace InTheArena.MainGame
{
    /// <summary>UI·배팅 모델·콘텐츠 검증이 공유하는 금액 규칙입니다.</summary>
    public static class BettingRules
    {
        public const int WagerStepCall = 100;

        /// <summary>최소 금액 이상이며 베팅 단위에 맞는 금액인지 검사합니다.</summary>
        public static bool IsValidWager(int amount)
        {
            return amount >= WagerStepCall && amount % WagerStepCall == 0;
        }
    }

    public enum SpecialBetType
    {
        RemainingTime = 0,
        SurvivingRow = 1,
        OddEven = 2,
        FirstEliminatedColumn = 3
    }

    public enum FactionPrediction
    {
        NotSelected = 0,
        Red = 1,
        Blue = 2,
        Draw = 3
    }

    public enum RemainingTimePrediction
    {
        Seconds0To5 = 0,
        Seconds5To10 = 1,
        Seconds10To15 = 2,
        Seconds15To20 = 3,
        Seconds20OrMore = 4
    }

    public enum OddEvenPrediction
    {
        Odd = 0,
        Even = 1
    }

    public enum FirstEliminatedColumnPrediction
    {
        RedFront = 0,
        RedBack = 1,
        BlueFront = 2,
        BlueBack = 3
    }

    public enum SurvivingRowPrediction
    {
        RedRow1 = 0,
        RedRow2 = 1,
        RedRow3 = 2,
        BlueRow1 = 3,
        BlueRow2 = 4,
        BlueRow3 = 5
    }

    public enum BetCategory
    {
        Faction,
        RemainingTime,
        OddEven,
        FirstEliminatedColumn,
        SurvivingRow
    }

    /// <summary>
    /// 선택 항목 하나의 선택 여부와 실제 적중 여부를 함께 전달합니다.
    /// </summary>
    public readonly struct BetOutcome
    {
        public BetCategory Category { get; }
        public bool IsSelected { get; }
        public bool IsMatched { get; }

        /// <summary>항목의 선택 여부와 적중 여부를 불변 값으로 보관합니다.</summary>
        public BetOutcome(BetCategory category, bool isSelected, bool isMatched)
        {
            Category = category;
            IsSelected = isSelected;
            IsMatched = isMatched;
        }
    }

    /// <summary>
    /// 한 라운드에 확정된 단일 복합 베팅입니다.
    /// 슬롯 번호는 에디터 표기와 동일하게 1~6을 사용합니다.
    /// </summary>
    [Serializable]
    public sealed class RoundBetTicket
    {
        public int WagerCall { get; private set; }
        public FactionPrediction Faction { get; private set; } = FactionPrediction.NotSelected;
        public RemainingTimePrediction? RemainingTime { get; private set; }
        public OddEvenPrediction? OddEven { get; private set; }
        public FirstEliminatedColumnPrediction? FirstEliminatedColumn { get; private set; }
        public SurvivingRowPrediction? SurvivingRow { get; private set; }
        public bool IsPlaced { get; private set; }
        public bool IsSettled { get; private set; }

        public bool HasAdditionalBet { get; private set; }
        public bool HasInsurance { get; private set; }

        /// <summary>메인에서 선택한 전투 팀을 반환하며, 무승부와 미선택은 None입니다.</summary>
        public Team BettingTeam
        {
            get
            {
                switch (Faction)
                {
                    case FactionPrediction.Red:
                        return Team.Red;
                    case FactionPrediction.Blue:
                        return Team.Blue;
                    default:
                        return Team.None;
                }
            }
        }

        public bool CanSelectSpecialBets => BettingTeam != Team.None;

        public int SelectedCategoryCount
        {
            get
            {
                int count = Faction != FactionPrediction.NotSelected ? 1 : 0;
                if (RemainingTime.HasValue) count++;
                if (OddEven.HasValue) count++;
                if (FirstEliminatedColumn.HasValue) count++;
                if (SurvivingRow.HasValue) count++;
                return count;
            }
        }

        /// <summary>무승부는 3배, 팀 배팅은 선택한 1~4개 항목에 따라 2~16배를 지급합니다.</summary>
        public int Multiplier
        {
            get
            {
                if (Faction == FactionPrediction.NotSelected)
                {
                    return 0;
                }

                if (Faction == FactionPrediction.Draw)
                {
                    return 3;
                }

                int count = SelectedCategoryCount;
                if ((count < 1) || (count > 4))
                {
                    return 0;
                }

                return 1 << count;
            }
        }

        /// <summary>확정 전 베팅 금액을 변경합니다.</summary>
        public void SetWager(int wagerCall)
        {
            EnsureEditable();
            WagerCall = wagerCall;
        }

        /// <summary>팀 변경은 행·열 선택을 새 팀으로 옮기며, 무승부·미선택은 서브를 지웁니다.</summary>
        public void SetFaction(FactionPrediction faction)
        {
            EnsureEditable();
            if (!Enum.IsDefined(typeof(FactionPrediction), faction))
            {
                throw new ArgumentOutOfRangeException(nameof(faction));
            }

            if ((faction == FactionPrediction.NotSelected) || (faction == FactionPrediction.Draw))
            {
                ClearSpecialPredictions();
            }
            else if (Faction != faction)
            {
                int rowOffset = 0;
                int columnOffset = 0;
                if (faction == FactionPrediction.Blue)
                {
                    rowOffset = 3;
                    columnOffset = 2;
                }

                if (SurvivingRow.HasValue)
                {
                    SurvivingRow = (SurvivingRowPrediction)(((int)SurvivingRow.Value % 3) + rowOffset);
                }

                if (FirstEliminatedColumn.HasValue)
                {
                    FirstEliminatedColumn = (FirstEliminatedColumnPrediction)(((int)FirstEliminatedColumn.Value % 2) + columnOffset);
                }
            }

            Faction = faction;
        }

        /// <summary>확정 전 베팅 선택을 변경합니다.</summary>
        public void SetRemainingTime(RemainingTimePrediction? prediction)
        {
            EnsureEditable();
            RemainingTime = prediction;
        }
        /// <summary>확정 전 베팅 선택을 변경합니다.</summary>
        public void SetOddEven(OddEvenPrediction? prediction)
        {
            EnsureEditable();
            OddEven = prediction;
        }

        /// <summary>확정 전 베팅 선택을 변경합니다.</summary>
        public void SetFirstEliminatedColumn(FirstEliminatedColumnPrediction? prediction)
        {
            EnsureEditable();
            FirstEliminatedColumn = prediction;
        }

        /// <summary>확정 전 베팅 선택을 변경합니다.</summary>
        public void SetSurvivingRow(SurvivingRowPrediction? prediction)
        {
            EnsureEditable();
            SurvivingRow = prediction;
        }

        public void ClearSpecialPredictions()
        {
            EnsureEditable();
            RemainingTime = null;
            OddEven = null;
            FirstEliminatedColumn = null;
            SurvivingRow = null;
        }

        public void SetItemUsages(bool hasAdditionalBet, bool hasInsurance)
        {
            EnsureEditable();
            HasAdditionalBet = hasAdditionalBet;
            HasInsurance = hasInsurance;
        }

        /// <summary>금액·메인 필수·팀 기준 선택·공개된 서브 종류를 검증합니다.</summary>
        public bool Validate(StageData stageData, RoundContext context, int availableCall, out string error)
        {
            if (stageData == null)
            {
                error = "StageData가 없습니다.";
                return false;
            }

            if (context == null)
            {
                error = "RoundContext가 없습니다.";
                return false;
            }

            if (!BettingRules.IsValidWager(WagerCall) || WagerCall > availableCall)
            {
                error = $"베팅액은 {BettingRules.WagerStepCall} Call 단위이며 보유 Call 이하여야 합니다.";
                return false;
            }

            if (!Enum.IsDefined(typeof(FactionPrediction), Faction) || (Faction == FactionPrediction.NotSelected))
            {
                error = "필수 메인 베팅에서 레드·블루·무승부를 선택해야 합니다.";
                return false;
            }

            if ((SelectedCategoryCount < 1) || (SelectedCategoryCount > 4))
            {
                error = "메인과 서브 배팅은 합계 1~4개를 선택해야 합니다.";
                return false;
            }

            if (Faction != FactionPrediction.NotSelected && !stageData.EnableFactionBet)
            {
                error = "이 스테이지에서는 진영 베팅을 제공하지 않습니다.";
                return false;
            }

            if (Faction == FactionPrediction.Draw)
            {
                if (RemainingTime.HasValue || OddEven.HasValue || FirstEliminatedColumn.HasValue || SurvivingRow.HasValue)
                {
                    error = "무승부 배팅에는 서브 배팅을 사용할 수 없습니다.";
                    return false;
                }
            }
            else if (CanSelectSpecialBets)
            {
                Team betTeam = BettingTeam;
                if (FirstEliminatedColumn.HasValue)
                {
                    int column = (int)FirstEliminatedColumn.Value;
                    bool isRedColumn = (column >= 0) && (column <= 1);
                    bool isBlueColumn = (column >= 2) && (column <= 3);
                    if (((betTeam == Team.Red) && !isRedColumn) || ((betTeam == Team.Blue) && !isBlueColumn))
                    {
                        error = "첫 전멸 열은 메인에서 배팅한 팀을 대상으로 선택해야 합니다.";
                        return false;
                    }
                }

                if (SurvivingRow.HasValue)
                {
                    int row = (int)SurvivingRow.Value;
                    bool isRedRow = (row >= 0) && (row <= 2);
                    bool isBlueRow = (row >= 3) && (row <= 5);
                    if (((betTeam == Team.Red) && !isRedRow) || ((betTeam == Team.Blue) && !isBlueRow))
                    {
                        error = "생존 행은 메인에서 배팅한 팀을 대상으로 선택해야 합니다.";
                        return false;
                    }
                }
            }

            if ((RemainingTime.HasValue && !context.ActiveSpecialBets.Contains(SpecialBetType.RemainingTime)) ||
                (OddEven.HasValue && !context.ActiveSpecialBets.Contains(SpecialBetType.OddEven)) ||
                (FirstEliminatedColumn.HasValue && !context.ActiveSpecialBets.Contains(SpecialBetType.FirstEliminatedColumn)) ||
                (SurvivingRow.HasValue && !context.ActiveSpecialBets.Contains(SpecialBetType.SurvivingRow)))
            {
                error = "스테이지에서 제공하지 않는 특수 베팅이 선택되었습니다.";
                return false;
            }

            error = null;
            return true;
        }

        /// <summary>확정한 티켓은 정산 시까지 동일한 선택을 유지합니다.</summary>
        private void EnsureEditable()
        {
            if (IsPlaced)
            {
                throw new InvalidOperationException("확정된 베팅 티켓은 수정할 수 없습니다.");
            }
        }

        internal void MarkPlaced() => IsPlaced = true;
        internal void MarkSettled() => IsSettled = true;
    }

    public sealed class CombatResultSnapshot
    {
        public Team Winner { get; }
        public float RemainingTime { get; }
        public int RedAliveCount { get; }
        public int BlueAliveCount { get; }
        public IReadOnlyList<SurvivingRowPrediction> SurvivingRows { get; }
        public FirstEliminatedColumnPrediction? FirstEliminatedColumn { get; }

        public FirstEliminatedColumnPrediction? RedFirstEliminatedColumn { get; }
        public FirstEliminatedColumnPrediction? BlueFirstEliminatedColumn { get; }

        public int TotalAliveCount => RedAliveCount + BlueAliveCount;

        /// <summary>전투 종료 결과와 각 팀의 첫 전멸을 복사하여 불변 스냅샷을 만듭니다.</summary>
        public CombatResultSnapshot(
            Team winner,
            float remainingTime,
            int redAliveCount,
            int blueAliveCount,
            IEnumerable<SurvivingRowPrediction> survivingRows,
            FirstEliminatedColumnPrediction? firstEliminatedColumn,
            FirstEliminatedColumnPrediction? redFirstEliminatedColumn = null,
            FirstEliminatedColumnPrediction? blueFirstEliminatedColumn = null)
        {
            Winner = winner;
            RemainingTime = Math.Max(0f, remainingTime);
            RedAliveCount = Math.Max(0, redAliveCount);
            BlueAliveCount = Math.Max(0, blueAliveCount);
            var rows = new HashSet<SurvivingRowPrediction>(survivingRows ?? Array.Empty<SurvivingRowPrediction>());
            SurvivingRows = new List<SurvivingRowPrediction>(rows).AsReadOnly();
            FirstEliminatedColumn = firstEliminatedColumn;
            RedFirstEliminatedColumn = redFirstEliminatedColumn;
            BlueFirstEliminatedColumn = blueFirstEliminatedColumn;

            // 기존 생성 호출도 최초 전멸이 발생한 팀의 결과는 보존합니다.
            if ((firstEliminatedColumn == FirstEliminatedColumnPrediction.RedFront) ||
                (firstEliminatedColumn == FirstEliminatedColumnPrediction.RedBack))
            {
                RedFirstEliminatedColumn = redFirstEliminatedColumn ?? firstEliminatedColumn;
            }
            else if ((firstEliminatedColumn == FirstEliminatedColumnPrediction.BlueFront) ||
                     (firstEliminatedColumn == FirstEliminatedColumnPrediction.BlueBack))
            {
                BlueFirstEliminatedColumn = blueFirstEliminatedColumn ?? firstEliminatedColumn;
            }
        }

        /// <summary>지정한 팀의 전투 종료 시 생존 수를 반환합니다.</summary>
        public int GetAliveCount(Team team)
        {
            switch (team)
            {
                case Team.Red:
                    return RedAliveCount;
                case Team.Blue:
                    return BlueAliveCount;
                default:
                    return 0;
            }
        }

        /// <summary>지정한 팀의 첫 전멸 열을 반환하며, 전멸이 없으면 null을 반환합니다.</summary>
        public FirstEliminatedColumnPrediction? GetFirstEliminatedColumn(Team team)
        {
            if (team == Team.Red)
            {
                return RedFirstEliminatedColumn;
            }

            if (team == Team.Blue)
            {
                return BlueFirstEliminatedColumn;
            }

            return null;
        }
    }

    public sealed class BetSettlement
    {
        public bool IsWin { get; }
        public int WagerCall { get; }
        public int Multiplier { get; }
        public int PayoutCall { get; }
        public int NetChange => PayoutCall - WagerCall;
        public IReadOnlyList<string> FailedCategories { get; }
        public IReadOnlyList<BetOutcome> Outcomes { get; }

        /// <summary>지급액과 항목별 판정을 복사하여 UI와 퀘스트에서 같은 결과를 사용합니다.</summary>
        public BetSettlement(
            bool isWin,
            int wagerCall,
            int multiplier,
            int payoutCall,
            IReadOnlyList<string> failedCategories,
            IReadOnlyList<BetOutcome> outcomes = null)
        {
            IsWin = isWin;
            WagerCall = wagerCall;
            Multiplier = multiplier;
            PayoutCall = payoutCall;
            FailedCategories = new List<string>(failedCategories ?? Array.Empty<string>()).AsReadOnly();
            Outcomes = new List<BetOutcome>(outcomes ?? Array.Empty<BetOutcome>()).AsReadOnly();
        }
    }

    public static class BetSettlementService
    {
        /// <summary>모든 선택 항목의 적중과 아이템 보정을 계산하고, 퀘스트용 판정도 보존합니다.</summary>
        public static BetSettlement Settle(RoundBetTicket ticket, CombatResultSnapshot result)
        {
            if (ticket == null)
            {
                throw new ArgumentNullException(nameof(ticket));
            }

            if (result == null)
            {
                throw new ArgumentNullException(nameof(result));
            }

            if (!ticket.IsPlaced)
            {
                throw new InvalidOperationException("확정되지 않은 베팅은 정산할 수 없습니다.");
            }

            if (ticket.IsSettled)
            {
                throw new InvalidOperationException("이미 정산된 베팅입니다.");
            }

            List<string> failed = new List<string>();
            List<BetOutcome> outcomes = BuildOutcomes(ticket, result);
            for (int i = 0; i < outcomes.Count; i++)
            {
                BetOutcome outcome = outcomes[i];
                if (outcome.IsSelected && !outcome.IsMatched)
                {
                    failed.Add(outcome.Category.ToString());
                }
            }

            bool isWin = failed.Count == 0;
            int payout = 0;

            if (isWin)
            {
                int effectiveWager = ticket.WagerCall;
                if (ticket.HasAdditionalBet)
                {
                    effectiveWager += 500;
                }
                payout = checked(effectiveWager * ticket.Multiplier);
            }
            else
            {
                if (ticket.HasInsurance)
                {
                    payout = ticket.WagerCall;
                }
            }

            ticket.MarkSettled();
            return new BetSettlement(isWin, ticket.WagerCall, ticket.Multiplier, payout, failed, outcomes);
        }

        public static RemainingTimePrediction ClassifyRemainingTime(float seconds)
        {
            if (seconds < 5f) return RemainingTimePrediction.Seconds0To5;
            if (seconds < 10f) return RemainingTimePrediction.Seconds5To10;
            if (seconds < 15f) return RemainingTimePrediction.Seconds10To15;
            if (seconds < 20f) return RemainingTimePrediction.Seconds15To20;
            return RemainingTimePrediction.Seconds20OrMore;
        }

        /// <summary>메인과 선택한 팀 기준 서브의 적중 여부를 항목별로 계산합니다.</summary>
        private static List<BetOutcome> BuildOutcomes(RoundBetTicket ticket, CombatResultSnapshot result)
        {
            List<BetOutcome> outcomes = new List<BetOutcome>();

            bool factionSelected = ticket.Faction != FactionPrediction.NotSelected;
            outcomes.Add(new BetOutcome(
                BetCategory.Faction,
                factionSelected,
                factionSelected && MatchesFaction(ticket.Faction, result.Winner)));

            bool remainingTimeSelected = ticket.RemainingTime.HasValue;
            outcomes.Add(new BetOutcome(
                BetCategory.RemainingTime,
                remainingTimeSelected,
                remainingTimeSelected && ticket.RemainingTime.Value == ClassifyRemainingTime(result.RemainingTime)));

            bool oddEvenSelected = ticket.OddEven.HasValue;
            Team betTeam = ticket.BettingTeam;
            bool hasBetTeam = ticket.CanSelectSpecialBets;
            int aliveCount = result.GetAliveCount(betTeam);

            OddEvenPrediction actualOddEven = OddEvenPrediction.Odd;
            if ((aliveCount % 2) == 0)
            {
                actualOddEven = OddEvenPrediction.Even;
            }
            outcomes.Add(new BetOutcome(
                BetCategory.OddEven,
                oddEvenSelected,
                oddEvenSelected && hasBetTeam && (ticket.OddEven.Value == actualOddEven)));

            bool firstColumnSelected = ticket.FirstEliminatedColumn.HasValue;
            outcomes.Add(new BetOutcome(
                BetCategory.FirstEliminatedColumn,
                firstColumnSelected,
                firstColumnSelected && hasBetTeam && (ticket.FirstEliminatedColumn == result.GetFirstEliminatedColumn(betTeam))));

            bool survivingRowSelected = ticket.SurvivingRow.HasValue;
            outcomes.Add(new BetOutcome(
                BetCategory.SurvivingRow,
                survivingRowSelected,
                survivingRowSelected && result.SurvivingRows.Contains(ticket.SurvivingRow.Value)));

            return outcomes;
        }

        /// <summary>메인의 레드·블루·무승부 예측을 전투 승자와 비교합니다.</summary>
        private static bool MatchesFaction(FactionPrediction prediction, Team winner)
        {
            switch (prediction)
            {
                case FactionPrediction.Red:
                    return winner == Team.Red;
                case FactionPrediction.Blue:
                    return winner == Team.Blue;
                case FactionPrediction.Draw:
                    return winner == Team.None;
                default:
                    return false;
            }
        }
    }

    /// <summary>
    /// 스테이지 재화의 유일한 변경 지점입니다.
    /// </summary>
    public sealed class StageSession
    {
        public StageData StageData { get; private set; }
        public int CurrentCall { get; private set; }

        public void Initialize(StageData stageData)
        {
            StageData = stageData ?? throw new ArgumentNullException(nameof(stageData));
            CurrentCall = stageData.InitialCall;
        }

        public bool TryPlaceBet(RoundBetTicket ticket, RoundContext context, out string error)
        {
            if (ticket == null)
            {
                error = "베팅 정보가 없습니다.";
                return false;
            }

            if (!ticket.Validate(StageData, context, CurrentCall, out error)) return false;
            if (ticket.IsPlaced)
            {
                error = "이미 확정된 베팅입니다.";
                return false;
            }

            CurrentCall -= ticket.WagerCall;
            ticket.MarkPlaced();
            return true;
        }

        public void ApplySettlement(BetSettlement settlement)
        {
            if (settlement == null) throw new ArgumentNullException(nameof(settlement));
            CurrentCall = checked(CurrentCall + settlement.PayoutCall);
        }

        public void Clear()
        {
            StageData = null;
            CurrentCall = 0;
        }
    }
}
#endif
