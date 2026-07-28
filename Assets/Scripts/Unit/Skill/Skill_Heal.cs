using System;
using UnityEngine;

namespace InTheArena.Unit
{
    /// <summary>
    /// 액티브 스킬 로직: 힐 (아군 단일 회복)
    /// </summary>
    [Serializable]
    public class Skill_Heal : Skill_Base
    {
        [Header("힐 설정")]
        [Tooltip("회복량 (고정)")]
        public float HealAmount;

        [Tooltip("최대 체력 비율 회복 (0~1)")]
        [Range(0f, 1f)]
        public float HealPercentOfMaxHp;

        public override void Execute(Unit owner, Unit target)
        {
            if (owner == null || target == null || target.IsDead) return;

            float heal = HealAmount;
            if (HealPercentOfMaxHp > 0f)
            {
                heal += target.MaxHp * HealPercentOfMaxHp;
            }

            target.Heal(heal, owner);
        }

        public override float CalculateDamage(Unit attacker, Unit defender)
        {
            return 0f; // 힐은 데미지가 아님
        }

        public override Skill_Base Clone()
        {
            var clone = (Skill_Heal)MemberwiseClone();
            clone.CurrentCooldown = 0f;
            return clone;
        }
    }
}