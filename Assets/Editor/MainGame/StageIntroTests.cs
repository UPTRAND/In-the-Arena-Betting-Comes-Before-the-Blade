#if UNITY_EDITOR
using InTheArena.UI;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

namespace InTheArena.MainGame.Editor
{
    public sealed class StageIntroTests
    {
        private const string BettingPrefabPath = "Assets/Prefabs/UI/Panel/UI_BettingPhase.prefab";

        [TestCase(
            "Assets/ScriptableObject/Stage/LevelDesign/StageData_Level01_RoyalKnights.asset",
            "스테이지 1",
            "라운드 횟수 3",
            "목표 콜 1500")]
        [TestCase(
            "Assets/ScriptableObject/Stage/LevelDesign/StageData_Level15_CentralOutskirtsVillage.asset",
            "스테이지 15",
            "라운드 횟수 7",
            "목표 콜 9000")]
        public void StageCopyUsesStageDataValues(
            string assetPath,
            string expectedStage,
            string expectedRounds,
            string expectedTarget)
        {
            StageData stageData = AssetDatabase.LoadAssetAtPath<StageData>(assetPath);
            Assert.That(stageData, Is.Not.Null);

            UI_StageIntro.GetStageCopy(
                stageData,
                out string stage,
                out string rounds,
                out string target);

            Assert.That(stage, Is.EqualTo(expectedStage));
            Assert.That(rounds, Is.EqualTo(expectedRounds));
            Assert.That(target, Is.EqualTo(expectedTarget));
        }

        [Test]
        public void BettingPrefabContainsPrimedStageIntroHierarchy()
        {
            GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(BettingPrefabPath);
            Assert.That(prefab, Is.Not.Null);
            Assert.That(prefab.GetComponentInChildren<UI_StageIntro>(true), Is.Not.Null);

            Transform visual = FindChild(prefab.transform, "StageIntroVisual");
            Transform blocker = FindChild(prefab.transform, "StageIntroInputBlocker");
            Transform content = FindChild(prefab.transform, "BettingContent");
            Assert.That(visual, Is.Not.Null);
            Assert.That(blocker, Is.Not.Null);
            Assert.That(content, Is.Not.Null);
            Assert.That(visual.gameObject.activeSelf, Is.False);
            Assert.That(blocker.gameObject.activeSelf, Is.False);

            CanvasGroup contentGroup = content.GetComponent<CanvasGroup>();
            Assert.That(contentGroup, Is.Not.Null);
            Assert.That(contentGroup.alpha, Is.Zero);
            Assert.That(contentGroup.interactable, Is.False);
            Assert.That(contentGroup.blocksRaycasts, Is.False);
        }

        [Test]
        public void IntroUsesThreeIndividualSpriteSlices()
        {
            GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(BettingPrefabPath);
            Image red = FindChild(prefab.transform, "RedSword")?.GetComponent<Image>();
            Image blue = FindChild(prefab.transform, "BlueSword")?.GetComponent<Image>();
            Image shield = FindChild(prefab.transform, "Shield")?.GetComponent<Image>();

            Assert.That(red?.sprite?.name, Is.EqualTo("BattleIntro_0"));
            Assert.That(blue?.sprite?.name, Is.EqualTo("BattleIntro_1"));
            Assert.That(shield?.sprite?.name, Is.EqualTo("BattleIntro_2"));
            Assert.That(FindChild(prefab.transform, "Composite"), Is.Null);
        }

        private static Transform FindChild(Transform root, string objectName)
        {
            if (root.name == objectName) return root;
            for (int i = 0; i < root.childCount; i++)
            {
                Transform found = FindChild(root.GetChild(i), objectName);
                if (found != null) return found;
            }
            return null;
        }
    }
}
#endif
