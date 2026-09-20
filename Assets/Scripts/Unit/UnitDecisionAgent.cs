#if UNITY_6000_0_OR_NEWER
using UnityEngine;

namespace InTheArena.Unit
{
    public enum AIState
    {
        Idle = 0,
        SearchTarget = 1,
        MoveToTarget = 2,
        Attack = 3,
        UseSkill = 4,
        Dead = 5
    }

    public enum TargetPriorityType
    {
        Nearest = 0,
        LowestHp = 1,
        HighestThreat = 2,
        Random = 3
    }

    public sealed class UnitDecisionAgent
    {
        private readonly TargetPriorityType m_TargetPriority;
        private readonly float m_SearchInterval;
        private readonly float m_MaxSearchDistance;
        private readonly float m_AttackRangeRatio;
        private readonly float m_InitialSearchDelay;
        private Unit m_Owner;
        private UnitHandle m_Target;
        private float m_InitialDelayRemaining;
        private float m_SearchRetryRemaining;
        private bool m_IsActive;

        public UnitDecisionAgent(AIData data)
        {
            m_TargetPriority = data != null ? data.TargetPriority : TargetPriorityType.Nearest;
            m_SearchInterval = data != null ? data.SearchInterval : 0.5f;
            m_MaxSearchDistance = data != null ? data.MaxSearchDistance : 0f;
            m_AttackRangeRatio = data != null ? data.AttackStopDistanceRatio : 0.9f;
            m_InitialSearchDelay = data != null ? data.InitialSearchDelay : 0.1f;
        }

        public AIState CurrentState { get; private set; } = AIState.Idle;
        public Unit CurrentTarget => m_Target.Unit;

        public void Initialize(Unit owner)
        {
            m_Owner = owner;
            m_Target = default;
            m_InitialDelayRemaining = m_InitialSearchDelay;
            m_SearchRetryRemaining = 0f;
            CurrentState = AIState.Idle;
            m_IsActive = true;
        }

        /// <summary>적 탐색 주기를 유지하면서 독립적으로 사용 가능한 스킬도 판단합니다.</summary>
        public void UpdateAI(float deltaTime)
        {
            if (!m_IsActive || m_Owner == null || m_Owner.IsDead)
            {
                return;
            }
            if (m_InitialDelayRemaining > 0f)
            {
                m_InitialDelayRemaining = Mathf.Max(0f, m_InitialDelayRemaining - deltaTime);
                return;
            }
            if (m_SearchRetryRemaining > 0f)
            {
                m_SearchRetryRemaining = Mathf.Max(0f, m_SearchRetryRemaining - deltaTime);
            }

            Unit target = CurrentTarget;
            if (target == null || target.IsDead || target.Team == m_Owner.Team ||
                !target.gameObject.activeInHierarchy)
            {
                CurrentState = AIState.SearchTarget;
                if (m_SearchRetryRemaining > 0f)
                {
                    UnitIntent waitingIntent = DecisionSystem.Decide(m_Owner, null, m_AttackRangeRatio);
                    Execute(in waitingIntent);
                    return;
                }
                target = UnitRegistry.FindBestTarget(
                    m_Owner,
                    m_TargetPriority,
                    m_MaxSearchDistance);
                m_Target = new UnitHandle(target);
                m_SearchRetryRemaining = 0f;
                if (target == null)
                {
                    m_SearchRetryRemaining = m_SearchInterval;
                }
            }

            UnitIntent intent = DecisionSystem.Decide(m_Owner, target, m_AttackRangeRatio);
            Execute(in intent);
        }

        public void Pause()
        {
            m_IsActive = false;
            m_Owner?.StopMovement();
        }

        public void Resume()
        {
            if (m_Owner == null || m_Owner.IsDead) return;
            m_IsActive = true;
            CurrentState = AIState.Idle;
        }

        public void Deactivate()
        {
            m_IsActive = false;
            m_Target = default;
            CurrentState = AIState.Dead;
        }

        private void Execute(in UnitIntent intent)
        {
            Unit target = intent.Target.Unit;
            switch (intent.Type)
            {
                case UnitIntentType.Move:
                    CurrentState = AIState.MoveToTarget;
                    m_Owner.MoveTo(
                        intent.Destination,
                        EngagementSlotSystem.ArrivalTolerance);
                    break;
                case UnitIntentType.CastSkill:
                    CurrentState = AIState.UseSkill;
                    m_Owner.StopMovement();
                    if (!m_Owner.TryUseSkill(target) && m_Owner.CanAttack)
                        m_Owner.TryAttack(target);
                    break;
                case UnitIntentType.BasicAttack:
                    CurrentState = AIState.Attack;
                    m_Owner.StopMovement();
                    if (m_Owner.CanAttack) m_Owner.TryAttack(target);
                    break;
                case UnitIntentType.AcquireTarget:
                    CurrentState = AIState.Idle;
                    m_Owner.StopMovement();
                    break;
                case UnitIntentType.Hold:
                default:
                    m_Owner.StopMovement();
                    break;
            }
        }
    }
}
#endif
