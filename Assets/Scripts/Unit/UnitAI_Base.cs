#if UNITY_6000_0_OR_NEWER
using UnityEngine;
using System.Collections.Generic;

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
    /// 모든 AI 공통 기능(탐색, 공격/스킬 알고리즘) 포함
    /// </summary>
    public abstract class UnitAI_Base : ScriptableObject
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

        /// <summary> AI 활성화 여부 </summary>
        protected bool m_IsActive = false;

        /// <summary> 현재 상태 </summary>
        public AIState CurrentState => m_CurrentState;

        /// <summary> 현재 타겟 </summary>
        public Unit CurrentTarget => m_CurrentTarget;

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

            List<Unit> candidates = GetTargetCandidates();
            if (candidates == null || candidates.Count == 0)
            {
                m_CurrentTarget = null;
                return false;
            }

            Unit bestTarget = SelectBestTarget(candidates);
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
        protected virtual List<Unit> GetTargetCandidates()
        {
            // 기본 구현: BattleManager나 주변 탐색을 통해 적 유닛 리스트 반환
            // 실제 구현은 프로젝트 구조에 맞게 자식에서 오버라이드
            return new List<Unit>();
        }

        /// <summary>
        /// 우선순위에 따른 최적 타겟 선택
        /// </summary>
        protected Unit SelectBestTarget(List<Unit> candidates)
        {
            if (candidates == null || candidates.Count == 0) return null;

            Unit best = null;
            float bestScore = float.MaxValue;

            foreach (var candidate in candidates)
            {
                if (candidate == null || candidate.IsDead) continue;

                float distance = Vector3.Distance(m_Owner.transform.position, candidate.transform.position);

                // 최대 탐색 거리 체크
                if (m_MaxSearchDistance > 0f && distance > m_MaxSearchDistance) continue;

                // 사거리 체크 (스킬 사거리 또는 기본 공격 사거리 중 큰 값)
                float effectiveRange = Mathf.Max(m_Owner.CurrentAttackRange, m_Owner.Skill?.SkillRange ?? 0f);
                if (distance > effectiveRange * 1.5f) continue; // 너무 먼 적은 후보에서 제외 (이동 고려)

                float score = CalculateTargetScore(candidate, distance);
                if (score < bestScore)
                {
                    bestScore = score;
                    best = candidate;
                }
            }

            return best;
        }

        /// <summary>
        /// 타겟 점수 계산 (낮을수록 우선순위 높음)
        /// </summary>
        protected virtual float CalculateTargetScore(Unit target, float distance)
        {
            switch (m_TargetPriority)
            {
                case TargetPriorityType.Nearest:
                    return distance;

                case TargetPriorityType.LowestHp:
                    return target.CurrentHp / Mathf.Max(1f, target.MaxHp) * 1000f + distance * 0.1f;

                case TargetPriorityType.HighestThreat:
                    // 위협도 시스템이 구현되면 사용
                    return distance;

                case TargetPriorityType.Random:
                    return Random.value * 1000f;

                default:
                    return distance;
            }
        }

        /// <summary>
        /// 타겟 변경 시 콜백
        /// </summary>
        protected virtual void OnTargetChanged(Unit newTarget) { }

        /// <summary>
        /// 현재 타겟이 유효한지 체크 (사거리 내, 생존 등)
        /// </summary>
        protected bool IsTargetValid(Unit target)
        {
            if (target == null || target.IsDead || m_Owner == null) return false;

            float distance = Vector3.Distance(m_Owner.transform.position, target.transform.position);
            float attackRange = m_Owner.CurrentAttackRange;

            // 스킬 사용 중이면 스킬 사거리 기준
            if (m_CurrentState == AIState.UseSkill && m_Owner.Skill != null)
            {
                attackRange = Mathf.Max(attackRange, m_Owner.Skill.SkillRange);
            }

            return distance <= attackRange * m_AttackStopDistanceRatio;
        }

        /// <summary>
        /// 타겟에게 이동
        /// </summary>
        protected void MoveToTarget(Unit target)
        {
            if (target == null || m_Owner == null) return;

            Vector3 direction = (target.transform.position - m_Owner.transform.position).normalized;
            float stopDistance = m_Owner.CurrentAttackRange * m_AttackStopDistanceRatio;
            m_Owner.MoveTo(target.transform.position, stopDistance);
        }

        /// <summary>
        /// 이동 정지
        /// </summary>
        protected void StopMovement()
        {
            m_Owner?.StopMovement();
        }

        /// <summary>
        /// 기본 공격 시도
        /// </summary>
        protected virtual bool TryAttack()
        {
            if (m_CurrentTarget == null || !IsTargetValid(m_CurrentTarget)) return false;

            if (m_Owner.CanAttack)
            {
                m_Owner.Attack(m_CurrentTarget);
                return true;
            }

            return false;
        }

        /// <summary>
        /// 스킬 사용 시도
        /// </summary>
        protected virtual bool TryUseSkill()
        {
            if (m_Owner.Skill == null) return false;

            Skill_Base skill = m_Owner.Skill;

            // 패시브 스킬은 별도 트리거로 처리
            if (skill.SkillType == SkillType.Passive) return false;

            // 쿨타임 체크
            if (!skill.CanUse()) return false;

            // 시전 중이면 스킵
            if (m_Owner.IsCastingSkill) return false;

            // 기본 공격 중이고 중단 설정이 아니면 스킵
            if (m_Owner.IsAttacking && !m_InterruptAttackForSkill) return false;

            // 타겟 유효성 체크 (스킬 타입별)
            if (!IsSkillTargetValid(skill, m_CurrentTarget)) return false;

            // 스킬 시전
            m_Owner.UseSkill(skill, m_CurrentTarget);
            return true;
        }

        /// <summary>
        /// 스킬 타겟 유효성 체크
        /// </summary>
        protected virtual bool IsSkillTargetValid(Skill_Base skill, Unit target)
        {
            if (target == null || target.IsDead) return false;

            float distance = Vector3.Distance(m_Owner.transform.position, target.transform.position);

            // 스킬 사거리 체크
            if (distance > skill.SkillRange) return false;

            // 타겟 타입별 체크
            switch (skill.TargetType)
            {
                case SkillTargetType.Enemy:
                case SkillTargetType.Enemies:
                    return target.Team != m_Owner.Team;

                case SkillTargetType.Ally:
                case SkillTargetType.Allies:
                    return target.Team == m_Owner.Team;

                case SkillTargetType.Self:
                    return target == m_Owner;

                case SkillTargetType.Ground:
                    return true; // 위치는 별도 처리

                default:
                    return true;
            }
        }

        /// <summary>
        /// 상태 변경
        /// </summary>
        protected void ChangeState(AIState newState)
        {
            if (m_CurrentState == newState) return;

            OnStateExit(m_CurrentState);
            m_CurrentState = newState;
            m_StateTimer = 0f;
            OnStateEnter(newState);
        }

        /// <summary>
        /// 상태 진입 시 로직
        /// </summary>
        protected virtual void OnStateEnter(AIState state) { }

        /// <summary>
        /// 상태 종료 시 로직
        /// </summary>
        protected virtual void OnStateExit(AIState state) { }

        /// <summary>
        /// AI 비활성화 (사망 등)
        /// </summary>
        public virtual void Deactivate()
        {
            m_IsActive = false;
            StopMovement();
            m_CurrentTarget = null;
            ChangeState(AIState.Dead);
            OnDeactivate();
        }

        /// <summary>
        /// 비활성화 시 추가 로직
        /// </summary>
        protected virtual void OnDeactivate() { }

        /// <summary>
        /// 강제 타겟 설정 (도발 등)
        /// </summary>
        public virtual void ForceSetTarget(Unit target)
        {
            if (target != null && !target.IsDead)
            {
                m_CurrentTarget = target;
                OnTargetChanged(target);
            }
        }

        /// <summary>
        /// 타겟 초기화
        /// </summary>
        public virtual void ClearTarget()
        {
            m_CurrentTarget = null;
        }

        /// <summary>
        /// AI 데이터 복사 (런타임 인스턴스 생성용)
        /// </summary>
        public virtual UnitAI_Base Clone()
        {
            return Instantiate(this);
        }
    }

    /// <summary>
    /// 기본 AI: 가장 가까운 적을 공격, 도망 안 감
    /// </summary>
    [CreateAssetMenu(fileName = "UnitAI_Default_", menuName = "In The Arena/Unit/AI/Default", order = 0)]
    public class UnitAI_Default : UnitAI_Base
    {
        [Header("기본 AI 설정")]
        [Tooltip("전투 시작 후 첫 타겟 탐색까지 대기 시간")]
        [SerializeField] private float m_InitialSearchDelay = 0.1f;

        private bool m_HasSearchedInitially = false;

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
                m_StateTimer += deltaTime;
                if (m_StateTimer >= m_InitialSearchDelay)
                {
                    m_HasSearchedInitially = true;
                    m_StateTimer = 0f;
                    ChangeState(AIState.SearchTarget);
                }
                return;
            }

            // 상태 머신
            switch (m_CurrentState)
            {
                case AIState.Idle:
                    ChangeState(AIState.SearchTarget);
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
            if (m_CurrentTarget == null || m_CurrentTarget.IsDead || !IsTargetValid(m_CurrentTarget))
            {
                if (SearchAndSetTarget())
                {
                    // 타겟이 사거리 내면 바로 공격, 아니면 이동
                    if (IsTargetValid(m_CurrentTarget))
                    {
                        ChangeState(AIState.Attack);
                    }
                    else
                    {
                        ChangeState(AIState.MoveToTarget);
                    }
                }
                else
                {
                    // 타겟 없으면 대기
                    ChangeState(AIState.Idle);
                }
            }
            else
            {
                // 타겟이 유효하면 사거리 체크
                if (IsTargetValid(m_CurrentTarget))
                {
                    ChangeState(AIState.Attack);
                }
                else
                {
                    ChangeState(AIState.MoveToTarget);
                }
            }
        }

        private void UpdateMoveToTarget()
        {
            if (m_CurrentTarget == null || m_CurrentTarget.IsDead)
            {
                ChangeState(AIState.SearchTarget);
                return;
            }

            // 타겟이 사거리 내로 들어왔으면 공격 상태로
            if (IsTargetValid(m_CurrentTarget))
            {
                StopMovement();
                ChangeState(AIState.Attack);
                return;
            }

            // 타겟에게 계속 이동
            MoveToTarget(m_CurrentTarget);

            // 주기적으로 타겟 재탐색 (더 좋은 타겟이 생겼을 수 있음)
            if (Time.time >= m_NextSearchTime)
            {
                m_NextSearchTime = Time.time + m_SearchInterval;
                SearchAndSetTarget(); // 타겟이 바뀌면 OnTargetChanged에서 상태 변경 처리
            }
        }

        private void UpdateAttack()
        {
            if (m_CurrentTarget == null || m_CurrentTarget.IsDead)
            {
                ChangeState(AIState.SearchTarget);
                return;
            }

            // 타겟이 사거리 밖으로 나갔으면 이동 상태로
            if (!IsTargetValid(m_CurrentTarget))
            {
                ChangeState(AIState.MoveToTarget);
                return;
            }

            // 스킬 사용 시도 (쿨타임 0이고 사거리 내)
            if (TryUseSkill())
            {
                ChangeState(AIState.UseSkill);
                return;
            }

            // 기본 공격 시도
            TryAttack();
        }

        private void UpdateUseSkill()
        {
            // 스킬 시전 완료 대기 (Unit에서 IsCastingSkill로 관리)
            if (!m_Owner.IsCastingSkill)
            {
                // 시전 완료 후 공격 상태로 복귀
                ChangeState(AIState.Attack);
            }
        }

        protected override void OnTargetChanged(Unit newTarget)
        {
            base.OnTargetChanged(newTarget);

            // 새 타겟이 사거리 밖이면 이동 상태로
            if (newTarget != null && !IsTargetValid(newTarget))
            {
                ChangeState(AIState.MoveToTarget);
            }
        }

        protected override void OnStateEnter(AIState state)
        {
            base.OnStateEnter(state);

            switch (state)
            {
                case AIState.MoveToTarget:
                    if (m_CurrentTarget != null)
                    {
                        MoveToTarget(m_CurrentTarget);
                    }
                    break;

                case AIState.Attack:
                    StopMovement();
                    break;
            }
        }

        protected override void OnStateExit(AIState state)
        {
            base.OnStateExit(state);

            if (state == AIState.MoveToTarget || state == AIState.Attack)
            {
                StopMovement();
            }
        }

        public override UnitAI_Base Clone()
        {
            return Instantiate(this);
        }
    }
}
#endif