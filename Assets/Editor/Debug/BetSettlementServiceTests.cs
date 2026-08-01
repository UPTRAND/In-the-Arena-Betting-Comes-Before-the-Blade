#if UNITY_EDITOR
using System.Collections.Generic;
using System.Reflection;
using InTheArena.MainGame;
using NUnit.Framework;
using UnityEngine;

public sealed class BetSettlementServiceTests
{
    private StageData m_StageData;

    [SetUp]
    public void SetUp()
    {
        m_StageData = ScriptableObject.CreateInstance<StageData>();
        SetField("m_StageName", "Test");
        SetField("m_StageNum", 1);
        SetField("m_InitialCall", 500);
        SetField("m_TargetCall", 1200);
        SetField("m_EnableFactionBet", true);
        SetField("m_SpecialBetTypes", new List<SpecialBetType>
        {
            SpecialBetType.OddEven,
            SpecialBetType.FirstEliminatedSlot
        });
    }

    [TearDown]
    public void TearDown()
    {
        Object.DestroyImmediate(m_StageData);
    }

    [Test]
    public void ThreeCorrectCategories_PayWagerTimesEight_ExactlyOnce()
    {
        var session = new StageSession();
        session.Initialize(m_StageData);

        var ticket = new RoundBetTicket();
        ticket.SetWager(100);
        ticket.SetFaction(FactionPrediction.Red);
        ticket.SetOddEven(OddEvenPrediction.Odd);
        ticket.SetFirstEliminatedSlot(3);

        var context = new RoundContext();
        context.InitializeStage(m_StageData);
        Assert.That(session.TryPlaceBet(ticket, context, out string error), Is.True, error);
        Assert.That(session.CurrentCall, Is.EqualTo(400));

        var result = new CombatResultSnapshot(
            Team.Red, 12f, 3, 0,
            new[] { 1, 3 }, new int[0], 3);
        BetSettlement settlement = BetSettlementService.Settle(ticket, result);
        session.ApplySettlement(settlement);

        Assert.That(settlement.IsWin, Is.True);
        Assert.That(settlement.Multiplier, Is.EqualTo(8));
        Assert.That(settlement.PayoutCall, Is.EqualTo(800));
        Assert.That(session.CurrentCall, Is.EqualTo(1200));
        Assert.Throws<System.InvalidOperationException>(() => BetSettlementService.Settle(ticket, result));
    }

    [Test]
    public void OneWrongCategory_LosesEntireWager()
    {
        var session = new StageSession();
        session.Initialize(m_StageData);

        var ticket = new RoundBetTicket();
        ticket.SetWager(200);
        ticket.SetFaction(FactionPrediction.Blue);
        ticket.SetOddEven(OddEvenPrediction.Even);

        var context = new RoundContext();
        context.InitializeStage(m_StageData);
        Assert.That(session.TryPlaceBet(ticket, context, out _), Is.True);
        var result = new CombatResultSnapshot(
            Team.Blue, 4f, 0, 3,
            new int[0], new[] { 1, 2 }, -1);
        BetSettlement settlement = BetSettlementService.Settle(ticket, result);
        session.ApplySettlement(settlement);

        Assert.That(settlement.IsWin, Is.False);
        Assert.That(settlement.PayoutCall, Is.Zero);
        Assert.That(session.CurrentCall, Is.EqualTo(300));
        Assert.That(settlement.FailedCategories, Does.Contain("OddEven"));
    }

    [TestCase(0f, RemainingTimePrediction.Seconds0To5)]
    [TestCase(4.999f, RemainingTimePrediction.Seconds0To5)]
    [TestCase(5f, RemainingTimePrediction.Seconds5To10)]
    [TestCase(10f, RemainingTimePrediction.Seconds10To15)]
    [TestCase(15f, RemainingTimePrediction.Seconds15To20)]
    [TestCase(20f, RemainingTimePrediction.Seconds20OrMore)]
    [TestCase(30f, RemainingTimePrediction.Seconds20OrMore)]
    public void RemainingTime_UsesDefinedBoundaries(float seconds, RemainingTimePrediction expected)
    {
        Assert.That(BetSettlementService.ClassifyRemainingTime(seconds), Is.EqualTo(expected));
    }

    [Test]
    public void SurvivingSlots_RequireExactSetForSelectedFaction()
    {
        SetField("m_SpecialBetTypes", new List<SpecialBetType> { SpecialBetType.SurvivingSlots });

        var session = new StageSession();
        session.Initialize(m_StageData);
        var ticket = new RoundBetTicket();
        ticket.SetWager(50);
        ticket.SetFaction(FactionPrediction.Blue);
        ticket.SetSurvivingSlots(Team.Blue, new HashSet<int> { 2, 5 });

        var context = new RoundContext();
        context.InitializeStage(m_StageData);
        Assert.That(session.TryPlaceBet(ticket, context, out string error), Is.True, error);
        var result = new CombatResultSnapshot(
            Team.Blue, 8f, 0, 2,
            new int[0], new[] { 2, 5 }, 1);

        Assert.That(BetSettlementService.Settle(ticket, result).IsWin, Is.True);
    }

    private void SetField(string name, object value)
    {
        typeof(StageData).GetField(name, BindingFlags.Instance | BindingFlags.NonPublic)
            ?.SetValue(m_StageData, value);
    }
}
#endif
