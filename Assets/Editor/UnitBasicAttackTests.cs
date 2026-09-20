#if UNITY_EDITOR && UNITY_INCLUDE_TESTS && UNITY_6000_0_OR_NEWER
using InTheArena.Unit;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

public sealed class UnitBasicAttackTests
{
    /// <summary>운영 콘텐츠 전체의 참조·최소 베팅·규칙을 빌드 검사와 같은 기준으로 확인합니다.</summary>
    [Test]
    public void BuildContent_HasValidDependencies()
    {
        Assert.That(InTheArena.MainGame.Editor.ContentBuildValidation.CollectErrors(), Is.Empty);
    }

    /// <summary>원거리 유닛의 공유 프리팹·공격·스킬·애니메이터 연결을 확인합니다.</summary>
    [TestCase("Prist")]
    [TestCase("Wizard")]
    public void CasterAssets_KeepRuntimeReferences(string unitName)
    {
        UnitData data = AssetDatabase.LoadAssetAtPath<UnitData>(
            "Assets/ScriptableObject/Unit/Unit_Base/UnitData_" + unitName + ".asset");
        GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(
            "Assets/Prefabs/Unit/Unit_" + unitName + ".prefab");
        Assert.That(data, Is.Not.Null);
        Assert.That(data.IsValid(), Is.True);
        Assert.That(data.UnitPrefab, Is.SameAs(prefab));
        Assert.That(data.BasicAttackData.Delivery.IsRanged, Is.True);
        Assert.That(data.SkillDatas, Is.Not.Empty);
        Assert.That(prefab.GetComponent<Animator>().runtimeAnimatorController, Is.Not.Null);
    }

    /// <summary>회복 유닛은 실제 프리팹의 Skill 애니메이션을 사용합니다.</summary>
    [Test]
    public void PristCast_UsesConfiguredSkillState()
    {
        GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/Unit/Unit_Prist.prefab");
        GameObject instance = Object.Instantiate(prefab);
        try
        {
            Animator animator = instance.GetComponent<Animator>();
            animator.Rebind();
            animator.Update(0f);
            new UnitAnimationPresenter(animator).PlayCast();
            animator.Update(0.1f);
            Assert.That(animator.GetCurrentAnimatorStateInfo(0).shortNameHash, Is.EqualTo(Animator.StringToHash("Skill")));
        }
        finally
        {
            Object.DestroyImmediate(instance);
        }
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
