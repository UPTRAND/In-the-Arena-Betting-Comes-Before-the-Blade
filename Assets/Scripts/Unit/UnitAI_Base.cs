#if UNITY_6000_0_OR_NEWER
using UnityEngine;
using System;
using MackySoft.SerializeReferenceExtensions;

namespace InTheArena.Unit
{
    /// <summary>
    /// AI 상태 열거형
    /// </summary>
    public enum AIState
    {
        Idle = 0,           // 대기
        SearchTarget = 1,   // 타겟 탐색
        MoveToTarget = 2,   // 타겟에게 이동
        Attack = 3,         // 공격 중
        UseSkill = 4,       // 스킬 사용 중
        Dead = 5            // 사망
    }

    /// <summary>
    /// AI 타겟 우선순위 타입
    /// </summary>
    public enum TargetPriorityType
    {
        Nearest = 0,        // 가장 가까운 적
        LowestHp = 1,       // 체력이 가장 낮은 적
        HighestThreat = 2,  // 위협도 가장 높은 적 (데미지 누적 등)
        Random = 3          // 무작위
    }

    /// <summary>
    /// 유닛 AI 기본 추상 클래스
    /// ScriptableObject 상속 안 함, [Serializable]로 SerializeReference 지원
    /// </summary>
    [Serializable]
    public abstract class UnitAI_Base
    {
        [Header("AI 기본 설정")]
        [Tooltip("타겟 탐색 주기 (초)")]
        [SerializeField] protected float m_SearchInterval = 0.5f;

        [Tooltip("타겟 우선순위 타입")]
        [SerializeField] protected TargetPriorityType m_TargetPriority = TargetPriorityType.Nearest;

        [Tooltip("최대 탐색 거리 (0 이하면 무제한)")]
        [SerializeField] protected float m_MaxSearchDistance = 0f;

        [Header("공격 설정")]
        [Tooltip("공격 시 정지 거리 여유 (공격 범위 * 이 비율)")]
        [SerializeField] [Range(0f, 1f)] protected float m_AttackStopDistanceRatio = 0.9f;

        [Header("스킬 설정")]
        [Tooltip("스킬 사용 시 기본 공격 중단 여부")]
        [SerializeField] protected bool m_InterruptAttackForSkill = true;

        /// <summary> 소유 유닛 (런타임에 설정) </summary>
        protected Unit m_Owner;

        /// <summary> 현재 타겟 </summary>
        protected Unit m_CurrentTarget;

        /// <summary> 현재 AI 상태 </summary>
        protected AIState m_CurrentState = AIState.Idle;

        /// <summary> 다음 탐색 가능 시간 </summary>
        protected float m_NextSearchTime;

        /// <summary> 현재 상태 지속 시간 </summary>
        protected float m_StateTimer;

        /// <summary> 다음 공격 가능 시간 </summary>
        protected float m_NextAttackTime;

        /// <summary> AI 활성화 여부 </summary>
        protected bool m_IsActive = false;

        /// <summary> 현재 상태 </summary>
        public AIState CurrentState => m_CurrentState;

        /// <summary> 현재 타겟 </summary>
        public Unit CurrentTarget => m_CurrentTarget;

        /// <summary>
        /// 복제하여 런타임 인스턴스 생성
        /// </summary>
        public abstract UnitAI_Base Clone();

        /// <summary>
        /// AI 초기화 (유닛 스폰 시 호출)
        /// </summary>
        /// <param name="owner">소유 유닛</param>
        public virtual void Initialize(Unit owner)
        {
            m_Owner = owner;
            m_CurrentState = AIState.Idle;
            m_CurrentTarget = null;
            m_NextSearchTime = 0f;
            m_StateTimer = 0f;
            m_IsActive = true;
            OnInitialize();
        }

        /// <summary>
        /// 초기화 후 추가 로직 (자식에서 오버라이드)
        /// </summary>
        protected virtual void OnInitialize() { }

        /// <summary>
        /// AI 메인 업데이트 (매 프레임 호출)
        /// </summary>
        /// <param name="deltaTime">델타 타임</param>
        public virtual void UpdateAI(float deltaTime)
        {
            if (!m_IsActive || m_Owner == null || m_Owner.IsDead)
            {
                return;
            }

            m_StateTimer += deltaTime;
            OnUpdateAI(deltaTime);
        }

        /// <summary>
        /// AI 업데이트 로직 (자식에서 구현)
        /// </summary>
        protected abstract void OnUpdateAI(float deltaTime);

        /// <summary>
        /// 타겟 탐색 및 설정
        /// </summary>
        /// <returns>타겟을 찾았으면 true</returns>
        protected bool SearchAndSetTarget()
        {
            if (m_Owner == null) return false;
            Unit bestTarget = UnitRegistry.FindBestTarget(m_Owner, m_TargetPriority, m_MaxSearchDistance);
            if (bestTarget != m_CurrentTarget)
            {
                m_CurrentTarget = bestTarget;
                OnTargetChanged(bestTarget);
            }

            return m_CurrentTarget != null;
        }

        /// <summary>
        /// 타겟 후보군 획득 (자식에서 오버라이드하여 팀 필터링 등 구현)
        /// </summary>
        /// <summary>
        /// 타겟 변경 시 콜백
        /// </summary>
        protected virtual void OnTargetChanged(Unit newTarget) { }

        /// <summary>
        /// 현재 상태 설정
        /// </summary>
        protected void SetState(AIState newState)
        {
            if (m_CurrentState == newState) return;

            m_CurrentState = newState;
            m_StateTimer = 0f;
            OnStateChanged(newState);
        }

        /// <summary>
        /// 상태 변경 시 콜백
        /// </summary>
        protected virtual void OnStateChanged(AIState newState) { }

        /// <summary>
        /// AI 비활성화
        /// </summary>
        public virtual void Deactivate()
        {
            m_IsActive = false;
            m_CurrentTarget = null;
            m_CurrentState = AIState.Dead;
        }

        /// <summary>
        /// 전투 시작 전 스폰 연출 중 AI 판단을 일시 정지합니다.
        /// </summary>
        public virtual void Pause()
        {
            m_IsActive = false;
        }

        /// <summary>
        /// 전투 시작 시 AI 판단을 재개합니다.
        /// </summary>
        public virtual void Resume()
        {
            if (m_Owner == null || m_Owner.IsDead) return;

            m_CurrentTarget = null;
            m_CurrentState = AIState.Idle;
            m_StateTimer = 0f;
            m_NextSearchTime = 0f;
            m_IsActive = true;
        }

        /// <summary>
        /// 타겟이 공격 범위 내에 있는지 확인
        /// </summary>
        protected bool IsTargetInAttackRange()
        {
            if (m_CurrentTarget == null) return false;

            float attackRange = m_Owner.CurrentAttackRange * m_AttackStopDistanceRatio;
            Vector3 delta = m_Owner.GroundPosition - m_CurrentTarget.GroundPosition;
            delta.y = 0f;
            return delta.sqrMagnitude <= attackRange * attackRange;
        }

        /// <summary>
        /// 타겟 방향으로 회전
        /// </summary>
        protected void FaceTarget()
        {
            if (m_CurrentTarget == null) return;

            Vector3 direction = (m_CurrentTarget.transform.position - m_Owner.transform.position).normalized;
            direction.y = 0f;

            if (direction != Vector3.zero)
            {
                Quaternion targetRotation = Quaternion.LookRotation(direction);
                m_Owner.transform.rotation = Quaternion.Slerp(
                    m_Owner.transform.rotation,
                    targetRotation,
                    0.5f);
            }
        }
    }
}
#endif
