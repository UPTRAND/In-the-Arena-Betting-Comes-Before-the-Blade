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

    [Serializable]
    public sealed class HunterWeaponSwitchSkillBehavior : SkillBehaviorDefinition
    {
        [SerializeField] private BasicAttackData m_BowAttackData;
        [SerializeField] private BasicAttackData m_DaggerAttackData;
        [SerializeField, Min(0f)] private float m_SwitchToDaggerDistance = 1.2f;
        [SerializeField, Min(0f)] private float m_SwitchToBowDistance = 1.6f;
        [SerializeField, Min(0f)] private float m_BowAttackPower = 13f;
        [SerializeField, Min(0.01f)] private float m_BowAttackSpeed = 0.5f;
        [SerializeField, Min(0f)] private float m_BowAttackRange = 2.5f;
        [SerializeField, Min(0f)] private float m_DaggerAttackPower = 8f;
        [SerializeField, Min(0.01f)] private float m_DaggerAttackSpeed = 1.6f;
        [SerializeField, Min(0f)] private float m_DaggerAttackRange = 1f;

        public override SkillBehaviorRuntime CreateRuntime()
            => new Runtime(
                this,
                m_BowAttackData,
                m_DaggerAttackData,
                m_SwitchToDaggerDistance,
                m_SwitchToBowDistance,
                m_BowAttackPower,
                m_BowAttackSpeed,
                m_BowAttackRange,
                m_DaggerAttackPower,
                m_DaggerAttackSpeed,
                m_DaggerAttackRange);

        private sealed class Runtime : SkillBehaviorRuntime
        {
            private readonly BasicAttackData m_BowAttackData;
            private readonly BasicAttackData m_DaggerAttackData;
            private readonly float m_SwitchToDaggerDistance;
            private readonly float m_SwitchToBowDistance;
            private readonly float m_BowAttackPower;
            private readonly float m_BowAttackSpeed;
            private readonly float m_BowAttackRange;
            private readonly float m_DaggerAttackPower;
            private readonly float m_DaggerAttackSpeed;
            private readonly float m_DaggerAttackRange;
            private bool m_UsingDagger;
            private bool m_HasLoggedMode;
            private bool m_BlockDaggerUntilBowDistance;

            public Runtime(
                SkillBehaviorDefinition definition,
                BasicAttackData bowAttackData,
                BasicAttackData daggerAttackData,
                float switchToDaggerDistance,
                float switchToBowDistance,
                float bowAttackPower,
                float bowAttackSpeed,
                float bowAttackRange,
                float daggerAttackPower,
                float daggerAttackSpeed,
                float daggerAttackRange)
                : base(definition)
            {
                m_BowAttackData = bowAttackData;
                m_DaggerAttackData = daggerAttackData;
                m_SwitchToDaggerDistance = Mathf.Max(0f, switchToDaggerDistance);
                m_SwitchToBowDistance = Mathf.Max(0f, switchToBowDistance);
                m_BowAttackPower = Mathf.Max(0f, bowAttackPower);
                m_BowAttackSpeed = Mathf.Max(0.01f, bowAttackSpeed);
                m_BowAttackRange = Mathf.Max(0f, bowAttackRange);
                m_DaggerAttackPower = Mathf.Max(0f, daggerAttackPower);
                m_DaggerAttackSpeed = Mathf.Max(0.01f, daggerAttackSpeed);
                m_DaggerAttackRange = Mathf.Max(0f, daggerAttackRange);
            }

            public override void Tick(SkillRuntime runtime, float deltaTime)
            {
                Unit owner = runtime?.Owner;
                if (owner == null || owner.IsDead) return;

                Unit target = owner.AI?.CurrentTarget;
                if (target == null || target.IsDead || target.Team == owner.Team ||
                    !target.gameObject.activeInHierarchy)
                {
                    EquipBow(owner);
                    return;
                }

                Vector3 delta = target.GroundPosition - owner.GroundPosition;
                delta.y = 0f;
                float distance = delta.magnitude;

                if (m_UsingDagger)
                {
                    if (distance >= m_SwitchToBowDistance)
                    {
                        m_BlockDaggerUntilBowDistance = false;
                        EquipBow(owner);
                    }
                }
                else if (!owner.IsMoving && distance <= m_SwitchToDaggerDistance)
                {
                    if (m_BlockDaggerUntilBowDistance) EquipBow(owner);
                    else EquipDagger(owner);
                }
                else if (owner.IsMoving && distance <= m_SwitchToDaggerDistance)
                {
                    m_BlockDaggerUntilBowDistance = true;
                    EquipBow(owner);
                }
                else
                {
                    if (distance >= m_SwitchToBowDistance)
                        m_BlockDaggerUntilBowDistance = false;
                    EquipBow(owner);
                }
            }

            public override void Reset()
            {
                m_UsingDagger = false;
                m_HasLoggedMode = false;
                m_BlockDaggerUntilBowDistance = false;
            }

            private void EquipBow(Unit owner)
            {
                bool alreadyEquipped = !m_UsingDagger && owner.CurrentBasicAttackData == m_BowAttackData;
                if (alreadyEquipped && m_HasLoggedMode) return;

                m_UsingDagger = false;
                if (!alreadyEquipped)
                {
                    owner.SetWeaponOverride(
                        m_BowAttackData,
                        m_BowAttackPower,
                        m_BowAttackSpeed,
                        m_BowAttackRange);
                }
                owner.LogHunterModeChange("\uC6D0\uAC70\uB9AC");
                m_HasLoggedMode = true;
            }

            private void EquipDagger(Unit owner)
            {
                bool alreadyEquipped = m_UsingDagger && owner.CurrentBasicAttackData == m_DaggerAttackData;
                if (alreadyEquipped && m_HasLoggedMode) return;

                m_UsingDagger = true;
                if (!alreadyEquipped)
                {
                    owner.SetWeaponOverride(
                        m_DaggerAttackData,
                        m_DaggerAttackPower,
                        m_DaggerAttackSpeed,
                        m_DaggerAttackRange);
                }
                owner.LogHunterModeChange("\uADFC\uC811");
                m_HasLoggedMode = true;
            }
        }
    }
}
#endif
