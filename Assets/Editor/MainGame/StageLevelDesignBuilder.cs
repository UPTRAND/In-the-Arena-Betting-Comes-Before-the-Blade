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

        [MenuItem(MenuPath)]
        public static void Rebuild()
        {
            EnsureDirectory(StageRoot);
            EnsureDirectory(RoundRoot);
            DeleteLegacyDifficultyRoundFolders();

            UnitLookup units = LoadUnits();
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
            Debug.Log("[StageLevelDesignBuilder] Rebuilt 15 stage level design assets.");
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
                Debug.LogError($"[StageLevelDesignBuilder] UnitData asset not found: {assetName}");
                return null;
            }

            return AssetDatabase.LoadAssetAtPath<UnitData>(AssetDatabase.GUIDToAssetPath(guids[0]));
        }

        private static void ConfigureStage(StageData stage, int stageNumber, StageRegionSpec region, UnitLookup units)
        {
            List<RoundData> rounds = BuildRounds(stageNumber, units);

            var so = new SerializedObject(stage);
            so.FindProperty("m_StageName").stringValue = region.DisplayName;
            so.FindProperty("m_Region").enumValueIndex = (int)StageRegion.CentralCastle;
            so.FindProperty("m_StageNum").intValue = stageNumber;
            so.FindProperty("m_BackgroundSprite").objectReferenceValue = region.Background;
            so.FindProperty("m_InitialCall").intValue = 500;
            so.FindProperty("m_TargetCall").intValue = GetStageTargetCall(stageNumber);
            SetRoundList(so.FindProperty("m_RoundDatas"), rounds, GetStageRoundCount(stageNumber));
            so.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(stage);
        }

        private static List<RoundData> BuildRounds(int stageNumber, UnitLookup units)
        {
            var rounds = new List<RoundData>(7);
            string roundDir = $"{RoundRoot}/Stage{stageNumber:00}";
            EnsureDirectory(roundDir);

            for (int roundNumber = 1; roundNumber <= 7; roundNumber++)
            {
                string roundPath = $"{roundDir}/RoundData_Stage{stageNumber:00}_Round{roundNumber:00}.asset";
                RoundData round = LoadOrCreateAsset<RoundData>(roundPath);
                ConfigureRound(round, roundNumber, stageNumber, units);
                rounds.Add(round);
            }

            return rounds;
        }

        private static void ConfigureRound(RoundData round, int roundNumber, int stageNumber, UnitLookup units)
        {
            var so = new SerializedObject(round);
            so.FindProperty("m_RoundNumber").intValue = roundNumber;
            so.FindProperty("m_SpecialRule").enumValueIndex = 0;

            UnitData[] pool = GetAllowedPool(stageNumber, units);
            if (roundNumber == 1)
            {
                ConfigureFixedOpeningRound(so.FindProperty("m_TeamAGrid"), so.FindProperty("m_TeamBGrid"), stageNumber, pool);
            }
            else
            {
                ConfigureProgressionGrid(so.FindProperty("m_TeamAGrid"), pool, roundNumber, stageNumber, 0);
                ConfigureProgressionGrid(so.FindProperty("m_TeamBGrid"), pool, roundNumber, stageNumber, 1);
            }

            so.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(round);
        }

        private static void ConfigureFixedOpeningRound(SerializedProperty teamA, SerializedProperty teamB, int stageNumber, UnitData[] pool)
        {
            ClearGrid(teamA);
            ClearGrid(teamB);

            int count = stageNumber == 1 || stageNumber == 11 ? 2 : 3;
            for (int i = 0; i < count; i++)
            {
                SetFixedCell(teamA, i, PickPoolUnit(pool, stageNumber + i), 1);
                SetFixedCell(teamB, i, PickPoolUnit(pool, stageNumber + i + count), 1);
            }
        }

        private static UnitData[] GetAllowedPool(int stageNumber, UnitLookup units)
        {
            return stageNumber switch
            {
                1 => new[] { units.Knight, units.Archer },
                2 => new[] { units.Knight, units.Archer, units.Wizard },
                3 => new[] { units.Knight, units.Wizard, units.Prist },
                4 => new[] { units.Wizard, units.Archer, units.Prist },
                5 => RoyalKnights(units),
                6 => Combine(new[] { units.King }, RoyalKnights(units)),
                7 => Combine(new[] { units.King, units.Peasant }, RoyalKnights(units)),
                8 => Combine(RoyalKnights(units), new[] { units.Peasant, units.Thief }),
                9 => CentralCastle(units),
                10 => Combine(RoyalKnights(units), CentralCastle(units)),
                11 => Combine(new[] { units.Lumberjack }, RoyalKnights(units), CentralCastle(units)),
                12 => Combine(new[] { units.Hunter, units.Lumberjack }, CentralCastle(units)),
                13 => Combine(new[] { units.Blacksmith, units.Hunter }, RoyalKnights(units)),
                14 => CentralOutskirts(units),
                15 => Combine(RoyalKnights(units), CentralCastle(units), CentralOutskirts(units)),
                _ => RoyalKnights(units)
            };
        }

        private static UnitData[] RoyalKnights(UnitLookup units)
        {
            return new[] { units.Knight, units.Archer, units.Wizard, units.Prist };
        }

        private static UnitData[] CentralCastle(UnitLookup units)
        {
            return new[] { units.King, units.Peasant, units.Thief };
        }

        private static UnitData[] CentralOutskirts(UnitLookup units)
        {
            return new[] { units.Lumberjack, units.Hunter, units.Blacksmith };
        }

        private static UnitData[] Combine(params UnitData[][] groups)
        {
            var result = new List<UnitData>();
            foreach (UnitData[] group in groups)
            {
                foreach (UnitData unit in group)
                {
                    if (unit != null && !result.Contains(unit))
                    {
                        result.Add(unit);
                    }
                }
            }

            return result.ToArray();
        }

        private static void ConfigureProgressionGrid(SerializedProperty grid, UnitData[] pool, int roundNumber, int stageNumber, int offset)
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
                        SetRandomCell(grid, (i + offset) % 6, pool, i == 0 ? 1 : 0);
                    }
                    break;
                case 6:
                    SetFixedCell(grid, offset % 6, PickPoolUnit(pool, stageNumber + roundNumber + offset), 2);
                    for (int i = 1; i < 6; i++)
                    {
                        SetRandomCell(grid, (i + offset) % 6, pool, i == 1 ? 1 : 0);
                    }
                    break;
                case 7:
                    SetFixedCell(grid, offset % 6, PickPoolUnit(pool, stageNumber + roundNumber + offset), 2);
                    SetFixedCell(grid, (offset + 1) % 6, PickPoolUnit(pool, stageNumber + roundNumber + offset + 1), 2);
                    for (int i = 2; i < 6; i++)
                    {
                        SetRandomCell(grid, (i + offset) % 6, pool, i <= 3 ? 1 : 0);
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
                SetRandomCell(grid, (i + offset) % 6, pool, extraCount);
            }
        }

        private static UnitData PickPoolUnit(UnitData[] pool, int seed)
        {
            if (pool == null || pool.Length == 0)
            {
                return null;
            }

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
                cell.FindPropertyRelative("m_VariableUnitPool").arraySize = 0;
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

        private static void SetRoundList(SerializedProperty property, List<RoundData> rounds, int count)
        {
            int clampedCount = Mathf.Clamp(count, 1, rounds.Count);
            property.arraySize = clampedCount;
            for (int i = 0; i < clampedCount; i++)
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
                    Debug.LogError("[StageLevelDesignBuilder] UI_LobbyStagePanel component not found in prefab.");
                    return;
                }

                var so = new SerializedObject(panel);
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

        private static int GetStageRoundCount(int stageNumber)
        {
            return stageNumber switch
            {
                1 or 2 => 3,
                3 or 4 => 4,
                5 => 5,
                6 => 4,
                7 or 8 => 5,
                9 or 10 => 6,
                11 => 4,
                12 => 5,
                13 => 6,
                _ => 7
            };
        }

        private static int GetStageTargetCall(int stageNumber)
        {
            return stageNumber switch
            {
                1 => 1500,
                2 => 1800,
                3 => 2400,
                4 => 3000,
                5 => 3600,
                6 => 3200,
                7 => 3900,
                8 => 4600,
                9 => 5400,
                10 => 6200,
                11 => 5000,
                12 => 5800,
                13 => 6600,
                14 => 7600,
                15 => 9000,
                _ => 1500
            };
        }

        private static Sprite LoadSprite(string path)
        {
            Sprite sprite = AssetDatabase.LoadAssetAtPath<Sprite>(path);
            if (sprite == null)
            {
                Debug.LogError($"[StageLevelDesignBuilder] Background sprite not found: {path}");
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
