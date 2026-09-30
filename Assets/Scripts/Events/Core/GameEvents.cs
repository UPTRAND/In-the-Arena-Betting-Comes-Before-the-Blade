using System;
using InTheArena.MainGame;

namespace InTheArena.Events.Core
{
    public abstract class GameEventBase : IGameEvent
    {
        public string EventId { get; }
        public long OccurredAtUtcTicks { get; }

        /// <summary>
        /// 모든 사실 이벤트에 고유 ID와 UTC 발생 시각을 기록합니다.
        /// </summary>
        protected GameEventBase(string eventId, long occurredAtUtcTicks)
        {
            EventId = string.IsNullOrWhiteSpace(eventId) ? Guid.NewGuid().ToString("N") : eventId;
            OccurredAtUtcTicks = occurredAtUtcTicks;
        }
    }

    public sealed class ApplicationReadyEvent : GameEventBase
    {
        public string ApplicationRunId { get; }

        /// <summary>
        /// 저장과 기능 서비스 구성이 끝난 앱 실행 사실을 만듭니다.
        /// </summary>
        public ApplicationReadyEvent(string eventId, long occurredAtUtcTicks, string applicationRunId)
            : base(eventId, occurredAtUtcTicks)
        {
            ApplicationRunId = applicationRunId;
        }
    }

    public sealed class StageStartedEvent : GameEventBase
    {
        public string StageRunId { get; }
        public int StageNumber { get; }

        /// <summary>
        /// 저장에 확정된 스테이지 도전 시작 사실을 만듭니다.
        /// </summary>
        public StageStartedEvent(string eventId, long occurredAtUtcTicks, string stageRunId, int stageNumber)
            : base(eventId, occurredAtUtcTicks)
        {
            StageRunId = stageRunId;
            StageNumber = stageNumber;
        }
    }

    public sealed class BetPlacedEvent : GameEventBase
    {
        public string StageRunId { get; }
        public int RoundId { get; }
        public int WagerCall { get; }

        /// <summary>
        /// 실제 원금 차감이 끝난 베팅 확정 사실을 만듭니다.
        /// </summary>
        public BetPlacedEvent(string eventId, long occurredAtUtcTicks, string stageRunId, int roundId, int wagerCall)
            : base(eventId, occurredAtUtcTicks)
        {
            StageRunId = stageRunId;
            RoundId = roundId;
            WagerCall = wagerCall;
        }
    }

    public sealed class BetSettledEvent : GameEventBase
    {
        public string StageRunId { get; }
        public int RoundId { get; }
        public FactionPrediction SelectedFaction { get; }
        public Team Winner { get; }
        public BetSettlement Settlement { get; }

        /// <summary>
        /// 항목별 적중과 지급액이 확정된 베팅 정산 사실을 만듭니다.
        /// </summary>
        public BetSettledEvent(
            string eventId,
            long occurredAtUtcTicks,
            string stageRunId,
            int roundId,
            FactionPrediction selectedFaction,
            Team winner,
            BetSettlement settlement)
            : base(eventId, occurredAtUtcTicks)
        {
            StageRunId = stageRunId;
            RoundId = roundId;
            SelectedFaction = selectedFaction;
            Winner = winner;
            Settlement = settlement;
        }
    }

    public sealed class ItemUseCommittedEvent : GameEventBase
    {
        public string StageRunId { get; }
        public int RoundId { get; }
        public ItemType ItemType { get; }
        public string UseId { get; }

        /// <summary>
        /// 효과와 아이템 소비 저장이 모두 성공한 사용 사실을 만듭니다.
        /// </summary>
        public ItemUseCommittedEvent(
            string eventId,
            long occurredAtUtcTicks,
            string stageRunId,
            int roundId,
            ItemType itemType,
            string useId)
            : base(eventId, occurredAtUtcTicks)
        {
            StageRunId = stageRunId;
            RoundId = roundId;
            ItemType = itemType;
            UseId = useId;
        }
    }

    public enum StageEndReason
    {
        Cleared,
        Defeated,
        UserExit,
        InterruptedRecovery,
        Error
    }

    public sealed class StageClearedEvent : GameEventBase
    {
        public string StageRunId { get; }
        public int StageNumber { get; }

        /// <summary>
        /// 보상 후보에 함께 저장할 스테이지 클리어 사실을 만듭니다.
        /// </summary>
        public StageClearedEvent(string eventId, long occurredAtUtcTicks, string stageRunId, int stageNumber)
            : base(eventId, occurredAtUtcTicks)
        {
            StageRunId = stageRunId;
            StageNumber = stageNumber;
        }
    }

    public sealed class StageEndedEvent : GameEventBase
    {
        public string StageRunId { get; }
        public StageEndReason Reason { get; }

        /// <summary>
        /// 실패·이탈·중단 복구를 포함한 도전 종료 사실을 만듭니다.
        /// </summary>
        public StageEndedEvent(string eventId, long occurredAtUtcTicks, string stageRunId, StageEndReason reason)
            : base(eventId, occurredAtUtcTicks)
        {
            StageRunId = stageRunId;
            Reason = reason;
        }
    }

    public sealed class StageReturnedToLobbyEvent : GameEventBase
    {
        public string ReturnId { get; }
        public string StageRunId { get; }
        public int StageNumber { get; }

        /// <summary>
        /// 저장된 클리어 영수증이 로비에서 한 번 처리된 사실을 만듭니다.
        /// </summary>
        public StageReturnedToLobbyEvent(
            string eventId,
            long occurredAtUtcTicks,
            string returnId,
            string stageRunId,
            int stageNumber)
            : base(eventId, occurredAtUtcTicks)
        {
            ReturnId = returnId;
            StageRunId = stageRunId;
            StageNumber = stageNumber;
        }
    }
}
