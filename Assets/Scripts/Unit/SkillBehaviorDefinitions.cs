#if UNITY_6000_0_OR_NEWER
using System;
using System.Collections.Generic;
using UnityEngine;

namespace InTheArena.Unit
{
    [Serializable]
    public sealed class CounterAttackSkillBehavior : SkillBehaviorDefinition
    {
        [SerializeField, Min(0f)] private float m_AttackPowerRatio = 0.5f;

        /// <summary>반응 피해에는 재반격하지 않고 자신을 공격한 적에게 반격합니다.</summary>
        public override void OnTrigger(SkillRuntime runtime, in SkillTriggerContext context)
        {
            if (context.Trigger != SkillTriggerType.OnDamaged || context.IsReaction)
            {
                return;
            }

            Unit owner = runtime.Owner;
            Unit attacker = context.Source.Unit;
            if (owner == null || owner.IsDead || attacker == null || attacker.IsDead ||
                attacker.Team == owner.Team)
            {
                return;
            }

            var damage = new DamageContext
            {
                Source = new UnitHandle(owner),
                Target = attacker,
                Amount = owner.CurrentAttackPower * m_AttackPowerRatio,
                IsSkill = true,
                IsReaction = true
            };

            float actualDamage = attacker.ApplyDamage(in damage);
            if (actualDamage <= 0f)
            {
                runtime.DelayRetry();
                return;
            }

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
    }

    [Serializable]
    public sealed class LifeStealSkillBehavior : SkillBehaviorDefinition
    {
        [SerializeField, Range(0f, 1f)] private float m_LifeStealRatio = 0.15f;

        /// <summary>공격으로 준 피해의 일부만큼 회복하고 실제 회복 시 쿨다운을 시작합니다.</summary>
        public override void OnTrigger(SkillRuntime runtime, in SkillTriggerContext context)
        {
            if (context.Trigger != SkillTriggerType.OnAttack || context.Amount <= 0f)
            {
                return;
            }

            Unit owner = runtime.Owner;
            if (owner == null || owner.IsDead)
            {
                return;
            }

            var heal = new HealContext
            {
                Source = new UnitHandle(owner),
                Target = owner,
                Amount = context.Amount * m_LifeStealRatio,
                IsSkill = true,
                IsReaction = context.IsReaction
            };

            if (owner.Heal(in heal) > 0f)
            {
                runtime.CommitPassiveSuccess();
            }
        }
    }

    [Serializable]
    public sealed class KillBuffSkillBehavior : SkillBehaviorDefinition
    {
        [SerializeField] private StatusEffectData m_Buff;
        [SerializeField] private float m_DurationOverride = -1f;

        /// <summary>처치한 유닛에게 지정한 버프를 적용합니다.</summary>
        public override void OnTrigger(SkillRuntime runtime, in SkillTriggerContext context)
        {
            if (context.Trigger != SkillTriggerType.OnKill || m_Buff == null)
            {
                return;
            }

            Unit owner = runtime.Owner;
            if (owner != null && !owner.IsDead &&
                owner.ApplyStatusEffect(m_Buff, owner, m_DurationOverride) != null)
            {
                runtime.CommitPassiveSuccess();
            }
        }
    }

    [Serializable]
    public sealed class HunterWeaponSwitchSkillBehavior : SkillBehaviorDefinition
    {
        [Header("무기")]
        [SerializeField] private BasicAttackData m_BowAttackData;
        [SerializeField] private BasicAttackData m_DaggerAttackData;

        [Header("전환 거리")]
        [SerializeField, Min(0f)] private float m_SwitchToDaggerDistance = 1.2f;
        [SerializeField, Min(0f)] private float m_SwitchToBowDistance = 1.6f;

        [Header("활 능력치")]
        [SerializeField, Min(0f)] private float m_BowAttackPower = 13f;
        [SerializeField, Min(0.01f)] private float m_BowAttackSpeed = 0.5f;
        [SerializeField, Min(0f)] private float m_BowAttackRange = 2.5f;

        [Header("단검 능력치")]
        [SerializeField, Min(0f)] private float m_DaggerAttackPower = 8f;
        [SerializeField, Min(0.01f)] private float m_DaggerAttackSpeed = 1.6f;
        [SerializeField, Min(0f)] private float m_DaggerAttackRange = 1f;

        [NonSerialized] private bool m_UsingDagger;
        [NonSerialized] private bool m_HasLoggedMode;
        [NonSerialized] private bool m_BlockDaggerUntilBowDistance;

        /// <summary>이동 여부와 대상 거리의 이력에 따라 활 또는 단검을 선택합니다.</summary>
        public override void Tick(SkillRuntime runtime, float deltaTime)
        {
            Unit owner = runtime.Owner;
            if (owner == null || owner.IsDead)
            {
                return;
            }

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
                if (m_BlockDaggerUntilBowDistance)
                {
                    EquipBow(owner);
                }
                else
                {
                    EquipDagger(owner);
                }
            }
            else
            {
                if (owner.IsMoving && distance <= m_SwitchToDaggerDistance)
                {
                    m_BlockDaggerUntilBowDistance = true;
                }
                else if (distance >= m_SwitchToBowDistance)
                {
                    m_BlockDaggerUntilBowDistance = false;
                }

                EquipBow(owner);
            }
        }

        /// <summary>활·단검에서 사용하는 투사체를 공통 풀 준비 단계에 등록합니다.</summary>
        public override void CollectProjectilePrefabs(List<GameObject> output)
        {
            m_BowAttackData?.CollectProjectilePrefabs(output);
            m_DaggerAttackData?.CollectProjectilePrefabs(output);
        }

        /// <summary>활 모드 진입 시에만 능력치와 표시를 변경합니다.</summary>
        private void EquipBow(Unit owner)
        {
            bool alreadyEquipped = !m_UsingDagger && owner.CurrentBasicAttackData == m_BowAttackData;
            if (alreadyEquipped && m_HasLoggedMode)
            {
                return;
            }

            m_UsingDagger = false;
            if (!alreadyEquipped)
            {
                owner.SetWeaponOverride(
                    m_BowAttackData,
                    m_BowAttackPower,
                    m_BowAttackSpeed,
                    m_BowAttackRange);
            }

            owner.LogHunterModeChange("원거리");
            m_HasLoggedMode = true;
        }

        /// <summary>단검 모드 진입 시에만 능력치와 표시를 변경합니다.</summary>
        private void EquipDagger(Unit owner)
        {
            bool alreadyEquipped = m_UsingDagger && owner.CurrentBasicAttackData == m_DaggerAttackData;
            if (alreadyEquipped && m_HasLoggedMode)
            {
                return;
            }

            m_UsingDagger = true;
            if (!alreadyEquipped)
            {
                owner.SetWeaponOverride(
                    m_DaggerAttackData,
                    m_DaggerAttackPower,
                    m_DaggerAttackSpeed,
                    m_DaggerAttackRange);
            }

            owner.LogHunterModeChange("근접");
            m_HasLoggedMode = true;
        }
    }
}
#endif
