#if UNITY_EDITOR && UNITY_INCLUDE_TESTS && UNITY_6000_0_OR_NEWER
using System.Linq;
using InTheArena.Unit;
using NUnit.Framework;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;

namespace InTheArena.Editor.Unit
{
    public sealed class WizardUnitAssetTests
    {
        [Test]
        public void WizardAssets_MatchSpecification()
        {
            UnitData unitData = AssetDatabase.LoadAssetAtPath<UnitData>(
                "Assets/ScriptableObject/Unit/Unit_Base/UnitData_Wizard.asset");
            BasicAttackData attack = AssetDatabase.LoadAssetAtPath<BasicAttackData>(
                "Assets/ScriptableObject/Unit/Unit_Attack/BasicAttackData_Wizard.asset");
            ProjectileData projectile = AssetDatabase.LoadAssetAtPath<ProjectileData>(
                "Assets/ScriptableObject/Unit/Unit_Attack/ProjectileData_Wizard.asset");
            SkillData skill = AssetDatabase.LoadAssetAtPath<SkillData>(
                "Assets/ScriptableObject/Unit/Unit_Skill/Skill_Fire_Ball_Wizard.asset");
            ProjectileData skillProjectile = AssetDatabase.LoadAssetAtPath<ProjectileData>(
                "Assets/ScriptableObject/Unit/Unit_Skill/ProjectileData_FireBall_Wizard.asset");
            GameObject archerPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(
                "Assets/Prefabs/Unit/Unit_Archer.prefab");
            GameObject wizardPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(
                "Assets/Prefabs/Unit/Unit_Wizard.prefab");
            AnimatorController controller = AssetDatabase.LoadAssetAtPath<AnimatorController>(
                "Assets/Animator/Unit/Wizard/Unit_Wizard.controller");

            Assert.That(unitData, Is.Not.Null);
            Assert.That(attack, Is.Not.Null);
            Assert.That(projectile, Is.Not.Null);
            Assert.That(skill, Is.Not.Null);
            Assert.That(skillProjectile, Is.Not.Null);
            Assert.That(wizardPrefab, Is.Not.Null);
            Assert.That(controller, Is.Not.Null);

            Assert.That(unitData.UnitName, Is.EqualTo("Wizard"));
            Assert.That(unitData.AttackType, Is.EqualTo(UnitAttackType.Ranged));
            Assert.That(unitData.BaseStat.maxHp, Is.EqualTo(100f));
            Assert.That(unitData.BaseStat.attackPower, Is.EqualTo(7f));
            Assert.That(unitData.BaseStat.defense, Is.Zero);
            Assert.That(unitData.BaseStat.attackSpeed, Is.EqualTo(1f));
            Assert.That(unitData.BaseStat.moveSpeed, Is.EqualTo(1f));
            Assert.That(unitData.BaseStat.attackRange, Is.EqualTo(2f));
            Assert.That(unitData.UnitPrefab, Is.SameAs(wizardPrefab));
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

            Assert.That(skill.SkillName, Is.EqualTo("Fire_Ball"));
            Assert.That(skill.Range, Is.EqualTo(8f));
            Assert.That(skill.Cooldown, Is.EqualTo(3f));
            Assert.That(skill.CastTime, Is.EqualTo(0.4f));
            Assert.That(skill.Targeting, Is.TypeOf<SingleUnitSkillTargeting>());
            Assert.That(skill.Effects.Single(), Is.TypeOf<SpawnProjectileSkillEffect>());
            Assert.That(skillProjectile.Prefab.transform.localScale, Is.EqualTo(Vector3.one * 0.3f));
            Assert.That(skillProjectile.ImpactStateName, Is.EqualTo("Explosion"));

            Assert.That(wizardPrefab.GetComponents<Component>().Select(component => component.GetType()),
                Is.EqualTo(archerPrefab.GetComponents<Component>().Select(component => component.GetType())));
            Assert.That(wizardPrefab.transform.Cast<Transform>().Select(child => child.name),
                Is.EqualTo(archerPrefab.transform.Cast<Transform>().Select(child => child.name)));
            Assert.That(wizardPrefab.GetComponent<Animator>().runtimeAnimatorController,
                Is.SameAs(controller));
            Assert.That(controller.layers[0].stateMachine.states.Select(state => state.state.name),
                Is.EquivalentTo(new[] { "Idle", "Walk", "Attack", "Death" }));
        }
    }
}
#endif
