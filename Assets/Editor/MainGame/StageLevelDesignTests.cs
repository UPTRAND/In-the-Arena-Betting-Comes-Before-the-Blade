#if UNITY_EDITOR
using NUnit.Framework;
using UnityEditor;

namespace InTheArena.MainGame.Editor
{
    public sealed class StageLevelDesignTests
    {
        [Test]
        public void GeneratedStagesUseStageSpecificRoundCountsAndTargetCalls()
        {
            string[] stageGuids = AssetDatabase.FindAssets("t:StageData", new[] { "Assets/ScriptableObject/Stage/LevelDesign" });
            Assert.That(stageGuids.Length, Is.EqualTo(15));

            string[] roundGuids = AssetDatabase.FindAssets("t:RoundData", new[] { "Assets/ScriptableObject/Round/LevelDesign" });
            Assert.That(roundGuids.Length, Is.EqualTo(105));

            StageData first = LoadStage(1, "RoyalKnights");
            Assert.That(first.StageNum, Is.EqualTo(1));
            Assert.That(first.StageName, Is.EqualTo("\uC655\uAD81 \uAE30\uC0AC\uB2E8"));
            Assert.That(first.RoundDatas.Count, Is.EqualTo(3));
            Assert.That(first.TargetCall, Is.EqualTo(1500));

            StageData middle = LoadStage(10, "CentralCastle");
            Assert.That(middle.RoundDatas.Count, Is.EqualTo(6));
            Assert.That(middle.TargetCall, Is.EqualTo(6200));

            StageData final = LoadStage(15, "CentralOutskirtsVillage");
            Assert.That(final.RoundDatas.Count, Is.EqualTo(7));
            Assert.That(final.TargetCall, Is.EqualTo(9000));
        }

        [Test]
        public void OpeningRoundsKeepTutorialSizedDeployments()
        {
            RoundData stageOneRoundOne = AssetDatabase.LoadAssetAtPath<RoundData>(
                "Assets/ScriptableObject/Round/LevelDesign/Stage01/RoundData_Stage01_Round01.asset");
            Assert.That(stageOneRoundOne, Is.Not.Null);
            Assert.That(stageOneRoundOne.TeamAGrid[0].FixedUnit.name, Is.EqualTo("UnitData_Archer"));
            Assert.That(stageOneRoundOne.TeamAGrid[1].FixedUnit.name, Is.EqualTo("UnitData_Knight"));
            Assert.That(stageOneRoundOne.TeamBGrid[0].FixedUnit.name, Is.EqualTo("UnitData_Archer"));
            Assert.That(stageOneRoundOne.TeamBGrid[1].FixedUnit.name, Is.EqualTo("UnitData_Knight"));

            RoundData stageElevenRoundOne = AssetDatabase.LoadAssetAtPath<RoundData>(
                "Assets/ScriptableObject/Round/LevelDesign/Stage11/RoundData_Stage11_Round01.asset");
            Assert.That(stageElevenRoundOne, Is.Not.Null);
            Assert.That(stageElevenRoundOne.TeamAGrid[0].FixedUnit.name, Is.EqualTo("UnitData_Wizard"));
            Assert.That(stageElevenRoundOne.TeamAGrid[1].FixedUnit.name, Is.EqualTo("UnitData_Prist"));
            Assert.That(stageElevenRoundOne.TeamBGrid[0].FixedUnit.name, Is.EqualTo("UnitData_King"));
            Assert.That(stageElevenRoundOne.TeamBGrid[1].FixedUnit.name, Is.EqualTo("UnitData_Peasant"));
        }

        [Test]
        public void GeneratedRoundsFollowRecommendedUnitCountProgression()
        {
            int[] minCounts = { 0, 2, 3, 4, 5, 6, 7, 8 };
            int[] maxCounts = { 0, 3, 4, 5, 6, 7, 8, 10 };

            for (int stageNumber = 1; stageNumber <= 15; stageNumber++)
            {
                for (int roundNumber = 1; roundNumber <= 7; roundNumber++)
                {
                    RoundData round = AssetDatabase.LoadAssetAtPath<RoundData>(
                        $"Assets/ScriptableObject/Round/LevelDesign/Stage{stageNumber:00}/RoundData_Stage{stageNumber:00}_Round{roundNumber:00}.asset");
                    Assert.That(round, Is.Not.Null);

                    AssertGridRange(round.TeamAGrid, minCounts[roundNumber], maxCounts[roundNumber]);
                    AssertGridRange(round.TeamBGrid, minCounts[roundNumber], maxCounts[roundNumber]);
                }
            }
        }

        private static StageData LoadStage(int stageNumber, string suffix)
        {
            StageData stage = AssetDatabase.LoadAssetAtPath<StageData>(
                $"Assets/ScriptableObject/Stage/LevelDesign/StageData_Level{stageNumber:00}_{suffix}.asset");
            Assert.That(stage, Is.Not.Null);
            return stage;
        }

        private static void AssertGridRange(GridCellData[] grid, int expectedMin, int expectedMax)
        {
            int min = 0;
            int max = 0;
            for (int i = 0; i < grid.Length; i++)
            {
                GridCellData cell = grid[i];
                if (cell == null || cell.SpawnProbability <= 0f)
                {
                    continue;
                }

                if (cell.IsFixed)
                {
                    min += cell.FixedCount;
                    max += cell.FixedCount;
                }
                else
                {
                    min += 1;
                    max += 1 + cell.ExtraCountRange;
                }
            }

            Assert.That(min, Is.GreaterThanOrEqualTo(expectedMin));
            Assert.That(max, Is.LessThanOrEqualTo(expectedMax));
        }
    }
}
#endif
