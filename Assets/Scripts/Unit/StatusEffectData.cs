#if UNITY_6000_0_OR_NEWER
using System;
using System.Collections.Generic;
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

        private int m_CallbackDepth;
        private bool m_IsRemoved;
        private bool m_ReleasePending;
        private bool m_ReturnPending;
        private bool m_IsInPool;
        private bool m_Expired;

        public StatusEffectData Data { get; private set; }
        public int Generation { get; private set; }
        public Unit Owner { get; private set; }
        public UnitHandle Caster { get; private set; }
        public float RemainingTime { get; set; }
        public float CustomTimer { get; set; }
        public float FloatState { get; set; }
        public int IntState { get; set; }
        public int Stacks { get; set; }
        public bool IsPermanent { get; private set; }
        public bool IsActive => Data != null && !m_IsRemoved && !m_Expired;
        public bool GrantsStun => IsActive && Data?.Behavior?.GrantsStun == true;
        public bool GrantsSilence => IsActive && Data?.Behavior?.GrantsSilence == true;

        /// <summary>원본 또는 덮어쓴 지속 시간으로 이번 적용의 수명을 확정합니다.</summary>
        public void Initialize(StatusEffectData data, Unit owner, Unit caster, float duration)
        {
            Generation++;
            Data = data;
            Owner = owner;
            Caster = new UnitHandle(caster);
            SetDuration(duration);
            CustomTimer = 0f;
            FloatState = 0f;
            IntState = 0;
            Stacks = 1;
            m_IsApplied = false;
            m_CallbackDepth = 0;
            m_IsRemoved = false;
            m_ReleasePending = false;
            m_ReturnPending = false;
            m_IsInPool = false;
            m_Expired = false;
        }

        /// <summary>적용 콜백이 끝나기 전에는 런타임을 초기화하거나 풀로 반환하지 않습니다.</summary>
        public void Apply()
        {
            if (m_IsApplied || !IsActive)
            {
                return;
            }

            m_IsApplied = true;
            m_CallbackDepth++;
            try
            {
                Data.Behavior?.OnApply(this);
            }
            finally
            {
                EndCallback();
            }
        }

        /// <summary>유효한 효과만 갱신하고 콜백 도중 제거되면 즉시 종료로 판정합니다.</summary>
        public bool Tick(float deltaTime)
        {
            if (!IsActive)
            {
                return false;
            }

            if (!IsPermanent)
            {
                RemainingTime -= deltaTime;
                if (RemainingTime <= 0f)
                {
                    return false;
                }
            }

            m_CallbackDepth++;
            try
            {
                Data.Behavior?.OnTick(this, deltaTime);
                return IsActive && (IsPermanent || RemainingTime > 0f);
            }
            finally
            {
                EndCallback();
            }
        }

        /// <summary>중첩 정책을 적용하고 갱신 콜백의 수명을 보호합니다.</summary>
        public void Refresh(float duration)
        {
            if (!IsActive)
            {
                return;
            }

            int previousStacks = Stacks;
            if (Data.StackType == StackType.Intensity || Data.StackType == StackType.Both)
            {
                Stacks = Mathf.Min(Data.MaxStacks, Stacks + 1);
            }

            if (Data.StackType == StackType.Duration || Data.StackType == StackType.Both ||
                Data.StackType == StackType.None)
            {
                SetDuration(duration);
            }

            m_CallbackDepth++;
            try
            {
                if (Stacks != previousStacks)
                {
                    Data.Behavior?.OnStacksChanged(this, previousStacks, Stacks);
                }
            }
            finally
            {
                EndCallback();
            }
        }

        /// <summary>피해 변경 도중 소진·사망 콜백이 발생해도 현재 효과를 보존합니다.</summary>
        public void ModifyIncomingDamage(ref DamageContext context)
        {
            if (!IsActive)
            {
                return;
            }

            m_CallbackDepth++;
            try
            {
                Data.Behavior?.ModifyIncomingDamage(this, ref context);
            }
            finally
            {
                EndCallback();
            }
        }

        /// <summary>영구 효과도 보호막 소진 등 명시적 종료를 요청할 수 있습니다.</summary>
        public void Expire()
        {
            m_Expired = true;
            RemainingTime = 0f;
        }

        /// <summary>제거를 즉시 표시하고 실행 중인 콜백이 끝난 뒤 실제 해제를 진행합니다.</summary>
        public void Release(bool expired)
        {
            if (m_IsRemoved)
            {
                return;
            }

            m_IsRemoved = true;
            m_Expired = expired;
            m_ReleasePending = true;
            FlushRelease();
        }

        /// <summary>풀 반환 요청을 보관하고 콜백과 해제가 모두 끝난 경우에만 반환합니다.</summary>
        internal void RequestPoolReturn()
        {
            m_ReturnPending = true;
            FlushRelease();
        }

        /// <summary>지속 시간 0은 영구, 음수는 원본 데이터 사용으로 해석합니다.</summary>
        private void SetDuration(float duration)
        {
            RemainingTime = duration;
            if (duration < 0f)
            {
                RemainingTime = Data.Duration;
            }

            IsPermanent = RemainingTime <= 0f;
        }

        /// <summary>가장 바깥 콜백 종료 시 예약된 해제와 반환을 처리합니다.</summary>
        private void EndCallback()
        {
            m_CallbackDepth--;
            FlushRelease();
        }

        /// <summary>제거 콜백을 한 번 호출하고 상태 초기화 후 풀에 중복 없이 반환합니다.</summary>
        private void FlushRelease()
        {
            if (m_CallbackDepth > 0)
            {
                return;
            }

            if (m_ReleasePending)
            {
                m_ReleasePending = false;
                m_CallbackDepth++;
                try
                {
                    if (m_IsApplied)
                    {
                        m_IsApplied = false;
                        Data?.Behavior?.OnRemove(this, m_Expired);
                    }
                }
                finally
                {
                    m_CallbackDepth--;
                    ClearState();
                }
            }

            if (m_ReturnPending && Data == null && !m_IsInPool)
            {
                m_ReturnPending = false;
                m_IsInPool = true;
                StatusEffectRuntimePool.ReturnReleased(this);
            }
        }

        /// <summary>더 이상 콜백에서 사용하지 않는 실행 상태를 비웁니다.</summary>
        private void ClearState()
        {
            Data = null;
            Owner = null;
            Caster = default;
            RemainingTime = 0f;
            CustomTimer = 0f;
            FloatState = 0f;
            IntState = 0;
            Stacks = 0;
            m_IsApplied = false;
            IsPermanent = false;
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
            runtime?.RequestPoolReturn();
        }

        /// <summary>런타임이 콜백 종료를 확인한 뒤 호출하는 실제 반환 경로입니다.</summary>
        internal static void ReturnReleased(StatusEffectRuntime runtime)
        {
            Pool.Push(runtime);
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
            context.ShieldAbsorbed += absorbed;
            runtime.Owner?.OnShieldAbsorbCallback(absorbed);
            if (runtime.FloatState <= 0f)
            {
                runtime.Expire();
            }
        }
    }
}
#endif
