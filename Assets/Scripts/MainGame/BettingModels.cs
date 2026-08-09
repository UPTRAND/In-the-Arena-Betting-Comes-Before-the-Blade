#if UNITY_6000_0_OR_NEWER
using System;
using System.Collections.Generic;

namespace InTheArena.MainGame
{
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

        public int Multiplier => SelectedCategoryCount switch
        {
            1 => 2,
            2 => 4,
            3 => 8,
            4 => 16,
            _ => 0
        };

        public void SetWager(int wagerCall) => WagerCall = wagerCall;
        public void SetFaction(FactionPrediction faction) => Faction = faction;
        public void SetRemainingTime(RemainingTimePrediction? prediction) => RemainingTime = prediction;
        public void SetOddEven(OddEvenPrediction? prediction) => OddEven = prediction;

        public void SetFirstEliminatedColumn(FirstEliminatedColumnPrediction? prediction) =>
            FirstEliminatedColumn = prediction;

        public void SetSurvivingRow(SurvivingRowPrediction? prediction) => SurvivingRow = prediction;

        public void ClearSpecialPredictions()
        {
            RemainingTime = null;
            OddEven = null;
            FirstEliminatedColumn = null;
            SurvivingRow = null;
        }

        public void SetItemUsages(bool hasAdditionalBet, bool hasInsurance)
        {
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

            if (WagerCall < 1 || WagerCall > availableCall)
            {
                error = $"베팅액은 1~{availableCall} Call 범위여야 합니다.";
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

        internal void MarkPlaced() => IsPlaced = true;
        internal void MarkSettled() => IsSettled = true;
    }

    public sealed class CombatResultSnapshot
    {
        public Team Winner { get; }
        public float RemainingTime { get; }
        public int RedAliveCount { get; }
        public int BlueAliveCount { get; }
        public HashSet<SurvivingRowPrediction> SurvivingRows { get; }
        public FirstEliminatedColumnPrediction? FirstEliminatedColumn { get; }

        public int TotalAliveCount => RedAliveCount + BlueAliveCount;

        public CombatResultSnapshot(
            Team winner,
            float remainingTime,
            int redAliveCount,
            int blueAliveCount,
            IEnumerable<SurvivingRowPrediction> survivingRows,
            FirstEliminatedColumnPrediction? firstEliminatedColumn)
        {
            Winner = winner;
            RemainingTime = Math.Max(0f, remainingTime);
            RedAliveCount = Math.Max(0, redAliveCount);
            BlueAliveCount = Math.Max(0, blueAliveCount);
            SurvivingRows = new HashSet<SurvivingRowPrediction>(
                survivingRows ?? Array.Empty<SurvivingRowPrediction>());
            FirstEliminatedColumn = firstEliminatedColumn;
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
            FailedCategories = failedCategories ?? Array.Empty<string>();
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
                bool isEven = result.TotalAliveCount % 2 == 0;
                bool matched = ticket.OddEven.Value == (isEven ? OddEvenPrediction.Even : OddEvenPrediction.Odd);
                if (!matched) failed.Add("OddEven");
            }

            if (ticket.FirstEliminatedColumn.HasValue &&
                ticket.FirstEliminatedColumn != result.FirstEliminatedColumn)
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
