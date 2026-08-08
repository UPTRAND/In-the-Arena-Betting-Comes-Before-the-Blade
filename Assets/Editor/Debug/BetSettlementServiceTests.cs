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
            SpecialBetType.FirstEliminatedColumn
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
        ticket.SetFirstEliminatedColumn(FirstEliminatedColumnPrediction.BlueFront);

        var context = new RoundContext();
        context.InitializeStage(m_StageData);
        context.RestoreSpecialBetOrder(new[]
        {
            SpecialBetType.OddEven,
            SpecialBetType.FirstEliminatedColumn,
            SpecialBetType.RemainingTime,
            SpecialBetType.SurvivingRow
        });
        context.SetRoundData(m_StageData, 4);
        Assert.That(session.TryPlaceBet(ticket, context, out string error), Is.True, error);
        Assert.That(session.CurrentCall, Is.EqualTo(400));

        var result = new CombatResultSnapshot(
            Team.Red, 12f, 3, 0,
            new[] { SurvivingRowPrediction.RedRow1, SurvivingRowPrediction.RedRow2 },
            FirstEliminatedColumnPrediction.BlueFront);
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
        context.RestoreSpecialBetOrder(new[]
        {
            SpecialBetType.OddEven,
            SpecialBetType.RemainingTime,
            SpecialBetType.SurvivingRow,
            SpecialBetType.FirstEliminatedColumn
        });
        context.SetRoundData(m_StageData, 2);
        Assert.That(session.TryPlaceBet(ticket, context, out _), Is.True);
        var result = new CombatResultSnapshot(
            Team.Blue, 4f, 0, 3,
            new[] { SurvivingRowPrediction.BlueRow1 }, null);
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
    public void SurvivingRow_SucceedsWhenSelectedRowHasAnySurvivor()
    {
        SetField("m_SpecialBetTypes", new List<SpecialBetType> { SpecialBetType.SurvivingRow });

        var session = new StageSession();
        session.Initialize(m_StageData);
        var ticket = new RoundBetTicket();
        ticket.SetWager(50);
        ticket.SetSurvivingRow(SurvivingRowPrediction.BlueRow2);

        var context = new RoundContext();
        context.InitializeStage(m_StageData);
        context.RestoreSpecialBetOrder(new[]
        {
            SpecialBetType.SurvivingRow,
            SpecialBetType.RemainingTime,
            SpecialBetType.OddEven,
            SpecialBetType.FirstEliminatedColumn
        });
        context.SetRoundData(m_StageData, 2);
        Assert.That(session.TryPlaceBet(ticket, context, out string error), Is.True, error);
        var result = new CombatResultSnapshot(
            Team.Blue, 8f, 0, 2,
            new[] { SurvivingRowPrediction.BlueRow2, SurvivingRowPrediction.BlueRow3 },
            FirstEliminatedColumnPrediction.RedFront);

        Assert.That(BetSettlementService.Settle(ticket, result).IsWin, Is.True);
    }

    [Test]
    public void SurvivingRow_FailsWhenOnlyOtherRowsSurvive()
    {
        var session = new StageSession();
        session.Initialize(m_StageData);
        var ticket = new RoundBetTicket();
        ticket.SetWager(50);
        ticket.SetSurvivingRow(SurvivingRowPrediction.BlueRow2);

        var context = new RoundContext();
        context.InitializeStage(m_StageData);
        context.RestoreSpecialBetOrder(new[]
        {
            SpecialBetType.SurvivingRow,
            SpecialBetType.RemainingTime,
            SpecialBetType.OddEven,
            SpecialBetType.FirstEliminatedColumn
        });
        context.SetRoundData(m_StageData, 2);
        Assert.That(session.TryPlaceBet(ticket, context, out string error), Is.True, error);

        var result = new CombatResultSnapshot(
            Team.Blue, 8f, 0, 1,
            new[] { SurvivingRowPrediction.BlueRow3 }, null);
        BetSettlement settlement = BetSettlementService.Settle(ticket, result);

        Assert.That(settlement.IsWin, Is.False);
        Assert.That(settlement.FailedCategories, Does.Contain("SurvivingRow"));
    }

    [TestCase(Team.Red, 0, FirstEliminatedColumnPrediction.RedBack)]
    [TestCase(Team.Red, 1, FirstEliminatedColumnPrediction.RedFront)]
    [TestCase(Team.Blue, 0, FirstEliminatedColumnPrediction.BlueFront)]
    [TestCase(Team.Blue, 1, FirstEliminatedColumnPrediction.BlueBack)]
    public void GridColumnMapping_MatchesTeamFacing(
        Team team,
        int cellIndex,
        FirstEliminatedColumnPrediction expected)
    {
        MethodInfo method = typeof(CombatPhase).GetMethod(
            "GetColumnPrediction",
            BindingFlags.Static | BindingFlags.NonPublic);
        Assert.That(method, Is.Not.Null);
        Assert.That(method.Invoke(null, new object[] { team, cellIndex }), Is.EqualTo(expected));
    }

    [TestCase(Team.Red, 0, SurvivingRowPrediction.RedRow1)]
    [TestCase(Team.Red, 2, SurvivingRowPrediction.RedRow2)]
    [TestCase(Team.Red, 4, SurvivingRowPrediction.RedRow3)]
    [TestCase(Team.Blue, 0, SurvivingRowPrediction.BlueRow1)]
    [TestCase(Team.Blue, 2, SurvivingRowPrediction.BlueRow2)]
    [TestCase(Team.Blue, 4, SurvivingRowPrediction.BlueRow3)]
    public void GridRowMapping_MatchesSlotPairs(
        Team team,
        int cellIndex,
        SurvivingRowPrediction expected)
    {
        MethodInfo method = typeof(CombatPhase).GetMethod(
            "GetRowPrediction",
            BindingFlags.Static | BindingFlags.NonPublic);
        Assert.That(method, Is.Not.Null);
        Assert.That(method.Invoke(null, new object[] { team, cellIndex }), Is.EqualTo(expected));
    }

    [Test]
    public void FirstEliminatedColumn_FailsWhenNoPopulatedColumnWasEliminated()
    {
        var session = new StageSession();
        session.Initialize(m_StageData);
        var ticket = new RoundBetTicket();
        ticket.SetWager(50);
        ticket.SetFirstEliminatedColumn(FirstEliminatedColumnPrediction.RedFront);

        var context = new RoundContext();
        context.InitializeStage(m_StageData);
        context.RestoreSpecialBetOrder(new[]
        {
            SpecialBetType.FirstEliminatedColumn,
            SpecialBetType.RemainingTime,
            SpecialBetType.OddEven,
            SpecialBetType.SurvivingRow
        });
        context.SetRoundData(m_StageData, 2);
        Assert.That(session.TryPlaceBet(ticket, context, out string error), Is.True, error);

        var result = new CombatResultSnapshot(
            Team.None, 0f, 1, 1,
            new[] { SurvivingRowPrediction.RedRow1, SurvivingRowPrediction.BlueRow1 }, null);
        BetSettlement settlement = BetSettlementService.Settle(ticket, result);

        Assert.That(settlement.IsWin, Is.False);
        Assert.That(settlement.FailedCategories, Does.Contain("FirstEliminatedColumn"));
    }

    [Test]
    public void FourCorrectCategories_PayWagerTimesSixteen()
    {
        var session = new StageSession();
        session.Initialize(m_StageData);
        var ticket = new RoundBetTicket();
        ticket.SetWager(50);
        ticket.SetFaction(FactionPrediction.Red);
        ticket.SetRemainingTime(RemainingTimePrediction.Seconds10To15);
        ticket.SetOddEven(OddEvenPrediction.Odd);
        ticket.SetFirstEliminatedColumn(FirstEliminatedColumnPrediction.BlueFront);

        var context = new RoundContext();
        context.InitializeStage(m_StageData);
        context.RestoreSpecialBetOrder(new[]
        {
            SpecialBetType.RemainingTime,
            SpecialBetType.OddEven,
            SpecialBetType.FirstEliminatedColumn,
            SpecialBetType.SurvivingRow
        });
        context.SetRoundData(m_StageData, 6);

        Assert.That(session.TryPlaceBet(ticket, context, out string error), Is.True, error);
        var result = new CombatResultSnapshot(
            Team.Red, 12f, 3, 0,
            new[] { SurvivingRowPrediction.RedRow1, SurvivingRowPrediction.RedRow2 },
            FirstEliminatedColumnPrediction.BlueFront);
        BetSettlement settlement = BetSettlementService.Settle(ticket, result);

        Assert.That(settlement.IsWin, Is.True);
        Assert.That(settlement.Multiplier, Is.EqualTo(16));
        Assert.That(settlement.PayoutCall, Is.EqualTo(800));
    }

    private void SetField(string name, object value)
    {
        typeof(StageData).GetField(name, BindingFlags.Instance | BindingFlags.NonPublic)
            ?.SetValue(m_StageData, value);
    }
}
#endif
