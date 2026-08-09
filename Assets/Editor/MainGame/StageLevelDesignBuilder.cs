#if UNITY_EDITOR
using System.Collections.Generic;
using System.IO;
using InTheArena.Unit;
using UnityEditor;
using UnityEngine;

namespace InTheArena.MainGame.Editor
{
    public static class StageLevelDesignBuilder
    {
        private const string MenuPath = "Tools/In The Arena/Rebuild 15 Stage Level Design";
        private const string StageRoot = "Assets/ScriptableObject/Stage/LevelDesign";
        private const string RoundRoot = "Assets/ScriptableObject/Round/LevelDesign";
        private const string LobbyStagePanelPrefab = "Assets/Prefabs/UI/Panel/UI_LobbyStagePanel.prefab";

        private static readonly StageDifficulty[] Difficulties =
        {
            StageDifficulty.Easy,
            StageDifficulty.Normal,
            StageDifficulty.Hard
        };

        [MenuItem(MenuPath)]
        public static void Rebuild()
        {
            EnsureDirectory(StageRoot);
            EnsureDirectory(RoundRoot);
            DeleteLegacyDifficultyRoundFolders();

            var units = LoadUnits();
            var stages = new List<StageData>(15);

            for (int stageNumber = 1; stageNumber <= 15; stageNumber++)
            {
                StageRegionSpec region = GetRegionSpec(stageNumber);
                string stagePath = $"{StageRoot}/StageData_Level{stageNumber:00}_{region.AssetSuffix}.asset";
                StageData stage = LoadOrCreateAsset<StageData>(stagePath);
                ConfigureStage(stage, stageNumber, region, units);
                stages.Add(stage);
            }

            UpdateLobbyStagePanel(stages);

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Debug.Log("[StageLevelDesignBuilder] 15개 StageData와 공통 7라운드 구조를 다시 생성했습니다.");
        }

        private static UnitLookup LoadUnits()
        {
            return new UnitLookup
            {
                Knight = LoadUnit("UnitData_Knight"),
                Archer = LoadUnit("UnitData_Archer"),
                Wizard = LoadUnit("UnitData_Wizard"),
                Prist = LoadUnit("UnitData_Prist"),
                King = LoadUnit("UnitData_King"),
                Peasant = LoadUnit("UnitData_Peasant"),
                Thief = LoadUnit("UnitData_Thief"),
                Lumberjack = LoadUnit("UnitData_Lumberjack"),
                Hunter = LoadUnit("UnitData_Hunter"),
                Blacksmith = LoadUnit("UnitData_Blacksmith")
            };
        }

        private static UnitData LoadUnit(string assetName)
        {
            string[] guids = AssetDatabase.FindAssets($"{assetName} t:UnitData");
            if (guids.Length == 0)
            {
                Debug.LogError($"[StageLevelDesignBuilder] UnitData asset을 찾을 수 없습니다: {assetName}");
                return null;
            }

            return AssetDatabase.LoadAssetAtPath<UnitData>(AssetDatabase.GUIDToAssetPath(guids[0]));
        }

        private static void ConfigureStage(StageData stage, int stageNumber, StageRegionSpec region, UnitLookup units)
        {
            List<RoundData> rounds = BuildRounds(stageNumber, region, units);

            SerializedObject so = new SerializedObject(stage);
            so.FindProperty("m_StageName").stringValue = region.DisplayName;
            so.FindProperty("m_Region").enumValueIndex = (int)StageRegion.CentralCastle;
            so.FindProperty("m_StageNum").intValue = stageNumber;
            so.FindProperty("m_Difficulty").enumValueIndex = 0;
            so.FindProperty("m_BackgroundSprite").objectReferenceValue = region.Background;
            so.FindProperty("m_InitialCall").intValue = 500;
            so.FindProperty("m_TargetCall").intValue = StageData.GetPresetTargetCall(StageDifficulty.Easy);
            SetRoundList(so.FindProperty("m_RoundDatas"), rounds);

            SerializedProperty configs = so.FindProperty("m_DifficultyConfigs");
            configs.arraySize = Difficulties.Length;
            for (int i = 0; i < Difficulties.Length; i++)
            {
                StageDifficulty difficulty = Difficulties[i];
                SerializedProperty config = configs.GetArrayElementAtIndex(i);
                config.FindPropertyRelative("m_Difficulty").enumValueIndex = (int)difficulty;
                config.FindPropertyRelative("m_InitialCall").intValue = 500;
                config.FindPropertyRelative("m_TargetCall").intValue = StageData.GetPresetTargetCall(difficulty);
                config.FindPropertyRelative("m_RoundCount").intValue = StageData.GetPresetRoundCount(difficulty);
            }

            so.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(stage);
        }

        private static List<RoundData> BuildRounds(int stageNumber, StageRegionSpec region, UnitLookup units)
        {
            var rounds = new List<RoundData>(7);
            string roundDir = $"{RoundRoot}/Stage{stageNumber:00}";
            EnsureDirectory(roundDir);

            for (int roundNumber = 1; roundNumber <= 7; roundNumber++)
            {
                string roundPath = $"{roundDir}/RoundData_Stage{stageNumber:00}_Round{roundNumber:00}.asset";
                RoundData round = LoadOrCreateAsset<RoundData>(roundPath);
                ConfigureRound(round, roundNumber, stageNumber, region, units);
                rounds.Add(round);
            }

            return rounds;
        }

        private static void ConfigureRound(
            RoundData round,
            int roundNumber,
            int stageNumber,
            StageRegionSpec region,
            UnitLookup units)
        {
            SerializedObject so = new SerializedObject(round);
            so.FindProperty("m_RoundNumber").intValue = roundNumber;
            so.FindProperty("m_SpecialRule").enumValueIndex = 0;

            UnitData[] pool = GetAllowedPool(stageNumber, units);
            if (roundNumber == 1)
            {
                ConfigureFixedOpeningRound(so.FindProperty("m_TeamAGrid"), so.FindProperty("m_TeamBGrid"), stageNumber, units);
            }
            else
            {
                ConfigureProgressionGrid(so.FindProperty("m_TeamAGrid"), pool, roundNumber, stageNumber, 0);
                ConfigureProgressionGrid(so.FindProperty("m_TeamBGrid"), pool, roundNumber, stageNumber, 1);
            }

            so.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(round);
        }

        private static void ConfigureFixedOpeningRound(SerializedProperty teamA, SerializedProperty teamB, int stageNumber, UnitLookup units)
        {
            ClearGrid(teamA);
            ClearGrid(teamB);

            if (stageNumber == 1)
            {
                SetFixedCell(teamA, 0, units.Knight, 1);
                SetFixedCell(teamA, 1, units.Archer, 1);
                SetFixedCell(teamA, 2, units.Wizard, 1);
                SetFixedCell(teamB, 0, units.Knight, 1);
                SetFixedCell(teamB, 1, units.Archer, 1);
                SetFixedCell(teamB, 2, units.Prist, 1);
                return;
            }

            if (stageNumber == 11)
            {
                SetFixedCell(teamA, 0, units.Lumberjack, 1);
                SetFixedCell(teamA, 1, units.Hunter, 1);
                SetFixedCell(teamB, 0, units.Blacksmith, 1);
                SetFixedCell(teamB, 1, units.Lumberjack, 1);
                return;
            }

            UnitData[] pool = GetAllowedPool(stageNumber, units);
            SetFixedCell(teamA, 0, pool[stageNumber % pool.Length], 1);
            SetFixedCell(teamA, 1, pool[(stageNumber + 1) % pool.Length], 1);
            SetFixedCell(teamA, 2, pool[(stageNumber + 2) % pool.Length], 1);
            SetFixedCell(teamB, 0, pool[(stageNumber + 3) % pool.Length], 1);
            SetFixedCell(teamB, 1, pool[(stageNumber + 4) % pool.Length], 1);
            SetFixedCell(teamB, 2, pool[(stageNumber + 5) % pool.Length], 1);
        }

        private static UnitData[] GetAllowedPool(int stageNumber, UnitLookup units)
        {
            if (stageNumber <= 5)
            {
                return new[] { units.Knight, units.Archer, units.Wizard, units.Prist };
            }

            if (stageNumber <= 10)
            {
                return new[] { units.Knight, units.Archer, units.Wizard, units.Prist, units.King, units.Peasant, units.Thief };
            }

            return new[]
            {
                units.Knight, units.Archer, units.Wizard, units.Prist,
                units.King, units.Peasant, units.Thief,
                units.Lumberjack, units.Hunter, units.Blacksmith
            };
        }

        private static void ConfigureRandomGrid(SerializedProperty grid, UnitData[] pool, int activeCells, int extraCount, int offset)
        {
            ClearGrid(grid);
            for (int i = 0; i < activeCells; i++)
            {
                int index = (i + offset) % 6;
                SetRandomCell(grid, index, pool, extraCount);
            }
        }

        private static void ConfigureProgressionGrid(
            SerializedProperty grid,
            UnitData[] pool,
            int roundNumber,
            int stageNumber,
            int offset)
        {
            ClearGrid(grid);

            switch (roundNumber)
            {
                case 2:
                    SetRandomCells(grid, pool, 3, offset, 0);
                    break;
                case 3:
                    SetRandomCells(grid, pool, 4, offset, 0);
                    break;
                case 4:
                    SetRandomCells(grid, pool, 5, offset, 0);
                    break;
                case 5:
                    for (int i = 0; i < 6; i++)
                    {
                        int index = (i + offset) % 6;
                        SetRandomCell(grid, index, pool, i == 0 ? 1 : 0);
                    }
                    break;
                case 6:
                    SetFixedCell(grid, offset % 6, PickPoolUnit(pool, stageNumber + roundNumber + offset), 2);
                    for (int i = 1; i < 6; i++)
                    {
                        int index = (i + offset) % 6;
                        SetRandomCell(grid, index, pool, i == 1 ? 1 : 0);
                    }
                    break;
                case 7:
                    SetFixedCell(grid, offset % 6, PickPoolUnit(pool, stageNumber + roundNumber + offset), 2);
                    SetFixedCell(grid, (offset + 1) % 6, PickPoolUnit(pool, stageNumber + roundNumber + offset + 1), 2);
                    for (int i = 2; i < 6; i++)
                    {
                        int index = (i + offset) % 6;
                        SetRandomCell(grid, index, pool, i <= 3 ? 1 : 0);
                    }
                    break;
                default:
                    SetRandomCells(grid, pool, 3, offset, 0);
                    break;
            }
        }

        private static void SetRandomCells(SerializedProperty grid, UnitData[] pool, int activeCells, int offset, int extraCount)
        {
            for (int i = 0; i < activeCells; i++)
            {
                int index = (i + offset) % 6;
                SetRandomCell(grid, index, pool, extraCount);
            }
        }

        private static UnitData PickPoolUnit(UnitData[] pool, int seed)
        {
            if (pool == null || pool.Length == 0) return null;
            return pool[Mathf.Abs(seed) % pool.Length];
        }

        private static void ClearGrid(SerializedProperty grid)
        {
            grid.arraySize = 6;
            for (int i = 0; i < 6; i++)
            {
                SerializedProperty cell = grid.GetArrayElementAtIndex(i);
                cell.FindPropertyRelative("m_IsFixed").boolValue = false;
                cell.FindPropertyRelative("m_FixedUnit").objectReferenceValue = null;
                cell.FindPropertyRelative("m_FixedCount").intValue = 1;
                SerializedProperty pool = cell.FindPropertyRelative("m_VariableUnitPool");
                pool.arraySize = 0;
                cell.FindPropertyRelative("m_ExtraCountRange").intValue = 0;
                cell.FindPropertyRelative("m_SpawnProbability").floatValue = 0f;
            }
        }

        private static void SetFixedCell(SerializedProperty grid, int index, UnitData unit, int count)
        {
            SerializedProperty cell = grid.GetArrayElementAtIndex(index);
            cell.FindPropertyRelative("m_IsFixed").boolValue = true;
            cell.FindPropertyRelative("m_FixedUnit").objectReferenceValue = unit;
            cell.FindPropertyRelative("m_FixedCount").intValue = Mathf.Clamp(count, 1, 9);
            cell.FindPropertyRelative("m_VariableUnitPool").arraySize = 0;
            cell.FindPropertyRelative("m_ExtraCountRange").intValue = 0;
            cell.FindPropertyRelative("m_SpawnProbability").floatValue = 1f;
        }

        private static void SetRandomCell(SerializedProperty grid, int index, UnitData[] units, int extraCount)
        {
            SerializedProperty cell = grid.GetArrayElementAtIndex(index);
            cell.FindPropertyRelative("m_IsFixed").boolValue = false;
            cell.FindPropertyRelative("m_FixedUnit").objectReferenceValue = null;
            cell.FindPropertyRelative("m_FixedCount").intValue = 1;
            SerializedProperty pool = cell.FindPropertyRelative("m_VariableUnitPool");
            pool.arraySize = units.Length;
            for (int i = 0; i < units.Length; i++)
            {
                pool.GetArrayElementAtIndex(i).objectReferenceValue = units[i];
            }

            cell.FindPropertyRelative("m_ExtraCountRange").intValue = Mathf.Clamp(extraCount, 0, 2);
            cell.FindPropertyRelative("m_SpawnProbability").floatValue = 1f;
        }

        private static void SetRoundList(SerializedProperty property, List<RoundData> rounds)
        {
            property.arraySize = rounds.Count;
            for (int i = 0; i < rounds.Count; i++)
            {
                property.GetArrayElementAtIndex(i).objectReferenceValue = rounds[i];
            }
        }

        private static void SetStageList(SerializedProperty property, List<StageData> stages)
        {
            property.arraySize = stages.Count;
            for (int i = 0; i < stages.Count; i++)
            {
                property.GetArrayElementAtIndex(i).objectReferenceValue = stages[i];
            }
        }

        private static void UpdateLobbyStagePanel(List<StageData> stages)
        {
            GameObject root = PrefabUtility.LoadPrefabContents(LobbyStagePanelPrefab);
            try
            {
                var panel = root.GetComponentInChildren<InTheArena.UI.UI_LobbyStagePanel>(true);
                if (panel == null)
                {
                    Debug.LogError("[StageLevelDesignBuilder] 프리팹에서 UI_LobbyStagePanel 컴포넌트를 찾을 수 없습니다.");
                    return;
                }

                SerializedObject so = new SerializedObject(panel);
                SetStageList(so.FindProperty("m_StageDatas"), stages);
                so.ApplyModifiedPropertiesWithoutUndo();
                PrefabUtility.SaveAsPrefabAsset(root, LobbyStagePanelPrefab);
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(root);
            }
        }

        private static void DeleteLegacyDifficultyRoundFolders()
        {
            for (int stageNumber = 1; stageNumber <= 15; stageNumber++)
            {
                foreach (string folder in new[] { "Easy", "Normal", "Hard" })
                {
                    string path = $"{RoundRoot}/Stage{stageNumber:00}/{folder}";
                    if (AssetDatabase.IsValidFolder(path))
                    {
                        AssetDatabase.DeleteAsset(path);
                    }
                }
            }
        }

        private static StageRegionSpec GetRegionSpec(int stageNumber)
        {
            if (stageNumber <= 5)
            {
                return new StageRegionSpec(
                    "RoyalKnights",
                    "\uC655\uAD81 \uAE30\uC0AC\uB2E8",
                    LoadSprite("Assets/Art/Stages/Stage01_RoyalKnights/Stage1Background.png"));
            }

            if (stageNumber <= 10)
            {
                return new StageRegionSpec(
                    "CentralCastle",
                    "\uC13C\uD2B8\uB7F4 \uCE90\uC2AC",
                    LoadSprite("Assets/Art/Stages/Stage02_CentralCastle/stage-02-central-castle-background-1080x1920.png"));
            }

            return new StageRegionSpec(
                "CentralOutskirtsVillage",
                "\uC13C\uD2B8\uB7F4 \uC678\uACFD \uB9C8\uC744",
                LoadSprite("Assets/Art/Stages/Stage03_CentralOutskirtsVillage/stage-03-central-outskirts-village-background-1080x1920.png"));
        }

        private static Sprite LoadSprite(string path)
        {
            Sprite sprite = AssetDatabase.LoadAssetAtPath<Sprite>(path);
            if (sprite == null)
            {
                Debug.LogError($"[StageLevelDesignBuilder] 배경 스프라이트를 찾을 수 없습니다: {path}");
            }

            return sprite;
        }

        private static T LoadOrCreateAsset<T>(string path) where T : ScriptableObject
        {
            T asset = AssetDatabase.LoadAssetAtPath<T>(path);
            if (asset != null)
            {
                return asset;
            }

            EnsureDirectory(Path.GetDirectoryName(path)?.Replace('\\', '/'));
            asset = ScriptableObject.CreateInstance<T>();
            AssetDatabase.CreateAsset(asset, path);
            return asset;
        }

        private static void EnsureDirectory(string path)
        {
            if (string.IsNullOrEmpty(path) || AssetDatabase.IsValidFolder(path))
            {
                return;
            }

            string parent = Path.GetDirectoryName(path)?.Replace('\\', '/');
            string name = Path.GetFileName(path);
            EnsureDirectory(parent);
            AssetDatabase.CreateFolder(parent, name);
        }

        private sealed class UnitLookup
        {
            public UnitData Knight;
            public UnitData Archer;
            public UnitData Wizard;
            public UnitData Prist;
            public UnitData King;
            public UnitData Peasant;
            public UnitData Thief;
            public UnitData Lumberjack;
            public UnitData Hunter;
            public UnitData Blacksmith;
        }

        private readonly struct StageRegionSpec
        {
            public StageRegionSpec(string assetSuffix, string displayName, Sprite background)
            {
                AssetSuffix = assetSuffix;
                DisplayName = displayName;
                Background = background;
            }

            public string AssetSuffix { get; }
            public string DisplayName { get; }
            public Sprite Background { get; }
        }
    }
}
#endif
