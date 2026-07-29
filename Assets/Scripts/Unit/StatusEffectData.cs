#if UNITY_6000_0_OR_NEWER
using System;
using System.Collections.Generic;
using MackySoft.SerializeReferenceExtensions;
using UnityEngine;

namespace InTheArena.Unit
{
    [Serializable]
    public abstract class StatusEffectBehaviorDefinition
    {
        public virtual void OnApply(StatusEffectRuntime runtime) { }
        public virtual void OnTick(StatusEffectRuntime runtime, float deltaTime) { }
        public virtual void OnRemove(StatusEffectRuntime runtime, bool expired) { }
    }

    public abstract class StatusEffectData : ScriptableObject
    {
        [SerializeField] private string m_EffectName;
        [SerializeField, TextArea(2, 3)] private string m_Description;
        [SerializeField] private Sprite m_Icon;
        [SerializeField, Min(0f)] private float m_Duration = 1f;
        [SerializeField] private StackType m_StackType = StackType.Duration;
        [SerializeField, Min(1)] private int m_MaxStacks = 1;
        [SerializeReference, SubclassSelector] private StatusEffectBehaviorDefinition m_Behavior;

        public string EffectName => m_EffectName;
        public string Description => m_Description;
        public Sprite Icon => m_Icon;
        public float Duration => m_Duration;
        public StackType StackType => m_StackType;
        public int MaxStacks => Mathf.Max(1, m_MaxStacks);
        public StatusEffectBehaviorDefinition Behavior => m_Behavior;
        public abstract StatusEffectType EffectType { get; }
    }

    [CreateAssetMenu(fileName = "BuffData_", menuName = "In The Arena/Unit/Status Effect/Buff Data")]
    public sealed class BuffData : StatusEffectData
    {
        public override StatusEffectType EffectType => StatusEffectType.Buff;
    }

    [CreateAssetMenu(fileName = "DebuffData_", menuName = "In The Arena/Unit/Status Effect/Debuff Data")]
    public sealed class DebuffData : StatusEffectData
    {
        public override StatusEffectType EffectType => StatusEffectType.Debuff;
    }

    public sealed class StatusEffectRuntime
    {
        public StatusEffectData Data { get; private set; }
        public Unit Owner { get; private set; }
        public Unit Caster { get; private set; }
        public float RemainingTime { get; set; }
        public float CustomTimer { get; set; }
        public int Stacks { get; set; }

        public void Initialize(StatusEffectData data, Unit owner, Unit caster, float duration)
        {
            Data = data;
            Owner = owner;
            Caster = caster;
            RemainingTime = duration > 0f ? duration : data.Duration;
            CustomTimer = 0f;
            Stacks = 1;
            data.Behavior?.OnApply(this);
        }

        public bool Tick(float deltaTime)
        {
            RemainingTime -= deltaTime;
            Data?.Behavior?.OnTick(this, deltaTime);
            return RemainingTime > 0f;
        }

        public void Refresh(float duration)
        {
            if (Data == null) return;
            if (Data.StackType == StackType.Intensity || Data.StackType == StackType.Both)
                Stacks = Mathf.Min(Data.MaxStacks, Stacks + 1);
            if (Data.StackType != StackType.Intensity)
                RemainingTime = duration > 0f ? duration : Data.Duration;
        }

        public void Release(bool expired)
        {
            Data?.Behavior?.OnRemove(this, expired);
            Data = null;
            Owner = null;
            Caster = null;
            RemainingTime = 0f;
            CustomTimer = 0f;
            Stacks = 0;
        }
    }

    internal static class StatusEffectRuntimePool
    {
        private static readonly Stack<StatusEffectRuntime> Pool = new Stack<StatusEffectRuntime>(256);

        public static void Prewarm(int count)
        {
            while (Pool.Count < count) Pool.Push(new StatusEffectRuntime());
        }

        public static StatusEffectRuntime Rent()
        {
            return Pool.Count > 0 ? Pool.Pop() : new StatusEffectRuntime();
        }

        public static void Return(StatusEffectRuntime runtime)
        {
            if (runtime != null) Pool.Push(runtime);
        }
    }

    [Serializable]
    public sealed class StatModifierStatusBehavior : StatusEffectBehaviorDefinition
    {
        [SerializeField] private UnitStat m_Modifier;

        public override void OnApply(StatusEffectRuntime runtime)
        {
            runtime.Owner?.ApplyStatModifier(m_Modifier, runtime.Data.EffectType == StatusEffectType.Buff);
        }

        public override void OnRemove(StatusEffectRuntime runtime, bool expired)
        {
            runtime.Owner?.RemoveStatModifier(m_Modifier, runtime.Data.EffectType == StatusEffectType.Buff);
        }
    }

    [Serializable]
    public sealed class PeriodicDamageStatusBehavior : StatusEffectBehaviorDefinition
    {
        [SerializeField, Min(0f)] private float m_Damage = 5f;
        [SerializeField, Min(0.05f)] private float m_Interval = 1f;

        public override void OnTick(StatusEffectRuntime runtime, float deltaTime)
        {
            runtime.CustomTimer += deltaTime;
            if (runtime.CustomTimer < m_Interval) return;
            runtime.CustomTimer -= m_Interval;
            runtime.Owner?.ApplyDamage(m_Damage * runtime.Stacks, runtime.Caster, false, true);
        }
    }

    [Serializable]
    public sealed class PeriodicHealStatusBehavior : StatusEffectBehaviorDefinition
    {
        [SerializeField, Min(0f)] private float m_Heal = 5f;
        [SerializeField, Min(0.05f)] private float m_Interval = 1f;

        public override void OnTick(StatusEffectRuntime runtime, float deltaTime)
        {
            runtime.CustomTimer += deltaTime;
            if (runtime.CustomTimer < m_Interval) return;
            runtime.CustomTimer -= m_Interval;
            runtime.Owner?.Heal(m_Heal * runtime.Stacks, runtime.Caster);
        }
    }

    [Serializable]
    public sealed class StunStatusBehavior : StatusEffectBehaviorDefinition
    {
        public override void OnApply(StatusEffectRuntime runtime) => runtime.Owner?.SetStunned(true);
        public override void OnRemove(StatusEffectRuntime runtime, bool expired) => runtime.Owner?.SetStunned(false);
    }

    [Serializable]
    public sealed class SilenceStatusBehavior : StatusEffectBehaviorDefinition
    {
        public override void OnApply(StatusEffectRuntime runtime) => runtime.Owner?.SetSilenced(true);
        public override void OnRemove(StatusEffectRuntime runtime, bool expired) => runtime.Owner?.SetSilenced(false);
    }
}
#endif
