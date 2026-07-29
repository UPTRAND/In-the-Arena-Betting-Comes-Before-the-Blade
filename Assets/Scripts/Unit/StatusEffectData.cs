#if UNITY_6000_0_OR_NEWER
using System;
using System.Collections.Generic;
using MackySoft.SerializeReferenceExtensions;
using UnityEngine;

namespace InTheArena.Unit
{
    public enum StatusEffectType
    {
        Buff = 0,
        Debuff = 1
    }

    public enum StackType
    {
        None = 0,
        Duration = 1,
        Intensity = 2,
        Both = 3
    }

    [Serializable]
    public abstract class StatusEffectBehaviorDefinition
    {
        public virtual bool GrantsStun => false;
        public virtual bool GrantsSilence => false;
        public virtual void OnApply(StatusEffectRuntime runtime) { }
        public virtual void OnTick(StatusEffectRuntime runtime, float deltaTime) { }
        public virtual void OnStacksChanged(StatusEffectRuntime runtime, int previous, int current) { }
        public virtual void ModifyIncomingDamage(StatusEffectRuntime runtime, ref DamageContext context) { }
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
        public float Duration => Mathf.Max(0f, m_Duration);
        public StackType StackType => m_StackType;
        public int MaxStacks => Mathf.Max(1, m_MaxStacks);
        public StatusEffectBehaviorDefinition Behavior => m_Behavior;
        public abstract StatusEffectType EffectType { get; }
    }

    public sealed class StatusEffectRuntime
    {
        private bool m_IsApplied;

        public StatusEffectData Data { get; private set; }
        public Unit Owner { get; private set; }
        public UnitHandle Caster { get; private set; }
        public float RemainingTime { get; set; }
        public float CustomTimer { get; set; }
        public float FloatState { get; set; }
        public int IntState { get; set; }
        public int Stacks { get; set; }
        public bool IsPermanent => Data != null && Data.Duration <= 0f;
        public bool GrantsStun => Data?.Behavior?.GrantsStun == true;
        public bool GrantsSilence => Data?.Behavior?.GrantsSilence == true;

        public void Initialize(StatusEffectData data, Unit owner, Unit caster, float duration)
        {
            Data = data;
            Owner = owner;
            Caster = new UnitHandle(caster);
            RemainingTime = duration > 0f ? duration : data.Duration;
            CustomTimer = 0f;
            FloatState = 0f;
            IntState = 0;
            Stacks = 1;
            m_IsApplied = false;
        }

        public void Apply()
        {
            if (m_IsApplied || Data == null) return;
            m_IsApplied = true;
            Data.Behavior?.OnApply(this);
        }

        public bool Tick(float deltaTime)
        {
            if (Data == null) return false;
            if (!IsPermanent)
            {
                RemainingTime -= deltaTime;
                if (RemainingTime <= 0f) return false;
            }
            Data.Behavior?.OnTick(this, deltaTime);
            return IsPermanent || RemainingTime > 0f;
        }

        public void Refresh(float duration)
        {
            if (Data == null) return;
            int previousStacks = Stacks;
            if (Data.StackType == StackType.Intensity || Data.StackType == StackType.Both)
                Stacks = Mathf.Min(Data.MaxStacks, Stacks + 1);
            if (Data.StackType == StackType.Duration || Data.StackType == StackType.Both ||
                Data.StackType == StackType.None)
                RemainingTime = duration > 0f ? duration : Data.Duration;
            if (Stacks != previousStacks)
                Data.Behavior?.OnStacksChanged(this, previousStacks, Stacks);
        }

        public void ModifyIncomingDamage(ref DamageContext context)
            => Data?.Behavior?.ModifyIncomingDamage(this, ref context);

        public void Release(bool expired)
        {
            if (m_IsApplied) Data?.Behavior?.OnRemove(this, expired);
            Data = null;
            Owner = null;
            Caster = default;
            RemainingTime = 0f;
            CustomTimer = 0f;
            FloatState = 0f;
            IntState = 0;
            Stacks = 0;
            m_IsApplied = false;
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
            => Pool.Count > 0 ? Pool.Pop() : new StatusEffectRuntime();

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
            => runtime.Owner?.ApplyStatModifier(
                m_Modifier,
                runtime.Data.EffectType == StatusEffectType.Buff);

        public override void OnStacksChanged(StatusEffectRuntime runtime, int previous, int current)
        {
            int delta = current - previous;
            if (delta == 0) return;
            runtime.Owner?.ApplyStatModifier(
                m_Modifier.Multiply(delta),
                runtime.Data.EffectType == StatusEffectType.Buff);
        }

        public override void OnRemove(StatusEffectRuntime runtime, bool expired)
            => runtime.Owner?.RemoveStatModifier(
                m_Modifier.Multiply(runtime.Stacks),
                runtime.Data.EffectType == StatusEffectType.Buff);
    }

    [Serializable]
    public sealed class PeriodicDamageStatusBehavior : StatusEffectBehaviorDefinition
    {
        [SerializeField, Min(0f)] private float m_Damage = 5f;
        [SerializeField, Min(0.05f)] private float m_Interval = 1f;

        public override void OnTick(StatusEffectRuntime runtime, float deltaTime)
        {
            runtime.CustomTimer += deltaTime;
            while (runtime.CustomTimer >= m_Interval)
            {
                runtime.CustomTimer -= m_Interval;
                Unit owner = runtime.Owner;
                if (owner == null || owner.IsDead) return;
                var damage = new DamageContext
                {
                    Source = runtime.Caster,
                    Target = owner,
                    Amount = m_Damage * runtime.Stacks,
                    IsCritical = false,
                    IsSkill = true,
                    IsReaction = false
                };
                owner.ApplyDamage(in damage);
            }
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
            while (runtime.CustomTimer >= m_Interval)
            {
                runtime.CustomTimer -= m_Interval;
                Unit owner = runtime.Owner;
                if (owner == null || owner.IsDead) return;
                var heal = new HealContext
                {
                    Source = runtime.Caster,
                    Target = owner,
                    Amount = m_Heal * runtime.Stacks,
                    IsSkill = true,
                    IsReaction = false
                };
                owner.Heal(in heal);
            }
        }
    }

    [Serializable]
    public sealed class StunStatusBehavior : StatusEffectBehaviorDefinition
    {
        public override bool GrantsStun => true;
    }

    [Serializable]
    public sealed class SilenceStatusBehavior : StatusEffectBehaviorDefinition
    {
        public override bool GrantsSilence => true;
    }

    [Serializable]
    public sealed class ShieldStatusBehavior : StatusEffectBehaviorDefinition
    {
        [SerializeField, Min(0f)] private float m_ShieldAmount = 25f;
        [SerializeField, Min(0f)] private float m_MaxHealthRatio;

        public override void OnApply(StatusEffectRuntime runtime)
        {
            runtime.FloatState = m_ShieldAmount +
                                 (runtime.Owner != null ? runtime.Owner.MaxHp * m_MaxHealthRatio : 0f);
        }

        public override void OnStacksChanged(StatusEffectRuntime runtime, int previous, int current)
        {
            int added = Mathf.Max(0, current - previous);
            if (added <= 0) return;
            runtime.FloatState += added * (m_ShieldAmount +
                (runtime.Owner != null ? runtime.Owner.MaxHp * m_MaxHealthRatio : 0f));
        }

        public override void ModifyIncomingDamage(StatusEffectRuntime runtime, ref DamageContext context)
        {
            if (context.Amount <= 0f || runtime.FloatState <= 0f) return;
            float absorbed = Mathf.Min(runtime.FloatState, context.Amount);
            runtime.FloatState -= absorbed;
            context.Amount -= absorbed;
            runtime.Owner?.OnShieldAbsorbCallback(absorbed);
            if (runtime.FloatState <= 0f) runtime.RemainingTime = 0f;
        }
    }
}
#endif
