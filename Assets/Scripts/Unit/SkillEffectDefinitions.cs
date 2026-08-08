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
        [SerializeField] private GameObject m_ImpactVfxPrefab;
        [SerializeField] private Vector3 m_ImpactVfxOffset;
        [SerializeField, Min(0f)] private float m_ImpactVfxScale = 1f;
        [SerializeField, Min(0f)] private float m_ImpactVfxDuration;

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
                float actualDamage = target.ApplyDamage(in damage);
                if (actualDamage <= 0f) continue;
                SkillVfxUtility.TryRequest(
                    m_ImpactVfxPrefab,
                    SkillVfxSpawnPosition.TargetHitAnchor,
                    m_ImpactVfxOffset,
                    context.Owner,
                    context.Targets,
                    target,
                    target.HitPosition,
                    m_ImpactVfxScale,
                    m_ImpactVfxDuration);
                context.Owner.LogCombatAction(
                    SkillCombatLogUtility.GetSkillLogName(context.Runtime),
                    target,
                    actualDamage,
                    "피해");
                applied = true;
            }
            return applied ? SkillExecutionResult.Success : SkillExecutionResult.NoEffect;
        }
    }

    [Serializable]
    public sealed class MultiHitDamageSkillEffect : SkillEffectDefinition
    {
        [SerializeField] private float[] m_Damages = { 4f, 5f, 6f };
        [SerializeField] private GameObject m_HitVfxPrefab;
        [SerializeField] private Vector3 m_HitVfxOffset;
        [SerializeField, Min(0f)] private float m_HitVfxScale = 1f;
        [SerializeField, Min(0f)] private float m_HitVfxDuration;

        public override SkillExecutionResult Apply(in SkillEffectContext context)
        {
            Unit target = context.Targets.Count > 0 ? context.Targets[0].Unit : null;
            if (target == null || target.IsDead || target.Team == context.Owner.Team)
                return SkillExecutionResult.InvalidTarget;

            bool applied = false;
            for (int i = 0; m_Damages != null && i < m_Damages.Length; i++)
            {
                float amount = Mathf.Max(0f, m_Damages[i]);
                if (amount <= 0f || target.IsDead) continue;

                var damage = new DamageContext
                {
                    Source = new UnitHandle(context.Owner),
                    Target = target,
                    Amount = amount + target.CurrentDefense,
                    IsCritical = false,
                    IsSkill = true,
                    IsReaction = context.IsReaction
                };

                float actualDamage = target.ApplyDamage(in damage);
                if (actualDamage <= 0f) continue;
                SkillVfxUtility.TryRequest(
                    m_HitVfxPrefab,
                    SkillVfxSpawnPosition.TargetHitAnchor,
                    m_HitVfxOffset,
                    context.Owner,
                    context.Targets,
                    target,
                    target.HitPosition,
                    m_HitVfxScale,
                    m_HitVfxDuration);
                context.Owner.LogCombatAction(
                    SkillCombatLogUtility.GetSkillLogName(context.Runtime),
                    target,
                    actualDamage,
                    "피해");
                applied = true;
            }

            return applied ? SkillExecutionResult.Success : SkillExecutionResult.NoEffect;
        }
    }

    [Serializable]
    public sealed class FixedDamageSkillEffect : SkillEffectDefinition
    {
        [SerializeField, Min(0f)] private float m_Damage = 10f;
        [SerializeField, Min(0f)] private float m_DefenseReduction;
        [SerializeField] private GameObject m_ImpactVfxPrefab;
        [SerializeField] private Vector3 m_ImpactVfxOffset;
        [SerializeField, Min(0f)] private float m_ImpactVfxScale = 1f;
        [SerializeField, Min(0f)] private float m_ImpactVfxDuration;

        public override SkillExecutionResult Apply(in SkillEffectContext context)
        {
            bool applied = false;
            for (int i = 0; i < context.Targets.Count; i++)
            {
                Unit target = context.Targets[i].Unit;
                if (target == null || target.IsDead || target.Team == context.Owner.Team) continue;

                var damage = new DamageContext
                {
                    Source = new UnitHandle(context.Owner),
                    Target = target,
                    Amount = m_Damage + target.CurrentDefense,
                    IsCritical = false,
                    IsSkill = true,
                    IsReaction = context.IsReaction
                };

                float actualDamage = target.ApplyDamage(in damage);
                if (actualDamage <= 0f) continue;
                SkillVfxUtility.TryRequest(
                    m_ImpactVfxPrefab,
                    SkillVfxSpawnPosition.TargetHitAnchor,
                    m_ImpactVfxOffset,
                    context.Owner,
                    context.Targets,
                    target,
                    target.HitPosition,
                    m_ImpactVfxScale,
                    m_ImpactVfxDuration);
                string skillName = SkillCombatLogUtility.GetSkillLogName(context.Runtime);
                context.Owner.LogCombatAction(
                    skillName,
                    target,
                    actualDamage,
                    "피해");
                ApplyDefenseReduction(context.Owner, target, skillName);
                applied = true;
            }

            return applied ? SkillExecutionResult.Success : SkillExecutionResult.NoEffect;
        }

        private void ApplyDefenseReduction(Unit source, Unit target, string skillName)
        {
            if (target == null || target.IsDead || m_DefenseReduction <= 0f) return;

            float previousDefense = target.CurrentDefense;
            target.ApplyStatModifier(
                new UnitStat { defense = m_DefenseReduction },
                isBuff: false);
            float currentDefense = target.CurrentDefense;
            float actualReduction = Mathf.Max(0f, previousDefense - currentDefense);
            source?.LogDefenseReduction(skillName, target, actualReduction, previousDefense, currentDefense);
        }
    }

    [Serializable]
    public sealed class AnvilDropSkillEffect : SkillEffectDefinition
    {
        [SerializeField] private GameObject m_AnvilPrefab;
        [SerializeField, Min(0f)] private float m_Damage = 15f;
        [SerializeField, Min(0.1f)] private float m_ImpactRadius = 1.25f;
        [SerializeField, Min(0.1f)] private float m_SpawnHeight = 2.5f;
        [SerializeField, Min(0f)] private float m_SpawnDepth = 1.25f;
        [SerializeField, Min(0.05f)] private float m_FallDuration = 0.35f;
        [SerializeField] private float m_BaseXRotationDegrees = 45f;
        [SerializeField] private float m_StartZRotationDegrees = -25f;
        [SerializeField] private float m_ZRotationDegrees = 180f;
        [SerializeField] private GameObject m_ImpactVfxPrefab;
        [SerializeField] private Vector3 m_ImpactVfxOffset;
        [SerializeField, Min(0f)] private float m_ImpactVfxScale = 1f;
        [SerializeField, Min(0f)] private float m_ImpactVfxDuration;

        public override SkillExecutionResult Apply(in SkillEffectContext context)
        {
            if (context.Owner == null || context.Owner.IsDead || m_AnvilPrefab == null)
                return SkillExecutionResult.NoEffect;

            Vector3 damageCenter = context.Targets.HasGroundPosition
                ? context.Targets.GroundPosition
                : context.Owner.GroundPosition;
            Vector3 visualImpactPosition = ResolveVisualImpactPosition(context.Targets, damageCenter);

            GameObject anvilObject = UnityEngine.Object.Instantiate(
                m_AnvilPrefab,
                visualImpactPosition + Vector3.up * Mathf.Max(0.1f, m_SpawnHeight),
                Quaternion.identity);
            AnvilDrop anvilDrop = anvilObject.GetComponent<AnvilDrop>();
            if (anvilDrop == null)
            {
                UnityEngine.Object.Destroy(anvilObject);
                return SkillExecutionResult.NoEffect;
            }

            anvilDrop.Initialize(
                context.Owner,
                visualImpactPosition,
                damageCenter,
                m_ImpactRadius,
                m_Damage,
                SkillCombatLogUtility.GetSkillLogName(context.Runtime),
                m_SpawnHeight,
                m_SpawnDepth,
                m_FallDuration,
                m_BaseXRotationDegrees,
                m_StartZRotationDegrees,
                m_ZRotationDegrees,
                m_ImpactVfxPrefab,
                m_ImpactVfxOffset,
                m_ImpactVfxScale,
                m_ImpactVfxDuration);
            return SkillExecutionResult.Success;
        }

        private static Vector3 ResolveVisualImpactPosition(SkillTargetSet targets, Vector3 damageCenter)
        {
            Unit best = null;
            float bestDistanceSqr = float.MaxValue;
            for (int i = 0; targets != null && i < targets.Count; i++)
            {
                Unit candidate = targets[i].Unit;
                if (candidate == null || candidate.IsDead) continue;

                Vector3 delta = candidate.GroundPosition - damageCenter;
                delta.y = 0f;
                float distanceSqr = delta.sqrMagnitude;
                if (distanceSqr >= bestDistanceSqr) continue;

                bestDistanceSqr = distanceSqr;
                best = candidate;
            }

            return best != null ? best.HitPosition : damageCenter + Vector3.up * 0.9f;
        }
    }

    [Serializable]
    public sealed class HealSkillEffect : SkillEffectDefinition
    {
        [SerializeField, Min(0f)] private float m_BaseHeal = 10f;
        [SerializeField, Min(0f)] private float m_AttackPowerRatio;
        [SerializeField, Range(0f, 1f)] private float m_MaxHealthRatio;
        [SerializeField] private GameObject m_HealVfxPrefab;
        [SerializeField] private Vector3 m_HealVfxOffset;
        [SerializeField, Min(0f)] private float m_HealVfxScale = 1f;
        [SerializeField, Min(0f)] private float m_HealVfxDuration;

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
                float actualHeal = target.Heal(in heal);
                SkillVfxUtility.TryRequest(
                    m_HealVfxPrefab,
                    target == context.Owner ? SkillVfxSpawnPosition.CasterCastAnchor : SkillVfxSpawnPosition.TargetHitAnchor,
                    m_HealVfxOffset,
                    context.Owner,
                    context.Targets,
                    target,
                    target.HitPosition,
                    m_HealVfxScale,
                    m_HealVfxDuration);
                context.Owner.LogCombatAction(
                    SkillCombatLogUtility.GetSkillLogName(context.Runtime),
                    target,
                    actualHeal,
                    "회복");
                applied = true;
            }
            return applied ? SkillExecutionResult.Success : SkillExecutionResult.NoEffect;
        }
    }

    internal static class SkillCombatLogUtility
    {
        public static string GetSkillLogName(SkillRuntime runtime)
            => !string.IsNullOrWhiteSpace(runtime?.Data?.SkillName)
                ? runtime.Data.SkillName
                : "스킬";

        public static string GetProjectileSkillLogName(in ProjectileImpactPayload payload)
            => !string.IsNullOrWhiteSpace(payload.ActionName)
                ? payload.ActionName
                : "스킬";
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
        public readonly float Scale;
        public readonly float Duration;

        public SkillVfxRequest(GameObject prefab, Vector3 position, Unit source, Unit target, float scale, float duration)
        {
            Prefab = prefab;
            Position = position;
            Source = new UnitHandle(source);
            Target = new UnitHandle(target);
            Scale = Mathf.Max(0f, scale);
            Duration = Mathf.Max(0f, duration);
        }
    }

    public static class SkillVfxRequestBus
    {
        public static event Action<SkillVfxRequest> Requested;
        public static bool HasListener => Requested != null;
        public static void Request(in SkillVfxRequest request) => Requested?.Invoke(request);
    }

    public enum SkillVfxSpawnPosition
    {
        TargetHitAnchor = 0,
        TargetGround = 1,
        CasterCastAnchor = 2,
        CasterGround = 3,
        GroundPosition = 4
    }

    internal static class SkillVfxUtility
    {
        public static bool TryRequest(
            GameObject prefab,
            SkillVfxSpawnPosition positionMode,
            Vector3 offset,
            Unit owner,
            SkillTargetSet targets,
            Unit target,
            Vector3 fallbackPosition,
            float scale = 1f,
            float duration = 0f)
        {
            if (prefab == null || !SkillVfxRequestBus.HasListener || owner == null)
                return false;

            Unit requestTarget = ResolveRequestTarget(positionMode, owner, target);
            Vector3 position = ResolvePosition(positionMode, owner, targets, target, fallbackPosition) + offset;
            var request = new SkillVfxRequest(prefab, position, owner, requestTarget, scale, duration);
            SkillVfxRequestBus.Request(in request);
            return true;
        }

        private static Unit ResolveRequestTarget(
            SkillVfxSpawnPosition positionMode,
            Unit owner,
            Unit target)
        {
            switch (positionMode)
            {
                case SkillVfxSpawnPosition.CasterCastAnchor:
                case SkillVfxSpawnPosition.CasterGround:
                    return owner;
                default:
                    return target;
            }
        }

        private static Vector3 ResolvePosition(
            SkillVfxSpawnPosition positionMode,
            Unit owner,
            SkillTargetSet targets,
            Unit target,
            Vector3 fallbackPosition)
        {
            switch (positionMode)
            {
                case SkillVfxSpawnPosition.TargetGround:
                    return target != null ? target.GroundPosition : fallbackPosition;
                case SkillVfxSpawnPosition.CasterCastAnchor:
                    return owner.CastPosition;
                case SkillVfxSpawnPosition.CasterGround:
                    return owner.GroundPosition;
                case SkillVfxSpawnPosition.GroundPosition:
                    return targets != null && targets.HasGroundPosition ? targets.GroundPosition : fallbackPosition;
                case SkillVfxSpawnPosition.TargetHitAnchor:
                default:
                    return target != null ? target.HitPosition : fallbackPosition;
            }
        }
    }

    [Serializable]
    public sealed class SpawnVfxSkillEffect : SkillEffectDefinition
    {
        [SerializeField] private GameObject m_VfxPrefab;
        [SerializeField] private SkillVfxSpawnPosition m_Position = SkillVfxSpawnPosition.TargetHitAnchor;
        [SerializeField] private Vector3 m_Offset;
        [SerializeField, Min(0f)] private float m_Scale = 1f;
        [SerializeField, Min(0f)] private float m_Duration;
        [SerializeField] private bool m_SpawnForEachTarget = true;
        [SerializeField] private bool m_UseHitAnchor;

        public override SkillExecutionResult Apply(in SkillEffectContext context)
        {
            if (m_VfxPrefab == null || !SkillVfxRequestBus.HasListener || context.Owner == null)
                return SkillExecutionResult.NoEffect;

            SkillVfxSpawnPosition position = ResolvePositionMode();
            if (position == SkillVfxSpawnPosition.CasterCastAnchor ||
                position == SkillVfxSpawnPosition.CasterGround ||
                position == SkillVfxSpawnPosition.GroundPosition ||
                context.Targets.Count == 0)
            {
                Vector3 fallback = context.Targets.HasGroundPosition
                    ? context.Targets.GroundPosition
                    : context.Owner.HitPosition;
                return SkillVfxUtility.TryRequest(
                    m_VfxPrefab,
                    position,
                    m_Offset,
                    context.Owner,
                    context.Targets,
                    null,
                    fallback,
                    m_Scale,
                    m_Duration)
                    ? SkillExecutionResult.Success
                    : SkillExecutionResult.NoEffect;
            }

            bool requested = false;
            for (int i = 0; i < context.Targets.Count; i++)
            {
                Unit target = context.Targets[i].Unit;
                if (target == null) continue;
                requested |= SkillVfxUtility.TryRequest(
                    m_VfxPrefab,
                    position,
                    m_Offset,
                    context.Owner,
                    context.Targets,
                    target,
                    target.HitPosition,
                    m_Scale,
                    m_Duration);
                if (!m_SpawnForEachTarget) break;
            }
            return requested ? SkillExecutionResult.Success : SkillExecutionResult.NoEffect;
        }

        private SkillVfxSpawnPosition ResolvePositionMode()
        {
            if (m_Position != SkillVfxSpawnPosition.TargetHitAnchor)
                return m_Position;

            return m_UseHitAnchor
                ? SkillVfxSpawnPosition.TargetHitAnchor
                : SkillVfxSpawnPosition.TargetGround;
        }
    }

    [Serializable]
    public sealed class SpawnProjectileSkillEffect : SkillEffectDefinition, IProjectileImpactResolver
    {
        [SerializeField] private ProjectileData m_ProjectileData;
        [SerializeField] private GameObject m_ProjectilePrefab;
        [SerializeField, Min(0.1f)] private float m_Speed = 20f;
        [SerializeField, Min(0.1f)] private float m_Lifetime = 5f;
        [SerializeField, Min(0f)] private float m_BaseDamage = 10f;
        [SerializeField, Min(0f)] private float m_AttackPowerRatio = 0.5f;
        [SerializeField, Min(0f)] private float m_ExplosionRadius;
        [SerializeField, Range(0f, 1f)] private float m_CriticalChance = 0.1f;
        [SerializeField] private StatusEffectData m_ImpactStatus;
        [SerializeField] private GameObject m_ImpactVfxPrefab;
        [SerializeField] private Vector3 m_ImpactVfxOffset;
        [SerializeField, Min(0f)] private float m_ImpactVfxScale = 1f;
        [SerializeField, Min(0f)] private float m_ImpactVfxDuration;

        public override SkillExecutionResult Apply(in SkillEffectContext context)
        {
            Unit target = context.Targets.Count > 0 ? context.Targets[0].Unit : null;
            GameObject projectilePrefab = m_ProjectileData != null
                ? m_ProjectileData.Prefab
                : m_ProjectilePrefab;
            if (projectilePrefab == null || target == null || target.IsDead)
                return SkillExecutionResult.InvalidTarget;

            var payload = new ProjectileImpactPayload(
                new UnitHandle(context.Owner),
                context.Owner.Team,
                m_BaseDamage + context.Owner.CurrentAttackPower * m_AttackPowerRatio,
                UnityEngine.Random.value < m_CriticalChance,
                true,
                context.IsReaction,
                this,
                SkillCombatLogUtility.GetSkillLogName(context.Runtime));

            ProjectilePoolService projectiles = PoolManager.Require().Projectiles;
            bool spawned = m_ProjectileData != null
                ? projectiles.TrySpawn(
                    m_ProjectileData,
                    context.Owner.CastPosition,
                    new UnitHandle(target),
                    payload,
                    out _)
                : projectiles.TrySpawn(
                    projectilePrefab,
                    context.Owner.CastPosition,
                    new UnitHandle(target),
                    payload,
                    m_Speed,
                    m_Lifetime,
                    out _);
            if (!spawned)
                return SkillExecutionResult.PoolExhausted;

            return SkillExecutionResult.Success;
        }

        public bool ApplyImpact(
            in ProjectileImpactPayload payload,
            Unit primaryTarget,
            Vector3 impactPosition)
        {
            if (primaryTarget == null || primaryTarget.IsDead) return false;
            SkillVfxUtility.TryRequest(
                m_ImpactVfxPrefab,
                SkillVfxSpawnPosition.GroundPosition,
                m_ImpactVfxOffset,
                payload.Source.Unit,
                null,
                primaryTarget,
                impactPosition,
                m_ImpactVfxScale,
                m_ImpactVfxDuration);
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
                applied |= ApplyTo(in payload, candidate, 1f);
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
            float actualDamage = target.ApplyDamage(in damage);
            bool applied = actualDamage > 0f;
            if (applied && payload.IsSkill)
            {
                Unit source = payload.Source.Unit;
                source?.LogCombatAction(
                    SkillCombatLogUtility.GetProjectileSkillLogName(in payload),
                    target,
                    actualDamage,
                    "피해");
            }
            if (m_ImpactStatus != null && !target.IsDead)
                applied |= target.ApplyStatusEffect(m_ImpactStatus, payload.Source.Unit) != null;
            return applied;
        }

        public override void CollectProjectilePrefabs(List<GameObject> output)
        {
            GameObject prefab = m_ProjectileData != null ? m_ProjectileData.Prefab : m_ProjectilePrefab;
            if (prefab != null && output != null && !output.Contains(prefab)) output.Add(prefab);
        }
    }
}
#endif
