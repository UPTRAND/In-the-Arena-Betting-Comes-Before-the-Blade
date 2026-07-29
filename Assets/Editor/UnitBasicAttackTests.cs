#if UNITY_EDITOR && UNITY_INCLUDE_TESTS && UNITY_6000_0_OR_NEWER
using InTheArena.Unit;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

public sealed class UnitBasicAttackTests
{
    private const string ArcherAttackPath =
        "Assets/ScriptableObject/Unit/Unit_Attack/BasicAttackData_Archer.asset";
    private const string KnightAttackPath =
        "Assets/ScriptableObject/Unit/Unit_Attack/BasicAttackData_Knight.asset";
    private const string ArrowProjectilePath =
        "Assets/ScriptableObject/Unit/Unit_Attack/ProjectileData_Arrow.asset";

    [Test]
    public void MigratedDefaultAssets_AreValidAndUseExpectedDeliveries()
    {
        BasicAttackData archer = AssetDatabase.LoadAssetAtPath<BasicAttackData>(ArcherAttackPath);
        BasicAttackData knight = AssetDatabase.LoadAssetAtPath<BasicAttackData>(KnightAttackPath);
        ProjectileData arrow = AssetDatabase.LoadAssetAtPath<ProjectileData>(ArrowProjectilePath);

        Assert.That(archer, Is.Not.Null);
        Assert.That(knight, Is.Not.Null);
        Assert.That(arrow, Is.Not.Null);
        Assert.That(archer.Delivery, Is.TypeOf<HomingProjectileAttackDelivery>());
        Assert.That(knight.Delivery, Is.TypeOf<ImmediateAttackDelivery>());
        Assert.That(arrow.IsValid(), Is.True);
        Assert.That(archer.IsValid(), Is.True);
        Assert.That(knight.IsValid(), Is.True);
    }

    [Test]
    public void ImmediateBasicAttack_AppliesDamageWhenAttackIsAccepted()
    {
        BasicAttackData attack = ScriptableObject.CreateInstance<BasicAttackData>();
        attack.ConfigureForEditor(
            1f,
            0f,
            0.25f,
            new ImmediateAttackDelivery(),
            new PrimaryDamageAttackEffect());

        Unit attacker = CreateInactiveUnit("Attacker");
        Unit target = CreateInactiveUnit("Target");
        UnitData attackerData = CreateUnitData("Attacker", attack, 10f, attacker.gameObject);
        UnitData targetData = CreateUnitData("Target", attack, 1f, target.gameObject);
        attacker.Initialize(attackerData, 0);
        target.Initialize(targetData, 1);

        float before = target.CurrentHp;
        Assert.That(attacker.TryAttack(target), Is.True);
        Assert.That(target.CurrentHp, Is.LessThan(before));

        Object.DestroyImmediate(attacker.gameObject);
        Object.DestroyImmediate(target.gameObject);
        Object.DestroyImmediate(attackerData);
        Object.DestroyImmediate(targetData);
        Object.DestroyImmediate(attack);
    }

    private static UnitData CreateUnitData(
        string unitName,
        BasicAttackData attack,
        float attackPower,
        GameObject unitPrefab)
    {
        UnitData data = ScriptableObject.CreateInstance<UnitData>();
        var serialized = new SerializedObject(data);
        serialized.FindProperty("m_UnitName").stringValue = unitName;
        serialized.FindProperty("m_AttackType").enumValueIndex = (int)UnitAttackType.Melee;
        serialized.FindProperty("m_BasicAttackData").objectReferenceValue = attack;
        serialized.FindProperty("m_UnitPrefab").objectReferenceValue = unitPrefab;
        SerializedProperty stat = serialized.FindProperty("m_BaseStat");
        stat.FindPropertyRelative("maxHp").floatValue = 100f;
        stat.FindPropertyRelative("attackPower").floatValue = attackPower;
        stat.FindPropertyRelative("defense").floatValue = 0f;
        stat.FindPropertyRelative("attackSpeed").floatValue = 1f;
        stat.FindPropertyRelative("moveSpeed").floatValue = 1f;
        stat.FindPropertyRelative("attackRange").floatValue = 3f;
        serialized.ApplyModifiedPropertiesWithoutUndo();
        return data;
    }

    private static Unit CreateInactiveUnit(string name)
    {
        var gameObject = new GameObject(name);
        gameObject.SetActive(false);
        gameObject.AddComponent<BoxCollider>();
        return gameObject.AddComponent<Unit>();
    }
}
#endif
