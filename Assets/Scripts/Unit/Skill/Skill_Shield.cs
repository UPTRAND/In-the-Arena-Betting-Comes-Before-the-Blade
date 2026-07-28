#if UNITY_6000_0_OR_NEWER
using System;
using UnityEngine;

namespace InTheArena.Unit
{
    /// <summary>
    /// 실드 스킬: 쿨타임이 0일 때 다음 공격을 1회 무효화 (데미지 0)
    /// 기존 Buff_Shield 상태효과를 활용하여 구현
    /// </summary>
    [Serializable]
    public class Skill_Shield : Skill_Base
    {
        [Header("실드 설정")]
        [Tooltip("실드 흡수량 (0이면 최대체력의 100%)")]
        [SerializeField] private float m_ShieldAmount = 0f;

        [Tooltip("최대 체력 비율로 실드량 설정 (0~1, 0이면 m_ShieldAmount 사용)")]
        [SerializeField] [Range(0f, 1f)] private float m_ShieldPercentOfMaxHp = 1f;

        public override void Initialize(Unit owner)
        {
            base.Initialize(owner);
            CurrentCooldown = Cooldown; // 시작 시 쿨타임 적용
        }

        public override bool CanUse()
        {
            // 실드 스킬은 패시브로, 쿨타임이 0일 때 자동 발동
            return CurrentCooldown <= 0f;
        }

        public override void Execute(Unit owner, Unit target = null)
        {
            // 패시브 스킬은 Execute 호출 안 함 (OnTrigger에서 처리)
        }

        public override void OnTrigger(Unit owner, PassiveTriggerType triggerType, object param = null)
        {
            if (triggerType != PassiveTriggerType.OnHit) return;
            if (CurrentCooldown > 0f) return; // 쿨타임 중이면 발동 안 함

            // 실드 버프 생성 및 적용 (다음 한 번 공격 흡수)
            var shieldBuff = ScriptableObject.CreateInstance<Buff_Shield>();
            shieldBuff.ShieldAmount = m_ShieldAmount;
            shieldBuff.ShieldPercentOfMaxHp = m_ShieldPercentOfMaxHp;
            
            // 10초 내에 공격 안 받으면 만료
            owner.ApplyStatusEffect(shieldBuff, owner, 10f);
            
            // 쿨타임 시작
            CurrentCooldown = Cooldown;
        }

        public override Skill_Base Clone()
        {
            var clone = new Skill_Shield();
            clone.m_ShieldAmount = m_ShieldAmount;
            clone.m_ShieldPercentOfMaxHp = m_ShieldPercentOfMaxHp;
            clone.CurrentCooldown = 0f;
            return clone;
        }
    }
}
#endif