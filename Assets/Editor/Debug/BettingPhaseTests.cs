#if UNITY_EDITOR
using System.Collections;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

public class BettingPhaseTests
{
    private GameObject _go;
    private BettingPhase _bettingPhase;

    [SetUp]
    public void Setup()
    {
        _go = new GameObject("BettingPhaseTest");
        _bettingPhase = _go.AddComponent<BettingPhase>();
    }

    [UnityTest]
    public IEnumerator BettingPhase_Rejects_50_50_Ratio_And_Applies_Extra_Bet()
    {
        var context = new RoundContext();
        _bettingPhase.InitializePhase(context);

        // 1. 50:50 배팅 비율 시도 (거부되어야 함)
        bool set50Result = _bettingPhase.SetBettingRatio(50);
        Assert.IsFalse(set50Result);
        Assert.AreNotEqual(50, context.TeamABetRatio);

        // 2. 정상 배팅 비율 설정 (70:30)
        bool set70Result = _bettingPhase.SetBettingRatio(70);
        Assert.IsTrue(set70Result);
        Assert.AreEqual(70, context.TeamABetRatio);
        Assert.AreEqual(30, context.TeamBBetRatio);

        // 3. 추가 배팅권 사용 테스트 (+50콜)
        _bettingPhase.UseExtraBetItem();
        Assert.AreEqual(50, context.ExtraBetCall);

        yield return null;
    }

    [TearDown]
    public void TearDown()
    {
        if (_go != null)
        {
            Object.DestroyImmediate(_go);
        }
    }
}
#endif