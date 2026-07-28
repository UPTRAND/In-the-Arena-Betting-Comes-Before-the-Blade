#if UNITY_6000_0_OR_NEWER
using UnityEngine;

namespace InTheArena.Unit
{
    /// <summary>
    /// 회복 버프 (지속 회복)
    /// </summary>
    [CreateAssetMenu(fileName = "Buff_Heal_", menuName = "In The Arena/Unit/Status Effect/Buff/Heal Over Time", order = 0)]
    public class Buff_Heal : Buff_Base
    {
        [Header("회복 설정")]
        [Tooltip("초당 회복량")]
        [SerializeField] private float m_HealPerSecond;

        [Tooltip("최대 체력 비율로 회복 (0~1, 0이면 고정값 사용)")]
        [SerializeField] [Range(0f, 1f)] private float m_HealPercentOfMaxHp;

        protected override void OnValidate()
        {
            base.OnValidate();
            m_Category = StatusEffectCategory.HealOverTime;
            m_StackType = StackType.Intensity; // 강도 중첩
            m_HealPerSecond = Mathf.Max(0f, m_HealPerSecond);
        }

        protected override void OnTick(float deltaTime)
        {
            if (Owner == null || Owner.IsDead) return;

            float healAmount = m_HealPerSecond * deltaTime;
            if (m_HealPercentOfMaxHp > 0f)
            {
                healAmount = Owner.MaxHp * m_HealPercentOfMaxHp * deltaTime;
            }

            Owner.Heal(healAmount, Caster);
        }

        public override UnitStatusEffect Clone()
        {
            return Instantiate(this);
        }
    }

    /// <summary>
    /// 공격력 증가 버프
    /// </summary>
    [CreateAssetMenu(fileName = "Buff_AttackUp_", menuName = "In The Arena/Unit/Status Effect/Buff/Attack Up", order = 1)]
    public class Buff_AttackUp : Buff_Base
    {
        [Header("공격력 증가 설정")]
        [Tooltip("고정 증가량")]
        [SerializeField] private float m_FlatAttackIncrease;

        [Tooltip("비율 증가량 (0~1)")]
        [SerializeField] [Range(0f, 1f)] private float m_PercentAttackIncrease;

        protected override void OnValidate()
        {
            base.OnValidate();
            m_Category = StatusEffectCategory.StatModifier;
            m_StackType = StackType.Intensity;
        }

        protected override void OnApplied()
        {
            if (Owner == null) return;

            float flatBonus = m_FlatAttackIncrease * CurrentStacks;
            float percentBonus = Owner.BaseStat.attackPower * m_PercentAttackIncrease * CurrentStacks;
            float totalBonus = flatBonus + percentBonus;

            m_StatModifier.attackPower = totalBonus;
            Owner.ApplyStatModifier(m_StatModifier, true);
        }

        protected override void OnRemoved(bool expired)
        {
            if (Owner == null) return;
            Owner.RemoveStatModifier(m_StatModifier, true);
        }

        public override UnitStat GetCurrentStatModifier()
        {
            return m_StatModifier.Multiply(CurrentStacks);
        }

        public override UnitStatusEffect Clone()
        {
            return Instantiate(this);
        }
    }

    /// <summary>
    /// 이동 속도 증가 버프
    /// </summary>
    [CreateAssetMenu(fileName = "Buff_SpeedUp_", menuName = "In The Arena/Unit/Status Effect/Buff/Speed Up", order = 2)]
    public class Buff_SpeedUp : Buff_Base
    {
        [Header("이동 속도 증가 설정")]
        [Tooltip("고정 증가량")]
        [SerializeField] private float m_FlatSpeedIncrease;

        [Tooltip("비율 증가량 (0~1)")]
        [SerializeField] [Range(0f, 1f)] private float m_PercentSpeedIncrease;

        protected override void OnValidate()
        {
            base.OnValidate();
            m_Category = StatusEffectCategory.StatModifier;
            m_StackType = StackType.Intensity;
        }

        protected override void OnApplied()
        {
            if (Owner == null) return;

            float flatBonus = m_FlatSpeedIncrease * CurrentStacks;
            float percentBonus = Owner.BaseStat.moveSpeed * m_PercentSpeedIncrease * CurrentStacks;
            float totalBonus = flatBonus + percentBonus;

            m_StatModifier.moveSpeed = totalBonus;
            Owner.ApplyStatModifier(m_StatModifier, true);
        }

        protected override void OnRemoved(bool expired)
        {
            if (Owner == null) return;
            Owner.RemoveStatModifier(m_StatModifier, true);
        }

        public override UnitStatusEffect Clone()
        {
            return Instantiate(this);
        }
    }

    /// <summary>
    /// 보호막 버프
    /// </summary>
    [CreateAssetMenu(fileName = "Buff_Shield_", menuName = "In The Arena/Unit/Status Effect/Buff/Shield", order = 3)]
    public class Buff_Shield : Buff_Base
    {
        [Header("보호막 설정")]
        [Tooltip("보호막 흡수량")]
        [SerializeField] private float m_ShieldAmount;

        [Tooltip("최대 체력 비율로 보호막량 설정 (0~1, 0이면 고정값 사용)")]
        [SerializeField] [Range(0f, 1f)] private float m_ShieldPercentOfMaxHp;

        private float m_RemainingShield;

        protected override void OnValidate()
        {
            base.OnValidate();
            m_Category = StatusEffectCategory.Shield;
            m_StackType = StackType.Intensity; // 보호막은 중첩 시 흡수량 합산
        }

        protected override void OnApplied()
        {
            if (Owner == null) return;

            float shieldValue = m_ShieldAmount;
            if (m_ShieldPercentOfMaxHp > 0f)
            {
                shieldValue = Owner.MaxHp * m_ShieldPercentOfMaxHp;
            }

            m_RemainingShield = shieldValue * CurrentStacks;
        }

        /// <summary>
        /// 데미지 흡수 처리
        /// </summary>
        /// <param name="damage">들어오는 데미지</param>
        /// <returns>보호막 후 남은 데미지</returns>
        public float AbsorbDamage(float damage)
        {
            if (m_RemainingShield <= 0f) return damage;

            float absorbed = Mathf.Min(m_RemainingShield, damage);
            m_RemainingShield -= absorbed;
            damage -= absorbed;

            Owner.OnShieldAbsorbCallback(absorbed);

            if (m_RemainingShield <= 0f)
            {
                // 보호막 소진 시 효과 제거
                Owner.RemoveStatusEffect(this, false);
            }

            return damage;
        }

        protected override void OnRemoved(bool expired)
        {
            m_RemainingShield = 0f;
        }

        public override UnitStatusEffect Clone()
        {
            return Instantiate(this);
        }
    }

    /// <summary>
    /// 방어력 증가 버프
    /// </summary>
    [CreateAssetMenu(fileName = "Buff_DefenseUp_", menuName = "In The Arena/Unit/Status Effect/Buff/Defense Up", order = 4)]
    public class Buff_DefenseUp : Buff_Base
    {
        [Header("방어력 증가 설정")]
        [Tooltip("고정 증가량")]
        [SerializeField] private float m_FlatDefenseIncrease;

        [Tooltip("비율 증가량 (0~1)")]
        [SerializeField] [Range(0f, 1f)] private float m_PercentDefenseIncrease;

        protected override void OnValidate()
        {
            base.OnValidate();
            m_Category = StatusEffectCategory.StatModifier;
            m_StackType = StackType.Intensity;
        }

        protected override void OnApplied()
        {
            if (Owner == null) return;

            float flatBonus = m_FlatDefenseIncrease * CurrentStacks;
            float percentBonus = Owner.BaseStat.defense * m_PercentDefenseIncrease * CurrentStacks;
            float totalBonus = flatBonus + percentBonus;

            m_StatModifier.defense = totalBonus;
            Owner.ApplyStatModifier(m_StatModifier, true);
        }

        protected override void OnRemoved(bool expired)
        {
            if (Owner == null) return;
            Owner.RemoveStatModifier(m_StatModifier, true);
        }

        public override UnitStatusEffect Clone()
        {
            return Instantiate(this);
        }
    }
}
#endif