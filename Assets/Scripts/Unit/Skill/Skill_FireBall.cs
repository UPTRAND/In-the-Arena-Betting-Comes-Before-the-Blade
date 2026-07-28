using System;
using UnityEngine;

namespace InTheArena.Unit
{
    /// <summary>
    /// 액티브 스킬 로직: 파이어볼 (단일 타겟 투사체)
    /// </summary>
    [Serializable]
    public class Skill_FireBall : Skill_Base
    {
        [Header("파이어볼 설정")]
        [Tooltip("투사체 프리팹")]
        public GameObject ProjectilePrefab;

        [Tooltip("투사체 속도")]
        public float ProjectileSpeed = 20f;

        [Tooltip("폭발 반경 (범위 데미지 시)")]
        public float ExplosionRadius = 0f;

        public override void Execute(Unit owner, Unit target)
        {
            if (owner == null || target == null || target.IsDead) return;

            if (ProjectilePrefab != null)
            {
                // 투사체 생성 및 발사
                GameObject projectile = UnityEngine.Object.Instantiate(ProjectilePrefab, owner.transform.position + Vector3.up, Quaternion.identity);
                var projScript = projectile.GetComponent<Projectile>();
                if (projScript != null)
                {
                    projScript.Initialize(owner, target, this, ProjectileSpeed);
                }
                else
                {
                    // 투사체 스크립트가 없으면 즉시 히트
                    ApplyDamageToTarget(owner, target);
                    UnityEngine.Object.Destroy(projectile);
                }
            }
            else
            {
                // 투사체 프리팹 없으면 즉시 적용
                ApplyDamageToTarget(owner, target);
            }
        }

        private void ApplyDamageToTarget(Unit owner, Unit target)
        {
            float damage = CalculateDamage(owner, target);
            bool isCritical = UnityEngine.Random.value < 0.1f; // 스킬 치명타 10%

            if (ExplosionRadius > 0f)
            {
                // 범위 데미지
                ApplyAreaDamage(owner, target.transform.position, damage, isCritical);
            }
            else
            {
                // 단일 타겟
                target.ApplyDamage(damage, owner, isCritical, true);
            }
        }

        private void ApplyAreaDamage(Unit owner, Vector3 center, float damage, bool isCritical)
        {
            // 주변 적 탐색 (Physics.OverlapSphere 사용)
            Collider[] hits = Physics.OverlapSphere(center, ExplosionRadius, LayerMask.GetMask("Enemy"));
            foreach (var hit in hits)
            {
                Unit unit = hit.GetComponent<Unit>();
                if (unit != null && !unit.IsDead && unit.Team != owner.Team)
                {
                    // 거리에 따른 데미지 감쇠
                    float dist = Vector3.Distance(center, unit.transform.position);
                    float damageMultiplier = 1f - (dist / ExplosionRadius) * 0.5f; // 중심 100%, 가장자리 50%
                    unit.ApplyDamage(damage * damageMultiplier, owner, isCritical, true);
                }
            }
        }

        public override float CalculateDamage(Unit attacker, Unit defender)
        {
            // 스킬 데미지 + 공격력 비례 (예: 스킬 데미지 100% + 공격력 50%)
            return SkillDamage + (attacker?.CurrentAttackPower ?? 0f) * 0.5f;
        }

        public override Skill_Base Clone()
        {
            var clone = (Skill_FireBall)MemberwiseClone();
            clone.CurrentCooldown = 0f;
            return clone;
        }
    }
}