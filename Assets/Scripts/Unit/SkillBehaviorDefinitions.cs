#if UNITY_6000_0_OR_NEWER
using System;
using UnityEngine;

namespace InTheArena.Unit
{
    [Serializable]
    public sealed class CounterAttackSkillBehavior : SkillBehaviorDefinition
    {
        [SerializeField, Min(0f)] private float m_AttackPowerRatio = 0.5f;

        public override SkillBehaviorRuntime CreateRuntime()
            => new Runtime(this, m_AttackPowerRatio);

        private sealed class Runtime : SkillBehaviorRuntime
        {
            private readonly float m_AttackPowerRatio;

            public Runtime(SkillBehaviorDefinition definition, float attackPowerRatio)
                : base(definition)
            {
                m_AttackPowerRatio = attackPowerRatio;
            }

            public override void OnTrigger(SkillRuntime runtime, in SkillTriggerContext context)
            {
                if (context.Trigger != SkillTriggerType.OnDamaged || context.IsReaction) return;
                Unit owner = context.Receiver.Unit;
                Unit attacker = context.Source.Unit;
                if (owner == null || owner.IsDead || attacker == null || attacker.IsDead ||
                    attacker.Team == owner.Team)
                    return;

                var damage = new DamageContext
                {
                    Source = new UnitHandle(owner),
                    Target = attacker,
                    Amount = owner.CurrentAttackPower * m_AttackPowerRatio,
                    IsCritical = false,
                    IsSkill = true,
                    IsReaction = true
                };
                float actualDamage = attacker.ApplyDamage(in damage);
                if (actualDamage > 0f)
                {
                    runtime.CommitPassiveSuccess();
                    var attackEvent = new SkillTriggerContext
                    {
                        Trigger = SkillTriggerType.OnAttack,
                        Receiver = new UnitHandle(owner),
                        Source = new UnitHandle(owner),
                        Target = new UnitHandle(attacker),
                        Amount = actualDamage,
                        Position = attacker.GroundPosition,
                        Flags = SkillEventFlags.Skill | SkillEventFlags.Reaction
                    };
                    BattleSimulation.EnqueueSkillEvent(in attackEvent);
                }
                else runtime.DelayRetry();
            }
        }
    }

    [Serializable]
    public sealed class LifeStealSkillBehavior : SkillBehaviorDefinition
    {
        [SerializeField, Range(0f, 1f)] private float m_LifeStealRatio = 0.15f;

        public override SkillBehaviorRuntime CreateRuntime()
            => new Runtime(this, m_LifeStealRatio);

        private sealed class Runtime : SkillBehaviorRuntime
        {
            private readonly float m_Ratio;

            public Runtime(SkillBehaviorDefinition definition, float ratio) : base(definition)
                => m_Ratio = ratio;

            public override void OnTrigger(SkillRuntime runtime, in SkillTriggerContext context)
            {
                if (context.Trigger != SkillTriggerType.OnAttack || context.Amount <= 0f) return;
                Unit owner = context.Receiver.Unit;
                if (owner == null || owner.IsDead) return;
                var heal = new HealContext
                {
                    Source = new UnitHandle(owner),
                    Target = owner,
                    Amount = context.Amount * m_Ratio,
                    IsSkill = true,
                    IsReaction = context.IsReaction
                };
                if (owner.Heal(in heal) > 0f) runtime.CommitPassiveSuccess();
            }
        }
    }

    [Serializable]
    public sealed class KillBuffSkillBehavior : SkillBehaviorDefinition
    {
        [SerializeField] private StatusEffectData m_Buff;
        [SerializeField] private float m_DurationOverride = -1f;

        public override SkillBehaviorRuntime CreateRuntime()
            => new Runtime(this, m_Buff, m_DurationOverride);

        private sealed class Runtime : SkillBehaviorRuntime
        {
            private readonly StatusEffectData m_Buff;
            private readonly float m_Duration;

            public Runtime(
                SkillBehaviorDefinition definition,
                StatusEffectData buff,
                float duration) : base(definition)
            {
                m_Buff = buff;
                m_Duration = duration;
            }

            public override void OnTrigger(SkillRuntime runtime, in SkillTriggerContext context)
            {
                if (context.Trigger != SkillTriggerType.OnKill || m_Buff == null) return;
                Unit owner = context.Receiver.Unit;
                if (owner != null && !owner.IsDead &&
                    owner.ApplyStatusEffect(m_Buff, owner, m_Duration) != null)
                    runtime.CommitPassiveSuccess();
            }
        }
    }
}
#endif
