using System;
using System.Collections;
using System.Collections.Generic;
using InTheArena.Events.Core;
using InTheArena.Mail;
using InTheArena.MainGame;
using InTheArena.Quests;
using InTheArena.Save;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// 공통 이벤트 버스와 퀘스트·우편 연결 모듈의 Unity 생명주기를 구성합니다.
/// </summary>
[DisallowMultipleComponent]
public sealed class EventManager : MonoBehaviour, ILobbyStateProvider
{
    private const string LobbySceneName = "Lobby";

    public static EventManager Instance { get; private set; }

    public IGameEventBus EventBus { get; private set; }
    public QuestService Quests { get; private set; }
    public MailboxService Mailbox { get; private set; }
    public bool IsInLobby { get; private set; }
    public bool IsReady { get; private set; }
    public string CurrentStageRunId { get; private set; }
    public bool IsEmergencyIntroVisible { get; private set; }

    public bool ShouldDeferLobbyRewardPresentation
    {
        get
        {
            if (IsEmergencyIntroVisible)
            {
                return true;
            }

            return Quests != null
                && (Quests.HasEmergencyIntroPending || Quests.HasPendingStageReturn);
        }
    }

    private readonly Queue<IGameEvent> m_PendingEvents = new Queue<IGameEvent>();
    private string m_ApplicationRunId;
    private float m_NextBoundaryCheckTime;

    /// <summary>
    /// 첫 씬이 열리기 전에 전역 이벤트 관리자를 한 번만 설치합니다.
    /// </summary>
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    private static void Install()
    {
        if (Instance != null)
        {
            return;
        }

        GameObject gameObject = new GameObject("EventManager");
        gameObject.AddComponent<EventManager>();
    }

    /// <summary>
    /// 싱글턴과 이벤트 버스를 구성하고 씬 로드 알림을 연결합니다.
    /// </summary>
    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;
        DontDestroyOnLoad(gameObject);
        EventBus = new GameEventBus();
        m_ApplicationRunId = Guid.NewGuid().ToString("N");
        SceneManager.sceneLoaded += OnSceneLoaded;
    }

    /// <summary>
    /// 저장 복원이 끝난 뒤 퀘스트·우편 서비스를 초기화합니다.
    /// </summary>
    private IEnumerator Start()
    {
        while (SaveManager.Instance == null || SaveManager.Instance.CreateSnapshot() == null)
        {
            yield return null;
        }

        QuestCatalog catalog = Resources.Load<QuestCatalog>("Quest/QuestCatalog");
        if (catalog == null)
        {
            catalog = QuestContentFactory.CreateRuntimeDefaultCatalog();
            Debug.LogWarning("[EventManager] QuestCatalog 에셋을 찾지 못해 확정된 기본 콘텐츠를 사용합니다.");
        }

        Quests = new QuestService(SaveManager.Instance, catalog, new SystemQuestRandom(), this);
        Mailbox = new MailboxService(SaveManager.Instance, this);
        QuestRefreshResult initialization = Quests.Initialize();
        IsReady = initialization != QuestRefreshResult.Unavailable
            && initialization != QuestRefreshResult.InvalidCatalog
            && initialization != QuestRefreshResult.SaveFailed;

        if (!IsReady)
        {
            Debug.LogError($"[EventManager] 퀘스트·우편 서비스 초기화 실패: {initialization}");
            yield break;
        }

        ApplicationReadyEvent readyEvent = new ApplicationReadyEvent(
            Guid.NewGuid().ToString("N"),
            SaveManager.Instance.UtcNow.Ticks,
            m_ApplicationRunId);
        EventBus.Publish(readyEvent);

        IsInLobby = SceneManager.GetActiveScene().name == LobbySceneName;
        if (IsInLobby)
        {
            ProcessLobbyArrival();
        }
    }

    /// <summary>
    /// 저장 실패로 대기한 이벤트, 우편 발송 재시도와 UTC 경계를 처리합니다.
    /// </summary>
    private void Update()
    {
        if (!IsReady || Quests == null || SaveManager.Instance == null)
        {
            return;
        }

        ProcessPendingEvents();
        Quests.RetryPendingDeliveries(false);

        if (Time.unscaledTime >= m_NextBoundaryCheckTime)
        {
            m_NextBoundaryCheckTime = Time.unscaledTime + 1f;
            Quests.RefreshTimeBoundaries(SaveManager.Instance.UtcNow);

            if (IsInLobby && UIManager.Instance != null)
            {
                UIManager.Instance.EnsureQuestAndMailboxUi();
            }
        }
    }

    /// <summary>
    /// 활성 씬이 로비인지 기록하고 준비된 서비스의 로비 복귀 처리를 예약합니다.
    /// </summary>
    private void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        IsInLobby = scene.name == LobbySceneName;
        if (IsReady && IsInLobby)
        {
            StartCoroutine(ProcessLobbyArrivalNextFrame());
        }
    }

    /// <summary>
    /// 로비 UI_Root가 등록될 시간을 한 프레임 확보한 뒤 복귀 처리를 실행합니다.
    /// </summary>
    private IEnumerator ProcessLobbyArrivalNextFrame()
    {
        yield return null;
        ProcessLobbyArrival();
    }

    /// <summary>
    /// 로비 도착 시 시간 경계, 클리어 복귀 영수증과 프리팹 UI를 연결합니다.
    /// </summary>
    private void ProcessLobbyArrival()
    {
        Quests.RefreshTimeBoundaries(SaveManager.Instance.UtcNow);
        PlayerProgressState state = SaveManager.Instance.CreateSnapshot();
        StageReturnReceiptPayload receipt = state?.PendingStageReturn?.DeepClone();
        if (receipt != null && Quests.ProcessPendingStageReturn())
        {
            StageReturnedToLobbyEvent returnEvent = new StageReturnedToLobbyEvent(
                receipt.returnId,
                SaveManager.Instance.UtcNow.Ticks,
                receipt.returnId,
                receipt.stageRunId,
                receipt.stageNumber);
            EventBus.Publish(returnEvent);
        }

        if (UIManager.Instance != null)
        {
            UIManager.Instance.EnsureQuestAndMailboxUi();
        }
    }

    /// <summary>
    /// 새 도전 ID를 만들고 시작 표식이 저장된 경우에만 성공을 반환합니다.
    /// </summary>
    public bool TryStartStage(int stageNumber, out string stageRunId)
    {
        if (!IsReady || Quests == null || SaveManager.Instance == null)
        {
            stageRunId = null;
            return false;
        }

        stageRunId = Guid.NewGuid().ToString("N");
        StageStartedEvent eventData = new StageStartedEvent(
            Guid.NewGuid().ToString("N"),
            SaveManager.Instance.UtcNow.Ticks,
            stageRunId,
            stageNumber);

        if (!Quests.ProcessStageStarted(eventData))
        {
            stageRunId = null;
            return false;
        }

        CurrentStageRunId = stageRunId;
        EventBus.Publish(eventData);
        return true;
    }

    /// <summary>
    /// 실제 차감이 확정된 베팅 사실을 퀘스트 저장과 공통 버스에 전달합니다.
    /// </summary>
    public void RecordBetPlaced(RoundBetTicket ticket, int roundId)
    {
        if (!IsReady || ticket == null || !ticket.IsPlaced)
        {
            return;
        }

        BetPlacedEvent eventData = new BetPlacedEvent(
            Guid.NewGuid().ToString("N"),
            SaveManager.Instance.UtcNow.Ticks,
            CurrentStageRunId,
            roundId,
            ticket.WagerCall);
        ProcessOrQueue(eventData);
    }

    /// <summary>
    /// 정산 확정 후 항목별 적중과 전체 지급액 사실을 전달합니다.
    /// </summary>
    public void RecordBetSettled(
        RoundBetTicket ticket,
        CombatResultSnapshot result,
        BetSettlement settlement,
        int roundId)
    {
        if (!IsReady || ticket == null || result == null || settlement == null)
        {
            return;
        }

        BetSettledEvent eventData = new BetSettledEvent(
            Guid.NewGuid().ToString("N"),
            SaveManager.Instance.UtcNow.Ticks,
            CurrentStageRunId,
            roundId,
            ticket.Faction,
            result.Winner,
            settlement);
        ProcessOrQueue(eventData);
    }

    /// <summary>
    /// 효과와 소비 저장이 모두 끝난 아이템 사용 사실을 공통 버스에 전달합니다.
    /// </summary>
    public void RecordItemUseCommitted(ItemType itemType, int roundId)
    {
        if (!IsReady || itemType == ItemType.None)
        {
            return;
        }

        string useId = Guid.NewGuid().ToString("N");
        ItemUseCommittedEvent eventData = new ItemUseCommittedEvent(
            Guid.NewGuid().ToString("N"),
            SaveManager.Instance.UtcNow.Ticks,
            CurrentStageRunId,
            roundId,
            itemType,
            useId);
        EventBus.Publish(eventData);
    }

    /// <summary>
    /// 클리어 보상 저장 후보에 퀘스트 진행과 복귀 영수증을 결합합니다.
    /// </summary>
    public StageClearedEvent ApplyStageClearedToCandidate(PlayerProgressState candidate, int stageNumber)
    {
        if (!IsReady || candidate == null)
        {
            return null;
        }

        StageClearedEvent eventData = new StageClearedEvent(
            Guid.NewGuid().ToString("N"),
            SaveManager.Instance.UtcNow.Ticks,
            CurrentStageRunId,
            stageNumber);
        Quests.ApplyStageClearedToCandidate(candidate, eventData);
        return eventData;
    }

    /// <summary>
    /// 결합된 클리어 저장 성공 뒤 공통 관찰자에게만 확정 사실을 알립니다.
    /// </summary>
    public void NotifyStageClearCommitted(StageClearedEvent eventData)
    {
        if (eventData == null)
        {
            return;
        }

        EventBus.Publish(eventData);
        CurrentStageRunId = null;
    }

    /// <summary>
    /// 패배·이탈·오류 종료를 퀘스트 실패 정책과 공통 버스에 전달합니다.
    /// </summary>
    public void RecordStageEnded(StageEndReason reason)
    {
        if (!IsReady || string.IsNullOrWhiteSpace(CurrentStageRunId))
        {
            return;
        }

        StageEndedEvent eventData = new StageEndedEvent(
            Guid.NewGuid().ToString("N"),
            SaveManager.Instance.UtcNow.Ticks,
            CurrentStageRunId,
            reason);
        if (Quests.ProcessStageEnded(eventData))
        {
            EventBus.Publish(eventData);
            CurrentStageRunId = null;
        }
        else
        {
            m_PendingEvents.Enqueue(eventData);
        }
    }

    /// <summary>
    /// 긴급 퀘스트 최초 정보 팝업이 열린 동안 기존 로비 보상 연출을 대기시킵니다.
    /// </summary>
    public void SetEmergencyIntroVisible(bool isVisible)
    {
        IsEmergencyIntroVisible = isVisible;
    }

    /// <summary>
    /// 사실 이벤트 저장이 실패하면 순서를 보존한 재시도 큐에 넣습니다.
    /// </summary>
    private void ProcessOrQueue(IGameEvent eventData)
    {
        if (TryProcessEvent(eventData))
        {
            PublishDynamic(eventData);
            return;
        }

        m_PendingEvents.Enqueue(eventData);
    }

    /// <summary>
    /// 현재 큐 길이만큼 순서대로 재시도하여 새 실패가 무한 반복되지 않게 합니다.
    /// </summary>
    private void ProcessPendingEvents()
    {
        int pendingCount = m_PendingEvents.Count;
        for (int i = 0; i < pendingCount; i++)
        {
            IGameEvent eventData = m_PendingEvents.Dequeue();
            if (TryProcessEvent(eventData))
            {
                PublishDynamic(eventData);
            }
            else
            {
                m_PendingEvents.Enqueue(eventData);
            }
        }
    }

    /// <summary>
    /// 사실 이벤트의 구체 타입을 해당 퀘스트 저장 처리로 전달합니다.
    /// </summary>
    private bool TryProcessEvent(IGameEvent eventData)
    {
        if (eventData is BetPlacedEvent placed)
        {
            return Quests.ProcessBetPlaced(placed);
        }

        if (eventData is BetSettledEvent settled)
        {
            return Quests.ProcessBetSettled(settled);
        }

        if (eventData is StageEndedEvent ended)
        {
            return Quests.ProcessStageEnded(ended);
        }

        return true;
    }

    /// <summary>
    /// 저장 확정이 끝난 사실 이벤트만 공통 이벤트 버스에 발행합니다.
    /// </summary>
    private void PublishDynamic(IGameEvent eventData)
    {
        if (eventData is BetPlacedEvent placed)
        {
            EventBus.Publish(placed);
        }
        else if (eventData is BetSettledEvent settled)
        {
            EventBus.Publish(settled);
        }
        else if (eventData is StageEndedEvent ended)
        {
            EventBus.Publish(ended);
            CurrentStageRunId = null;
        }
    }

    /// <summary>
    /// 전역 인스턴스가 파괴될 때 씬 이벤트 구독과 싱글턴을 정리합니다.
    /// </summary>
    private void OnDestroy()
    {
        if (Instance != this)
        {
            return;
        }

        SceneManager.sceneLoaded -= OnSceneLoaded;
        Instance = null;
    }
}
