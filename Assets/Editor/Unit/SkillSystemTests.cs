#if UNITY_EDITOR
using System.Collections.Generic;
using System.Reflection;
using InTheArena.Unit;
using NUnit.Framework;
using UnityEngine;
using UnitType = InTheArena.Unit.Unit;

namespace InTheArena.Editor.Unit
{
    public sealed class SkillSystemTests
    {
        private readonly List<Object> m_Created = new List<Object>();

        [TearDown]
        public void TearDown()
        {
            for (int i = m_Created.Count - 1; i >= 0; i--)
                if (m_Created[i] != null) Object.DestroyImmediate(m_Created[i]);
            m_Created.Clear();
            UnitRegistry.Clear();
        }

        [Test]
        public void SkillRuntime_StateIsIsolatedPerUnit()
        {
            UnitType first = CreateUnit(0);
            UnitType second = CreateUnit(0);
            SkillData data = CreateSkill(
                SkillType.Passive,
                new SelfSkillTargeting(),
                SkillExecutionMode.BehaviorOnly,
                new LifeStealSkillBehavior(),
                4f);
            SetField(data, "m_Cooldown", 4f);

            SkillRuntime firstRuntime = data.CreateRuntime(first);
            SkillRuntime secondRuntime = data.CreateRuntime(second);
            firstRuntime.CommitPassiveSuccess();

            Assert.That(firstRuntime.CurrentCooldown, Is.EqualTo(4f));
            Assert.That(secondRuntime.CurrentCooldown, Is.Zero);
        }

        [Test]
        public void LowestHealthAllyTargeting_IgnoresEnemyHint()
        {
            UnitType owner = CreateUnit(0);
            UnitType ally = CreateUnit(0);
            UnitType enemy = CreateUnit(1);
            SetField(ally, "m_CurrentHp", ally.MaxHp * 0.25f);
            SkillData data = CreateSkill(
                SkillType.Active,
                new LowestHealthAllySkillTargeting(),
                SkillExecutionMode.EffectsOnly,
                null,
                3f,
                new HealSkillEffect());
            SkillRuntime runtime = data.CreateRuntime(owner);
            var targets = new SkillTargetSet();

            bool resolved = runtime.TryResolve(new SkillUseRequest(enemy), targets);

            Assert.That(resolved, Is.True);
            Assert.That(targets.Count, Is.EqualTo(1));
            Assert.That(targets[0].Unit, Is.SameAs(ally));
        }

        [Test]
        public void GroundTargeting_ConvertsUnitHintToGroundPosition()
        {
            UnitType owner = CreateUnit(0);
            UnitType enemy = CreateUnit(1);
            enemy.transform.position = new Vector3(2f, 0f, 1f);
            SkillData data = CreateSkill(
                SkillType.Active,
                new GroundAtTargetSkillTargeting(),
                SkillExecutionMode.EffectsOnly,
                null,
                5f,
                new SpawnVfxSkillEffect());
            SkillRuntime runtime = data.CreateRuntime(owner);
            var targets = new SkillTargetSet();

            bool resolved = runtime.TryResolve(new SkillUseRequest(enemy), targets);

            Assert.That(resolved, Is.True);
            Assert.That(targets.HasGroundPosition, Is.True);
            Assert.That(targets.GroundPosition.x, Is.EqualTo(2f).Within(0.001f));
            Assert.That(targets.GroundPosition.z, Is.EqualTo(1f).Within(0.001f));
        }

        [Test]
        public void SkillCooldown_StartsOnlyWhenEffectSucceeds()
        {
            UnitType owner = CreateUnit(0);
            var healEffect = new HealSkillEffect();
            SetField(healEffect, "m_BaseHeal", 20f);
            SkillData data = CreateSkill(
                SkillType.Active,
                new SelfSkillTargeting(),
                SkillExecutionMode.EffectsOnly,
                null,
                0f,
                healEffect);
            SetField(data, "m_Cooldown", 3f);
            SkillRuntime runtime = data.CreateRuntime(owner);
            var targets = new SkillTargetSet();
            Assert.That(runtime.TryResolve(new SkillUseRequest(owner), targets), Is.True);

            SkillExecutionResult first = runtime.Execute(targets);

            Assert.That(first, Is.EqualTo(SkillExecutionResult.NoEffect));
            Assert.That(runtime.CurrentCooldown, Is.Zero);
            SetField(owner, "m_CurrentHp", owner.MaxHp * 0.5f);
            runtime.Tick(data.FailureRetryDelay);
            Assert.That(runtime.TryResolve(new SkillUseRequest(owner), targets), Is.True);

            SkillExecutionResult second = runtime.Execute(targets);

            Assert.That(second, Is.EqualTo(SkillExecutionResult.Success));
            Assert.That(runtime.CurrentCooldown, Is.EqualTo(3f));
        }

        [Test]
        public void UnitHandle_BecomesInvalidAfterPoolStyleReinitialize()
        {
            UnitType unit = CreateUnit(0);
            var handle = new UnitHandle(unit);

            unit.Initialize(unit.UnitData, 0);

            Assert.That(handle.IsValid, Is.False);
            Assert.That(handle.Unit, Is.Null);
        }

        [Test]
        public void ShieldStatus_ModifiesTypedDamageWithoutLegacyEffect()
        {
            UnitType target = CreateUnit(0);
            UnitType attacker = CreateUnit(1);
            BuffData shieldData = CreateStatus<BuffData>(new ShieldStatusBehavior(), 5f);
            SetField(shieldData.Behavior, "m_ShieldAmount", 50f);
            target.ApplyStatusEffect(shieldData, target);
            float hp = target.CurrentHp;
            var damage = new DamageContext
            {
                Source = new UnitHandle(attacker),
                Target = target,
                Amount = 20f,
                IsSkill = true
            };

            float applied = target.ApplyDamage(in damage);

            Assert.That(applied, Is.Zero);
            Assert.That(target.CurrentHp, Is.EqualTo(hp));
        }

        [Test]
        public void CounterAttack_DoesNotReactToReactionDamage()
        {
            UnitType owner = CreateUnit(0);
            UnitType attacker = CreateUnit(1);
            var behavior = new CounterAttackSkillBehavior();
            SetField(behavior, "m_AttackPowerRatio", 1f);
            SkillData data = CreateSkill(
                SkillType.Passive,
                new SelfSkillTargeting(),
                SkillExecutionMode.BehaviorOnly,
                behavior,
                0f);
            SkillRuntime runtime = data.CreateRuntime(owner);
            float initialHp = attacker.CurrentHp;
            var first = new SkillTriggerContext
            {
                Trigger = SkillTriggerType.OnDamaged,
                Receiver = new UnitHandle(owner),
                Source = new UnitHandle(attacker),
                Target = new UnitHandle(owner)
            };
            runtime.HandleTrigger(in first);
            float afterFirst = attacker.CurrentHp;
            var reaction = first;
            reaction.Flags = SkillEventFlags.Reaction;

            runtime.HandleTrigger(in reaction);

            Assert.That(afterFirst, Is.LessThan(initialHp));
            Assert.That(attacker.CurrentHp, Is.EqualTo(afterFirst));
        }

        [Test]
        public void StatusRemoval_RecalculatesStunFromActiveData()
        {
            UnitType unit = CreateUnit(0);
            DebuffData stun = CreateStatus<DebuffData>(new StunStatusBehavior(), 2f);

            StatusEffectRuntime runtime = unit.ApplyStatusEffect(stun, unit);

            Assert.That(unit.IsStunned, Is.True);
            unit.RemoveStatusEffect(runtime);
            Assert.That(unit.IsStunned, Is.False);
        }

        [Test]
        public void PrepareForPool_ClearsSkillAndStatusRuntime()
        {
            UnitType unit = CreateUnit(0);
            SkillData skill = CreateSkill(
                SkillType.Passive,
                new SelfSkillTargeting(),
                SkillExecutionMode.BehaviorOnly,
                new LifeStealSkillBehavior(),
                0f);
            BuffData status = CreateStatus<BuffData>(new ShieldStatusBehavior(), 2f);
            SetField(unit.UnitData, "m_SkillDatas", new List<SkillData> { skill });
            unit.Initialize(unit.UnitData, 0);
            unit.ApplyStatusEffect(status, unit);

            typeof(UnitType).GetMethod(
                    "PrepareForPool",
                    BindingFlags.Instance | BindingFlags.NonPublic)
                .Invoke(unit, null);

            Assert.That(unit.Skills, Is.Empty);
            Assert.That(unit.ActiveDataEffects, Is.Empty);
            Assert.That(unit.IsCastingSkill, Is.False);
        }

        private UnitType CreateUnit(int team)
        {
            UnitData data = ScriptableObject.CreateInstance<UnitData>();
            m_Created.Add(data);
            SetField(data, "m_BaseStat", UnitStat.Default);
            var gameObject = new GameObject($"Unit_{team}");
            m_Created.Add(gameObject);
            gameObject.AddComponent<BoxCollider>();
            UnitType unit = gameObject.AddComponent<UnitType>();
            unit.Initialize(data, team);
            return unit;
        }

        private SkillData CreateSkill(
            SkillType type,
            SkillTargetingDefinition targeting,
            SkillExecutionMode mode,
            SkillBehaviorDefinition behavior,
            float range,
            params SkillEffectDefinition[] effects)
        {
            SkillData data = ScriptableObject.CreateInstance<SkillData>();
            m_Created.Add(data);
            SetField(data, "m_SkillName", "Test");
            SetField(data, "m_SkillType", type);
            SetField(data, "m_Range", range);
            SetField(data, "m_ExecutionMode", mode);
            SetField(data, "m_Targeting", targeting);
            SetField(data, "m_Behavior", behavior);
            SetField(data, "m_Effects", new List<SkillEffectDefinition>(effects));
            return data;
        }

        private T CreateStatus<T>(StatusEffectBehaviorDefinition behavior, float duration)
            where T : StatusEffectData
        {
            T data = ScriptableObject.CreateInstance<T>();
            m_Created.Add(data);
            SetField(data, "m_EffectName", "Test");
            SetField(data, "m_Duration", duration);
            SetField(data, "m_Behavior", behavior);
            return data;
        }

        private static void SetField(object target, string name, object value)
        {
            System.Type type = target.GetType();
            FieldInfo field = null;
            while (type != null && field == null)
            {
                field = type.GetField(name, BindingFlags.Instance | BindingFlags.NonPublic);
                type = type.BaseType;
            }
            Assert.That(field, Is.Not.Null, $"Field not found: {name}");
            field.SetValue(target, value);
        }
    }
}
#endif
