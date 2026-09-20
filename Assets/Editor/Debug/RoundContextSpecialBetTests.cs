#if UNITY_EDITOR
using System.Collections.Generic;
using System.Reflection;
using InTheArena.UI;
using TMPro;
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

    /// <summary>HUD의 실제 직렬화 참조와 확정 배팅 표시를 검사합니다.</summary>
    [Test]
    public void BattleHistory_BindsEveryCategoryAndDisplaysTicket()
    {
        GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/UI/HUD/UI_BattlePhaseHUD.prefab");
        GameObject instance = Object.Instantiate(prefab);
        try
        {
            UI_BattlePhaseHUD hud = instance.GetComponent<UI_BattlePhaseHUD>();
            var serialized = new SerializedObject(hud);
            string[] categories = { "WinningTeam", "GameEndTime", "OddEven", "FirstAnnihilated", "SurvivingSlots" };
            foreach (string category in categories)
            {
                Assert.That(serialized.FindProperty("m_" + category + "HistoryRoot").objectReferenceValue, Is.Not.Null, category);
                Assert.That(serialized.FindProperty("m_" + category + "HistoryText").objectReferenceValue, Is.Not.Null, category);
            }

            var context = new RoundContext();
            context.InitializeStage(m_StageData);
            context.SetRoundData(m_StageData, 6);
            context.BetTicket = new RoundBetTicket();
            context.BetTicket.SetFaction(FactionPrediction.Blue);
            typeof(UI_BattlePhaseHUD).GetField("m_RoundContext", BindingFlags.Instance | BindingFlags.NonPublic).SetValue(hud, context);
            typeof(UI_BattlePhaseHUD).GetMethod("RefreshBetHistory", BindingFlags.Instance | BindingFlags.NonPublic).Invoke(hud, null);
            TMP_Text value = (TMP_Text)serialized.FindProperty("m_WinningTeamHistoryText").objectReferenceValue;
            Assert.That(value.text, Is.EqualTo("Blue"));
        }
        finally
        {
            Object.DestroyImmediate(instance);
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
