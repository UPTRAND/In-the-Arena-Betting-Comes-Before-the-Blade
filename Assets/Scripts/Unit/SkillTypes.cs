#if UNITY_6000_0_OR_NEWER
using System;
using UnityEngine;

namespace InTheArena.Unit
{
    public enum SkillType
    {
        Active = 0,
        Passive = 1
    }

    public enum SkillExecutionMode
    {
        EffectsOnly = 0,
        BehaviorThenEffects = 1,
        BehaviorOnly = 2
    }

    public enum SkillExecutionResult
    {
        Success = 0,
        InvalidTarget = 1,
        NoEffect = 2,
        PoolExhausted = 3,
        Interrupted = 4
    }

    public enum SkillTargetRelation
    {
        Enemy = 0,
        Ally = 1,
        Any = 2
    }

    public enum SkillTriggerType
    {
        OnAttack = 0,
        OnDamaged = 1,
        OnKill = 2,
        OnLowHealth = 3,
        OnBattleStart = 4,
        OnBattleEnd = 5,
        Always = 6
    }

    [Flags]
    public enum SkillEventFlags
    {
        None = 0,
        Skill = 1 << 0,
        Critical = 1 << 1,
        Reaction = 1 << 2
    }

    public readonly struct UnitHandle
    {
        private readonly Unit m_Unit;
        private readonly int m_SpawnVersion;

        public UnitHandle(Unit unit)
        {
            m_Unit = unit;
            m_SpawnVersion = unit != null ? unit.SpawnVersion : 0;
        }

        public Unit Unit => IsValid ? m_Unit : null;
        public int SpawnVersion => m_SpawnVersion;
        public bool IsValid => m_Unit != null && m_Unit.SpawnVersion == m_SpawnVersion;
        public bool IsAlive => IsValid && !m_Unit.IsDead && m_Unit.gameObject.activeInHierarchy;
    }

    public readonly struct SkillUseRequest
    {
        public readonly UnitHandle TargetHint;
        public readonly Vector3 GroundPosition;
        public readonly bool HasGroundPosition;

        public SkillUseRequest(Unit targetHint)
        {
            TargetHint = new UnitHandle(targetHint);
            GroundPosition = default;
            HasGroundPosition = false;
        }

        public SkillUseRequest(Vector3 groundPosition)
        {
            TargetHint = default;
            GroundPosition = groundPosition;
            HasGroundPosition = true;
        }
    }

    public struct SkillTriggerContext
    {
        public SkillTriggerType Trigger;
        public UnitHandle Receiver;
        public UnitHandle Source;
        public UnitHandle Target;
        public float Amount;
        public Vector3 Position;
        public SkillEventFlags Flags;

        public bool IsReaction => (Flags & SkillEventFlags.Reaction) != 0;
    }

    public struct DamageContext
    {
        public UnitHandle Source;
        public Unit Target;
        public float Amount;
        public bool IsCritical;
        public bool IsSkill;
        public bool IsReaction;
    }

    public struct HealContext
    {
        public UnitHandle Source;
        public Unit Target;
        public float Amount;
        public bool IsSkill;
        public bool IsReaction;
    }

    public sealed class SkillTargetSet
    {
        private const int MaximumTargets = 108;
        private readonly UnitHandle[] m_Targets = new UnitHandle[MaximumTargets];

        public int Count { get; private set; }
        public Vector3 GroundPosition { get; private set; }
        public bool HasGroundPosition { get; private set; }

        public UnitHandle this[int index] => index >= 0 && index < Count ? m_Targets[index] : default;

        public void Clear()
        {
            for (int i = 0; i < Count; i++) m_Targets[i] = default;
            Count = 0;
            GroundPosition = default;
            HasGroundPosition = false;
        }

        public bool Add(Unit unit)
        {
            if (unit == null || Count >= MaximumTargets) return false;
            for (int i = 0; i < Count; i++)
            {
                if (m_Targets[i].Unit == unit) return false;
            }

            m_Targets[Count++] = new UnitHandle(unit);
            return true;
        }

        public void SetGroundPosition(Vector3 position)
        {
            GroundPosition = position;
            HasGroundPosition = true;
        }
    }

    public readonly struct SkillEffectContext
    {
        public readonly SkillRuntime Runtime;
        public readonly Unit Owner;
        public readonly SkillTargetSet Targets;
        public readonly bool IsReaction;

        public SkillEffectContext(SkillRuntime runtime, Unit owner, SkillTargetSet targets, bool isReaction)
        {
            Runtime = runtime;
            Owner = owner;
            Targets = targets;
            IsReaction = isReaction;
        }
    }

    [Serializable]
    public abstract class SkillTargetingDefinition
    {
        public abstract bool TryResolve(
            Unit owner,
            SkillData data,
            in SkillUseRequest request,
            SkillTargetSet result);

        public virtual bool Revalidate(Unit owner, SkillData data, SkillTargetSet targets)
        {
            if (owner == null || owner.IsDead || targets == null) return false;
            for (int i = 0; i < targets.Count; i++)
            {
                Unit target = targets[i].Unit;
                if (target == null || target.IsDead) return false;
                if (!SkillTargetingUtility.IsInRange(owner, target.GroundPosition, data.Range)) return false;
            }
            return targets.Count > 0 || targets.HasGroundPosition;
        }
    }

    [Serializable]
    public abstract class SkillEffectDefinition
    {
        public abstract SkillExecutionResult Apply(in SkillEffectContext context);

        public virtual void CollectProjectilePrefabs(System.Collections.Generic.List<GameObject> output) { }
    }

    [Serializable]
    public abstract class SkillBehaviorDefinition
    {
        public virtual SkillBehaviorRuntime CreateRuntime() => new SkillBehaviorRuntime(this);
    }

    public class SkillBehaviorRuntime
    {
        protected readonly SkillBehaviorDefinition Definition;

        public SkillBehaviorRuntime(SkillBehaviorDefinition definition) => Definition = definition;

        public virtual bool CanExecute(SkillRuntime runtime, Unit owner, SkillTargetSet targets) => true;
        public virtual SkillExecutionResult Execute(in SkillEffectContext context) => SkillExecutionResult.NoEffect;
        public virtual void Tick(SkillRuntime runtime, float deltaTime) { }
        public virtual void OnTrigger(SkillRuntime runtime, in SkillTriggerContext context) { }
        public virtual void Reset() { }
    }

    internal static class SkillTargetingUtility
    {
        public static bool IsInRange(Unit owner, Vector3 position, float range)
        {
            if (owner == null) return false;
            if (range <= 0f) return true;
            Vector3 delta = position - owner.GroundPosition;
            delta.y = 0f;
            return delta.sqrMagnitude <= range * range;
        }

        public static bool MatchesRelation(Unit owner, Unit candidate, SkillTargetRelation relation)
        {
            if (owner == null || candidate == null || candidate.IsDead ||
                !candidate.gameObject.activeInHierarchy)
                return false;

            return relation == SkillTargetRelation.Any ||
                   relation == SkillTargetRelation.Enemy && owner.Team != candidate.Team ||
                   relation == SkillTargetRelation.Ally && owner.Team == candidate.Team;
        }
    }
}
#endif
