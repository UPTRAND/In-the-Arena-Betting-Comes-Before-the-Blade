using System;
using System.Collections.Generic;

namespace InTheArena.Events.Core
{
    public readonly struct RuleProgressView
    {
        public string RuleInstanceId { get; }
        public int Progress { get; }
        public int Target { get; }
        public bool IsCompleted { get; }

        /// <summary>
        /// 현재 수치와 목표로 변경 불가능한 규칙 진행도 보기를 만듭니다.
        /// </summary>
        public RuleProgressView(string ruleInstanceId, int progress, int target)
        {
            RuleInstanceId = ruleInstanceId;
            Progress = progress;
            Target = target;
            IsCompleted = progress >= target;
        }
    }

    public interface IEventCondition
    {
        int Evaluate(IGameEvent eventData);
    }

    public sealed class EventRuleRegistration
    {
        public string RuleInstanceId { get; }
        public int Target { get; }
        public IEventCondition Condition { get; }
        public Action<RuleProgressView> ProgressChanged { get; }

        /// <summary>
        /// 규칙 ID, 목표, 조건과 진행 알림을 하나의 등록 정보로 묶습니다.
        /// </summary>
        public EventRuleRegistration(
            string ruleInstanceId,
            int target,
            IEventCondition condition,
            Action<RuleProgressView> progressChanged)
        {
            RuleInstanceId = ruleInstanceId;
            Target = target;
            Condition = condition;
            ProgressChanged = progressChanged;
        }
    }

    public interface IEventRuleEngine
    {
        IDisposable RegisterRule(EventRuleRegistration registration);
        RuleProgressView GetProgress(string ruleInstanceId);
        bool TryPrepare(string ruleInstanceId, IGameEvent eventData, out RuleChange change);
        void Commit(RuleChange change);
    }

    public readonly struct RuleChange
    {
        public string RuleInstanceId { get; }
        public string EventId { get; }
        public int PreviousProgress { get; }
        public int NextProgress { get; }

        /// <summary>
        /// 저장 전에 검토할 규칙 진행 변경안을 만듭니다.
        /// </summary>
        public RuleChange(string ruleInstanceId, string eventId, int previousProgress, int nextProgress)
        {
            RuleInstanceId = ruleInstanceId;
            EventId = eventId;
            PreviousProgress = previousProgress;
            NextProgress = nextProgress;
        }
    }

    /// <summary>
    /// 기능 타입과 무관한 조건 진행도, 이벤트 중복 방지, 준비/확정을 담당합니다.
    /// </summary>
    public sealed class EventRuleEngine : IEventRuleEngine
    {
        private readonly Dictionary<string, RuleState> m_Rules = new Dictionary<string, RuleState>(StringComparer.Ordinal);

        /// <summary>
        /// 공통 조건 규칙을 등록하고 규칙 수명과 같은 해제 핸들을 반환합니다.
        /// </summary>
        public IDisposable RegisterRule(EventRuleRegistration registration)
        {
            if (registration == null)
            {
                throw new ArgumentNullException(nameof(registration));
            }

            if (string.IsNullOrWhiteSpace(registration.RuleInstanceId))
            {
                throw new ArgumentException("Rule instance id is required.", nameof(registration));
            }

            if (registration.Target <= 0 || registration.Condition == null)
            {
                throw new ArgumentException("A positive target and condition are required.", nameof(registration));
            }

            RuleState state = new RuleState(registration);
            m_Rules.Add(registration.RuleInstanceId, state);
            return new RuleHandle(this, registration.RuleInstanceId);
        }

        /// <summary>
        /// 현재 규칙 진행도를 읽기 전용 값으로 반환합니다.
        /// </summary>
        public RuleProgressView GetProgress(string ruleInstanceId)
        {
            if (!m_Rules.TryGetValue(ruleInstanceId, out RuleState state))
            {
                return new RuleProgressView(ruleInstanceId, 0, 0);
            }

            return state.View;
        }

        /// <summary>
        /// 원본 상태를 바꾸지 않고 이벤트에 대한 진행 변경안을 만듭니다.
        /// </summary>
        public bool TryPrepare(string ruleInstanceId, IGameEvent eventData, out RuleChange change)
        {
            change = default;
            if (eventData == null || !m_Rules.TryGetValue(ruleInstanceId, out RuleState state))
            {
                return false;
            }

            if (state.ConsumedEventIds.Contains(eventData.EventId) || state.View.IsCompleted)
            {
                return false;
            }

            int delta = state.Registration.Condition.Evaluate(eventData);
            if (delta <= 0)
            {
                return false;
            }

            int next = Math.Min(state.Registration.Target, checked(state.Progress + delta));
            change = new RuleChange(ruleInstanceId, eventData.EventId, state.Progress, next);
            return true;
        }

        /// <summary>
        /// 저장 성공 후 준비된 변경안을 규칙 상태에 확정합니다.
        /// </summary>
        public void Commit(RuleChange change)
        {
            if (!m_Rules.TryGetValue(change.RuleInstanceId, out RuleState state))
            {
                return;
            }

            if (state.ConsumedEventIds.Contains(change.EventId) || state.Progress != change.PreviousProgress)
            {
                return;
            }

            state.Progress = change.NextProgress;
            state.ConsumedEventIds.Add(change.EventId);
            state.Registration.ProgressChanged?.Invoke(state.View);
        }

        /// <summary>
        /// 수명이 끝난 규칙 상태를 등록표에서 제거합니다.
        /// </summary>
        private void Remove(string ruleInstanceId)
        {
            m_Rules.Remove(ruleInstanceId);
        }

        private sealed class RuleState
        {
            public EventRuleRegistration Registration { get; }
            public HashSet<string> ConsumedEventIds { get; } = new HashSet<string>(StringComparer.Ordinal);
            public int Progress { get; set; }
            public RuleProgressView View => new RuleProgressView(Registration.RuleInstanceId, Progress, Registration.Target);

            /// <summary>
            /// 등록 정보로 내부 진행 상태를 초기화합니다.
            /// </summary>
            public RuleState(EventRuleRegistration registration)
            {
                Registration = registration;
            }
        }

        private sealed class RuleHandle : IDisposable
        {
            private EventRuleEngine m_Owner;
            private readonly string m_RuleInstanceId;

            /// <summary>
            /// 해제할 엔진과 규칙 ID를 보관합니다.
            /// </summary>
            public RuleHandle(EventRuleEngine owner, string ruleInstanceId)
            {
                m_Owner = owner;
                m_RuleInstanceId = ruleInstanceId;
            }

            /// <summary>
            /// 등록한 규칙을 한 번만 제거합니다.
            /// </summary>
            public void Dispose()
            {
                if (m_Owner == null)
                {
                    return;
                }

                m_Owner.Remove(m_RuleInstanceId);
                m_Owner = null;
            }
        }
    }
}
