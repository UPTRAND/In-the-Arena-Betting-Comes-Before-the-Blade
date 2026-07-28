#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.Reflection;
using InTheArena.MainGame;
using UnityEditor;
using UnityEngine;

namespace InTheArena.MainGame.Editor
{
    public static class StageBettingVerification
    {
        [MenuItem("Tools/In The Arena/Verify Stage Betting Core")]
        public static void Run()
        {
            StageData stageData = ScriptableObject.CreateInstance<StageData>();
            try
            {
                ConfigureStage(stageData);
                VerifyTimeBoundaries();
                VerifyWinningParlay(stageData);
                VerifyLosingParlay(stageData);
                Debug.Log("[StageBettingVerification] All core checks passed.");
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(stageData);
            }
        }

        private static void ConfigureStage(StageData stageData)
        {
            SetField(stageData, "m_StageName", "Verification");
            SetField(stageData, "m_StageNum", 1);
            SetField(stageData, "m_InitialCall", 500);
            SetField(stageData, "m_TargetCall", 1200);
            SetField(stageData, "m_EnableFactionBet", true);
            SetField(stageData, "m_SpecialBetTypes", new List<SpecialBetType>
            {
                SpecialBetType.OddEven,
                SpecialBetType.FirstEliminatedSlot
            });
        }

        private static void VerifyTimeBoundaries()
        {
            AssertEqual(RemainingTimePrediction.Seconds0To5, BetSettlementService.ClassifyRemainingTime(4.999f));
            AssertEqual(RemainingTimePrediction.Seconds5To10, BetSettlementService.ClassifyRemainingTime(5f));
            AssertEqual(RemainingTimePrediction.Seconds10To15, BetSettlementService.ClassifyRemainingTime(10f));
            AssertEqual(RemainingTimePrediction.Seconds15To20, BetSettlementService.ClassifyRemainingTime(15f));
            AssertEqual(RemainingTimePrediction.Seconds20OrMore, BetSettlementService.ClassifyRemainingTime(20f));
            AssertEqual(RemainingTimePrediction.Seconds20OrMore, BetSettlementService.ClassifyRemainingTime(30f));
        }

        private static void VerifyWinningParlay(StageData stageData)
        {
            var session = new StageSession();
            session.Initialize(stageData);
            var ticket = new RoundBetTicket();
            ticket.SetWager(100);
            ticket.SetFaction(FactionPrediction.Red);
            ticket.SetOddEven(OddEvenPrediction.Odd);
            ticket.SetFirstEliminatedSlot(3);

            if (!session.TryPlaceBet(ticket, out string error)) throw new InvalidOperationException(error);
            var result = new CombatResultSnapshot(
                Team.Red, 12f, 3, 0,
                new[] { 1, 3 }, Array.Empty<int>(), 3);
            BetSettlement settlement = BetSettlementService.Settle(ticket, result);
            session.ApplySettlement(settlement);

            AssertEqual(true, settlement.IsWin);
            AssertEqual(8, settlement.Multiplier);
            AssertEqual(800, settlement.PayoutCall);
            AssertEqual(1200, session.CurrentCall);
        }

        private static void VerifyLosingParlay(StageData stageData)
        {
            var session = new StageSession();
            session.Initialize(stageData);
            var ticket = new RoundBetTicket();
            ticket.SetWager(200);
            ticket.SetFaction(FactionPrediction.Blue);
            ticket.SetOddEven(OddEvenPrediction.Even);

            if (!session.TryPlaceBet(ticket, out string error)) throw new InvalidOperationException(error);
            var result = new CombatResultSnapshot(
                Team.Blue, 4f, 0, 3,
                Array.Empty<int>(), new[] { 1, 2 }, -1);
            BetSettlement settlement = BetSettlementService.Settle(ticket, result);
            session.ApplySettlement(settlement);

            AssertEqual(false, settlement.IsWin);
            AssertEqual(0, settlement.PayoutCall);
            AssertEqual(300, session.CurrentCall);
        }

        private static void AssertEqual<T>(T expected, T actual)
        {
            if (!Equals(expected, actual))
                throw new InvalidOperationException($"Expected {expected}, actual {actual}");
        }

        private static void SetField(StageData stageData, string name, object value)
        {
            typeof(StageData).GetField(name, BindingFlags.Instance | BindingFlags.NonPublic)
                ?.SetValue(stageData, value);
        }
    }
}
#endif
