#if UNITY_EDITOR
using System.Collections.Generic;
using InTheArena.MainGame;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

public sealed class RoundContextSpecialBetTests
{
    private StageData m_StageData;

    [SetUp]
    public void SetUp()
    {
        m_StageData = ScriptableObject.CreateInstance<StageData>();
    }

    [TearDown]
    public void TearDown()
    {
        Object.DestroyImmediate(m_StageData);
    }

    [Test]
    public void RoundsRevealUniqueSpecialBetsCumulatively()
    {
        var context = new RoundContext();
        context.InitializeStage(m_StageData);
        var previous = new HashSet<SpecialBetType>();
        int[] expectedCounts = { 0, 0, 1, 1, 2, 2, 3, 3 };

        for (int roundIndex = 0; roundIndex < expectedCounts.Length; roundIndex++)
        {
            context.SetRoundData(m_StageData, roundIndex);
            var current = new HashSet<SpecialBetType>(context.ActiveSpecialBets);

            Assert.That(context.ActiveSpecialBets.Count, Is.EqualTo(expectedCounts[roundIndex]));
            Assert.That(current.Count, Is.EqualTo(context.ActiveSpecialBets.Count), "Special bets must be unique");
            Assert.That(current.IsSupersetOf(previous), Is.True, "Revealed bets must remain active");
            previous = current;
        }
    }

    [Test]
    public void RerollKeepsCountAndChangesActiveSet()
    {
        var context = new RoundContext();
        context.InitializeStage(m_StageData);
        context.SetRoundData(m_StageData, 6);
        var previousActive = new HashSet<SpecialBetType>(context.ActiveSpecialBets);

        Assert.That(context.RerollSpecialBets(), Is.True);
        Assert.That(context.ActiveSpecialBets.Count, Is.EqualTo(3));
        Assert.That(new HashSet<SpecialBetType>(context.ActiveSpecialBets).SetEquals(previousActive), Is.False);
        Assert.That(new HashSet<SpecialBetType>(context.SpecialBetOrder).Count, Is.EqualTo(4));
    }

    [Test]
    public void RestoringOrderRestoresVisiblePrefix()
    {
        var context = new RoundContext();
        context.InitializeStage(m_StageData);
        context.SetRoundData(m_StageData, 4);
        var previousOrder = new List<SpecialBetType>(context.SpecialBetOrder);
        var previousActive = new List<SpecialBetType>(context.ActiveSpecialBets);

        context.RerollSpecialBets();
        context.RestoreSpecialBetOrder(previousOrder);

        CollectionAssert.AreEqual(previousOrder, context.SpecialBetOrder);
        CollectionAssert.AreEqual(previousActive, context.ActiveSpecialBets);
    }

    [TestCase("Assets/Prefabs/UI/Panel/UI_BettingPhase.prefab", "WinningTeam_Group", "GameEndTime_Group", "OddEven_Group", "FirstAnnihilated_Group", "SurvivingSlots_Group")]
    [TestCase("Assets/Prefabs/UI/HUD/UI_BattlePhaseHUD.prefab", "WinningTeam_History", "GameEndTime_History", "OddEven_History", "FirstAnnihilated_History", "SurvivingSlots_History")]
    public void BettingGroupContainsAllPossibleEntries(string path, params string[] entryNames)
    {
        GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
        Assert.That(prefab, Is.Not.Null);
        Transform bettingGroup = FindByName(prefab.transform, "BettingGroup");
        Assert.That(bettingGroup, Is.Not.Null);
        Assert.That(bettingGroup.GetComponent<GridLayoutGroup>(), Is.Not.Null);

        foreach (string entryName in entryNames)
        {
            Transform entry = FindByName(bettingGroup, entryName);
            Assert.That(entry, Is.Not.Null, $"{entryName} is missing from {path}");
            Assert.That(entry.parent, Is.SameAs(bettingGroup));
        }
    }

    private static Transform FindByName(Transform root, string targetName)
    {
        foreach (Transform child in root.GetComponentsInChildren<Transform>(true))
        {
            if (child.name == targetName) return child;
        }
        return null;
    }
}
#endif
