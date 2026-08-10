#if UNITY_EDITOR
using System.Collections;
using System.Collections.Generic;
using System.Threading;
using InTheArena.UI;
using InTheArena.MainGame;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UnityEngine.TestTools;

public sealed class ItemPurchaseUseStateTests
{
    private GameObject m_DefaultSaveManagerObject;

    [SetUp]
    public void SetUp()
    {
        typeof(SaveManager).GetProperty("Instance")?.SetValue(null, null);
        m_DefaultSaveManagerObject = new GameObject("ItemPurchaseUseDefaultSaveManager");
        var manager = m_DefaultSaveManagerObject.AddComponent<SaveManager>();
        var state = new InTheArena.Save.PlayerProgressState();
        state.SetGold(100);

        foreach (ItemType itemType in System.Enum.GetValues(typeof(ItemType)))
        {
            if (itemType != ItemType.None)
            {
                state.SetItemCount(itemType, 5);
            }
        }

        manager.InitializeForTests(
            new InTheArena.Tests.Editor.FakeSaveRepository(),
            new InTheArena.Tests.Editor.FakeClock(),
            state);
        typeof(SaveManager).GetProperty("Instance")?.SetValue(null, manager);
    }

    [TearDown]
    public void TearDown()
    {
        typeof(SaveManager).GetProperty("Instance")?.SetValue(null, null);
        if (m_DefaultSaveManagerObject != null)
        {
            Object.DestroyImmediate(m_DefaultSaveManagerObject);
        }
    }

    [Test]
    public void RoundItemUsageState_RejectsNoneAndAllowsEachItemTypeOnce()
    {
        var state = new RoundItemUsageState();

        Assert.That(state.HasUsed(ItemType.None), Is.False);
        Assert.That(state.TryMarkUsed(ItemType.None), Is.False);
        Assert.That(state.TryMarkUsed(ItemType.Meteor), Is.True);
        Assert.That(state.TryMarkUsed(ItemType.Meteor), Is.False);
        Assert.That(state.TryMarkUsed(ItemType.Mercenary), Is.True);
        Assert.That(state.HasUsed(ItemType.Meteor), Is.True);
        Assert.That(state.HasUsed(ItemType.Mercenary), Is.True);
    }

    [Test]
    public void RoundItemUsageState_ResetClearsOnlyRoundUsage()
    {
        var state = new RoundItemUsageState();
        state.TryMarkUsed(ItemType.TimeExtension);

        state.Reset();

        Assert.That(state.HasUsed(ItemType.TimeExtension), Is.False);
    }

    [Test]
    public void RoundContext_SetRoundDataResetsOncePerRoundBoundary()
    {
        var stageData = ScriptableObject.CreateInstance<StageData>();
        try
        {
            SetField(stageData, "m_TotalRounds", 2);
            SetField(stageData, "m_RoundDatas", new System.Collections.Generic.List<RoundData>
            {
                ScriptableObject.CreateInstance<RoundData>(),
                ScriptableObject.CreateInstance<RoundData>()
            });

            var context = new RoundContext();
            context.InitializeStage(stageData);

            context.SetRoundData(stageData, 0);
            Assert.That(context.RoundItemUsage.TryMarkUsed(ItemType.Meteor), Is.True);

            context.SetRoundData(stageData, 0);
            Assert.That(context.RoundItemUsage.HasUsed(ItemType.Meteor), Is.True);

            context.CurrentRound = 2;
            context.SetRoundData(stageData, 1);
            Assert.That(context.RoundItemUsage.HasUsed(ItemType.Meteor), Is.False);

            Assert.That(context.RoundItemUsage.TryMarkUsed(ItemType.TimeExtension), Is.True);
            context.CurrentRound = 99;
            context.SetRoundData(stageData, 1);
            Assert.That(context.RoundItemUsage.HasUsed(ItemType.TimeExtension), Is.True);

            context.Clear();
            Assert.That(context.RoundItemUsage.HasUsed(ItemType.Meteor), Is.False);
        }
        finally
        {
            var roundDatas = stageData != null
                ? GetField<System.Collections.Generic.List<RoundData>>(stageData, "m_RoundDatas")
                : null;
            if (roundDatas != null)
            {
                for (int i = 0; i < roundDatas.Count; i++)
                {
                    if (roundDatas[i] != null)
                    {
                        Object.DestroyImmediate(roundDatas[i]);
                    }
                }
            }

            if (stageData != null)
            {
                Object.DestroyImmediate(stageData);
            }
        }
    }

    [UnityTest]
    public IEnumerator Coordinator_RejectsDuplicateRequestAndCancelIsIdempotent()
    {
        CreateDependencies(out StageData stageData, out RoundContext context, out StagePlayerState playerState, out ItemData itemData);
        SetDefaultItemCount(itemData.ItemType, 0);
        try
        {
            var service = new ItemPurchaseUseService(context, playerState);
            var coordinator = new ItemPurchaseUseCoordinator(context, playerState, service);
            var popup = new PendingConfirmationView();

            coordinator.RequestImmediatePreviewAsync(
                itemData,
                popup,
                new FakeExecutor(true),
                CancellationToken.None);
            yield return null;

            Assert.That(coordinator.State, Is.EqualTo(ItemPurchaseUseState.ConfirmingPurchase));

            coordinator.RequestImmediatePreviewAsync(
                itemData,
                popup,
                new FakeExecutor(true),
                CancellationToken.None);
            yield return null;

            Assert.That(coordinator.State, Is.EqualTo(ItemPurchaseUseState.ConfirmingPurchase));

            coordinator.CancelActiveRequest();
            coordinator.CancelActiveRequest();
            yield return null;

            Assert.That(coordinator.State, Is.EqualTo(ItemPurchaseUseState.Idle));
            coordinator.Dispose();
            coordinator.Dispose();
            Assert.That(coordinator.IsDisposed, Is.True);
        }
        finally
        {
            DestroyDependencies(stageData, itemData);
        }
    }

    [Test]
    public void Service_PreviewDoesNotChangeGoldOrRoundUsage()
    {
        CreateDependencies(out StageData stageData, out RoundContext context, out StagePlayerState playerState, out ItemData itemData);
        try
        {
            int initialGold = playerState.Gold;
            var executor = new FakeExecutor(true);
            var service = new ItemPurchaseUseService(context, playerState);

            Assert.That(service.TryPreview(itemData, executor, initialGold, out string message), Is.True, message);
            Assert.That(executor.CallCount, Is.EqualTo(1));
            Assert.That(playerState.Gold, Is.EqualTo(initialGold));
            Assert.That(context.RoundItemUsage.HasUsed(itemData.ItemType), Is.False);
        }
        finally
        {
            DestroyDependencies(stageData, itemData);
        }
    }

    [Test]
    public void Service_PreviewFailureLeavesGoldAndRoundUsageUnchanged()
    {
        CreateDependencies(out StageData stageData, out RoundContext context, out StagePlayerState playerState, out ItemData itemData);
        try
        {
            int initialGold = playerState.Gold;
            var service = new ItemPurchaseUseService(context, playerState);

            Assert.That(service.TryPreview(itemData, new FakeExecutor(false), initialGold, out _), Is.False);
            Assert.That(playerState.Gold, Is.EqualTo(initialGold));
            Assert.That(context.RoundItemUsage.HasUsed(itemData.ItemType), Is.False);
        }
        finally
        {
            DestroyDependencies(stageData, itemData);
        }
    }

    [UnityTest]
    public IEnumerator Coordinator_TargetRequestRequiresConfirmedPopup()
    {
        CreateDependencies(out StageData stageData, out RoundContext context, out StagePlayerState playerState, out ItemData itemData);
        SetDefaultItemCount(itemData.ItemType, 0);
        try
        {
            var service = new ItemPurchaseUseService(context, playerState);
            var coordinator = new ItemPurchaseUseCoordinator(context, playerState, service);
            var popup = new PendingConfirmationView();

            coordinator.RequestTargetedPreviewAsync(itemData, popup, CancellationToken.None);
            yield return null;
            Assert.That(coordinator.State, Is.EqualTo(ItemPurchaseUseState.ConfirmingPurchase));
            Assert.That(popup.ObservedGold, Is.EqualTo(100));

            popup.Complete(ItemPurchaseDecision.Cancelled);
            yield return null;

            Assert.That(coordinator.State, Is.EqualTo(ItemPurchaseUseState.Idle));
            Assert.That(coordinator.LastResult, Is.EqualTo(ItemPurchaseUseResult.Cancelled));

            using var targetingCancellationSource = new CancellationTokenSource();
            coordinator.RequestTargetedPreviewAsync(itemData, popup, targetingCancellationSource.Token);
            yield return null;
            popup.Complete(ItemPurchaseDecision.Confirmed);
            yield return null;

            Assert.That(coordinator.State, Is.EqualTo(ItemPurchaseUseState.AwaitingTarget));
            Assert.That(coordinator.LastResult, Is.EqualTo(ItemPurchaseUseResult.AwaitingTarget));

            targetingCancellationSource.Cancel();
            yield return null;

            Assert.That(coordinator.State, Is.EqualTo(ItemPurchaseUseState.Idle));
            Assert.That(coordinator.LastResult, Is.EqualTo(ItemPurchaseUseResult.Cancelled));

            coordinator.RequestTargetedPreviewAsync(itemData, popup, CancellationToken.None);
            yield return null;
            popup.Complete(ItemPurchaseDecision.Confirmed);
            yield return null;

            Assert.That(coordinator.State, Is.EqualTo(ItemPurchaseUseState.AwaitingTarget));
            coordinator.CancelActiveRequest();
            Assert.That(coordinator.State, Is.EqualTo(ItemPurchaseUseState.Idle));
            Assert.That(coordinator.LastResult, Is.EqualTo(ItemPurchaseUseResult.Cancelled));

            coordinator.RequestTargetedPreviewAsync(itemData, popup, CancellationToken.None);
            yield return null;
            popup.Complete(ItemPurchaseDecision.Confirmed);
            yield return null;

            long version = coordinator.ActiveRequestVersion;
            Assert.That(coordinator.TryCompleteTargetPreview(
                version,
                new FakeExecutor(true),
                out ItemPurchaseUseResult result,
                out _), Is.True);
            Assert.That(result, Is.EqualTo(ItemPurchaseUseResult.PreviewSucceeded));
            Assert.That(coordinator.State, Is.EqualTo(ItemPurchaseUseState.Idle));
            Assert.That(context.RoundItemUsage.HasUsed(itemData.ItemType), Is.False);
            Assert.That(playerState.Gold, Is.EqualTo(50));
        }
        finally
        {
            DestroyDependencies(stageData, itemData);
        }
    }

    [UnityTest]
    public IEnumerator Coordinator_OwnedItemUsesWithoutOpeningPurchasePopup()
    {
        CreateDependencies(out StageData stageData, out RoundContext context, out StagePlayerState playerState, out ItemData itemData);
        GameObject saveManagerObject = CreateItemSaveManager(itemData.ItemType, 100, 1);
        try
        {
            var coordinator = new ItemPurchaseUseCoordinator(
                context,
                playerState,
                new ItemPurchaseUseService(context, playerState));

            coordinator.RequestImmediateUseAsync(
                itemData,
                null,
                new FakeExecutor(true),
                CancellationToken.None);
            yield return null;

            Assert.That(coordinator.LastResult, Is.EqualTo(ItemPurchaseUseResult.UseSucceeded));
            Assert.That(SaveManager.Instance.GetItemCount(itemData.ItemType), Is.Zero);
            Assert.That(SaveManager.Instance.Gold, Is.EqualTo(100));
        }
        finally
        {
            Object.DestroyImmediate(saveManagerObject);
            DestroyDependencies(stageData, itemData);
        }
    }

    [UnityTest]
    public IEnumerator Coordinator_PurchasesThenUsesWhenItemIsNotOwned()
    {
        CreateDependencies(out StageData stageData, out RoundContext context, out StagePlayerState playerState, out ItemData itemData);
        GameObject saveManagerObject = CreateItemSaveManager(itemData.ItemType, 100, 0);
        try
        {
            var coordinator = new ItemPurchaseUseCoordinator(
                context,
                playerState,
                new ItemPurchaseUseService(context, playerState));
            var popup = new PendingConfirmationView();

            coordinator.RequestImmediateUseAsync(
                itemData,
                popup,
                new FakeExecutor(true),
                CancellationToken.None);
            yield return null;
            Assert.That(popup.ObservedGold, Is.EqualTo(100));

            popup.Complete(ItemPurchaseDecision.Confirmed);
            yield return null;

            Assert.That(coordinator.LastResult, Is.EqualTo(ItemPurchaseUseResult.UseSucceeded));
            Assert.That(SaveManager.Instance.Gold, Is.EqualTo(50));
            Assert.That(SaveManager.Instance.GetItemCount(itemData.ItemType), Is.Zero);
            Assert.That(playerState.Gold, Is.EqualTo(50));
        }
        finally
        {
            Object.DestroyImmediate(saveManagerObject);
            DestroyDependencies(stageData, itemData);
        }
    }

    [UnityTest]
    public IEnumerator Coordinator_PurchasedTargetedItemRemainsAfterCancellation()
    {
        CreateDependencies(out StageData stageData, out RoundContext context, out StagePlayerState playerState, out ItemData itemData);
        GameObject saveManagerObject = CreateItemSaveManager(itemData.ItemType, 100, 0);
        try
        {
            var coordinator = new ItemPurchaseUseCoordinator(
                context,
                playerState,
                new ItemPurchaseUseService(context, playerState));
            var popup = new PendingConfirmationView();

            coordinator.RequestTargetedUseAsync(itemData, popup, CancellationToken.None);
            yield return null;
            popup.Complete(ItemPurchaseDecision.Confirmed);
            yield return null;

            Assert.That(coordinator.State, Is.EqualTo(ItemPurchaseUseState.AwaitingTarget));
            coordinator.CancelActiveRequest();
            Assert.That(SaveManager.Instance.Gold, Is.EqualTo(50));
            Assert.That(SaveManager.Instance.GetItemCount(itemData.ItemType), Is.EqualTo(1));
        }
        finally
        {
            Object.DestroyImmediate(saveManagerObject);
            DestroyDependencies(stageData, itemData);
        }
    }

    [Test]
    public void ItemPurchaseUseService_CommitsGoldAndUsageOnlyAfterSuccessfulEffect()
    {
        CreateDependencies(out StageData stageData, out RoundContext context, out StagePlayerState playerState, out ItemData itemData);
        try
        {
            var service = new ItemPurchaseUseService(context, playerState);
            var executor = new ReversibleExecutor(true);

            Assert.That(service.TryUse(itemData, executor, 100, out _), Is.True, "Item transaction should succeed");
            Assert.That(playerState.Gold, Is.EqualTo(100));
            Assert.That(context.RoundItemUsage.HasUsed(ItemType.Meteor), Is.True);
            Assert.That(executor.CallCount, Is.EqualTo(1));
            Assert.That(executor.RollbackCount, Is.EqualTo(0));

            playerState.Gold = 100;
            context.RoundItemUsage.Reset();
            executor = new ReversibleExecutor(false);

            Assert.That(service.TryUse(itemData, executor, 100, out _), Is.False);
            Assert.That(playerState.Gold, Is.EqualTo(100));
            Assert.That(context.RoundItemUsage.HasUsed(ItemType.Meteor), Is.False);
            Assert.That(executor.CallCount, Is.EqualTo(1));
            Assert.That(executor.RollbackCount, Is.EqualTo(1));
        }
        finally
        {
            DestroyDependencies(stageData, itemData);
        }
    }

    [UnityTest]
    public IEnumerator Coordinator_CapturesGoldAtPopupOpen()
    {
        CreateDependencies(out StageData stageData, out RoundContext context, out StagePlayerState playerState, out ItemData itemData);
        SetDefaultItemCount(itemData.ItemType, 0);
        try
        {
            var service = new ItemPurchaseUseService(context, playerState);
            var coordinator = new ItemPurchaseUseCoordinator(context, playerState, service);
            var popup = new PendingConfirmationView();
            var executor = new FakeExecutor(true);

            coordinator.RequestImmediatePreviewAsync(itemData, popup, executor, CancellationToken.None);
            yield return null;
            Assert.That(popup.ObservedGold, Is.EqualTo(100));

            playerState.Gold = 50;
            popup.Complete(ItemPurchaseDecision.Confirmed);
            yield return null;

            Assert.That(coordinator.LastResult, Is.EqualTo(ItemPurchaseUseResult.PreviewSucceeded));
            Assert.That(executor.CallCount, Is.EqualTo(1));
            Assert.That(playerState.Gold, Is.EqualTo(50));
        }
        finally
        {
            DestroyDependencies(stageData, itemData);
        }
    }

    [Test]
    public void BettingItemExecutor_UsesSharedRoundUsageState()
    {
        CreateDependencies(out StageData stageData, out RoundContext context, out StagePlayerState playerState, out ItemData itemData);
        GameObject phaseObject = new GameObject("BettingItemExecutorTest");
        try
        {
            SetField(itemData, "m_ItemType", ItemType.AdditionalBetTicket);
            var bettingPhase = phaseObject.AddComponent<BettingPhase>();
            bettingPhase.InitializePhase(context);

            var service = new ItemPurchaseUseService(context, playerState);
            var executor = new BettingItemUseExecutor(bettingPhase);

            Assert.That(service.TryUse(itemData, executor, 100, out _), Is.True, "Reroll transaction should succeed");
            Assert.That(playerState.Gold, Is.EqualTo(100));
            Assert.That(context.RoundItemUsage.HasUsed(ItemType.AdditionalBetTicket), Is.True);
            Assert.That(bettingPhase.UsedAdditionalBetTicket, Is.True);
        }
        finally
        {
            Object.DestroyImmediate(phaseObject);
            DestroyDependencies(stageData, itemData);
        }
    }

    [Test]
    public void InsuranceItemExecutor_UsesSharedRoundUsageStateAndNotifiesOnce()
    {
        CreateDependencies(out StageData stageData, out RoundContext context, out StagePlayerState playerState, out ItemData itemData);
        GameObject phaseObject = new GameObject("InsuranceItemExecutorTest");
        try
        {
            SetField(itemData, "m_ItemType", ItemType.Insurance);
            var bettingPhase = phaseObject.AddComponent<BettingPhase>();
            bettingPhase.InitializePhase(context);
            int eventCount = 0;
            System.Action<ItemData> handler = _ => eventCount++;
            bettingPhase.OnItemUsed += handler;

            var service = new ItemPurchaseUseService(context, playerState);
            var executor = new BettingItemUseExecutor(bettingPhase);

            Assert.That(service.TryUse(itemData, executor, 100, out _), Is.True);
            Assert.That(playerState.Gold, Is.EqualTo(100));
            Assert.That(context.RoundItemUsage.HasUsed(ItemType.Insurance), Is.True);
            Assert.That(bettingPhase.UsedInsurance, Is.True);
            Assert.That(eventCount, Is.EqualTo(1));

            bettingPhase.OnItemUsed -= handler;
        }
        finally
        {
            Object.DestroyImmediate(phaseObject);
            DestroyDependencies(stageData, itemData);
        }
    }

    [Test]
    public void BettingItemExecutor_RejectsUseAfterBetPlacementAndRollsBackTransaction()
    {
        CreateDependencies(out StageData stageData, out RoundContext context, out StagePlayerState playerState, out ItemData itemData);
        GameObject phaseObject = new GameObject("PlacedBetItemExecutorTest");
        try
        {
            SetField(itemData, "m_ItemType", ItemType.AdditionalBetTicket);
            var bettingPhase = phaseObject.AddComponent<BettingPhase>();
            bettingPhase.InitializePhase(context);
            var ticket = new RoundBetTicket();
            SetPlainField(ticket, "<IsPlaced>k__BackingField", true);
            SetField(bettingPhase, "m_DraftTicket", ticket);

            var service = new ItemPurchaseUseService(context, playerState);
            var executor = new BettingItemUseExecutor(bettingPhase);

            Assert.That(service.TryUse(itemData, executor, 100, out string message), Is.False);
            Assert.That(message, Does.Contain("확정"));
            Assert.That(playerState.Gold, Is.EqualTo(100));
            Assert.That(context.RoundItemUsage.HasUsed(ItemType.AdditionalBetTicket), Is.False);
        }
        finally
        {
            Object.DestroyImmediate(phaseObject);
            DestroyDependencies(stageData, itemData);
        }
    }

    [Test]
    public void RerollItemExecutor_UpdatesActiveSpecialBetAndUsageState()
    {
        CreateDependencies(out StageData stageData, out RoundContext context, out StagePlayerState playerState, out ItemData itemData);
        GameObject phaseObject = new GameObject("RerollItemExecutorTest");
        try
        {
            context.InitializeStage(stageData);
            context.SetRoundData(stageData, 6);
            SetField(itemData, "m_ItemType", ItemType.RerollTicket);
            var bettingPhase = phaseObject.AddComponent<BettingPhase>();
            bettingPhase.InitializePhase(context);
            var ticket = new RoundBetTicket();
            ticket.SetRemainingTime(RemainingTimePrediction.Seconds0To5);
            ticket.SetOddEven(OddEvenPrediction.Odd);
            ticket.SetFirstEliminatedColumn(FirstEliminatedColumnPrediction.RedFront);
            SetField(bettingPhase, "m_DraftTicket", ticket);
            var previousActive = new HashSet<SpecialBetType>(context.ActiveSpecialBets);

            var service = new ItemPurchaseUseService(context, playerState);
            var executor = new BettingItemUseExecutor(bettingPhase);

            Assert.That(service.TryUse(itemData, executor, 100, out _), Is.True);
            Assert.That(playerState.Gold, Is.EqualTo(100));
            Assert.That(context.RoundItemUsage.HasUsed(ItemType.RerollTicket), Is.True, "Reroll usage should be recorded");
            Assert.That(context.ActiveSpecialBets.Count, Is.EqualTo(3));
            Assert.That(new HashSet<SpecialBetType>(context.ActiveSpecialBets).SetEquals(previousActive), Is.False);
            Assert.That(ticket.SelectedCategoryCount, Is.Zero, "Reroll should clear special predictions");
        }
        finally
        {
            Object.DestroyImmediate(phaseObject);
            DestroyDependencies(stageData, itemData);
        }
    }

    [Test]
    public void RerollItemExecutor_PreservesWagerFactionAndOtherItemUsage()
    {
        CreateDependencies(out StageData stageData, out RoundContext context, out StagePlayerState playerState, out ItemData itemData);
        GameObject phaseObject = new GameObject("RerollPreservationTest");
        try
        {
            context.InitializeStage(stageData);
            context.SetRoundData(stageData, 6);
            Assert.That(context.RoundItemUsage.TryMarkUsed(ItemType.AdditionalBetTicket), Is.True);
            SetField(itemData, "m_ItemType", ItemType.RerollTicket);
            var bettingPhase = phaseObject.AddComponent<BettingPhase>();
            bettingPhase.InitializePhase(context);
            var ticket = new RoundBetTicket();
            ticket.SetWager(300);
            ticket.SetFaction(FactionPrediction.Red);
            ticket.SetRemainingTime(RemainingTimePrediction.Seconds0To5);
            ticket.SetOddEven(OddEvenPrediction.Odd);
            ticket.SetFirstEliminatedColumn(FirstEliminatedColumnPrediction.RedFront);
            SetField(bettingPhase, "m_DraftTicket", ticket);

            var service = new ItemPurchaseUseService(context, playerState);
            var executor = new BettingItemUseExecutor(bettingPhase);

            Assert.That(service.TryUse(itemData, executor, 100, out _), Is.True);
            Assert.That(ticket.WagerCall, Is.EqualTo(300));
            Assert.That(ticket.Faction, Is.EqualTo(FactionPrediction.Red));
            Assert.That(ticket.SelectedCategoryCount, Is.EqualTo(1));
            Assert.That(context.RoundItemUsage.HasUsed(ItemType.AdditionalBetTicket), Is.True);
            Assert.That(context.RoundItemUsage.HasUsed(ItemType.RerollTicket), Is.True);
        }
        finally
        {
            Object.DestroyImmediate(phaseObject);
            DestroyDependencies(stageData, itemData);
        }
    }

    [Test]
    public void RerollItemExecutor_RollbackRestoresDraftSpecialPredictions()
    {
        CreateDependencies(out StageData stageData, out RoundContext context, out StagePlayerState playerState, out ItemData itemData);
        GameObject phaseObject = new GameObject("RerollRollbackTest");
        try
        {
            context.InitializeStage(stageData);
            context.SetRoundData(stageData, 6);
            SetField(itemData, "m_ItemType", ItemType.RerollTicket);
            var bettingPhase = phaseObject.AddComponent<BettingPhase>();
            bettingPhase.InitializePhase(context);
            var ticket = new RoundBetTicket();
            ticket.SetRemainingTime(RemainingTimePrediction.Seconds0To5);
            ticket.SetOddEven(OddEvenPrediction.Odd);
            ticket.SetFirstEliminatedColumn(FirstEliminatedColumnPrediction.RedFront);
            SetField(bettingPhase, "m_DraftTicket", ticket);
            var previousActive = new HashSet<SpecialBetType>(context.ActiveSpecialBets);
            System.Action<ItemData> failingHandler = _ => throw new System.InvalidOperationException("forced item-used listener failure");
            bettingPhase.OnItemUsed += failingHandler;

            var service = new ItemPurchaseUseService(context, playerState);
            var executor = new BettingItemUseExecutor(bettingPhase);

            Assert.That(service.TryUse(itemData, executor, 100, out _), Is.False);
            Assert.That(playerState.Gold, Is.EqualTo(100));
            Assert.That(context.RoundItemUsage.HasUsed(ItemType.RerollTicket), Is.False);
            Assert.That(new HashSet<SpecialBetType>(context.ActiveSpecialBets).SetEquals(previousActive), Is.True);
            Assert.That(ticket.SelectedCategoryCount, Is.EqualTo(3));
            Assert.That(ticket.RemainingTime, Is.EqualTo(RemainingTimePrediction.Seconds0To5));
            Assert.That(ticket.OddEven, Is.EqualTo(OddEvenPrediction.Odd));
            Assert.That(ticket.FirstEliminatedColumn, Is.EqualTo(FirstEliminatedColumnPrediction.RedFront));

            bettingPhase.OnItemUsed -= failingHandler;
        }
        finally
        {
            Object.DestroyImmediate(phaseObject);
            DestroyDependencies(stageData, itemData);
        }
    }

    [Test]
    public void CombatTimeExtensionExecutor_ChangesTimeThroughTransactionalService()
    {
        CreateDependencies(out StageData stageData, out RoundContext context, out StagePlayerState playerState, out ItemData itemData);
        GameObject phaseObject = new GameObject("CombatTimeExtensionExecutorTest");
        try
        {
            SetField(itemData, "m_ItemType", ItemType.TimeExtension);
            var combatPhase = phaseObject.AddComponent<CombatPhase>();
            SetField(combatPhase, "m_RemainingCombatTime", 10f);

            var service = new ItemPurchaseUseService(context, playerState);
            var executor = new CombatTimeExtensionUseExecutor(combatPhase);

            Assert.That(service.TryUse(itemData, executor, 100, out _), Is.True);
            Assert.That(playerState.Gold, Is.EqualTo(100));
            Assert.That(combatPhase.RemainingCombatTime, Is.EqualTo(15f));
            Assert.That(context.RoundItemUsage.HasUsed(ItemType.TimeExtension), Is.True);
        }
        finally
        {
            Object.DestroyImmediate(phaseObject);
            DestroyDependencies(stageData, itemData);
        }
    }

    [Test]
    public void CombatMeteorExecutor_InvalidCombatStateRollsBackTransaction()
    {
        CreateDependencies(out StageData stageData, out RoundContext context, out StagePlayerState playerState, out ItemData itemData);
        GameObject phaseObject = new GameObject("CombatMeteorExecutorTest");
        try
        {
            SetField(itemData, "m_ItemType", ItemType.Meteor);
            var combatPhase = phaseObject.AddComponent<CombatPhase>();
            var service = new ItemPurchaseUseService(context, playerState);
            var executor = new CombatMeteorUseExecutor(combatPhase, Vector3.zero);

            Assert.That(service.TryUse(itemData, executor, 100, out _), Is.False);
            Assert.That(playerState.Gold, Is.EqualTo(100));
            Assert.That(context.RoundItemUsage.HasUsed(ItemType.Meteor), Is.False);
        }
        finally
        {
            Object.DestroyImmediate(phaseObject);
            DestroyDependencies(stageData, itemData);
        }
    }

    [Test]
    public void CombatMercenaryExecutor_InvalidCombatStateRollsBackTransaction()
    {
        CreateDependencies(out StageData stageData, out RoundContext context, out StagePlayerState playerState, out ItemData itemData);
        GameObject phaseObject = new GameObject("CombatMercenaryExecutorTest");
        try
        {
            SetField(itemData, "m_ItemType", ItemType.Mercenary);
            var combatPhase = phaseObject.AddComponent<CombatPhase>();
            var service = new ItemPurchaseUseService(context, playerState);
            var executor = new CombatMercenaryUseExecutor(combatPhase, Vector3.zero);

            Assert.That(service.TryUse(itemData, executor, 100, out _), Is.False);
            Assert.That(playerState.Gold, Is.EqualTo(100));
            Assert.That(context.RoundItemUsage.HasUsed(ItemType.Mercenary), Is.False);
        }
        finally
        {
            Object.DestroyImmediate(phaseObject);
            DestroyDependencies(stageData, itemData);
        }
    }

    [Test]
    public void PopupPrefab_PreservesBuyAndUsesUiBaseControl()
    {
        GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(
            "Assets/Prefabs/UI/Popup/UI_ItemPurchasePopup.prefab");

        Assert.That(prefab, Is.Not.Null);
        Assert.That(prefab.activeSelf, Is.False);

        UI_ItemPurchasePopupController controller = prefab.GetComponent<UI_ItemPurchasePopupController>();
        Assert.That(controller, Is.Not.Null);
        Assert.That(prefab.GetComponent<UI_Base>(), Is.Not.Null);
        Assert.That(controller.HasControl, Is.True);
        Assert.That(prefab.GetComponent<CanvasGroup>(), Is.Not.Null);

        Transform buyTransform = prefab.transform.Find("PopupPanel/InnerPanel/Btn_Buy");
        Assert.That(buyTransform, Is.Not.Null);
        Assert.That(prefab.transform.Find("PopupPanel/InnerPanel/Btn_Cancel"), Is.Null);
        Assert.That(prefab.transform.Find("PopupPanel/InnerPanel/Btn_Cancel/CancelLabel"), Is.Null);

        Assert.That(GetField<UnityEngine.UI.Button>(controller, "m_BuyButton"),
            Is.SameAs(buyTransform.GetComponent<UnityEngine.UI.Button>()));
        Assert.That(GetField<TMPro.TMP_Text>(controller, "m_ItemInfoText"), Is.Not.Null);
        Assert.That(GetField<TMPro.TMP_Text>(controller, "m_PriceText"), Is.Not.Null);
        Assert.That(GetField<RectTransform>(controller, "m_PopupPanel"),
            Is.SameAs(prefab.transform.Find("PopupPanel")));
    }

    [Test]
    public void PopupController_BuyClickConfirmsAndRejectedReentryPreservesDecision()
    {
        ItemData itemData = CreateItemData();
        CreatePopupRuntime(
            out GameObject uiManagerObject,
            out GameObject rootObject,
            out GameObject instance);

        try
        {
            UI_ItemPurchasePopupController controller = instance.GetComponent<UI_ItemPurchasePopupController>();
            UnityEngine.UI.Button buyButton = instance.transform
                .Find("PopupPanel/InnerPanel/Btn_Buy")
                .GetComponent<UnityEngine.UI.Button>();

            controller.ShowAsync(itemData, 100, CancellationToken.None);
            buyButton.onClick.Invoke();
            Assert.That(controller.LastDecision, Is.EqualTo(ItemPurchaseDecision.Confirmed));

            controller.ShowAsync(null, 100, CancellationToken.None);
            Assert.That(controller.LastDecision, Is.EqualTo(ItemPurchaseDecision.Confirmed));
        }
        finally
        {
            DestroyPopupRuntime(uiManagerObject, rootObject, instance);
            Object.DestroyImmediate(itemData);
        }
    }

    [Test]
    public void PopupController_TokenCancellationCompletesCancelled()
    {
        ItemData itemData = CreateItemData();
        CreatePopupRuntime(
            out GameObject uiManagerObject,
            out GameObject rootObject,
            out GameObject instance);
        using var cancellationSource = new CancellationTokenSource();

        try
        {
            UI_ItemPurchasePopupController controller = instance.GetComponent<UI_ItemPurchasePopupController>();
            controller.ShowAsync(itemData, 100, cancellationSource.Token);
            cancellationSource.Cancel();

            Assert.That(controller.LastDecision, Is.EqualTo(ItemPurchaseDecision.Cancelled));
        }
        finally
        {
            DestroyPopupRuntime(uiManagerObject, rootObject, instance);
            Object.DestroyImmediate(itemData);
        }
    }

    private static void CreatePopupRuntime(
        out GameObject uiManagerObject,
        out GameObject rootObject,
        out GameObject instance)
    {
        uiManagerObject = null;
        rootObject = null;
        instance = null;

        UIManager uiManager = UIManager.Instance;
        if (uiManager == null)
        {
            uiManagerObject = new GameObject("ItemPurchasePopupTestUIManager");
            uiManager = uiManagerObject.AddComponent<UIManager>();
            SetStaticField(typeof(UIManager), "<Instance>k__BackingField", uiManager);
        }

        rootObject = new GameObject(
            "ItemPurchasePopupTestRoot",
            typeof(RectTransform),
            typeof(Canvas),
            typeof(CanvasGroup),
            typeof(UI_Root));
        rootObject.GetComponent<Canvas>().renderMode = RenderMode.ScreenSpaceOverlay;
        UI_Root root = rootObject.GetComponent<UI_Root>();

        GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(
            "Assets/Prefabs/UI/Popup/UI_ItemPurchasePopup.prefab");
        Assert.That(prefab, Is.Not.Null);

        instance = Object.Instantiate(prefab, root.transform);
        instance.GetComponent<UI_ItemPurchasePopupController>().SetRoot(root);
    }

    private static void DestroyPopupRuntime(
        GameObject uiManagerObject,
        GameObject rootObject,
        GameObject instance)
    {
        if (instance != null)
        {
            Object.DestroyImmediate(instance);
        }

        if (rootObject != null)
        {
            Object.DestroyImmediate(rootObject);
        }

        if (uiManagerObject != null)
        {
            SetStaticField(typeof(UIManager), "<Instance>k__BackingField", null);
            Object.DestroyImmediate(uiManagerObject);
        }
    }

    private static void CreateDependencies(
        out StageData stageData,
        out RoundContext context,
        out StagePlayerState playerState,
        out ItemData itemData)
    {
        stageData = ScriptableObject.CreateInstance<StageData>();
        SetField(stageData, "m_RoundDatas", new List<RoundData>
        {
            ScriptableObject.CreateInstance<RoundData>()
        });

        context = new RoundContext();
        context.InitializeStage(stageData);
        context.SetRoundData(stageData, 0);

        playerState = new StagePlayerState { Gold = 100 };
        itemData = ScriptableObject.CreateInstance<ItemData>();
        SetField(itemData, "m_ItemType", ItemType.Meteor);
        SetField(itemData, "m_ItemName", "Test Meteor");
        SetField(itemData, "m_PriceGold", 50);
    }

    private static GameObject CreateItemSaveManager(ItemType itemType, int gold, int itemCount)
    {
        typeof(SaveManager).GetProperty("Instance")?.SetValue(null, null);
        GameObject managerObject = new GameObject("ItemPurchaseUseTestSaveManager");
        var manager = managerObject.AddComponent<SaveManager>();
        var state = new InTheArena.Save.PlayerProgressState();
        state.SetGold(gold);
        state.SetItemCount(itemType, itemCount);
        manager.InitializeForTests(
            new InTheArena.Tests.Editor.FakeSaveRepository(),
            new InTheArena.Tests.Editor.FakeClock(),
            state);
        typeof(SaveManager).GetProperty("Instance")?.SetValue(null, manager);
        return managerObject;
    }

    private static void SetDefaultItemCount(ItemType itemType, int count)
    {
        Assert.That(SaveManager.Instance.DebugTryModifyState(
            state => state.SetItemCount(itemType, count),
            out string error), Is.True, error);
    }

    private static ItemData CreateItemData()
    {
        var itemData = ScriptableObject.CreateInstance<ItemData>();
        SetField(itemData, "m_ItemType", ItemType.Meteor);
        SetField(itemData, "m_ItemName", "Test Meteor");
        SetField(itemData, "m_PriceGold", 50);
        return itemData;
    }

    private static void DestroyDependencies(StageData stageData, ItemData itemData)
    {
        if (itemData != null)
        {
            Object.DestroyImmediate(itemData);
        }

        if (stageData != null)
        {
            var roundDatas = GetField<List<RoundData>>(stageData, "m_RoundDatas");
            if (roundDatas != null)
            {
                for (int i = 0; i < roundDatas.Count; i++)
                {
                    if (roundDatas[i] != null)
                    {
                        Object.DestroyImmediate(roundDatas[i]);
                    }
                }
            }

            Object.DestroyImmediate(stageData);
        }
    }

    private sealed class FakeExecutor : IItemPurchaseUseExecutor
    {
        private readonly bool m_Result;

        public FakeExecutor(bool result)
        {
            m_Result = result;
        }

        public int CallCount { get; private set; }

        public bool TryExecute(ItemData itemData, out string message)
        {
            CallCount++;
            message = m_Result ? "preview success" : "preview failure";
            return m_Result;
        }
    }

    private sealed class ReversibleExecutor : IReversibleItemPurchaseUseExecutor
    {
        private readonly bool m_Result;

        public ReversibleExecutor(bool result)
        {
            m_Result = result;
        }

        public int CallCount { get; private set; }
        public int RollbackCount { get; private set; }

        public bool TryExecute(ItemData itemData, out string message)
        {
            CallCount++;
            message = m_Result ? "use success" : "use failure";
            return m_Result;
        }

        public void Rollback(ItemData itemData)
        {
            RollbackCount++;
        }
    }

    private sealed class PendingConfirmationView : IItemPurchaseConfirmationView
    {
        private AwaitableCompletionSource m_Source;
        private bool m_IsCompleted;

        public int ObservedGold { get; private set; }
        public ItemPurchaseDecision LastDecision { get; private set; } = ItemPurchaseDecision.Cancelled;

        public Awaitable ShowAsync(ItemData itemData, int currentGold, CancellationToken token)
        {
            ObservedGold = currentGold;
            LastDecision = ItemPurchaseDecision.Cancelled;
            m_IsCompleted = false;
            m_Source = new AwaitableCompletionSource();
            return m_Source.Awaitable;
        }

        public void Complete(ItemPurchaseDecision decision)
        {
            if (m_IsCompleted || m_Source == null)
            {
                return;
            }

            m_IsCompleted = true;
            LastDecision = decision;
            m_Source.TrySetResult();
        }

        public void Cancel()
        {
            Complete(ItemPurchaseDecision.Cancelled);
        }
    }

    private static void SetField(UnityEngine.Object target, string name, object value)
    {
        target.GetType().GetField(name, System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)
            ?.SetValue(target, value);
    }

    private static void SetPlainField(object target, string name, object value)
    {
        target.GetType().GetField(name, System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)
            ?.SetValue(target, value);
    }

    private static void SetStaticField(System.Type type, string name, object value)
    {
        type.GetField(name, System.Reflection.BindingFlags.Static | System.Reflection.BindingFlags.NonPublic)
            ?.SetValue(null, value);
    }

    private static T GetField<T>(UnityEngine.Object target, string name)
    {
        return (T)target.GetType().GetField(name, System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)
            ?.GetValue(target);
    }
}
#endif
