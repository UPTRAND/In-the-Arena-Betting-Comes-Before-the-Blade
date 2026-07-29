#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.Reflection;
using InTheArena.Unit;
using UnityEditor;
using UnityEngine;
using UnitType = InTheArena.Unit.Unit;

namespace InTheArena.Editor.Unit
{
    public static class UnitSystemMigration
    {
        private const string SkillFolder = "Assets/ScriptableObject/Unit/Unit_Skill/Examples";
        private const string EffectFolder = "Assets/ScriptableObject/Unit/Unit_Effect/Examples";
        private const string ProjectileFolder = "Assets/Prefabs/Projectile";

        [MenuItem("Tools/In The Arena/Unit/Migrate Unit Prefabs And Validate Data")]
        public static void MigrateAll()
        {
            int prefabCount = MigratePrefabs();
            int invalidCount = ValidateAllData();
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Debug.Log(
                $"[UnitSystemMigration] Prefab {prefabCount}개 검사, 유효하지 않은 Data {invalidCount}개.");
        }

        [MenuItem("Tools/In The Arena/Unit/Create Skill System Example Assets")]
        public static void CreateSkillSystemExampleAssets()
        {
            EnsureFolder(SkillFolder);
            EnsureFolder(EffectFolder);
            EnsureFolder(ProjectileFolder);

            string legacyHeal = "Assets/ScriptableObject/Unit/Unit_Skill/SkillData_Heal.asset";
            if (AssetDatabase.LoadMainAssetAtPath(legacyHeal) != null)
                AssetDatabase.DeleteAsset(legacyHeal);

            GameObject projectilePrefab = CreateProjectilePrefab();
            BuffData shield = CreateStatus<BuffData>(
                "Status_Shield",
                new ShieldStatusBehavior(),
                "Shield",
                8f);
            SetField(shield.Behavior, "m_ShieldAmount", 40f);
            EditorUtility.SetDirty(shield);

            DebuffData stun = CreateStatus<DebuffData>(
                "Status_Stun",
                new StunStatusBehavior(),
                "Stun",
                1.5f);

            BuffData killBuff = CreateStatus<BuffData>(
                "Status_KillPower",
                new StatModifierStatusBehavior(),
                "Kill Power",
                10f);
            SetField(
                killBuff.Behavior,
                "m_Modifier",
                new UnitStat { attackPower = 5f });
            EditorUtility.SetDirty(killBuff);

            SkillData fireBall = CreateSkill(
                "Skill_FireBall",
                SkillType.Active,
                8f,
                3f,
                0.4f,
                new SingleUnitSkillTargeting(),
                SkillExecutionMode.EffectsOnly,
                null,
                CreateProjectileEffect(projectilePrefab));

            var healTargeting = new LowestHealthAllySkillTargeting();
            SetField(healTargeting, "m_MaxHealthRatio", 0.85f);
            SkillData heal = CreateSkill(
                "Skill_Heal",
                SkillType.Active,
                5f,
                5f,
                0.6f,
                healTargeting,
                SkillExecutionMode.EffectsOnly,
                null,
                CreateHealEffect());

            var areaTargeting = new AreaSkillTargeting();
            SetField(areaTargeting, "m_Radius", 2.25f);
            SkillData areaStun = CreateSkill(
                "Skill_AreaStun",
                SkillType.Active,
                6f,
                8f,
                0.8f,
                areaTargeting,
                SkillExecutionMode.EffectsOnly,
                null,
                CreateStatusEffect(stun));

            SkillData shieldSkill = CreateSkill(
                "Skill_Shield",
                SkillType.Active,
                0f,
                10f,
                0.2f,
                new SelfSkillTargeting(),
                SkillExecutionMode.EffectsOnly,
                null,
                CreateStatusEffect(shield));

            var counterBehavior = new CounterAttackSkillBehavior();
            SetField(counterBehavior, "m_AttackPowerRatio", 0.5f);
            SkillData counter = CreateSkill(
                "Skill_CounterAttack",
                SkillType.Passive,
                0f,
                1f,
                0f,
                new SelfSkillTargeting(),
                SkillExecutionMode.BehaviorOnly,
                counterBehavior);

            var killBehavior = new KillBuffSkillBehavior();
            SetField(killBehavior, "m_Buff", killBuff);
            SkillData kill = CreateSkill(
                "Skill_KillBuff",
                SkillType.Passive,
                0f,
                0f,
                0f,
                new SelfSkillTargeting(),
                SkillExecutionMode.BehaviorOnly,
                killBehavior);

            var lifeStealBehavior = new LifeStealSkillBehavior();
            SetField(lifeStealBehavior, "m_LifeStealRatio", 0.15f);
            SkillData lifeSteal = CreateSkill(
                "Skill_LifeSteal",
                SkillType.Passive,
                0f,
                0f,
                0f,
                new SelfSkillTargeting(),
                SkillExecutionMode.BehaviorOnly,
                lifeStealBehavior);

            AssignSkillsToKnight(
                fireBall,
                heal,
                areaStun,
                shieldSkill,
                counter,
                kill,
                lifeSteal);

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Debug.Log("[UnitSystemMigration] 새 Skill/Status 예제 에셋과 Projectile 프리팹을 생성했습니다.");
        }

        [MenuItem("Tools/In The Arena/Unit/Validate Skill System")]
        public static void ValidateAllDataMenu() => ValidateAllData();

        public static int ValidateAllData()
        {
            int invalid = 0;
            foreach (string guid in AssetDatabase.FindAssets("t:SkillData"))
            {
                SkillData data = AssetDatabase.LoadAssetAtPath<SkillData>(
                    AssetDatabase.GUIDToAssetPath(guid));
                if (data != null && !data.IsValid()) invalid++;
            }

            foreach (string guid in AssetDatabase.FindAssets("t:UnitData"))
            {
                UnitData data = AssetDatabase.LoadAssetAtPath<UnitData>(
                    AssetDatabase.GUIDToAssetPath(guid));
                if (data != null && !data.IsValid()) invalid++;
            }

            Debug.Log($"[UnitSystemMigration] Skill/Unit 데이터 검증 완료. Invalid={invalid}");
            return invalid;
        }

        private static SkillEffectDefinition CreateProjectileEffect(GameObject prefab)
        {
            var effect = new SpawnProjectileSkillEffect();
            SetField(effect, "m_ProjectilePrefab", prefab);
            SetField(effect, "m_Speed", 18f);
            SetField(effect, "m_Lifetime", 5f);
            SetField(effect, "m_BaseDamage", 25f);
            SetField(effect, "m_AttackPowerRatio", 0.7f);
            SetField(effect, "m_CriticalChance", 0.1f);
            return effect;
        }

        private static SkillEffectDefinition CreateHealEffect()
        {
            var effect = new HealSkillEffect();
            SetField(effect, "m_BaseHeal", 30f);
            SetField(effect, "m_AttackPowerRatio", 0.5f);
            return effect;
        }

        private static SkillEffectDefinition CreateStatusEffect(StatusEffectData data)
        {
            var effect = new ApplyStatusEffectSkillEffect();
            SetField(effect, "m_StatusEffect", data);
            return effect;
        }

        private static SkillData CreateSkill(
            string assetName,
            SkillType type,
            float range,
            float cooldown,
            float castTime,
            SkillTargetingDefinition targeting,
            SkillExecutionMode mode,
            SkillBehaviorDefinition behavior,
            params SkillEffectDefinition[] effects)
        {
            string path = $"{SkillFolder}/{assetName}.asset";
            SkillData data = AssetDatabase.LoadAssetAtPath<SkillData>(path);
            bool isNew = data == null;
            if (isNew) data = ScriptableObject.CreateInstance<SkillData>();
            data.name = assetName;
            SetField(data, "m_SkillName", assetName.Replace("Skill_", string.Empty));
            SetField(data, "m_SkillType", type);
            SetField(data, "m_Range", range);
            SetField(data, "m_Cooldown", cooldown);
            SetField(data, "m_CastTime", castTime);
            SetField(data, "m_ExecutionMode", mode);
            SetField(data, "m_Targeting", targeting);
            SetField(data, "m_Effects", new List<SkillEffectDefinition>(effects));
            SetField(data, "m_Behavior", behavior);
            if (isNew) AssetDatabase.CreateAsset(data, path);
            else EditorUtility.SetDirty(data);
            return data;
        }

        private static T CreateStatus<T>(
            string assetName,
            StatusEffectBehaviorDefinition behavior,
            string displayName,
            float duration) where T : StatusEffectData
        {
            string path = $"{EffectFolder}/{assetName}.asset";
            T data = AssetDatabase.LoadAssetAtPath<T>(path);
            bool isNew = data == null;
            if (isNew) data = ScriptableObject.CreateInstance<T>();
            data.name = assetName;
            SetField(data, "m_EffectName", displayName);
            SetField(data, "m_Duration", duration);
            SetField(data, "m_Behavior", behavior);
            if (isNew) AssetDatabase.CreateAsset(data, path);
            else EditorUtility.SetDirty(data);
            return data;
        }

        private static GameObject CreateProjectilePrefab()
        {
            const string path = ProjectileFolder + "/Projectile_FireBall.prefab";
            GameObject existing = AssetDatabase.LoadAssetAtPath<GameObject>(path);
            if (existing != null) return existing;
            var root = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            root.name = "Projectile_FireBall";
            root.transform.localScale = Vector3.one * 0.25f;
            UnityEngine.Object.DestroyImmediate(root.GetComponent<Collider>());
            root.AddComponent<Projectile>();
            root.AddComponent<PoolMember>();
            GameObject prefab = PrefabUtility.SaveAsPrefabAsset(root, path);
            UnityEngine.Object.DestroyImmediate(root);
            return prefab;
        }

        private static void AssignSkillsToKnight(params SkillData[] skills)
        {
            const string path = "Assets/ScriptableObject/Unit/Unit_Base/UnitData_Knight.asset";
            UnitData knight = AssetDatabase.LoadAssetAtPath<UnitData>(path);
            if (knight == null) return;
            SetField(knight, "m_SkillDatas", new List<SkillData>(skills));
            EditorUtility.SetDirty(knight);
        }

        private static int MigratePrefabs()
        {
            int changed = 0;
            foreach (string guid in AssetDatabase.FindAssets("t:Prefab"))
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
                    if (root.GetComponent<PoolMember>() == null) root.AddComponent<PoolMember>();
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

        private static void EnsureFolder(string folder)
        {
            string[] parts = folder.Split('/');
            string current = parts[0];
            for (int i = 1; i < parts.Length; i++)
            {
                string next = $"{current}/{parts[i]}";
                if (!AssetDatabase.IsValidFolder(next))
                    AssetDatabase.CreateFolder(current, parts[i]);
                current = next;
            }
        }

        private static void SetField(object target, string fieldName, object value)
        {
            Type type = target.GetType();
            FieldInfo field = null;
            while (type != null && field == null)
            {
                field = type.GetField(fieldName, BindingFlags.Instance | BindingFlags.NonPublic);
                type = type.BaseType;
            }
            if (field == null)
                throw new MissingFieldException(target.GetType().FullName, fieldName);
            field.SetValue(target, value);
        }
    }
}
#endif
