#if UNITY_6000_0_OR_NEWER
using System;
using System.Collections.Generic;
using UnityEngine;

namespace InTheArena.Unit
{
    public readonly struct BasicAttackImpactContext
    {
        public readonly ProjectileImpactPayload Payload;
        public readonly Unit PrimaryTarget;
        public readonly Vector3 ImpactPosition;

        public BasicAttackImpactContext(
            in ProjectileImpactPayload payload,
            Unit primaryTarget,
            Vector3 impactPosition)
        {
            Payload = payload;
            PrimaryTarget = primaryTarget;
            ImpactPosition = impactPosition;
        }
    }

    [Serializable]
    public abstract class AttackDeliveryDefinition
    {
        public abstract bool TryDeliver(
            Unit owner,
            Unit target,
            in ProjectileImpactPayload payload);

        public virtual void CollectProjectilePrefabs(List<GameObject> output) { }
        public virtual bool IsValid(UnityEngine.Object context) => true;
    }

    [Serializable]
    public sealed class ImmediateAttackDelivery : AttackDeliveryDefinition
    {
        public override bool TryDeliver(
            Unit owner,
            Unit target,
            in ProjectileImpactPayload payload)
        {
            if (target == null || target.IsDead || target.Team == owner.Team) return false;
            payload.Apply(target, target.HitPosition);
            return true;
        }
    }

    [Serializable]
    public sealed class HomingProjectileAttackDelivery : AttackDeliveryDefinition
    {
        [SerializeField] private ProjectileData m_ProjectileData;

        public ProjectileData ProjectileData => m_ProjectileData;

#if UNITY_EDITOR
        public void ConfigureForEditor(ProjectileData projectileData)
            => m_ProjectileData = projectileData;
#endif

        public override bool TryDeliver(
            Unit owner,
            Unit target,
            in ProjectileImpactPayload payload)
        {
            if (owner == null || target == null || target.IsDead || m_ProjectileData == null)
                return false;

            PoolManager manager = PoolManager.Instance;
            return manager != null &&
                   manager.Projectiles.TrySpawn(
                       m_ProjectileData,
                       owner.CastPosition,
                       new UnitHandle(target),
                       payload,
                       out _);
        }

        public override void CollectProjectilePrefabs(List<GameObject> output)
        {
            GameObject prefab = m_ProjectileData != null ? m_ProjectileData.Prefab : null;
            if (prefab != null && output != null && !output.Contains(prefab)) output.Add(prefab);
        }

        public override bool IsValid(UnityEngine.Object context)
        {
            if (m_ProjectileData != null && m_ProjectileData.IsValid()) return true;
            Debug.LogError($"[BasicAttackData] {context.name}: 원거리 투사체 데이터가 유효하지 않습니다.", context);
            return false;
        }
    }

    [Serializable]
    public abstract class AttackImpactEffectDefinition
    {
        public abstract bool Apply(in BasicAttackImpactContext context);
        public virtual bool IsValid(UnityEngine.Object context) => true;

        protected static float ApplyDamage(
            in ProjectileImpactPayload payload,
            Unit target,
            float multiplier)
        {
            if (target == null || target.IsDead || target.Team == payload.SourceTeam) return 0f;
            var damage = new DamageContext
            {
                Source = payload.Source,
                Target = target,
                Amount = payload.Damage * Mathf.Max(0f, multiplier),
                IsCritical = payload.IsCritical,
                IsSkill = payload.IsSkill,
                IsReaction = payload.IsReaction
            };
            float applied = target.ApplyDamage(in damage);
            if (!payload.IsSkill && applied > 0f)
                payload.Source.Unit?.NotifyBasicAttackHit(target, applied, payload.IsCritical, payload.IsReaction);
            return applied;
        }
    }

    [Serializable]
    public sealed class PrimaryDamageAttackEffect : AttackImpactEffectDefinition
    {
        [SerializeField, Min(0f)] private float m_DamageMultiplier = 1f;

        public override bool Apply(in BasicAttackImpactContext context)
            => ApplyDamage(context.Payload, context.PrimaryTarget, m_DamageMultiplier) > 0f;
    }

    [Serializable]
    public sealed class AreaDamageAttackEffect : AttackImpactEffectDefinition
    {
        private static readonly Unit[] CandidateBuffer = new Unit[UnitSpatialIndex.MaxUnits];
        [SerializeField, Min(0.01f)] private float m_Radius = 1.5f;
        [SerializeField, Min(0f)] private float m_DamageMultiplier = 0.5f;
        [SerializeField] private bool m_UseDistanceFalloff = true;
        [SerializeField, Range(0f, 1f)] private float m_MinimumMultiplier = 0.5f;
        [SerializeField] private bool m_ExcludePrimaryTarget = true;

        public override bool Apply(in BasicAttackImpactContext context)
        {
            float radius = Mathf.Max(0.01f, m_Radius);
            float radiusSqr = radius * radius;
            bool applied = false;
            int candidateCount = UnitRegistry.CollectEnemiesInRadius(
                context.Payload.SourceTeam,
                context.ImpactPosition,
                radius,
                CandidateBuffer);

            for (int i = 0; i < candidateCount; i++)
            {
                Unit candidate = CandidateBuffer[i];
                CandidateBuffer[i] = null;
                if (candidate == null || candidate.IsDead ||
                    m_ExcludePrimaryTarget && candidate == context.PrimaryTarget)
                    continue;

                Vector3 delta = candidate.GroundPosition - context.ImpactPosition;
                delta.y = 0f;
                float distanceSqr = delta.sqrMagnitude;
                if (distanceSqr > radiusSqr) continue;

                float falloff = 1f;
                if (m_UseDistanceFalloff)
                {
                    float normalizedDistance = Mathf.Sqrt(distanceSqr) / radius;
                    falloff = Mathf.Lerp(1f, Mathf.Clamp01(m_MinimumMultiplier), normalizedDistance);
                }

                applied |= ApplyDamage(
                    context.Payload,
                    candidate,
                    m_DamageMultiplier * falloff) > 0f;
            }
            return applied;
        }

        public override bool IsValid(UnityEngine.Object context)
        {
            if (m_Radius > 0f) return true;
            Debug.LogError($"[BasicAttackData] {context.name}: 범위 공격 반경은 0보다 커야 합니다.", context);
            return false;
        }
    }

    [Serializable]
    public sealed class ApplyStatusAttackEffect : AttackImpactEffectDefinition
    {
        [SerializeField] private StatusEffectData m_StatusEffect;
        [SerializeField] private float m_DurationOverride = -1f;

        public override bool Apply(in BasicAttackImpactContext context)
        {
            Unit target = context.PrimaryTarget;
            return m_StatusEffect != null && target != null && !target.IsDead &&
                   target.ApplyStatusEffect(
                       m_StatusEffect,
                       context.Payload.Source.Unit,
                       m_DurationOverride) != null;
        }

        public override bool IsValid(UnityEngine.Object context)
        {
            if (m_StatusEffect != null) return true;
            Debug.LogError($"[BasicAttackData] {context.name}: 적용할 상태효과가 없습니다.", context);
            return false;
        }
    }

    [CreateAssetMenu(
        fileName = "BasicAttackData_",
        menuName = "In The Arena/Unit/Basic Attack/Basic Attack Data",
        order = 0)]
    public sealed class BasicAttackData : ScriptableObject, IProjectileImpactResolver
    {
        [Header("공격")]
        [SerializeField, Min(0f)] private float m_AttackPowerMultiplier = 1f;
        [SerializeField, Range(0f, 1f)] private float m_CriticalChance = 0.05f;
        [SerializeField, Min(0.05f)] private float m_FailureRetryDelay = 0.25f;
        [SerializeField, Min(0f)] private float m_ProjectileReleaseFrameOffset = 3f;

        [Header("전달 방식")]
        [SerializeReference, SubclassSelector] private AttackDeliveryDefinition m_Delivery =
            new ImmediateAttackDelivery();

        [Header("적중 효과")]
        [SerializeReference, SubclassSelector] private List<AttackImpactEffectDefinition> m_ImpactEffects =
            new List<AttackImpactEffectDefinition> { new PrimaryDamageAttackEffect() };

        public float AttackPowerMultiplier => Mathf.Max(0f, m_AttackPowerMultiplier);
        public float CriticalChance => Mathf.Clamp01(m_CriticalChance);
        public float FailureRetryDelay => Mathf.Max(0.05f, m_FailureRetryDelay);
        public float ProjectileReleaseFrameOffset => Mathf.Max(0f, m_ProjectileReleaseFrameOffset);
        public AttackDeliveryDefinition Delivery => m_Delivery;
        public IReadOnlyList<AttackImpactEffectDefinition> ImpactEffects => m_ImpactEffects;

        public bool TryExecute(Unit owner, Unit target)
        {
            if (owner == null || target == null || target.IsDead || target.Team == owner.Team ||
                m_Delivery == null)
                return false;

            var payload = new ProjectileImpactPayload(
                new UnitHandle(owner),
                owner.Team,
                owner.CurrentAttackPower * AttackPowerMultiplier,
                UnityEngine.Random.value < CriticalChance,
                false,
                false,
                this);
            return m_Delivery.TryDeliver(owner, target, in payload);
        }

        public bool ApplyImpact(
            in ProjectileImpactPayload payload,
            Unit primaryTarget,
            Vector3 impactPosition)
        {
            if (primaryTarget == null || primaryTarget.IsDead ||
                m_ImpactEffects == null || m_ImpactEffects.Count == 0)
                return false;

            var context = new BasicAttackImpactContext(in payload, primaryTarget, impactPosition);
            bool applied = false;
            for (int i = 0; i < m_ImpactEffects.Count; i++)
                applied |= m_ImpactEffects[i]?.Apply(in context) == true;
            return applied;
        }

        public void CollectProjectilePrefabs(List<GameObject> output)
            => m_Delivery?.CollectProjectilePrefabs(output);

        public bool IsValid()
        {
            bool valid = true;
            if (m_Delivery == null)
            {
                Debug.LogError($"[BasicAttackData] {name}: 전달 방식이 없습니다.", this);
                valid = false;
            }
            else
            {
                valid &= m_Delivery.IsValid(this);
            }

            if (m_ImpactEffects == null || m_ImpactEffects.Count == 0)
            {
                Debug.LogError($"[BasicAttackData] {name}: 적중 효과가 없습니다.", this);
                valid = false;
            }
            else
            {
                for (int i = 0; i < m_ImpactEffects.Count; i++)
                {
                    if (m_ImpactEffects[i] == null)
                    {
                        Debug.LogError($"[BasicAttackData] {name}: 비어 있는 적중 효과가 있습니다.", this);
                        valid = false;
                    }
                    else
                    {
                        valid &= m_ImpactEffects[i].IsValid(this);
                    }
                }
            }
            return valid;
        }

#if UNITY_EDITOR
        public void ConfigureForEditor(
            float attackPowerMultiplier,
            float criticalChance,
            float failureRetryDelay,
            AttackDeliveryDefinition delivery,
            params AttackImpactEffectDefinition[] effects)
        {
            m_AttackPowerMultiplier = attackPowerMultiplier;
            m_CriticalChance = criticalChance;
            m_FailureRetryDelay = failureRetryDelay;
            m_Delivery = delivery;
            m_ImpactEffects.Clear();
            if (effects != null) m_ImpactEffects.AddRange(effects);
        }

        private void OnValidate()
        {
            m_AttackPowerMultiplier = Mathf.Max(0f, m_AttackPowerMultiplier);
            m_CriticalChance = Mathf.Clamp01(m_CriticalChance);
            m_FailureRetryDelay = Mathf.Max(0.05f, m_FailureRetryDelay);
            m_ProjectileReleaseFrameOffset = Mathf.Max(0f, m_ProjectileReleaseFrameOffset);
        }
#endif
    }
}
#endif
