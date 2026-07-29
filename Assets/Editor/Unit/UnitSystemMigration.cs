#if UNITY_EDITOR
using System.Collections.Generic;
using InTheArena.Unit;
using UnityEditor;
using UnityEngine;
using UnitType = InTheArena.Unit.Unit;

namespace InTheArena.Editor.Unit
{
    public static class UnitSystemMigration
    {
        [MenuItem("Tools/In The Arena/Unit/Migrate Unit Data And Prefabs")]
        public static void MigrateAll()
        {
            int dataCount = MigrateUnitData();
            int prefabCount = MigratePrefabs();
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Debug.Log($"[UnitSystemMigration] UnitData {dataCount}개, Prefab {prefabCount}개를 검사/마이그레이션했습니다.");
        }

        private static int MigrateUnitData()
        {
            int changed = 0;
            string[] guids = AssetDatabase.FindAssets("t:UnitData");
            foreach (string guid in guids)
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                UnitData data = AssetDatabase.LoadAssetAtPath<UnitData>(path);
                if (data == null) continue;

                var serialized = new SerializedObject(data);
                SerializedProperty legacy = serialized.FindProperty("m_SkillData");
                SerializedProperty skills = serialized.FindProperty("m_SkillDatas");
                if (legacy?.objectReferenceValue != null && skills != null && !Contains(skills, legacy.objectReferenceValue))
                {
                    skills.InsertArrayElementAtIndex(0);
                    skills.GetArrayElementAtIndex(0).objectReferenceValue = legacy.objectReferenceValue;
                    serialized.ApplyModifiedPropertiesWithoutUndo();
                    EditorUtility.SetDirty(data);
                    changed++;
                }
            }
            return changed;
        }

        private static int MigratePrefabs()
        {
            int changed = 0;
            string[] guids = AssetDatabase.FindAssets("t:Prefab");
            foreach (string guid in guids)
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                GameObject root = PrefabUtility.LoadPrefabContents(path);
                try
                {
                    UnitType unit = root.GetComponent<UnitType>();
                    if (unit == null) continue;

                    Transform visual = EnsureChild(root.transform, "VisualRoot", Vector3.zero);
                    Transform ground = EnsureChild(root.transform, "GroundAnchor", Vector3.zero);
                    Transform cast = EnsureChild(root.transform, "CastAnchor", Vector3.up * 0.75f);
                    Transform hit = EnsureChild(root.transform, "HitAnchor", Vector3.up * 0.5f);

                    var serialized = new SerializedObject(unit);
                    serialized.FindProperty("m_VisualRoot").objectReferenceValue = visual;
                    serialized.FindProperty("m_GroundAnchor").objectReferenceValue = ground;
                    serialized.FindProperty("m_CastAnchor").objectReferenceValue = cast;
                    serialized.FindProperty("m_HitAnchor").objectReferenceValue = hit;
                    serialized.ApplyModifiedPropertiesWithoutUndo();

                    if (root.TryGetComponent(out Rigidbody rigidbody))
                    {
                        rigidbody.isKinematic = true;
                        rigidbody.useGravity = false;
                    }
                    if (root.TryGetComponent(out Collider collider)) collider.isTrigger = true;

                    PrefabUtility.SaveAsPrefabAsset(root, path);
                    changed++;
                }
                finally
                {
                    PrefabUtility.UnloadPrefabContents(root);
                }
            }
            return changed;
        }

        private static Transform EnsureChild(Transform root, string name, Vector3 localPosition)
        {
            Transform child = root.Find(name);
            if (child != null) return child;
            var childObject = new GameObject(name);
            child = childObject.transform;
            child.SetParent(root, false);
            child.localPosition = localPosition;
            return child;
        }

        private static bool Contains(SerializedProperty array, Object value)
        {
            for (int i = 0; i < array.arraySize; i++)
            {
                if (array.GetArrayElementAtIndex(i).objectReferenceValue == value) return true;
            }
            return false;
        }
    }
}
#endif
