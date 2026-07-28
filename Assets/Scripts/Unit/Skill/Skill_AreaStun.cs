using System;
using UnityEngine;

namespace InTheArena.Unit
{
    /// <summary>
    /// 액티브 스킬 로직: 광역 스턴
    /// </summary>
    public class Skill_AreaStun : Skill_Base
    {
        [Header("광역 스턴 설정")]
        [Tooltip("스턴 지속 시간")]
        public float StunDuration = 2f;

        [Tooltip("스턴 확률 (0~1)")]
        [Range(0f, 1f)]
        public float StunChance = 1f;

        public override void Execute(Unit owner, Vector3 position)
        {
            if (owner == null) return;

            // 범위 내 적 탐색
            Collider[] hits = Physics.OverlapSphere(position, SkillRange, LayerMask.GetMask("Enemy"));
            foreach (var hit in hits)
            {
                Unit unit = hit.GetComponent<Unit>();
                if (unit != null && !unit.IsDead && unit.Team != owner.Team)
                {
                    // 데미지 적용
                    float damage = CalculateDamage(owner, unit);
                    unit.ApplyDamage(damage, owner, false, true);

                    // 스턴 적용 (확률)
                    if (UnityEngine.Random.value < StunChance)
                    {
                        // 스턴 디버프 적용 (Debuff_Stun이 프로젝트에 있어야 함)
                        var stunEffect = Resources.Load<Debuff_Stun>("Debuff_Stun");
                        if (stunEffect != null)
                        {
                            var instance = stunEffect.Clone();
                            instance.Initialize(unit, owner, StunDuration);
                            unit.ApplyStatusEffect(instance, owner, StunDuration);
                        }
                    }
                }
            }
        }

        public override float CalculateDamage(Unit attacker, Unit defender)
        {
            return SkillDamage;
        }

        public override Skill_Base Clone()
        {
            var clone = (Skill_AreaStun)MemberwiseClone();
            clone.CurrentCooldown = 0f;
            return clone;
        }
    }
}