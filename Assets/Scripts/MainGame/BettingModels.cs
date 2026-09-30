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

        public Team BettingTeam => Faction switch
        {
            FactionPrediction.Red => Team.Red,
            FactionPrediction.Blue => Team.Blue,
            _ => Team.None
        };
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

        public int Multiplier => Faction == FactionPrediction.NotSelected ? 0
            : Faction == FactionPrediction.Draw ? 3 : SelectedCategoryCount switch
        {
            1 => 2,
            2 => 4,
            3 => 8,
            4 => 16,
            _ => 0
        };

        /// <summary>확정 전 베팅 선택을 변경합니다.</summary>
        public void SetWager(int wagerCall)
        {
            EnsureEditable();
            WagerCall = wagerCall;
        }
        /// <summary>확정 전 베팅 선택을 변경합니다.</summary>
        public void SetFaction(FactionPrediction faction)
        {
            EnsureEditable();
            if (faction != FactionPrediction.Red && faction != FactionPrediction.Blue &&
                faction != FactionPrediction.Draw && faction != FactionPrediction.NotSelected)
                throw new ArgumentOutOfRangeException(nameof(faction));

            if (faction == FactionPrediction.NotSelected || faction == FactionPrediction.Draw)
            {
                ClearSpecialPredictions();
            }
            else if (Faction != faction)
            {
                if (SurvivingRow.HasValue)
                    SurvivingRow = (SurvivingRowPrediction)((int)SurvivingRow.Value % 3 +
                        (faction == FactionPrediction.Blue ? 3 : 0));
                if (FirstEliminatedColumn.HasValue)
                    FirstEliminatedColumn = (FirstEliminatedColumnPrediction)((int)FirstEliminatedColumn.Value % 2 +
                        (faction == FactionPrediction.Blue ? 2 : 0));
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

            if (Faction == FactionPrediction.NotSelected)
            {
                error = "필수 메인 베팅에서 레드·블루·무승부를 선택해야 합니다.";
                return false;
            }

            if (Faction == FactionPrediction.Draw && SelectedCategoryCount != 1)
            {
                error = "무승부는 서브 베팅을 할 수 없습니다.";
                return false;
            }

            if (SelectedCategoryCount < 1 || SelectedCategoryCount > 4)
            {
                error = "베팅 항목은 1~4개를 선택해야 합니다.";
                return false;
            }

            if (Faction != FactionPrediction.NotSelected && !stageData.EnableFactionBet)
            {
                error = "이 스테이지에서는 진영 베팅을 제공하지 않습니다.";
                return false;
            }

            if (SurvivingRow.HasValue && ((int)SurvivingRow.Value < 0 || (int)SurvivingRow.Value > 5 ||
                    ((int)SurvivingRow.Value < 3 ? Team.Red : Team.Blue) != BettingTeam) ||
                FirstEliminatedColumn.HasValue && ((int)FirstEliminatedColumn.Value < 0 || (int)FirstEliminatedColumn.Value > 3 ||
                    ((int)FirstEliminatedColumn.Value < 2 ? Team.Red : Team.Blue) != BettingTeam))
            {
                error = "서브 베팅은 메인 베팅한 팀을 기준으로 선택해야 합니다.";
                return false;
            }

            if (RemainingTime.HasValue && !context.ActiveSpecialBets.Contains(SpecialBetType.RemainingTime) ||
                OddEven.HasValue && !context.ActiveSpecialBets.Contains(SpecialBetType.OddEven) ||
                FirstEliminatedColumn.HasValue && !context.ActiveSpecialBets.Contains(SpecialBetType.FirstEliminatedColumn) ||
                SurvivingRow.HasValue && !context.ActiveSpecialBets.Contains(SpecialBetType.SurvivingRow))
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
            RedFirstEliminatedColumn = redFirstEliminatedColumn ??
                (firstEliminatedColumn.HasValue && (int)firstEliminatedColumn.Value < 2 ? firstEliminatedColumn : null);
            BlueFirstEliminatedColumn = blueFirstEliminatedColumn ??
                (firstEliminatedColumn.HasValue && (int)firstEliminatedColumn.Value >= 2 ? firstEliminatedColumn : null);
        }

        public int GetAliveCount(Team team) => team switch
        {
            Team.Red => RedAliveCount,
            Team.Blue => BlueAliveCount,
            _ => 0
        };

        public FirstEliminatedColumnPrediction? GetFirstEliminatedColumn(Team team) => team switch
        {
            Team.Red => RedFirstEliminatedColumn,
            Team.Blue => BlueFirstEliminatedColumn,
            _ => null
        };
    }

    public sealed class BetSettlement
    {
        public bool IsWin { get; }
        public int WagerCall { get; }
        public int Multiplier { get; }
        public int PayoutCall { get; }
        public int NetChange => PayoutCall - WagerCall;
        public IReadOnlyList<string> FailedCategories { get; }

        public BetSettlement(
            bool isWin,
            int wagerCall,
            int multiplier,
            int payoutCall,
            IReadOnlyList<string> failedCategories)
        {
            IsWin = isWin;
            WagerCall = wagerCall;
            Multiplier = multiplier;
            PayoutCall = payoutCall;
            FailedCategories = new List<string>(failedCategories ?? Array.Empty<string>()).AsReadOnly();
        }
    }

    public static class BetSettlementService
    {
        public static BetSettlement Settle(RoundBetTicket ticket, CombatResultSnapshot result)
        {
            if (ticket == null) throw new ArgumentNullException(nameof(ticket));
            if (result == null) throw new ArgumentNullException(nameof(result));
            if (!ticket.IsPlaced) throw new InvalidOperationException("확정되지 않은 베팅은 정산할 수 없습니다.");
            if (ticket.IsSettled) throw new InvalidOperationException("이미 정산된 베팅입니다.");

            var failed = new List<string>();

            if (ticket.Faction != FactionPrediction.NotSelected &&
                !MatchesFaction(ticket.Faction, result.Winner))
            {
                failed.Add("Faction");
            }

            if (ticket.RemainingTime.HasValue &&
                ticket.RemainingTime.Value != ClassifyRemainingTime(result.RemainingTime))
            {
                failed.Add("RemainingTime");
            }

            if (ticket.OddEven.HasValue)
            {
                bool isEven = result.GetAliveCount(ticket.BettingTeam) % 2 == 0;
                bool matched = ticket.OddEven.Value == (isEven ? OddEvenPrediction.Even : OddEvenPrediction.Odd);
                if (!matched) failed.Add("OddEven");
            }

            if (ticket.FirstEliminatedColumn.HasValue &&
                ticket.FirstEliminatedColumn != result.GetFirstEliminatedColumn(ticket.BettingTeam))
            {
                failed.Add("FirstEliminatedColumn");
            }

            if (ticket.SurvivingRow.HasValue &&
                !result.SurvivingRows.Contains(ticket.SurvivingRow.Value))
            {
                failed.Add("SurvivingRow");
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
            return new BetSettlement(isWin, ticket.WagerCall, ticket.Multiplier, payout, failed);
        }

        public static RemainingTimePrediction ClassifyRemainingTime(float seconds)
        {
            if (seconds < 5f) return RemainingTimePrediction.Seconds0To5;
            if (seconds < 10f) return RemainingTimePrediction.Seconds5To10;
            if (seconds < 15f) return RemainingTimePrediction.Seconds10To15;
            if (seconds < 20f) return RemainingTimePrediction.Seconds15To20;
            return RemainingTimePrediction.Seconds20OrMore;
        }

        private static bool MatchesFaction(FactionPrediction prediction, Team winner)
        {
            return prediction switch
            {
                FactionPrediction.Red => winner == Team.Red,
                FactionPrediction.Blue => winner == Team.Blue,
                FactionPrediction.Draw => winner == Team.None,
                _ => false
            };
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
