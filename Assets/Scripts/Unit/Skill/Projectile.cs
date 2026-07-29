#if UNITY_6000_0_OR_NEWER
using System;
using System.Collections.Generic;
using UnityEngine;

namespace InTheArena.Unit
{
    public readonly struct SkillImpactPayload
    {
        public readonly UnitHandle Source;
        public readonly int SourceTeam;
        public readonly float Damage;
        public readonly float ExplosionRadius;
        public readonly float CriticalChance;
        public readonly StatusEffectData ImpactStatus;
        public readonly bool IsReaction;

        public SkillImpactPayload(
            UnitHandle source,
            int sourceTeam,
            float damage,
            float explosionRadius,
            float criticalChance,
            StatusEffectData impactStatus,
            bool isReaction)
        {
            Source = source;
            SourceTeam = sourceTeam;
            Damage = Mathf.Max(0f, damage);
            ExplosionRadius = Mathf.Max(0f, explosionRadius);
            CriticalChance = Mathf.Clamp01(criticalChance);
            ImpactStatus = impactStatus;
            IsReaction = isReaction;
        }

        public bool Apply(Unit primaryTarget)
        {
            if (primaryTarget == null || primaryTarget.IsDead) return false;
            if (ExplosionRadius <= 0f) return ApplyTo(primaryTarget, 1f);

            bool applied = false;
            IReadOnlyList<Unit> enemies = SourceTeam == 0 ? UnitRegistry.BlueTeam : UnitRegistry.RedTeam;
            Vector3 center = primaryTarget.GroundPosition;
            float radiusSqr = ExplosionRadius * ExplosionRadius;
            for (int i = 0; i < enemies.Count; i++)
            {
                Unit candidate = enemies[i];
                if (candidate == null || candidate.IsDead) continue;
                Vector3 delta = candidate.GroundPosition - center;
                delta.y = 0f;
                float distanceSqr = delta.sqrMagnitude;
                if (distanceSqr > radiusSqr) continue;
                float multiplier = 1f - Mathf.Sqrt(distanceSqr) / ExplosionRadius * 0.5f;
                applied |= ApplyTo(candidate, multiplier);
            }
            return applied;
        }

        private bool ApplyTo(Unit target, float multiplier)
        {
            var context = new DamageContext
            {
                Source = Source,
                Target = target,
                Amount = Damage * multiplier,
                IsCritical = UnityEngine.Random.value < CriticalChance,
                IsSkill = true,
                IsReaction = IsReaction
            };
            bool applied = target.ApplyDamage(in context) > 0f;
            if (ImpactStatus != null && !target.IsDead)
                applied |= target.ApplyStatusEffect(ImpactStatus, Source.Unit) != null;
            return applied;
        }
    }

    [DisallowMultipleComponent]
    public sealed class Projectile : MonoBehaviour, IPoolLifecycle
    {
        private UnitHandle m_Target;
        private SkillImpactPayload m_Payload;
        private float m_Speed;
        private float m_RemainingLifetime;
        private bool m_Initialized;

        internal void Initialize(
            UnitHandle target,
            in SkillImpactPayload payload,
            float speed,
            float lifetime)
        {
            m_Target = target;
            m_Payload = payload;
            m_Speed = Mathf.Max(0.1f, speed);
            m_RemainingLifetime = Mathf.Max(0.1f, lifetime);
            m_Initialized = true;
            Unit unit = target.Unit;
            if (unit != null) transform.LookAt(unit.HitPosition);
        }

        internal bool SimulationFrame(float deltaTime)
        {
            Unit target = m_Target.Unit;
            if (!m_Initialized || target == null || target.IsDead) return false;

            m_RemainingLifetime -= deltaTime;
            if (m_RemainingLifetime <= 0f) return false;

            Vector3 destination = target.HitPosition;
            Vector3 offset = destination - transform.position;
            float distance = offset.magnitude;
            float step = m_Speed * deltaTime;
            if (distance <= Mathf.Max(0.25f, step))
            {
                m_Payload.Apply(target);
                return false;
            }

            transform.position += offset / distance * step;
            transform.rotation = Quaternion.LookRotation(offset, Vector3.up);
            return true;
        }

        public void OnPoolRent(in PoolSpawnContext context) { }

        public void OnPoolReturn()
        {
            m_Target = default;
            m_Payload = default;
            m_Speed = 0f;
            m_RemainingLifetime = 0f;
            m_Initialized = false;
        }
    }
}
#endif
