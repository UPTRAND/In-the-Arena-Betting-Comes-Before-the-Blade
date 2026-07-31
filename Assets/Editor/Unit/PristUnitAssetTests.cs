#if UNITY_EDITOR && UNITY_INCLUDE_TESTS && UNITY_6000_0_OR_NEWER
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using InTheArena.Unit;
using NUnit.Framework;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;
using UnitType = InTheArena.Unit.Unit;

namespace InTheArena.Editor.Unit
{
    public sealed class PristUnitAssetTests
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
        public void PristAssets_MatchSpecification()
        {
            UnitData unitData = AssetDatabase.LoadAssetAtPath<UnitData>(
                "Assets/ScriptableObject/Unit/Unit_Base/UnitData_Prist.asset");
            BasicAttackData attack = AssetDatabase.LoadAssetAtPath<BasicAttackData>(
                "Assets/ScriptableObject/Unit/Unit_Attack/BasicAttackData_Prist.asset");
            ProjectileData projectile = AssetDatabase.LoadAssetAtPath<ProjectileData>(
                "Assets/ScriptableObject/Unit/Unit_Attack/ProjectileData_Prist.asset");
            SkillData skill = AssetDatabase.LoadAssetAtPath<SkillData>(
                "Assets/ScriptableObject/Unit/Unit_Skill/Skill_Heal_Prist.asset");
            GameObject archerPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(
                "Assets/Prefabs/Unit/Unit_Archer.prefab");
            GameObject pristPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(
                "Assets/Prefabs/Unit/Unit_Prist.prefab");
            AnimatorController controller = AssetDatabase.LoadAssetAtPath<AnimatorController>(
                "Assets/Animator/Unit/Prist/Unit_Prist.controller");

            Assert.That(unitData, Is.Not.Null);
            Assert.That(attack, Is.Not.Null);
            Assert.That(projectile, Is.Not.Null);
            Assert.That(skill, Is.Not.Null);
            Assert.That(pristPrefab, Is.Not.Null);
            Assert.That(controller, Is.Not.Null);

            Assert.That(unitData.UnitName, Is.EqualTo("Prist"));
            Assert.That(unitData.AttackType, Is.EqualTo(UnitAttackType.Ranged));
            Assert.That(unitData.BaseStat.maxHp, Is.EqualTo(100f));
            Assert.That(unitData.BaseStat.attackPower, Is.EqualTo(5f));
            Assert.That(unitData.BaseStat.defense, Is.Zero);
            Assert.That(unitData.BaseStat.attackSpeed, Is.EqualTo(1f));
            Assert.That(unitData.BaseStat.moveSpeed, Is.EqualTo(1f));
            Assert.That(unitData.BaseStat.attackRange, Is.EqualTo(3f));
            Assert.That(unitData.UnitPrefab, Is.SameAs(pristPrefab));
            Assert.That(unitData.BasicAttackData, Is.SameAs(attack));
            Assert.That(unitData.SkillData, Is.SameAs(skill));
            Assert.That(unitData.AIData.name, Is.EqualTo("AIData_Default"));

            Assert.That(attack.Delivery, Is.TypeOf<HomingProjectileAttackDelivery>());
            Assert.That(((HomingProjectileAttackDelivery)attack.Delivery).ProjectileData,
                Is.SameAs(projectile));
            Assert.That(projectile.Prefab.name, Is.EqualTo("Projectile_FireBall"));
            Assert.That(projectile.Speed, Is.EqualTo(12f));
            Assert.That(projectile.OrientationMode, Is.EqualTo(ProjectileOrientationMode.FullBillboard));
            Assert.That(projectile.FlightStateName, Is.EqualTo("FireBall"));
            Assert.That(projectile.ImpactStateName, Is.EqualTo("Explosion"));
            Assert.That(projectile.ImpactPresentationDuration, Is.EqualTo(5f / 12f).Within(0.0001f));

            Assert.That(skill.SkillName, Is.EqualTo("Heal"));
            Assert.That(skill.Range, Is.EqualTo(5f));
            Assert.That(skill.Cooldown, Is.EqualTo(5f));
            Assert.That(skill.CastTime, Is.EqualTo(0.6f));
            Assert.That(skill.Targeting, Is.TypeOf<LowestHealthAllySkillTargeting>());
            Assert.That(skill.Effects.Single(), Is.TypeOf<HealSkillEffect>());

            Assert.That(pristPrefab.GetComponents<Component>().Select(component => component.GetType()),
                Is.EqualTo(archerPrefab.GetComponents<Component>().Select(component => component.GetType())));
            Assert.That(pristPrefab.transform.Cast<Transform>().Select(child => child.name),
                Is.EqualTo(archerPrefab.transform.Cast<Transform>().Select(child => child.name)));
            Assert.That(pristPrefab.GetComponent<SpriteRenderer>().sprite.name, Does.StartWith("MiniArchMage_"));
            Assert.That(pristPrefab.GetComponent<Animator>().runtimeAnimatorController,
                Is.SameAs(controller));
            Assert.That(controller.layers[0].stateMachine.states.Select(state => state.state.name),
                Is.EquivalentTo(new[] { "Idle", "Walk", "Attack", "Skill", "Death" }));
        }

        [Test]
        public void Heal_RestoresThirtyTwoPointFiveAndStartsCooldown()
        {
            SkillData skill = AssetDatabase.LoadAssetAtPath<SkillData>(
                "Assets/ScriptableObject/Unit/Unit_Skill/Skill_Heal_Prist.asset");
            UnitType owner = CreateUnit("Prist", 0, 5f);
            UnitType ally = CreateUnit("Ally", 0, 1f);
            SetField(ally, "m_CurrentHp", 50f);
            SkillRuntime runtime = skill.CreateRuntime(owner);
            var targets = new SkillTargetSet();

            Assert.That(runtime.TryResolve(new SkillUseRequest((UnitType)null), targets), Is.True);
            Assert.That(targets[0].Unit, Is.SameAs(ally));
            Assert.That(runtime.Execute(targets), Is.EqualTo(SkillExecutionResult.Success));
            Assert.That(ally.CurrentHp, Is.EqualTo(82.5f));
            Assert.That(runtime.CurrentCooldown, Is.EqualTo(5f));
        }

        [Test]
        public void PlayCast_UsesSkillAnimatorState()
        {
            GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(
                "Assets/Prefabs/Unit/Unit_Prist.prefab");
            GameObject instance = Object.Instantiate(prefab);
            m_Created.Add(instance);
            Animator animator = instance.GetComponent<Animator>();
            animator.Rebind();
            animator.Update(0f);
            var presenter = new UnitAnimationPresenter(animator);

            presenter.PlayCast();
            animator.Update(0.1f);

            Assert.That(animator.GetCurrentAnimatorStateInfo(0).shortNameHash,
                Is.EqualTo(Animator.StringToHash("Skill")));
        }

        private UnitType CreateUnit(string name, int team, float attackPower)
        {
            UnitData data = ScriptableObject.CreateInstance<UnitData>();
            m_Created.Add(data);
            SetField(data, "m_BaseStat", new UnitStat
            {
                maxHp = 100f,
                attackPower = attackPower,
                attackSpeed = 1f,
                moveSpeed = 1f,
                attackRange = 3f
            });
            var gameObject = new GameObject(name);
            m_Created.Add(gameObject);
            gameObject.AddComponent<BoxCollider>();
            UnitType unit = gameObject.AddComponent<UnitType>();
            unit.Initialize(data, team);
            return unit;
        }

        private static void SetField(object target, string name, object value)
        {
            FieldInfo field = target.GetType().GetField(
                name,
                BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.That(field, Is.Not.Null, $"Field not found: {name}");
            field.SetValue(target, value);
        }
    }
}
#endif
