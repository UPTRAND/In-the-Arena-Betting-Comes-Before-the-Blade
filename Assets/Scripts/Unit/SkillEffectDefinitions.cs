#if UNITY_6000_0_OR_NEWER
using System;
using System.Collections.Generic;
using UnityEngine;

namespace InTheArena.Unit
{
    [Serializable]
    public sealed class DamageSkillEffect : SkillEffectDefinition
    {
        [SerializeField, Min(0f)] private float m_BaseDamage = 10f;
        [SerializeField, Min(0f)] private float m_AttackPowerRatio;
        [SerializeField, Range(0f, 1f)] private float m_CriticalChance;

        public override SkillExecutionResult Apply(in SkillEffectContext context)
        {
            bool applied = false;
            for (int i = 0; i < context.Targets.Count; i++)
            {
                Unit target = context.Targets[i].Unit;
                if (target == null || target.IsDead || target.Team == context.Owner.Team) continue;
                float amount = m_BaseDamage + context.Owner.CurrentAttackPower * m_AttackPowerRatio;
                var damage = new DamageContext
                {
                    Source = new UnitHandle(context.Owner),
                    Target = target,
                    Amount = amount,
                    IsCritical = UnityEngine.Random.value < m_CriticalChance,
                    IsSkill = true,
                    IsReaction = context.IsReaction
                };
                applied |= target.ApplyDamage(in damage) > 0f;
            }
            return applied ? SkillExecutionResult.Success : SkillExecutionResult.NoEffect;
        }
    }

    [Serializable]
    public sealed class HealSkillEffect : SkillEffectDefinition
    {
        [SerializeField, Min(0f)] private float m_BaseHeal = 10f;
        [SerializeField, Min(0f)] private float m_AttackPowerRatio;
        [SerializeField, Range(0f, 1f)] private float m_MaxHealthRatio;

        public override SkillExecutionResult Apply(in SkillEffectContext context)
        {
            bool applied = false;
            for (int i = 0; i < context.Targets.Count; i++)
            {
                Unit target = context.Targets[i].Unit;
                if (target == null || target.IsDead || target.Team != context.Owner.Team) continue;
                float amount = m_BaseHeal + context.Owner.CurrentAttackPower * m_AttackPowerRatio +
                               target.MaxHp * m_MaxHealthRatio;
                var heal = new HealContext
                {
                    Source = new UnitHandle(context.Owner),
                    Target = target,
                    Amount = amount,
                    IsSkill = true,
                    IsReaction = context.IsReaction
                };
                applied |= target.Heal(in heal) > 0f;
            }
            return applied ? SkillExecutionResult.Success : SkillExecutionResult.NoEffect;
        }
    }

    [Serializable]
    public sealed class ApplyStatusEffectSkillEffect : SkillEffectDefinition
    {
        [SerializeField] private StatusEffectData m_StatusEffect;
        [SerializeField] private float m_DurationOverride = -1f;

        public override SkillExecutionResult Apply(in SkillEffectContext context)
        {
            if (m_StatusEffect == null) return SkillExecutionResult.NoEffect;
            bool applied = false;
            for (int i = 0; i < context.Targets.Count; i++)
            {
                Unit target = context.Targets[i].Unit;
                if (target == null || target.IsDead) continue;
                applied |= target.ApplyStatusEffect(m_StatusEffect, context.Owner, m_DurationOverride) != null;
            }
            return applied ? SkillExecutionResult.Success : SkillExecutionResult.NoEffect;
        }
    }

    [Serializable]
    public sealed class KnockbackSkillEffect : SkillEffectDefinition
    {
        [SerializeField, Min(0f)] private float m_Distance = 1f;

        public override SkillExecutionResult Apply(in SkillEffectContext context)
        {
            bool applied = false;
            for (int i = 0; i < context.Targets.Count; i++)
            {
                Unit target = context.Targets[i].Unit;
                if (target == null || target.IsDead || target == context.Owner) continue;
                Vector3 direction = target.GroundPosition - context.Owner.GroundPosition;
                direction.y = 0f;
                if (direction.sqrMagnitude <= 0.0001f) continue;
                target.MoveTo(target.GroundPosition + direction.normalized * m_Distance);
                applied = true;
            }
            return applied ? SkillExecutionResult.Success : SkillExecutionResult.NoEffect;
        }
    }

    public readonly struct SkillVfxRequest
    {
        public readonly GameObject Prefab;
        public readonly Vector3 Position;
        public readonly UnitHandle Source;
        public readonly UnitHandle Target;

        public SkillVfxRequest(GameObject prefab, Vector3 position, Unit source, Unit target)
        {
            Prefab = prefab;
            Position = position;
            Source = new UnitHandle(source);
            Target = new UnitHandle(target);
        }
    }

    public static class SkillVfxRequestBus
    {
        public static event Action<SkillVfxRequest> Requested;
        public static bool HasListener => Requested != null;
        public static void Request(in SkillVfxRequest request) => Requested?.Invoke(request);
    }

    [Serializable]
    public sealed class SpawnVfxSkillEffect : SkillEffectDefinition
    {
        [SerializeField] private GameObject m_VfxPrefab;
        [SerializeField] private bool m_UseHitAnchor;

        public override SkillExecutionResult Apply(in SkillEffectContext context)
        {
            if (m_VfxPrefab == null || !SkillVfxRequestBus.HasListener)
                return SkillExecutionResult.NoEffect;

            if (context.Targets.Count == 0 && context.Targets.HasGroundPosition)
            {
                var groundRequest = new SkillVfxRequest(
                    m_VfxPrefab,
                    context.Targets.GroundPosition,
                    context.Owner,
                    null);
                SkillVfxRequestBus.Request(in groundRequest);
                return SkillExecutionResult.Success;
            }

            bool requested = false;
            for (int i = 0; i < context.Targets.Count; i++)
            {
                Unit target = context.Targets[i].Unit;
                if (target == null) continue;
                var request = new SkillVfxRequest(
                    m_VfxPrefab,
                    m_UseHitAnchor ? target.HitPosition : target.GroundPosition,
                    context.Owner,
                    target);
                SkillVfxRequestBus.Request(in request);
                requested = true;
            }
            return requested ? SkillExecutionResult.Success : SkillExecutionResult.NoEffect;
        }
    }

    [Serializable]
    public sealed class SpawnProjectileSkillEffect : SkillEffectDefinition, IProjectileImpactResolver
    {
        [SerializeField] private GameObject m_ProjectilePrefab;
        [SerializeField, Min(0.1f)] private float m_Speed = 20f;
        [SerializeField, Min(0.1f)] private float m_Lifetime = 5f;
        [SerializeField, Min(0f)] private float m_BaseDamage = 10f;
        [SerializeField, Min(0f)] private float m_AttackPowerRatio = 0.5f;
        [SerializeField, Min(0f)] private float m_ExplosionRadius;
        [SerializeField, Range(0f, 1f)] private float m_CriticalChance = 0.1f;
        [SerializeField] private StatusEffectData m_ImpactStatus;

        public override SkillExecutionResult Apply(in SkillEffectContext context)
        {
            Unit target = context.Targets.Count > 0 ? context.Targets[0].Unit : null;
            if (m_ProjectilePrefab == null || target == null || target.IsDead)
                return SkillExecutionResult.InvalidTarget;

            var payload = new ProjectileImpactPayload(
                new UnitHandle(context.Owner),
                context.Owner.Team,
                m_BaseDamage + context.Owner.CurrentAttackPower * m_AttackPowerRatio,
                UnityEngine.Random.value < m_CriticalChance,
                true,
                context.IsReaction,
                this);

            if (!PoolManager.Require().Projectiles.TrySpawn(
                    m_ProjectilePrefab,
                    context.Owner.CastPosition,
                    new UnitHandle(target),
                    payload,
                    m_Speed,
                    m_Lifetime,
                    out _))
                return SkillExecutionResult.PoolExhausted;

            return SkillExecutionResult.Success;
        }

        public bool ApplyImpact(
            in ProjectileImpactPayload payload,
            Unit primaryTarget,
            Vector3 impactPosition)
        {
            if (primaryTarget == null || primaryTarget.IsDead) return false;
            if (m_ExplosionRadius <= 0f)
                return ApplyTo(in payload, primaryTarget, 1f);

            bool applied = false;
            IReadOnlyList<Unit> enemies = payload.SourceTeam == 0
                ? UnitRegistry.BlueTeam
                : UnitRegistry.RedTeam;
            float radiusSqr = m_ExplosionRadius * m_ExplosionRadius;
            for (int i = 0; i < enemies.Count; i++)
            {
                Unit candidate = enemies[i];
                if (candidate == null || candidate.IsDead) continue;
                Vector3 delta = candidate.GroundPosition - impactPosition;
                delta.y = 0f;
                float distanceSqr = delta.sqrMagnitude;
                if (distanceSqr > radiusSqr) continue;
                float multiplier = 1f -
                                   Mathf.Sqrt(distanceSqr) / m_ExplosionRadius * 0.5f;
                applied |= ApplyTo(in payload, candidate, multiplier);
            }
            return applied;
        }

        private bool ApplyTo(
            in ProjectileImpactPayload payload,
            Unit target,
            float multiplier)
        {
            var damage = new DamageContext
            {
                Source = payload.Source,
                Target = target,
                Amount = payload.Damage * multiplier,
                IsCritical = payload.IsCritical,
                IsSkill = true,
                IsReaction = payload.IsReaction
            };
            bool applied = target.ApplyDamage(in damage) > 0f;
            if (m_ImpactStatus != null && !target.IsDead)
                applied |= target.ApplyStatusEffect(m_ImpactStatus, payload.Source.Unit) != null;
            return applied;
        }

        public override void CollectProjectilePrefabs(List<GameObject> output)
        {
            if (m_ProjectilePrefab != null && output != null && !output.Contains(m_ProjectilePrefab))
                output.Add(m_ProjectilePrefab);
        }
    }
}
#endif
