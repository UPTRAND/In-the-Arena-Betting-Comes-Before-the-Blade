#if UNITY_EDITOR
using System.Collections.Generic;
using InTheArena.Unit;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEngine;

namespace InTheArena.MainGame.Editor
{
    /// <summary>실제 스테이지가 참조하는 콘텐츠와 필수 빌드 UI를 검사합니다.</summary>
    public sealed class ContentBuildValidation : IPreprocessBuildWithReport
    {
        public int callbackOrder => 0;

        /// <summary>실행 불가능한 콘텐츠가 포함된 빌드를 중단합니다.</summary>
        public void OnPreprocessBuild(BuildReport report)
        {
            List<string> errors = CollectErrors();
            if (errors.Count > 0)
            {
                throw new BuildFailedException(string.Join("\n", errors));
            }
        }

        /// <summary>에디터 메뉴에서 빌드와 동일한 검사를 실행합니다.</summary>
        [MenuItem("Tools/In The Arena/Validate Build Content")]
        public static void ValidateFromMenu()
        {
            List<string> errors = CollectErrors();
            if (errors.Count == 0)
            {
                Debug.Log("[ContentBuildValidation] 콘텐츠 검사 통과");
                return;
            }
            Debug.LogError(string.Join("\n", errors));
        }

        /// <summary>스테이지·라운드·참조 유닛과 리소스 오류를 변경 없이 수집합니다.</summary>
        public static List<string> CollectErrors()
        {
            var errors = new List<string>();
            var checkedUnits = new HashSet<UnitData>();
            var buildRoots = new List<string>();
            foreach (EditorBuildSettingsScene scene in EditorBuildSettings.scenes)
            {
                if (scene.enabled)
                {
                    buildRoots.Add(scene.path);
                }
            }
            foreach (string path in AssetDatabase.GetAllAssetPaths())
            {
                if (path.Contains("/Resources/") && !AssetDatabase.IsValidFolder(path))
                {
                    buildRoots.Add(path);
                }
            }
            var included = new HashSet<string>(AssetDatabase.GetDependencies(buildRoots.ToArray(), true));
            if (Resources.Load<GameObject>(UnitHpBarPresenter.ResourcePath) == null)
            {
                errors.Add("빌드에 포함되는 공용 HP바 리소스가 없습니다.");
            }

            foreach (string guid in AssetDatabase.FindAssets("t:StageData", new[] { "Assets/ScriptableObject/Stage" }))
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                if (!included.Contains(path))
                {
                    continue;
                }
                StageData stage = AssetDatabase.LoadAssetAtPath<StageData>(path);
                if (stage == null || !stage.IsValid())
                {
                    errors.Add(path + ": StageData 검증 실패");
                    continue;
                }
                if (!BettingRules.IsValidWager(stage.InitialCall))
                {
                    errors.Add(path + ": 시작 Call은 최소 베팅액 이상이며 베팅 단위에 맞아야 합니다.");
                }
                foreach (RoundData round in stage.RoundDatas)
                {
                    if (round == null)
                    {
                        continue;
                    }
                    if (round.SpecialRule != RoundRule.None)
                    {
                        errors.Add(AssetDatabase.GetAssetPath(round) + ": 구현되지 않은 특별 규칙입니다.");
                    }
                    int maximum = ValidateGrid(round.TeamAGrid, checkedUnits, errors);
                    maximum += ValidateGrid(round.TeamBGrid, checkedUnits, errors);
                    if (maximum > UnitPoolService.MaxActiveUnits)
                    {
                        errors.Add(round.name + ": 최대 편성 수가 유닛 수용량을 넘습니다.");
                    }
                }
            }
            return errors;
        }

        /// <summary>확률 편성의 최대 수와 모든 후보 유닛의 필수 참조를 검사합니다.</summary>
        private static int ValidateGrid(GridCellData[] grid, HashSet<UnitData> checkedUnits, List<string> errors)
        {
            int count = 0;
            if (grid == null)
            {
                return count;
            }
            foreach (GridCellData cell in grid)
            {
                if (cell == null)
                {
                    continue;
                }
                if (cell.IsFixed)
                {
                    count += cell.FixedCount;
                    ValidateUnit(cell.FixedUnit, checkedUnits, errors);
                }
                else
                {
                    count += 1 + cell.ExtraCountRange;
                    foreach (UnitData unit in cell.VariableUnitPool)
                    {
                        ValidateUnit(unit, checkedUnits, errors);
                    }
                }
            }
            return count;
        }

        /// <summary>같은 유닛은 한 번만 필수 공격·스킬·프리팹 설정을 검사합니다.</summary>
        private static void ValidateUnit(UnitData unit, HashSet<UnitData> checkedUnits, List<string> errors)
        {
            if (unit == null)
            {
                errors.Add("편성에 null 유닛이 있습니다.");
                return;
            }
            if (checkedUnits.Add(unit) && !unit.IsValid())
            {
                errors.Add(AssetDatabase.GetAssetPath(unit) + ": UnitData 검증 실패");
            }
        }
    }
}
#endif
