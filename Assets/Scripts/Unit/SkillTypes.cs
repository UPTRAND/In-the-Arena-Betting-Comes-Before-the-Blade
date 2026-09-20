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
        public bool IgnoreDefense;

        // 공용 보호막과 커스텀 흡수 효과가 누적하는 실제 흡수량입니다.
        public float ShieldAbsorbed;
    }

    /// <summary>한 피해 요청으로 실제 적용된 결과입니다. 후속 반응은 이 값을 기준으로 계산합니다.</summary>
    public readonly struct DamageResult
    {
        public readonly float HpLost;
        public readonly float ShieldAbsorbed;
        public readonly bool Killed;

        /// <summary>피해 처리 완료 시 결과를 고정합니다.</summary>
        public DamageResult(float hpLost, float shieldAbsorbed, bool killed)
        {
            HpLost = hpLost;
            ShieldAbsorbed = shieldAbsorbed;
            Killed = killed;
        }
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

    /// <summary>
    /// 한 클래스에서 설정과 동작을 작성하는 스킬 확장점입니다.
    /// 에셋은 원본 설정만 보관하며, CreateRuntime은 유닛별 복사본을 만듭니다.
    /// 가변 실행 필드는 NonSerialized로 선언하고 OnCreated에서 준비합니다.
    /// </summary>
    [Serializable]
    public abstract class SkillBehaviorDefinition
    {
        /// <summary>Targeting 에셋 없이 대상 선택과 재검증을 직접 구현할 때 활성화합니다.</summary>
        public virtual bool UsesCustomTargeting => false;

        /// <summary>Unity 직렬화로 설정을 복제하여 리스트와 실행 상태의 공유를 방지합니다.</summary>
        public virtual SkillBehaviorRuntime CreateRuntime()
        {
            string settings = JsonUtility.ToJson(this);
            var instance = (SkillBehaviorDefinition)JsonUtility.FromJson(settings, GetType());

            return new SkillBehaviorRuntime(instance);
        }

        /// <summary>유닛별 행동이 생성될 때 실행 상태나 이벤트 구독을 준비합니다.</summary>
        public virtual void OnCreated(SkillRuntime runtime)
        {
        }

        /// <summary>풀 반환 또는 유닛 파괴 전에 구독과 지연 동작을 해제합니다.</summary>
        public virtual void OnRemoved(SkillRuntime runtime)
        {
        }

        /// <summary>기본 Targeting을 사용하거나 이 함수에서 직접 대상을 선정합니다.</summary>
        public virtual bool TryResolveTargets(
            SkillRuntime runtime,
            in SkillUseRequest request,
            SkillTargetSet targets)
        {
            if (runtime.Data.Targeting == null)
            {
                return false;
            }

            return runtime.Data.Targeting.TryResolve(runtime.Owner, runtime.Data, request, targets);
        }

        /// <summary>시전 완료 시 대상을 재검사합니다. 직접 타기팅 시 함께 재정의합니다.</summary>
        public virtual bool RevalidateTargets(SkillRuntime runtime, SkillTargetSet targets)
        {
            if (runtime.Data.Targeting == null)
            {
                return false;
            }

            return runtime.Data.Targeting.Revalidate(runtime.Owner, runtime.Data, targets);
        }

        /// <summary>대상 외 추가 실행 조건을 검사합니다. 상태를 변경하지 않습니다.</summary>
        public virtual bool CanExecute(SkillRuntime runtime, Unit owner, SkillTargetSet targets)
        {
            return true;
        }

        /// <summary>시전 완료 시 스킬을 실행하고 실제 적용 결과를 반환합니다.</summary>
        public virtual SkillExecutionResult Execute(in SkillEffectContext context)
        {
            return SkillExecutionResult.NoEffect;
        }

        /// <summary>쿨다운 중에도 주기 동작이나 여러 단계로 진행되는 스킬을 갱신합니다.</summary>
        public virtual void Tick(SkillRuntime runtime, float deltaTime)
        {
        }

        /// <summary>공격·피격·처치·전투 생명주기 이벤트에 반응합니다.</summary>
        public virtual void OnTrigger(SkillRuntime runtime, in SkillTriggerContext context)
        {
        }

        /// <summary>커스텀 스킬이 사용하는 투사체를 전투 준비 단계의 풀 예열에 포함합니다.</summary>
        public virtual void CollectProjectilePrefabs(System.Collections.Generic.List<GameObject> output)
        {
        }
    }

    /// <summary>모든 단일 클래스 스킬에 공통으로 사용하는 실행 어댑터입니다.</summary>
    public class SkillBehaviorRuntime
    {
        protected readonly SkillBehaviorDefinition Definition;

        private SkillRuntime m_Runtime;

        /// <summary>유닛 전용 행동 복사본을 보관합니다. 기존 Runtime 상속도 지원합니다.</summary>
        public SkillBehaviorRuntime(SkillBehaviorDefinition definition)
        {
            Definition = definition;
        }

        /// <summary>소유 런타임을 연결한 후 행동의 초기화를 호출합니다.</summary>
        internal void Bind(SkillRuntime runtime)
        {
            m_Runtime = runtime;
            Definition?.OnCreated(runtime);
        }

        /// <summary>행동이 정의한 대상 선택을 실행합니다.</summary>
        public virtual bool TryResolveTargets(
            SkillRuntime runtime,
            in SkillUseRequest request,
            SkillTargetSet targets)
        {
            return Definition.TryResolveTargets(runtime, request, targets);
        }

        /// <summary>행동이 정의한 시전 완료 대상 검증을 실행합니다.</summary>
        public virtual bool RevalidateTargets(SkillRuntime runtime, SkillTargetSet targets)
        {
            return Definition.RevalidateTargets(runtime, targets);
        }

        /// <summary>실행 전 추가 조건을 전달합니다.</summary>
        public virtual bool CanExecute(SkillRuntime runtime, Unit owner, SkillTargetSet targets)
        {
            return Definition.CanExecute(runtime, owner, targets);
        }

        /// <summary>행동의 실제 스킬 실행 함수를 호출합니다.</summary>
        public virtual SkillExecutionResult Execute(in SkillEffectContext context)
        {
            return Definition.Execute(context);
        }

        /// <summary>고정 간격 갱신을 행동으로 전달합니다.</summary>
        public virtual void Tick(SkillRuntime runtime, float deltaTime)
        {
            Definition?.Tick(runtime, deltaTime);
        }

        /// <summary>전투 이벤트를 행동으로 전달합니다.</summary>
        public virtual void OnTrigger(SkillRuntime runtime, in SkillTriggerContext context)
        {
            Definition?.OnTrigger(runtime, context);
        }

        /// <summary>소유자가 유효할 때 정리 콜백을 한 번 실행하고 참조를 해제합니다.</summary>
        public virtual void Reset()
        {
            if (m_Runtime == null)
            {
                return;
            }

            SkillRuntime runtime = m_Runtime;
            m_Runtime = null;
            Definition?.OnRemoved(runtime);
        }
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
