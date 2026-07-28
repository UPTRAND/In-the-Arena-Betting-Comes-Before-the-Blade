#if UNITY_6000_0_OR_NEWER
using UnityEngine;

namespace InTheArena.Unit
{
    /// <summary>
    /// 스턴 디버프 (행동 불가)
    /// </summary>
    [CreateAssetMenu(fileName = "Debuff_Stun_", menuName = "In The Arena/Unit/Status Effect/Debuff/Stun", order = 0)]
    public class Debuff_Stun : Debuff_Base
    {
        protected override void OnValidate()
        {
            base.OnValidate();
            m_Category = StatusEffectCategory.Stun;
            m_StackType = StackType.Duration; // 스턴은 중첩 대신 지속시간 갱신
        }

        protected override void OnApplied()
        {
            if (Owner != null)
            {
                Owner.SetStunned(true);
            }
        }

        protected override void OnRemoved(bool expired)
        {
            if (Owner != null)
            {
                Owner.SetStunned(false);
            }
        }

        public override float CalculateResistance(Unit target)
        {
            // 스턴 저항 스탯이 있다면 여기서 계산
            // 예: target.CurrentStat.stunResistance (추후 확장)
            return 0f;
        }

        public override UnitStatusEffect Clone()
        {
            return Instantiate(this);
        }
    }

    /// <summary>
    /// 둔화 디버프 (이동/공격 속도 감소)
    /// </summary>
    [CreateAssetMenu(fileName = "Debuff_Slow_", menuName = "In The Arena/Unit/Status Effect/Debuff/Slow", order = 1)]
    public class Debuff_Slow : Debuff_Base
    {
        [Header("둔화 설정")]
        [Tooltip("이동 속도 감소 비율 (0~1)")]
        [SerializeField] [Range(0f, 1f)] private float m_MoveSpeedReductionPercent;

        [Tooltip("공격 속도 감소 비율 (0~1)")]
        [SerializeField] [Range(0f, 1f)] private float m_AttackSpeedReductionPercent;

        protected override void OnValidate()
        {
            base.OnValidate();
            m_Category = StatusEffectCategory.Slow;
            m_StackType = StackType.Intensity; // 중첩 시 감속 효과 누적
        }

        protected override void OnApplied()
        {
            if (Owner == null) return;

            float moveReduction = Owner.BaseStat.moveSpeed * m_MoveSpeedReductionPercent * CurrentStacks;
            float attackReduction = Owner.BaseStat.attackSpeed * m_AttackSpeedReductionPercent * CurrentStacks;

            m_StatModifier.moveSpeed = -moveReduction;
            m_StatModifier.attackSpeed = -attackReduction;
            Owner.ApplyStatModifier(m_StatModifier, false);
        }

        protected override void OnRemoved(bool expired)
        {
            if (Owner == null) return;
            Owner.RemoveStatModifier(m_StatModifier, false);
        }

        public override UnitStat GetCurrentStatModifier()
        {
            return m_StatModifier.Multiply(CurrentStacks);
        }

        public override float CalculateResistance(Unit target)
        {
            // 둔화 저항 계산
            return 0f;
        }

        public override UnitStatusEffect Clone()
        {
            return Instantiate(this);
        }
    }

    /// <summary>
    /// 지속 데미지 디버프 (독, 화상 등)
    /// </summary>
    [CreateAssetMenu(fileName = "Debuff_DoT_", menuName = "In The Arena/Unit/Status Effect/Debuff/Damage Over Time", order = 2)]
    public class Debuff_DamageOverTime : Debuff_Base
    {
        [Header("지속 데미지 설정")]
        [Tooltip("초당 데미지")]
        [SerializeField] private float m_DamagePerSecond;

        [Tooltip("데미지 틱 간격 (초)")]
        [SerializeField] private float m_TickInterval = 1f;

        [Tooltip("최대 체력 비율 데미지 (0~1, 0이면 고정값 사용)")]
        [SerializeField] [Range(0f, 1f)] private float m_PercentMaxHpDamage;

        private float m_TickTimer;

        protected override void OnValidate()
        {
            base.OnValidate();
            m_Category = StatusEffectCategory.DamageOverTime;
            m_StackType = StackType.Intensity; // 중첩 시 데미지 누적
            m_DamagePerSecond = Mathf.Max(0f, m_DamagePerSecond);
            m_TickInterval = Mathf.Max(0.1f, m_TickInterval);
        }

        protected override void OnApplied()
        {
            m_TickTimer = 0f;
        }

        protected override void OnTick(float deltaTime)
        {
            if (Owner == null || Owner.IsDead) return;

            m_TickTimer += deltaTime;
            if (m_TickTimer >= m_TickInterval)
            {
                m_TickTimer = 0f;

                float damage = m_DamagePerSecond * m_TickInterval * CurrentStacks;
                if (m_PercentMaxHpDamage > 0f)
                {
                    damage += Owner.MaxHp * m_PercentMaxHpDamage * CurrentStacks;
                }

                // 지속 데미지는 방어력 무시 옵션 가능 (여기서는 방어력 적용)
                Owner.ApplyDamage(damage, Caster, false, true);
            }
        }

        public override float CalculateResistance(Unit target)
        {
            // 독/화상 저항 계산
            return 0f;
        }

        public override UnitStatusEffect Clone()
        {
            return Instantiate(this);
        }
    }

    /// <summary>
    /// 침묵 디버프 (스킬 사용 불가)
    /// </summary>
    [CreateAssetMenu(fileName = "Debuff_Silence_", menuName = "In The Arena/Unit/Status Effect/Debuff/Silence", order = 3)]
    public class Debuff_Silence : Debuff_Base
    {
        private bool m_WasCastingSkill;

        protected override void OnValidate()
        {
            base.OnValidate();
            m_Category = StatusEffectCategory.Silence;
            m_StackType = StackType.Duration;
        }

        protected override void OnApplied()
        {
            if (Owner != null)
            {
                // 현재 시전 중인 스킬 중단
                if (Owner.IsCastingSkill)
                {
                    m_WasCastingSkill = true;
                    // 스킬 중단 로직 필요시 추가
                }
                // 스킬 사용 불가 플래그 설정 (Unit 측에서 처리 필요)
                Owner.SetSilenced(true);
            }
        }

        protected override void OnRemoved(bool expired)
        {
            if (Owner != null)
            {
                Owner.SetSilenced(false);
            }
        }

        public override float CalculateResistance(Unit target)
        {
            return 0f;
        }

        public override UnitStatusEffect Clone()
        {
            return Instantiate(this);
        }
    }

    /// <summary>
    /// 방어력 감소 디버프
    /// </summary>
    [CreateAssetMenu(fileName = "Debuff_DefenseDown_", menuName = "In The Arena/Unit/Status Effect/Debuff/Defense Down", order = 4)]
    public class Debuff_DefenseDown : Debuff_Base
    {
        [Header("방어력 감소 설정")]
        [Tooltip("고정 감소량")]
        [SerializeField] private float m_FlatDefenseDecrease;

        [Tooltip("비율 감소량 (0~1)")]
        [SerializeField] [Range(0f, 1f)] private float m_PercentDefenseDecrease;

        protected override void OnValidate()
        {
            base.OnValidate();
            m_Category = StatusEffectCategory.StatModifier;
            m_StackType = StackType.Intensity;
        }

        protected override void OnApplied()
        {
            if (Owner == null) return;

            float flatReduction = m_FlatDefenseDecrease * CurrentStacks;
            float percentReduction = Owner.BaseStat.defense * m_PercentDefenseDecrease * CurrentStacks;
            float totalReduction = flatReduction + percentReduction;

            m_StatModifier.defense = -totalReduction;
            Owner.ApplyStatModifier(m_StatModifier, false);
        }

        protected override void OnRemoved(bool expired)
        {
            if (Owner == null) return;
            Owner.RemoveStatModifier(m_StatModifier, false);
        }

        public override UnitStat GetCurrentStatModifier()
        {
            return m_StatModifier.Multiply(CurrentStacks);
        }

        public override float CalculateResistance(Unit target)
        {
            return 0f;
        }

        public override UnitStatusEffect Clone()
        {
            return Instantiate(this);
        }
    }
}
#endif