#if UNITY_EDITOR && UNITY_6000_0_OR_NEWER
using System.Collections.Generic;
using InTheArena.MainGame;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

/// <summary>원격 배팅 규칙과 로컬 퀘스트용 항목별 판정의 통합을 검증합니다.</summary>
public sealed class BettingRevisionTests
{
    private StageData m_Stage;

    private RoundContext m_Context;

    /// <summary>실제 저장 파일을 사용하지 않는 독립된 배팅 컨텍스트를 준비합니다.</summary>
    [SetUp]
    public void SetUp()
    {
        m_Stage = ScriptableObject.CreateInstance<StageData>();
        m_Context = new RoundContext();
        m_Context.InitializeStage(m_Stage);
        m_Context.SetRoundData(m_Stage, 6);
        m_Context.RestoreSpecialBetOrder(new[]
        {
            SpecialBetType.RemainingTime,
            SpecialBetType.OddEven,
            SpecialBetType.FirstEliminatedColumn,
            SpecialBetType.SurvivingRow
        });
    }

    /// <summary>테스트에서 생성한 스테이지 에셋을 제거합니다.</summary>
    [TearDown]
    public void TearDown()
    {
        Object.DestroyImmediate(m_Stage);
    }

    /// <summary>무승부의 3배 정산과 퀘스트에 전달할 메인 판정이 일치해야 합니다.</summary>
    [TestCase(Team.None, 300, true)]
    [TestCase(Team.Red, 0, false)]
    public void Draw_PaysThreeTimesAndPreservesOutcome(Team winner, int payout, bool matched)
    {
        RoundBetTicket ticket = CreateTicket(FactionPrediction.Draw);
        Place(ticket);
        CombatResultSnapshot result = new CombatResultSnapshot(winner, 0f, 1, 1, null, null);

        BetSettlement settlement = BetSettlementService.Settle(ticket, result);

        Assert.That(settlement.Multiplier, Is.EqualTo(3));
        Assert.That(settlement.PayoutCall, Is.EqualTo(payout));
        Assert.That(settlement.Outcomes.Count, Is.EqualTo(5));
        Assert.That(GetOutcome(settlement, BetCategory.Faction).IsSelected, Is.True);
        Assert.That(GetOutcome(settlement, BetCategory.Faction).IsMatched, Is.EqualTo(matched));
        Assert.That(GetOutcome(settlement, BetCategory.OddEven).IsSelected, Is.False);
    }

    /// <summary>전체 배팅이 실패해도 팀 홀짝의 개별 적중 결과는 퀘스트에 남겨야 합니다.</summary>
    [Test]
    public void LosingTicket_PreservesTeamBasedSuccessfulSubOutcome()
    {
        RoundBetTicket ticket = CreateTicket(FactionPrediction.Red);
        ticket.SetOddEven(OddEvenPrediction.Odd);
        Place(ticket);
        CombatResultSnapshot result = new CombatResultSnapshot(Team.Blue, 12f, 3, 1, null, null);

        BetSettlement settlement = BetSettlementService.Settle(ticket, result);

        Assert.That(settlement.IsWin, Is.False);
        Assert.That(settlement.PayoutCall, Is.Zero);
        Assert.That(settlement.FailedCategories, Is.EquivalentTo(new[] { "Faction" }));
        Assert.That(GetOutcome(settlement, BetCategory.OddEven).IsMatched, Is.True);
    }

    /// <summary>상대 열이 먼저 전멸해도 배팅 팀의 첫 전멸은 개별 적중으로 보존합니다.</summary>
    [Test]
    public void FirstColumnOutcome_UsesIndependentTeamResult()
    {
        RoundBetTicket ticket = CreateTicket(FactionPrediction.Red);
        ticket.SetFirstEliminatedColumn(FirstEliminatedColumnPrediction.RedBack);
        Place(ticket);
        CombatResultSnapshot result = new CombatResultSnapshot(
            Team.Red, 12f, 3, 0, null,
            FirstEliminatedColumnPrediction.BlueFront,
            FirstEliminatedColumnPrediction.RedBack,
            FirstEliminatedColumnPrediction.BlueFront);

        BetSettlement settlement = BetSettlementService.Settle(ticket, result);

        Assert.That(settlement.IsWin, Is.True);
        Assert.That(GetOutcome(settlement, BetCategory.FirstEliminatedColumn).IsMatched, Is.True);
    }

    /// <summary>팀 배팅의 네 항목은 16배이며, 한 항목 실패 시에도 다른 적중은 기록합니다.</summary>
    [TestCase(OddEvenPrediction.Odd, 1600, true)]
    [TestCase(OddEvenPrediction.Even, 0, false)]
    public void FourCategories_KeepSixteenTimesAndIndividualOutcomes(
        OddEvenPrediction oddEven, int payout, bool matched)
    {
        RoundBetTicket ticket = CreateTicket(FactionPrediction.Red);
        ticket.SetRemainingTime(RemainingTimePrediction.Seconds10To15);
        ticket.SetOddEven(oddEven);
        ticket.SetFirstEliminatedColumn(FirstEliminatedColumnPrediction.RedFront);
        Place(ticket);
        CombatResultSnapshot result = new CombatResultSnapshot(
            Team.Red, 12f, 3, 0, null, FirstEliminatedColumnPrediction.RedFront);

        BetSettlement settlement = BetSettlementService.Settle(ticket, result);

        Assert.That(settlement.Multiplier, Is.EqualTo(16));
        Assert.That(settlement.PayoutCall, Is.EqualTo(payout));
        Assert.That(GetOutcome(settlement, BetCategory.OddEven).IsMatched, Is.EqualTo(matched));
        Assert.That(GetOutcome(settlement, BetCategory.RemainingTime).IsMatched, Is.True);
        Assert.That(GetOutcome(settlement, BetCategory.SurvivingRow).IsSelected, Is.False);
    }

    /// <summary>확장 기능은 제거되어 다섯 항목 티켓이 모델에서 거부되어야 합니다.</summary>
    [Test]
    public void FiveCategories_AreRejectedWithoutSpendingCall()
    {
        RoundBetTicket ticket = CreateTicket(FactionPrediction.Red);
        ticket.SetRemainingTime(RemainingTimePrediction.Seconds10To15);
        ticket.SetOddEven(OddEvenPrediction.Odd);
        ticket.SetFirstEliminatedColumn(FirstEliminatedColumnPrediction.RedFront);
        ticket.SetSurvivingRow(SurvivingRowPrediction.RedRow1);

        Assert.That(m_Context.StageSession.TryPlaceBet(ticket, m_Context, out _), Is.False);
        Assert.That(ticket.Multiplier, Is.Zero);
        Assert.That(m_Context.CurrentCall, Is.EqualTo(500));
    }

    /// <summary>리롤은 서브 개수를 늘리지 않고 같은 개수의 다른 종류를 제시합니다.</summary>
    [TestCase(2, 1)]
    [TestCase(4, 2)]
    [TestCase(6, 3)]
    public void Reroll_KeepsBaseSubCount(int roundIndex, int expectedCount)
    {
        m_Context.SetRoundData(m_Stage, roundIndex);
        List<SpecialBetType> before = new List<SpecialBetType>(m_Context.ActiveSpecialBets);

        Assert.That(m_Context.RerollSpecialBets(), Is.True);
        Assert.That(m_Context.ActiveSpecialBets.Count, Is.EqualTo(expectedCount));
        Assert.That(m_Context.ActiveSpecialBets, Is.Not.EquivalentTo(before));
    }

    /// <summary>무승부에도 추가 배팅권과 보험을 적용하고 항목별 결과를 보존합니다.</summary>
    [TestCase(Team.None, 1800)]
    [TestCase(Team.Blue, 100)]
    public void Draw_PreservesAdditionalBetAndInsurance(Team winner, int payout)
    {
        RoundBetTicket ticket = CreateTicket(FactionPrediction.Draw);
        ticket.SetItemUsages(true, true);
        Place(ticket);

        BetSettlement settlement = BetSettlementService.Settle(
            ticket, new CombatResultSnapshot(winner, 0f, 1, 1, null, null));

        Assert.That(settlement.PayoutCall, Is.EqualTo(payout));
        Assert.That(GetOutcome(settlement, BetCategory.Faction).IsSelected, Is.True);
    }

    /// <summary>기존 리롤권의 저장 번호·가격·GUID와 프리팹 참조를 유지합니다.</summary>
    [Test]
    public void Reroll_AssetAndPrefabReferencesRemainCompatible()
    {
        const string path = "Assets/ScriptableObject/Item/ItemData_RerollTicket.asset";
        ItemData item = AssetDatabase.LoadAssetAtPath<ItemData>(path);

        Assert.That(item.ItemType, Is.EqualTo(ItemType.RerollTicket));
        Assert.That((int)item.ItemType, Is.EqualTo(3));
        Assert.That(item.PriceGold, Is.EqualTo(300));
        Assert.That(item.Icon, Is.Not.Null);
        Assert.That(AssetDatabase.AssetPathToGUID(path), Is.EqualTo("1f9e233786c48fc459491e7178655e66"));

        GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/UI/Panel/UI_BettingPhase.prefab");
        SerializedObject serialized = new SerializedObject(prefab.GetComponent<InTheArena.UI.UI_BettingPhase>());
        Assert.That(serialized.FindProperty("m_RerollData").objectReferenceValue, Is.SameAs(item));
        Assert.That(serialized.FindProperty("m_RerollButton").objectReferenceValue, Is.Not.Null);
    }

    /// <summary>미선택 항목을 포함한 정산 결과에서 지정한 카테고리의 판정을 찾습니다.</summary>
    private static BetOutcome GetOutcome(BetSettlement settlement, BetCategory category)
    {
        foreach (BetOutcome outcome in settlement.Outcomes)
        {
            if (outcome.Category == category)
            {
                return outcome;
            }
        }

        Assert.Fail("Missing outcome: " + category);
        return default;
    }

    /// <summary>메인 예측과 100 Call 원금을 가진 초안 티켓을 생성합니다.</summary>
    private static RoundBetTicket CreateTicket(FactionPrediction faction)
    {
        RoundBetTicket ticket = new RoundBetTicket();
        ticket.SetFaction(faction);
        ticket.SetWager(100);
        return ticket;
    }

    /// <summary>모델 검증과 실제 원금 차감을 거쳐 티켓을 확정합니다.</summary>
    private void Place(RoundBetTicket ticket)
    {
        Assert.That(m_Context.StageSession.TryPlaceBet(ticket, m_Context, out string error), Is.True, error);
        m_Context.BetTicket = ticket;
    }
}
#endif
