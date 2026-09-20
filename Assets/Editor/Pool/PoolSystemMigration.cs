#if UNITY_EDITOR && UNITY_6000_0_OR_NEWER
using InTheArena.Unit;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

public static class PoolSystemMigration
{
    private const string TitleScenePath = "Assets/Scenes/Title.unity";
    private const string CatalogPath = "Assets/Resources/PoolCatalog.asset";

    [MenuItem("Tools/In The Arena/Pooling/Migrate Pool System")]
    /// <summary>변경 범위를 확인한 뒤 풀 참조와 프리팹 구성을 갱신합니다.</summary>
    public static void Migrate()
    {
        if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo() ||
            !EditorUtility.DisplayDialog("풀 마이그레이션", "유닛·투사체 프리팹에 PoolMember를 추가하고 Title 씬의 PoolManager 연결을 저장합니다.", "적용", "취소"))
        {
            return;
        }
        if (AssetDatabase.LoadAssetAtPath<SceneAsset>(TitleScenePath) == null)
        {
            throw new MissingReferenceException("Title 씬이 없습니다.");
        }
        PoolCatalog catalog = EnsureCatalog();
        MigratePrefabs();
        RegisterPoolManager(catalog);
        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
        Debug.Log("[PoolSystemMigration] Pool 시스템 마이그레이션 완료");
    }

    [MenuItem("Tools/In The Arena/Pooling/Validate Pool System")]
    public static void Validate()
    {
        int missing = 0;
        string[] guids = AssetDatabase.FindAssets("t:Prefab");
        for (int i = 0; i < guids.Length; i++)
        {
            string path = AssetDatabase.GUIDToAssetPath(guids[i]);
            GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
            if (prefab == null) continue;
            bool poolable = prefab.GetComponent<Unit>() != null || prefab.GetComponent<Projectile>() != null;
            if (poolable && prefab.GetComponent<PoolMember>() == null)
            {
                missing++;
                Debug.LogError($"[PoolSystemMigration] PoolMember 누락: {path}", prefab);
            }
        }
        Debug.Log(missing == 0 ? "[PoolSystemMigration] Pool 프리팹 검증 성공" :
            $"[PoolSystemMigration] PoolMember 누락: {missing}개");
    }

    private static PoolCatalog EnsureCatalog()
    {
        PoolCatalog catalog = AssetDatabase.LoadAssetAtPath<PoolCatalog>(CatalogPath);
        if (catalog != null) return catalog;
        if (!AssetDatabase.IsValidFolder("Assets/Resources"))
            AssetDatabase.CreateFolder("Assets", "Resources");
        catalog = ScriptableObject.CreateInstance<PoolCatalog>();
        AssetDatabase.CreateAsset(catalog, CatalogPath);
        return catalog;
    }

    private static void MigratePrefabs()
    {
        string[] guids = AssetDatabase.FindAssets("t:Prefab");
        for (int i = 0; i < guids.Length; i++)
        {
            string path = AssetDatabase.GUIDToAssetPath(guids[i]);
            GameObject root = PrefabUtility.LoadPrefabContents(path);
            try
            {
                bool poolable = root.GetComponent<Unit>() != null || root.GetComponent<Projectile>() != null;
                if (!poolable || root.GetComponent<PoolMember>() != null) continue;
                root.AddComponent<PoolMember>();
                PrefabUtility.SaveAsPrefabAsset(root, path);
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(root);
            }
        }
    }

    /// <summary>Title 씬을 갱신하고 예외 시에도 기존 다중 씬 구성을 복원합니다.</summary>
    private static void RegisterPoolManager(PoolCatalog catalog)
    {
        SceneSetup[] previousSetup = EditorSceneManager.GetSceneManagerSetup();
        try
        {
            Scene title = EditorSceneManager.OpenScene(TitleScenePath, OpenSceneMode.Single);

            Managers managers = Object.FindAnyObjectByType<Managers>();
            if (managers == null)
                throw new MissingReferenceException("Title 씬에서 Managers를 찾을 수 없습니다.");

            PoolManager poolManager = managers.GetComponentInChildren<PoolManager>(true);
            if (poolManager == null)
            {
                var child = new GameObject("PoolManager");
                child.transform.SetParent(managers.transform, false);
                poolManager = child.AddComponent<PoolManager>();
            }

            var poolSerialized = new SerializedObject(poolManager);
            poolSerialized.FindProperty("m_Catalog").objectReferenceValue = catalog;
            poolSerialized.ApplyModifiedPropertiesWithoutUndo();

            var managersSerialized = new SerializedObject(managers);
            SerializedProperty list = managersSerialized.FindProperty("_allManagers");
            bool registered = false;
            for (int i = 0; i < list.arraySize; i++)
            {
                if (list.GetArrayElementAtIndex(i).objectReferenceValue == poolManager)
                {
                    registered = true;
                    break;
                }
            }
            if (!registered)
            {
                int index = list.arraySize;
                list.InsertArrayElementAtIndex(index);
                list.GetArrayElementAtIndex(index).objectReferenceValue = poolManager;
                managersSerialized.ApplyModifiedPropertiesWithoutUndo();
            }

            EditorUtility.SetDirty(poolManager);
            EditorUtility.SetDirty(managers);
            EditorSceneManager.MarkSceneDirty(title);
            EditorSceneManager.SaveScene(title);
        }
        finally
        {
            EditorSceneManager.RestoreSceneManagerSetup(previousSetup);
        }
    }
}
#endif
