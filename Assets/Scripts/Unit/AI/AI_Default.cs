#if UNITY_6000_0_OR_NEWER
using System;
using UnityEngine;

namespace InTheArena.Unit
{
    /// <summary>
    /// 기본 AI: 가장 가까운 적을 찾아 이동하여 공격, 도망 안 감
    /// </summary>
    [Serializable]
    public class UnitAI_Default : UnitAI_Base
    {
        [Header("기본 AI 설정")]
        [Tooltip("전투 시작 후 첫 타겟 탐색까지 대기 시간")]
        [SerializeField] private float m_InitialSearchDelay = 0.1f;

        private bool m_HasSearchedInitially = false;

        public override UnitAI_Base Clone()
        {
            var clone = new UnitAI_Default();
            clone.m_SearchInterval = m_SearchInterval;
            clone.m_TargetPriority = m_TargetPriority;
            clone.m_MaxSearchDistance = m_MaxSearchDistance;
            clone.m_AttackStopDistanceRatio = m_AttackStopDistanceRatio;
            clone.m_InterruptAttackForSkill = m_InterruptAttackForSkill;
            clone.m_InitialSearchDelay = m_InitialSearchDelay;
            return clone;
        }

        protected override void OnInitialize()
        {
            m_HasSearchedInitially = false;
        }

        protected override void OnUpdateAI(float deltaTime)
        {
            // 사망 상태면 처리 안 함
            if (m_CurrentState == AIState.Dead) return;

            // 초기 탐색 지연
            if (!m_HasSearchedInitially)
            {
                if (m_StateTimer >= m_InitialSearchDelay)
                {
                    m_HasSearchedInitially = true;
                    m_StateTimer = 0f;
                    SetState(AIState.SearchTarget);
                }
                return;
            }

            // 상태 머신
            switch (m_CurrentState)
            {
                case AIState.Idle:
                    SetState(AIState.SearchTarget);
                    break;

                case AIState.SearchTarget:
                    UpdateSearchTarget();
                    break;

                case AIState.MoveToTarget:
                    UpdateMoveToTarget();
                    break;

                case AIState.Attack:
                    UpdateAttack();
                    break;

                case AIState.UseSkill:
                    UpdateUseSkill();
                    break;
            }
        }

        private void UpdateSearchTarget()
        {
            // 타겟이 없거나 유효하지 않으면 새로 탐색
            if (m_CurrentTarget == null || m_CurrentTarget.IsDead || !IsTargetInAttackRange())
            {
                if (SearchAndSetTarget())
                {
                    // 타겟이 사거리 내면 바로 공격, 아니면 이동
                    if (IsTargetInAttackRange())
                    {
                        SetState(AIState.Attack);
                    }
                    else
                    {
                        SetState(AIState.MoveToTarget);
                    }
                }
                else
                {
                    // 타겟 없으면 대기
                    SetState(AIState.Idle);
                }
            }
            else
            {
                // 타겟이 유효하면 사거리 체크
                if (IsTargetInAttackRange())
                {
                    SetState(AIState.Attack);
                }
                else
                {
                    SetState(AIState.MoveToTarget);
                }
            }
        }

        private void UpdateMoveToTarget()
        {
            if (m_CurrentTarget == null || m_CurrentTarget.IsDead)
            {
                SetState(AIState.SearchTarget);
                return;
            }

            // 타겟이 사거리 내로 들어왔으면 공격
            if (IsTargetInAttackRange())
            {
                SetState(AIState.Attack);
                return;
            }

            // 타겟이 너무 멀어지면 다시 탐색
            float distance = Vector3.Distance(m_Owner.transform.position, m_CurrentTarget.transform.position);
            if (distance > m_MaxSearchDistance && m_MaxSearchDistance > 0f)
            {
                SetState(AIState.SearchTarget);
                return;
            }

            // 이동
            FaceTarget();
            if (!m_Owner.IsMoving)
            {
                float stopDistance = m_Owner.CurrentAttackRange * m_AttackStopDistanceRatio;
                m_Owner.MoveTo(m_CurrentTarget.transform.position, stopDistance);
            }
        }

        private void UpdateAttack()
        {
            if (m_CurrentTarget == null || m_CurrentTarget.IsDead)
            {
                SetState(AIState.SearchTarget);
                return;
            }

            // 타겟이 범위 밖으로 나가면 이동
            if (!IsTargetInAttackRange())
            {
                SetState(AIState.MoveToTarget);
                return;
            }

            FaceTarget();
            m_Owner.StopMovement();

            // 공격 쿨타임 체크
            if (Time.time >= m_NextAttackTime)
            {
                m_Owner.Attack(m_CurrentTarget);
                m_NextAttackTime = Time.time + m_Owner.CurrentStat.AttackInterval;
            }
        }

        private void UpdateUseSkill()
        {
            // 스킬 사용 중...
            // 스킬 완료 시 공격 또는 탐색으로 복귀
        }

    }
}
#endif
