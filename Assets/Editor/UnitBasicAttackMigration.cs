#if UNITY_EDITOR && UNITY_6000_0_OR_NEWER
using System.IO;
using InTheArena.Unit;
using UnityEditor;
using UnityEngine;

public static class UnitBasicAttackMigration
{
    private const string DataFolder = "Assets/ScriptableObject/Unit/Unit_Attack";
    private const string ArrowProjectilePath = DataFolder + "/ProjectileData_Arrow.asset";
    private const string ArcherAttackPath = DataFolder + "/BasicAttackData_Archer.asset";
    private const string KnightAttackPath = DataFolder + "/BasicAttackData_Knight.asset";
    private const string ArrowPrefabPath = "Assets/Prefabs/Projectile/Projectile_Arrow.prefab";
    private const string ArcherUnitDataPath =
        "Assets/ScriptableObject/Unit/Unit_Base/UnitData_Archer.asset";
    private const string KnightUnitDataPath =
        "Assets/ScriptableObject/Unit/Unit_Base/UnitData_Knight.asset";

    [MenuItem("Tools/In The Arena/Migration/Migrate Basic Attack Data")]
    public static void MigrateDefaults()
    {
        EnsureFolder(DataFolder);

        GameObject arrowPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(ArrowPrefabPath);
        if (arrowPrefab == null)
        {
            Debug.LogError($"[BasicAttackMigration] Arrow 프리팹을 찾지 못했습니다: {ArrowPrefabPath}");
            return;
        }

        ProjectileData arrowData = LoadOrCreate<ProjectileData>(ArrowProjectilePath);
        arrowData.ConfigureForEditor(
            arrowPrefab,
            12f,
            5f,
            0.2f,
            ProjectileOrientationMode.FaceVelocity);
        EditorUtility.SetDirty(arrowData);

        var rangedDelivery = new HomingProjectileAttackDelivery();
        rangedDelivery.ConfigureForEditor(arrowData);
        BasicAttackData archerAttack = LoadOrCreate<BasicAttackData>(ArcherAttackPath);
        archerAttack.ConfigureForEditor(
            1f,
            0.05f,
            0.25f,
            rangedDelivery,
            new PrimaryDamageAttackEffect());
        EditorUtility.SetDirty(archerAttack);

        BasicAttackData knightAttack = LoadOrCreate<BasicAttackData>(KnightAttackPath);
        knightAttack.ConfigureForEditor(
            1f,
            0.05f,
            0.25f,
            new ImmediateAttackDelivery(),
            new PrimaryDamageAttackEffect());
        EditorUtility.SetDirty(knightAttack);

        AssignBasicAttack(ArcherUnitDataPath, archerAttack);
        AssignBasicAttack(KnightUnitDataPath, knightAttack);

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
        Debug.Log("[BasicAttackMigration] Archer/Knight 기본 공격 데이터 마이그레이션 완료.");
    }

    private static T LoadOrCreate<T>(string path) where T : ScriptableObject
    {
        T asset = AssetDatabase.LoadAssetAtPath<T>(path);
        if (asset != null) return asset;
        asset = ScriptableObject.CreateInstance<T>();
        AssetDatabase.CreateAsset(asset, path);
        return asset;
    }

    private static void AssignBasicAttack(string unitDataPath, BasicAttackData attackData)
    {
        UnitData unitData = AssetDatabase.LoadAssetAtPath<UnitData>(unitDataPath);
        if (unitData == null)
        {
            Debug.LogError($"[BasicAttackMigration] UnitData를 찾지 못했습니다: {unitDataPath}");
            return;
        }

        var serializedObject = new SerializedObject(unitData);
        serializedObject.FindProperty("m_BasicAttackData").objectReferenceValue = attackData;
        serializedObject.ApplyModifiedPropertiesWithoutUndo();
        EditorUtility.SetDirty(unitData);
    }

    private static void EnsureFolder(string folderPath)
    {
        if (AssetDatabase.IsValidFolder(folderPath)) return;
        string parent = Path.GetDirectoryName(folderPath)?.Replace('\\', '/');
        string name = Path.GetFileName(folderPath);
        if (!string.IsNullOrEmpty(parent) && !AssetDatabase.IsValidFolder(parent))
            EnsureFolder(parent);
        AssetDatabase.CreateFolder(parent, name);
    }
}
#endif
