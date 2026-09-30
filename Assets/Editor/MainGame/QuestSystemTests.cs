#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using InTheArena.Events.Core;
using InTheArena.Mail;
using InTheArena.MainGame;
using InTheArena.Quests;
using InTheArena.Save;
using InTheArena.Tests.Editor;
using NUnit.Framework;
using UnityEngine;

public sealed class QuestSystemTests
{
    private GameObject m_SaveManagerObject;
    private SaveManager m_SaveManager;
    private FakeSaveRepository m_Repository;
    private FakeClock m_Clock;
    private QuestCatalog m_Catalog;
    private QuestService m_Quests;
    private TestLobbyState m_Lobby;

    [SetUp]
    /// <summary>각 테스트에 격리된 저장과 고정 시각·난수를 구성합니다.</summary>
    public void SetUp()
    {
        m_Clock = new FakeClock { UtcNow = new DateTime(2026, 9, 28, 3, 0, 0, DateTimeKind.Utc) };
        m_Repository = new FakeSaveRepository();

        PlayerProgressState state = new PlayerProgressState();
        state.SetCreatedWithSchemaVersion(6);
        state.SetHearts(5);
        state.SetLastHeartRecoveryUtcTicks(m_Clock.UtcNow.Ticks);

        m_SaveManagerObject = new GameObject("QuestSystemTests_SaveManager");
        m_SaveManager = m_SaveManagerObject.AddComponent<SaveManager>();
        m_SaveManager.InitializeForTests(m_Repository, m_Clock, state);

        m_Catalog = QuestContentFactory.CreateRuntimeDefaultCatalog();
        m_Lobby = new TestLobbyState { IsInLobby = true };
        m_Quests = new QuestService(m_SaveManager, m_Catalog, new FixedQuestRandom(), m_Lobby);
    }

    [TearDown]
    /// <summary>테스트에서 만든 SaveManager와 ScriptableObject를 정리합니다.</summary>
    public void TearDown()
    {
        if (m_SaveManagerObject != null)
        {
            UnityEngine.Object.DestroyImmediate(m_SaveManagerObject);
        }

        if (m_Catalog != null)
        {
            DestroyCatalogDefinitions(m_Catalog.DailyDefinitions);
            DestroyCatalogDefinitions(m_Catalog.EmergencyDefinitions);
            UnityEngine.Object.DestroyImmediate(m_Catalog);
        }
    }

    [Test]
    /// <summary>신규 초기화가 일일 3개와 환영 우편 한 건을 만드는지 확인합니다.</summary>
    public void Initialize_CreatesThreeDailyQuestsAndOneWelcomeMail()
    {
        QuestRefreshResult result = m_Quests.Initialize();

        Assert.That(result, Is.EqualTo(QuestRefreshResult.Committed));
        Assert.That(m_Quests.GetDailyQuests(), Has.Count.EqualTo(3));
        Assert.That(m_Repository.SavedState.MailEntries, Has.Count.EqualTo(1));
        Assert.That(m_Repository.SavedState.MailEntries[0].sourceKey, Is.EqualTo("welcome:first-launch:v1"));
    }

    [Test]
    /// <summary>달성 후 확인 전에는 골드가 지급되지 않고 확인 뒤 우편만 생기는지 확인합니다.</summary>
    public void BigBettor_ReachesReadyThenConfirmCreatesMailWithoutGrantingGold()
    {
        m_Quests.Initialize();
        QuestView bigBettor = FindQuestByTitle("큰손 배팅");
        int goldBefore = m_SaveManager.Gold;

        BetPlacedEvent placed = new BetPlacedEvent(
            "bet-1",
            m_Clock.UtcNow.Ticks,
            "stage-1",
            1,
            bigBettor.Target);
        Assert.That(m_Quests.ProcessBetPlaced(placed), Is.True);
        Assert.That(m_Quests.GetQuest(bigBettor.InstanceId).Status, Is.EqualTo(QuestStatus.ReadyToConfirm));
        Assert.That(m_SaveManager.Gold, Is.EqualTo(goldBefore));

        QuestCompletionResult completion = m_Quests.TryConfirmCompletion(bigBettor.InstanceId);
        Assert.That(completion, Is.EqualTo(QuestCompletionResult.Success));
        Assert.That(m_Quests.GetQuest(bigBettor.InstanceId).Status, Is.EqualTo(QuestStatus.Delivered));
        Assert.That(m_Repository.SavedState.MailEntries, Has.Count.EqualTo(2));
        Assert.That(m_SaveManager.Gold, Is.EqualTo(goldBefore));
    }

    [Test]
    /// <summary>발송 저장 실패를 재시도해도 우편이 중복되지 않는지 확인합니다.</summary>
    public void CompletionSaveFailure_KeepsPendingRequestAndManualRetryDoesNotDuplicateMail()
    {
        m_Quests.Initialize();
        QuestView bigBettor = FindQuestByTitle("큰손 배팅");
        m_Quests.ProcessBetPlaced(new BetPlacedEvent(
            "bet-2",
            m_Clock.UtcNow.Ticks,
            "stage-1",
            1,
            bigBettor.Target));

        m_Repository.FailNextSave = true;
        Assert.That(m_Quests.TryConfirmCompletion(bigBettor.InstanceId), Is.EqualTo(QuestCompletionResult.SaveFailed));
        Assert.That(m_Quests.PendingDeliveryCount, Is.EqualTo(1));

        m_Quests.RetryPendingDeliveries(true);
        m_Quests.RetryPendingDeliveries(true);

        Assert.That(m_Quests.PendingDeliveryCount, Is.EqualTo(0));
        Assert.That(CountMailSource("quest:" + bigBettor.InstanceId), Is.EqualTo(1));
    }

    [Test]
    /// <summary>모든 첨부가 원자적으로 지급되고 입장권이 5개를 초과하는지 확인합니다.</summary>
    public void MailClaim_AppliesAllRewardsAtomicallyAndAllowsTicketsAboveFive()
    {
        m_Quests.Initialize();
        MailboxService mailbox = new MailboxService(m_SaveManager, m_Lobby);
        List<RewardEntryPayload> rewards = new List<RewardEntryPayload>
        {
            new RewardEntryPayload { kind = (int)MailRewardKind.Gold, amount = 400 },
            new RewardEntryPayload { kind = (int)MailRewardKind.EntranceTicket, amount = 3 }
        };
        Assert.That(mailbox.TrySend(new MailGrantRequest("test:bundle", "묶음", "보상", rewards)), Is.EqualTo(MailSendResult.Success));

        MailView mail = FindMail(mailbox.GetMails(), "test:bundle");
        Assert.That(mailbox.TryClaim(mail.MailId), Is.EqualTo(MailClaimResult.Claimed));
        Assert.That(m_SaveManager.Gold, Is.EqualTo(400));
        Assert.That(m_SaveManager.Hearts, Is.EqualTo(8));
        Assert.That(mailbox.TryClaim(mail.MailId), Is.EqualTo(MailClaimResult.AlreadyClaimed));
    }

    [Test]
    /// <summary>클리어 복귀 하나가 긴급을 한 번만 만들고 로비 표시를 예약하는지 확인합니다.</summary>
    public void ProcessStageReturn_CreatesOneEmergencyAndDoesNotRerollReceipt()
    {
        m_Quests.Initialize();
        PlayerProgressState candidate = m_SaveManager.CreateSnapshot();
        candidate.SetPendingStageReturn(new StageReturnReceiptPayload
        {
            returnId = "return-1",
            stageRunId = "stage-1",
            stageNumber = 1,
            clearedAtUtcTicks = m_Clock.UtcNow.Ticks
        });
        Assert.That(m_SaveManager.TryCommitCandidate(candidate, out string error), Is.True, error);

        Assert.That(m_Quests.ProcessPendingStageReturn(), Is.True);
        Assert.That(m_Quests.GetEmergencyQuest(), Is.Not.Null);
        Assert.That(m_SaveManager.CreateSnapshot().EmergencyGenerationCount, Is.EqualTo(1));
        Assert.That(m_SaveManager.CreateSnapshot().EmergencyIntroPending, Is.True);
        Assert.That(m_Quests.ProcessPendingStageReturn(), Is.False);
        Assert.That(m_SaveManager.CreateSnapshot().EmergencyGenerationCount, Is.EqualTo(1));
    }

    [Test]
    /// <summary>이벤트 버스의 재진입 FIFO와 구독 해제를 확인합니다.</summary>
    public void GameEventBus_ReentrantPublishPreservesFifoAndDisposeStopsDelivery()
    {
        GameEventBus eventBus = new GameEventBus();
        List<string> order = new List<string>();
        ReentrantListener listener = new ReentrantListener(eventBus, order);
        IDisposable subscription = eventBus.Subscribe<TestEvent>(listener.Handle);

        eventBus.Publish(new TestEvent("first"));
        subscription.Dispose();
        eventBus.Publish(new TestEvent("after-dispose"));

        Assert.That(order, Is.EqualTo(new[] { "first", "second" }));
    }

    [Test]
    /// <summary>정산이 선택·적중을 항목별 공통 결과로 제공하는지 확인합니다.</summary>
    public void Settlement_ExposesSelectedAndMatchedOutcomePerCategory()
    {
        RoundBetTicket ticket = new RoundBetTicket();
        ticket.SetWager(100);
        ticket.SetFaction(FactionPrediction.Red);
        ticket.SetOddEven(OddEvenPrediction.Odd);

        StageData stage = ScriptableObject.CreateInstance<StageData>();
        SetStageForSettlement(stage);
        RoundContext context = new RoundContext();
        context.InitializeStage(stage);
        context.RestoreSpecialBetOrder(new[] { SpecialBetType.OddEven });
        context.SetRoundData(stage, 4);
        Assert.That(context.StageSession.TryPlaceBet(ticket, context, out string error), Is.True, error);

        CombatResultSnapshot result = new CombatResultSnapshot(
            Team.Red,
            12f,
            2,
            1,
            Array.Empty<SurvivingRowPrediction>(),
            null);
        BetSettlement settlement = BetSettlementService.Settle(ticket, result);

        Assert.That(settlement.Outcomes, Has.Count.EqualTo(5));
        Assert.That(settlement.Outcomes[0].Category, Is.EqualTo(BetCategory.Faction));
        Assert.That(settlement.Outcomes[0].IsSelected, Is.True);
        Assert.That(settlement.Outcomes[0].IsMatched, Is.True);
        Assert.That(settlement.Outcomes[1].IsSelected, Is.False);
        UnityEngine.Object.DestroyImmediate(stage);
    }

    [Test]
    /// <summary>스키마 6의 우편·퀘스트·도전·복귀 필드가 깊은 복사와 왕복을 유지하는지 확인합니다.</summary>
    public void PlayerProgressState_RoundTripsSchemaSixFieldsWithDeepCopies()
    {
        PlayerSavePayload payload = new PlayerSavePayload
        {
            createdWithSchemaVersion = 6,
            dailyQuestDateUtc = "2026-09-28",
            highestProcessedUtcDate = "2026-09-28",
            emergencyGenerationDateUtc = "2026-09-28",
            emergencyGenerationCount = 2,
            emergencyIntroPending = true,
            mailEntries = new[]
            {
                new MailEntryPayload
                {
                    mailId = "mail-1",
                    sourceKey = "quest:daily-1",
                    title = "보상",
                    rewards = new[] { new RewardEntryPayload { kind = (int)MailRewardKind.Gold, amount = 300 } }
                }
            },
            completedOneShotRuleIds = new[] { "welcome:first-launch:v1" },
            dailyQuestInstances = new[]
            {
                new QuestInstancePayload
                {
                    instanceId = "daily-1",
                    definitionId = "daily_big_bettor",
                    status = (int)QuestStatus.Active,
                    consumedEventIds = new[] { "bet-1" }
                }
            },
            emergencyQuestInstance = new QuestInstancePayload
            {
                instanceId = "emergency-1",
                definitionId = "emergency_wager",
                kind = (int)QuestKind.Emergency,
                status = (int)QuestStatus.Active
            },
            processedStageReturnIds = new[] { "return-1" },
            activeStageRun = new StageRunPayload { stageRunId = "stage-1", stageNumber = 2 },
            pendingStageReturn = new StageReturnReceiptPayload { returnId = "return-2", stageRunId = "stage-2" }
        };

        PlayerProgressState state = new PlayerProgressState();
        state.CopyFromPayload(payload);
        PlayerProgressState clone = state.DeepClone();
        clone.FindQuest("daily-1").progressAmount = 99;
        PlayerSavePayload roundTrip = clone.ToPayload();

        Assert.That(state.FindQuest("daily-1").progressAmount, Is.EqualTo(0));
        Assert.That(roundTrip.mailEntries[0].rewards[0].amount, Is.EqualTo(300));
        Assert.That(roundTrip.dailyQuestInstances[0].progressAmount, Is.EqualTo(99));
        Assert.That(roundTrip.emergencyQuestInstance.instanceId, Is.EqualTo("emergency-1"));
        Assert.That(roundTrip.activeStageRun.stageRunId, Is.EqualTo("stage-1"));
        Assert.That(roundTrip.pendingStageReturn.returnId, Is.EqualTo("return-2"));
        Assert.That(roundTrip.processedStageReturnIds, Does.Contain("return-1"));
        Assert.That(roundTrip.emergencyIntroPending, Is.True);
    }

    [Test]
    /// <summary>스키마 5 체크섬이 새 스키마 6 필드를 포함하지 않아 기존 저장과 호환되는지 확인합니다.</summary>
    public void VersionFiveChecksum_DoesNotIncludeQuestOrMailFields()
    {
        PlayerSaveEnvelope original = new PlayerSaveEnvelope
        {
            schemaVersion = 5,
            revision = 3,
            savedAtUtcTicks = 12345,
            payload = new PlayerSavePayload { gold = 100, hearts = 5 }
        };
        PlayerSaveEnvelope injected = new PlayerSaveEnvelope
        {
            schemaVersion = 5,
            revision = original.revision,
            savedAtUtcTicks = original.savedAtUtcTicks,
            payload = new PlayerSavePayload
            {
                gold = 100,
                hearts = 5,
                createdWithSchemaVersion = 6,
                mailEntries = new[] { new MailEntryPayload { mailId = "injected" } },
                dailyQuestInstances = new[] { new QuestInstancePayload { instanceId = "injected" } },
                emergencyGenerationCount = 3,
                emergencyIntroPending = true
            }
        };

        Assert.That(
            JsonFileSaveStorage.ComputeChecksum(injected),
            Is.EqualTo(JsonFileSaveStorage.ComputeChecksum(original)));
    }

    /// <summary>일일 목록에서 제목이 일치하는 퀘스트를 찾습니다.</summary>
    private QuestView FindQuestByTitle(string title)
    {
        IReadOnlyList<QuestView> quests = m_Quests.GetDailyQuests();
        for (int i = 0; i < quests.Count; i++)
        {
            if (quests[i].Title == title)
            {
                return quests[i];
            }
        }
        Assert.Fail("Quest was not found: " + title);
        return null;
    }

    /// <summary>지정 발송 근거 키의 저장 우편 개수를 셉니다.</summary>
    private int CountMailSource(string sourceKey)
    {
        int count = 0;
        IReadOnlyList<MailEntryPayload> mails = m_SaveManager.CreateSnapshot().MailEntries;
        for (int i = 0; i < mails.Count; i++)
        {
            if (mails[i].sourceKey == sourceKey)
            {
                count++;
            }
        }
        return count;
    }

    /// <summary>읽기 모델 목록에서 발송 근거 키가 일치하는 우편을 찾습니다.</summary>
    private static MailView FindMail(IReadOnlyList<MailView> mails, string sourceKey)
    {
        for (int i = 0; i < mails.Count; i++)
        {
            if (mails[i].SourceKey == sourceKey)
            {
                return mails[i];
            }
        }
        return null;
    }

    /// <summary>런타임 테스트 카탈로그가 만든 정의 오브젝트를 정리합니다.</summary>
    private static void DestroyCatalogDefinitions(IReadOnlyList<QuestDefinition> definitions)
    {
        for (int i = 0; i < definitions.Count; i++)
        {
            if (definitions[i] != null)
            {
                UnityEngine.Object.DestroyImmediate(definitions[i]);
            }
        }
    }

    /// <summary>정산 테스트에 필요한 최소 StageData 필드를 설정합니다.</summary>
    private static void SetStageForSettlement(StageData stage)
    {
        System.Reflection.BindingFlags flags = System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic;
        typeof(StageData).GetField("m_StageName", flags)?.SetValue(stage, "Quest Test");
        typeof(StageData).GetField("m_StageNum", flags)?.SetValue(stage, 1);
        typeof(StageData).GetField("m_InitialCall", flags)?.SetValue(stage, 500);
        typeof(StageData).GetField("m_TargetCall", flags)?.SetValue(stage, 1000);
        typeof(StageData).GetField("m_EnableFactionBet", flags)?.SetValue(stage, true);
        typeof(StageData).GetField("m_SpecialBetTypes", flags)?.SetValue(stage, new List<SpecialBetType> { SpecialBetType.OddEven });
    }

    private sealed class TestLobbyState : ILobbyStateProvider
    {
        public bool IsInLobby { get; set; }
    }

    private sealed class FixedQuestRandom : IQuestRandom
    {
        /// <summary>항상 범위의 첫 값을 선택합니다.</summary>
        public int Range(int minimumInclusive, int maximumExclusive)
        {
            return minimumInclusive;
        }

        /// <summary>항상 0을 반환하여 생성 경계를 결정적으로 만듭니다.</summary>
        public double Value()
        {
            return 0d;
        }
    }

    private sealed class TestEvent : GameEventBase
    {
        /// <summary>이벤트 버스 순서 검증용 사실 이벤트를 만듭니다.</summary>
        public TestEvent(string eventId) : base(eventId, DateTime.UtcNow.Ticks)
        {
        }
    }

    private sealed class ReentrantListener
    {
        private readonly GameEventBus m_EventBus;
        private readonly List<string> m_Order;

        /// <summary>이벤트 버스와 관찰 순서 목록을 연결합니다.</summary>
        public ReentrantListener(GameEventBus eventBus, List<string> order)
        {
            m_EventBus = eventBus;
            m_Order = order;
        }

        /// <summary>첫 이벤트 처리 중 두 번째 이벤트를 재진입 발행합니다.</summary>
        public void Handle(TestEvent eventData)
        {
            m_Order.Add(eventData.EventId);
            if (eventData.EventId == "first")
            {
                m_EventBus.Publish(new TestEvent("second"));
            }
        }
    }
}
#endif
