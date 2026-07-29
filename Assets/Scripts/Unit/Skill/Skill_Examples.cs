#if UNITY_6000_0_OR_NEWER
using System;
using UnityEngine;

namespace InTheArena.Unit
{

    /// <summary>
    /// 패시브 스킬 로직: 반격 (피격 시 일정 확률로 반격)
    /// </summary>
    public class SkillLogic_CounterAttack : Skill_Base
    {
        [Header("반격 설정")]
        [Tooltip("반격 발동 확률 (0~1)")]
        [Range(0f, 1f)]
        public float CounterChance = 0.2f;

        [Tooltip("반격 데미지 배율 (자신 공격력 대비)")]
        public float CounterDamageMultiplier = 0.5f;

        public override void OnTrigger(Unit owner, PassiveTriggerType triggerType, object param = null)
        {
            if (triggerType != PassiveTriggerType.OnHit) return;
            if (owner == null || owner.IsDead) return;

            if (UnityEngine.Random.value < CounterChance)
            {
                // 공격자 정보 가져오기
                Unit attacker = param as Unit;
                if (attacker != null && !attacker.IsDead && attacker.Team != owner.Team)
                {
                    // 사거리 체크
                    float dist = Vector3.Distance(owner.transform.position, attacker.transform.position);
                    if (dist <= owner.CurrentAttackRange)
                    {
                        float damage = owner.CurrentAttackPower * CounterDamageMultiplier;
                        attacker.ApplyDamage(damage, owner, false, false);
                    }
                }
            }
        }

        public override Skill_Base Clone()
        {
            var clone = (SkillLogic_CounterAttack)MemberwiseClone();
            clone.CurrentCooldown = 0f;
            return clone;
        }
    }

    /// <summary>
    /// 패시브 스킬 로직: 흡혈 (공격 시 체력 회복)
    /// </summary>
    public class SkillLogic_LifeSteal : Skill_Base
    {
        [Header("흡혈 설정")]
        [Tooltip("데미지의 몇 %를 체력으로 회복 (0~1)")]
        [Range(0f, 1f)]
        public float LifeStealPercent = 0.15f;

        public override void OnTrigger(Unit owner, PassiveTriggerType triggerType, object param = null)
        {
            if (triggerType != PassiveTriggerType.OnAttack) return;
            if (owner == null || owner.IsDead) return;

            // param으로 데미지 정보 받기 (구현에 따라 확장)
            float damageDealt = param is float ? (float)param : owner.CurrentAttackPower;
            float healAmount = damageDealt * LifeStealPercent;

            if (healAmount > 0f)
            {
                owner.Heal(healAmount, owner);
            }
        }

        public override Skill_Base Clone()
        {
            var clone = (SkillLogic_LifeSteal)MemberwiseClone();
            clone.CurrentCooldown = 0f;
            return clone;
        }
    }

    /// <summary>
    /// 패시브 스킬 로직: 처치 시 버프 획득
    /// </summary>
    public class SkillLogic_KillBuff : Skill_Base
    {
        [Header("처치 버프 설정")]
        [Tooltip("획득할 버프 (ScriptableObject)")]
        public Buff_Base BuffToGrant;

        [Tooltip("버프 지속 시간")]
        public float BuffDuration = 10f;

        public override void OnTrigger(Unit owner, PassiveTriggerType triggerType, object param = null)
        {
            if (triggerType != PassiveTriggerType.OnKill) return;
            if (owner == null || owner.IsDead || BuffToGrant == null) return;

            owner.ApplyStatusEffect(BuffToGrant, owner, BuffDuration);
        }

        public override Skill_Base Clone()
        {
            var clone = (SkillLogic_KillBuff)MemberwiseClone();
            clone.CurrentCooldown = 0f;
            return clone;
        }
    }

    /// <summary>
    /// 간단한 투사체 컴포넌트 (예시)
    /// </summary>
    public class Projectile : MonoBehaviour
    {
        private Unit m_Owner;
        private Unit m_Target;
        private Skill_Base m_Skill;
        private float m_Speed;
        private bool m_Initialized = false;
        private GameObject m_PoolSource;

        internal GameObject PoolSource => m_PoolSource;

        internal void SetPoolSource(GameObject source) => m_PoolSource = source;

        internal void ResetRuntime()
        {
            m_Owner = null;
            m_Target = null;
            m_Skill = null;
            m_Speed = 0f;
            m_Initialized = false;
        }

        public void Initialize(Unit owner, Unit target, Skill_Base skill, float speed)
        {
            m_Owner = owner;
            m_Target = target;
            m_Skill = skill;
            m_Speed = speed;
            m_Initialized = true;

            // 타겟 방향 회전
            if (m_Target != null)
            {
                transform.LookAt(m_Target.HitPosition);
            }
        }

        private void Update()
        {
            if (!m_Initialized || m_Target == null || m_Target.IsDead)
            {
                ProjectilePoolService.Return(this);
                return;
            }

            // 타겟 방향으로 이동
            Vector3 direction = (m_Target.HitPosition - transform.position).normalized;
            transform.position += direction * m_Speed * Time.deltaTime;

            // 도착 체크
            if ((transform.position - m_Target.HitPosition).sqrMagnitude < 0.25f)
            {
                OnHit();
            }
        }

        private void OnHit()
        {
            if (m_Skill != null && m_Owner != null && m_Target != null && !m_Target.IsDead)
            {
                m_Skill.OnProjectileHit(m_Owner, m_Target);
            }
            ProjectilePoolService.Return(this);
        }
    }
}
#endif
