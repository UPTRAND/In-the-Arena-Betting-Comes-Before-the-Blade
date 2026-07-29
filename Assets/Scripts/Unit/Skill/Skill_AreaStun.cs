using System;
using System.Collections.Generic;
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
            position.y = owner.GroundPosition.y;
            IReadOnlyList<Unit> enemies = owner.Team == 0 ? UnitRegistry.BlueTeam : UnitRegistry.RedTeam;
            float rangeSqr = SkillRange * SkillRange;
            for (int i = 0; i < enemies.Count; i++)
            {
                Unit unit = enemies[i];
                if (unit != null && !unit.IsDead)
                {
                    Vector3 offset = unit.GroundPosition - position;
                    offset.y = 0f;
                    if (offset.sqrMagnitude > rangeSqr) continue;

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
                            unit.ApplyStatusEffect(stunEffect, owner, StunDuration);
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
