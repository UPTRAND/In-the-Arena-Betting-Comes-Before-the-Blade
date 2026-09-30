using System;
using UnityEngine;
using InTheArena.Save;
using InTheArena.MainGame;

public enum HeartRefreshResult
{
    NoChange,
    Committed,
    SaveFailed,
    Unavailable
}

public enum SaveAvailability
{
    Ready,
    UnsupportedFutureVersion,
    Corrupted,
    IoFailure
}

[DisallowMultipleComponent]
public class SaveManager : Manager_Base
{
    public const int MaxHearts = 5;
    public const int HeartRecoverySeconds = 300;
    public static SaveManager Instance { get; private set; }

    public event Action StateChanged;

    [SerializeField] private int m_DefaultClearedStageNumber;
    [SerializeField] private int m_DefaultGold;
    [SerializeField] private int m_DefaultHearts = MaxHearts;
    [SerializeField] private int m_DefaultStars;
    [SerializeField] private ushort m_InitializationOrder = 5;

    public override ushort InitializationOrder => m_InitializationOrder;

    private IPlayerSaveRepository m_Repository;
    private PlayerProgressState m_State;
    private IClock m_Clock;

    private bool m_IsReadOnly = false;
    private bool m_IsTestInitialized = false;

    public SaveAvailability Availability { get; private set; } = SaveAvailability.Ready;

    public int Gold => m_State?.Gold ?? 0;
    public int Hearts => m_State?.Hearts ?? 0;
    public int Stars => m_State?.Stars ?? 0;
    public int ClearedStageNumber => m_State?.ClearedStageNumber ?? 0;
    public int GetItemCount(ItemType itemType) => m_State?.GetItemCount(itemType) ?? 0;

    public long FreePassExpirationUtcTicks => m_State?.FreePassExpirationUtcTicks ?? 0;
    public DateTime UtcNow => m_Clock != null ? m_Clock.UtcNow : DateTime.UtcNow;

    public bool HasActiveFreePass
    {
        get
        {
            return GetRemainingFreePassTime() > TimeSpan.Zero;
        }
    }

    private bool m_IsSaving = false;

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;
    }

    internal void InitializeForTests(IPlayerSaveRepository repository, IClock clock, PlayerProgressState initialState, bool isReadOnly = false)
    {
        m_Repository = repository;
        m_Clock = clock;
        m_State = initialState;
        m_IsReadOnly = isReadOnly;
        Availability = isReadOnly ? SaveAvailability.UnsupportedFutureVersion : SaveAvailability.Ready;
        m_IsTestInitialized = true;
    }

    protected override bool Init()
    {
        if (!m_IsTestInitialized)
        {
            m_Clock = new SystemClock();
            Load();
        }

        RefreshHearts();
        return Availability == SaveAvailability.Ready;
    }

    public void Load()
    {
        Debug.Log($"[SaveManager.Load] ENTER. m_IsTestInitialized = {m_IsTestInitialized}");
        if (!m_IsTestInitialized)
        {
            Debug.Log("[SaveManager.Load] INSIDE IF BLOCK!");
            string saveDir = System.IO.Path.Combine(Application.persistentDataPath, "Save");
            m_Repository = new PlayerSaveRepository(saveDir, "player-data.json", m_Clock);
        }

        var defaultCandidate = new PlayerProgressState();
        defaultCandidate.SetClearedStageNumber(Mathf.Max(0, m_DefaultClearedStageNumber));
        defaultCandidate.SetGold(Mathf.Max(0, m_DefaultGold));
        defaultCandidate.SetHearts(Mathf.Clamp(m_DefaultHearts, 0, MaxHearts));
        defaultCandidate.SetStars(Mathf.Max(0, m_DefaultStars));
        defaultCandidate.SetLastHeartRecoveryUtcTicks(m_Clock.UtcNow.Ticks);
        defaultCandidate.SetCreatedWithSchemaVersion(PlayerSaveValidator.CurrentSchemaVersion);

        var result = m_Repository.LoadOrCreate(defaultCandidate);

        if (result.Status == SaveLoadStatus.UnsupportedFutureVersion)
        {
            m_IsReadOnly = true;
            m_State = null;
            Availability = SaveAvailability.UnsupportedFutureVersion;
            Debug.LogError("[SaveManager] 誘몃옒 踰꾩쟾 ?몄씠釉뚯엯?덈떎. ?쎄린 ?꾩슜(濡쒕뱶 遺덇?) 紐⑤뱶濡??꾪솚?⑸땲??");
            return;
        }

        if (result.Status == SaveLoadStatus.Corrupted)
        {
            m_State = null;
            Availability = SaveAvailability.Corrupted;
            Debug.LogError("[SaveManager] 세이브 데이터가 손상되었습니다.");
            return;
        }

        if (result.Status == SaveLoadStatus.IoFailure)
        {
            m_State = null;
            Availability = SaveAvailability.IoFailure;
            Debug.LogError("[SaveManager] 세이브 데이터 로드 중 IO 오류가 발생했습니다.");
            return;
        }

        if (result.Status == SaveLoadStatus.MigratedWithMarkerWarning)
        {
            Debug.LogWarning(result.Warning);
        }

        m_State = result.State;
        Availability = SaveAvailability.Ready;
    }

    public bool TrySave(PlayerProgressState candidate, out string error)
    {
        error = null;
        if (m_IsReadOnly || Availability != SaveAvailability.Ready)
        {
            error = "Save data is read-only, unavailable, or from a newer schema version.";
            return false;
        }
        if (m_Repository == null || candidate == null)
        {
            error = "Repository or state is null.";
            return false;
        }

        if (m_IsSaving)
        {
            error = "Already saving.";
            return false;
        }

        m_IsSaving = true;
        try
        {
            bool success = m_Repository.TrySave(candidate, out error);
            return success;
        }
        finally { m_IsSaving = false; }
    }

    /// <summary>
    /// 최신 상태에서 만든 후보를 원자적으로 저장하고 성공한 경우에만 현재 상태를 교체합니다.
    /// </summary>
    public bool TryCommitCandidate(PlayerProgressState candidate, out string error)
    {
        if (!TrySave(candidate, out error))
        {
            return false;
        }

        m_State = candidate.DeepClone();
        StateChanged?.Invoke();
        return true;
    }

    /// <summary>
    /// 외부 서비스가 안전하게 변경할 수 있는 최신 저장 상태 복사본을 반환합니다.
    /// </summary>
    public PlayerProgressState CreateSnapshot()
    {
        if (m_State == null || Availability != SaveAvailability.Ready)
        {
            return null;
        }

        return m_State.DeepClone();
    }

    public void Save()
    {
        if (m_State == null || m_IsReadOnly || Availability != SaveAvailability.Ready) return;
        TrySave(m_State, out _);
    }

    public HeartRefreshResult RefreshHearts()
    {
        if (m_IsReadOnly || m_State == null || Availability != SaveAvailability.Ready) return HeartRefreshResult.Unavailable;
        DateTime now = m_Clock.UtcNow;
        if (m_State.Hearts >= MaxHearts)
        {
            return HeartRefreshResult.NoChange;
        }

        DateTime last = new DateTime(m_State.LastHeartRecoveryUtcTicks <= 0 ? now.Ticks : m_State.LastHeartRecoveryUtcTicks, DateTimeKind.Utc);
        int recovered = Mathf.FloorToInt((float)(Math.Max(0, (now - last).TotalSeconds) / HeartRecoverySeconds));
        if (recovered <= 0) return HeartRefreshResult.NoChange;

        var copy = m_State.DeepClone();
        copy.SetHearts(Mathf.Min(MaxHearts, copy.Hearts + recovered));
        copy.SetLastHeartRecoveryUtcTicks(copy.Hearts == MaxHearts ? now.Ticks : last.AddSeconds(recovered * HeartRecoverySeconds).Ticks);

        if (TrySave(copy, out _))
        {
            m_State = copy;
            StateChanged?.Invoke();
            return HeartRefreshResult.Committed;
        }
        return HeartRefreshResult.SaveFailed;
    }

    public TimeSpan GetRemainingHeartTime()
    {
        RefreshHearts();
        if (m_State == null || m_State.Hearts >= MaxHearts || Availability != SaveAvailability.Ready) return TimeSpan.Zero;
        DateTime last = new DateTime(m_State.LastHeartRecoveryUtcTicks, DateTimeKind.Utc);
        return TimeSpan.FromSeconds(Mathf.Clamp(HeartRecoverySeconds - (float)Math.Max(0, (m_Clock.UtcNow - last).TotalSeconds), 0, HeartRecoverySeconds));
    }

    /// <summary>
    /// 현재 UTC 시각을 기준으로 자유 이용권의 남은 시간을 반환합니다.
    /// </summary>
    public TimeSpan GetRemainingFreePassTime()
    {
        if (m_State == null || m_Clock == null || Availability != SaveAvailability.Ready)
        {
            return TimeSpan.Zero;
        }

        long expirationTicks = m_State.FreePassExpirationUtcTicks;
        long nowTicks = m_Clock.UtcNow.Ticks;
        long remainingTicks = expirationTicks - nowTicks;

        if (remainingTicks <= 0)
        {
            return TimeSpan.Zero;
        }

        return TimeSpan.FromTicks(remainingTicks);
    }

    /// <summary>
    /// 로비 아이템의 종류를 확인하고 해당 효과를 적용합니다.
    /// </summary>
    public bool TryUseLobbyItem(ItemData itemData, out string error)
    {
        error = null;

        if (itemData == null || itemData.Category != ItemCategory.Lobby)
        {
            error = "A lobby item is required.";
            return false;
        }

        if (itemData.ItemType == ItemType.FreePass)
        {
            return TryActivateFreePass(itemData.EffectDurationSeconds, true, out error);
        }

        error = "The lobby item type is not supported.";
        return false;
    }

    /// <summary>
    /// 스테이지 입장권을 소비합니다. 자유 이용권이 활성 상태이면 입장권 수량을 유지합니다.
    /// </summary>
    public bool TrySpendHeart()
    {
        var refreshResult = RefreshHearts();
        if (refreshResult == HeartRefreshResult.Unavailable || refreshResult == HeartRefreshResult.SaveFailed)
        {
            return false;
        }

        if (m_State == null || Availability != SaveAvailability.Ready)
        {
            return false;
        }

        if (HasActiveFreePass)
        {
            return true;
        }

        if (m_State.Hearts <= 0)
        {
            return false;
        }

        var copy = m_State.DeepClone();
        if (copy.Hearts == MaxHearts)
        {
            copy.SetLastHeartRecoveryUtcTicks(m_Clock.UtcNow.Ticks);
        }

        copy.SetHearts(copy.Hearts - 1);

        if (TrySave(copy, out _))
        {
            m_State = copy;
            StateChanged?.Invoke();
            return true;
        }
        return false;
    }

    /// <summary>
    /// 자유 이용권 만료 시각을 연장하고, 일반 사용인 경우 보유 아이템 한 개를 함께 소비합니다.
    /// </summary>
    private bool TryActivateFreePass(int durationSeconds, bool spendItem, out string error)
    {
        error = null;

        if (m_IsReadOnly || m_State == null || m_Clock == null || Availability != SaveAvailability.Ready)
        {
            error = "Save data is unavailable.";
            return false;
        }

        if (durationSeconds <= 0)
        {
            error = "Free pass duration must be greater than zero.";
            return false;
        }

        if (spendItem && m_State.GetItemCount(ItemType.FreePass) <= 0)
        {
            error = "Free pass is unavailable.";
            return false;
        }

        long durationTicks = TimeSpan.FromSeconds(durationSeconds).Ticks;
        long nowTicks = m_Clock.UtcNow.Ticks;
        long activationBaseTicks = Math.Max(nowTicks, m_State.FreePassExpirationUtcTicks);

        if (activationBaseTicks > DateTime.MaxValue.Ticks - durationTicks)
        {
            error = "Free pass expiration exceeds the supported date range.";
            return false;
        }

        PlayerProgressState copy = m_State.DeepClone();
        copy.SetFreePassExpirationUtcTicks(activationBaseTicks + durationTicks);

        if (spendItem)
        {
            int remainingCount = copy.GetItemCount(ItemType.FreePass) - 1;
            copy.SetItemCount(ItemType.FreePass, remainingCount);
        }

        if (!TrySave(copy, out error))
        {
            return false;
        }

        m_State = copy;
        return true;
    }

    public bool TryOpenChest(ItemType itemType, int amount, out string error)
    {
        error = null;
        if (m_IsReadOnly || m_State == null || Availability != SaveAvailability.Ready || itemType == ItemType.None || amount <= 0)
        {
            error = "Save data or reward is unavailable.";
            return false;
        }
        const int chestCost = 3;
        if (m_State.Stars < chestCost)
        {
            error = "Not enough stars.";
            return false;
        }
        PlayerProgressState copy = m_State.DeepClone();
        copy.SetStars(copy.Stars - chestCost);
        copy.SetItemCount(itemType, copy.GetItemCount(itemType) + amount);
        if (!TrySave(copy, out error)) return false;
        m_State = copy;
        return true;
    }

    public bool TrySpendItem(ItemType itemType, out string error)
    {
        error = null;
        if (m_IsReadOnly || m_State == null || Availability != SaveAvailability.Ready || m_State.GetItemCount(itemType) <= 0)
        {
            error = "Item is unavailable.";
            return false;
        }
        PlayerProgressState copy = m_State.DeepClone();
        copy.SetItemCount(itemType, copy.GetItemCount(itemType) - 1);
        if (!TrySave(copy, out error)) return false;
        m_State = copy;
        return true;
    }

    public bool TryAddItem(ItemType itemType, int amount, out string error)
    {
        error = null;
        if (m_IsReadOnly || m_State == null || Availability != SaveAvailability.Ready ||
            itemType == ItemType.None || amount <= 0)
        {
            error = "Item grant is unavailable.";
            return false;
        }

        int currentCount = m_State.GetItemCount(itemType);
        if (currentCount > int.MaxValue - amount)
        {
            error = "Item count exceeds the supported limit.";
            return false;
        }

        PlayerProgressState copy = m_State.DeepClone();
        copy.SetItemCount(itemType, currentCount + amount);
        if (!TrySave(copy, out error))
        {
            return false;
        }

        m_State = copy;
        return true;
    }

    public bool TryPurchaseItem(ItemType itemType, int priceGold, out string error)
    {
        error = null;
        if (m_IsReadOnly || m_State == null || Availability != SaveAvailability.Ready ||
            itemType == ItemType.None || priceGold < 0)
        {
            error = "Item purchase is unavailable.";
            return false;
        }

        if (m_State.Gold < priceGold)
        {
            error = "Not enough gold.";
            return false;
        }

        PlayerProgressState copy = m_State.DeepClone();
        copy.SetGold(copy.Gold - priceGold);
        copy.SetItemCount(itemType, copy.GetItemCount(itemType) + 1);
        if (!TrySave(copy, out error)) return false;

        m_State = copy;
        return true;
    }


    public PlayerProgressState CreatePendingStageClearCandidate(InTheArena.MainGame.StagePlayerState stageState, int stageNumber, int goldReward, int starReward)
    {
        if (m_IsReadOnly || m_State == null || Availability != SaveAvailability.Ready)
        {
            return null;
        }

        var copy = m_State.DeepClone();
        copy.SetClearedStageNumber(Mathf.Max(copy.ClearedStageNumber, stageNumber));
        copy.SetStars(copy.Stars + starReward);
        copy.SetGold(Mathf.Max(0, stageState.Gold + goldReward));
        return copy;
    }

    public bool TryCommitPendingStageClear(PlayerProgressState candidate, out string error)
    {
        error = null;
        if (m_IsReadOnly || m_State == null || Availability != SaveAvailability.Ready || candidate == null)
        {
            error = "Save data is read-only, unavailable, or candidate is null.";
            return false;
        }

        if (TrySave(candidate, out error))
        {
            m_State = candidate.DeepClone();
            StateChanged?.Invoke();
            return true;
        }
        return false;
    }

    public override void OnApplicationPauseChanged(bool paused) { if (paused) Save(); }
    public override void Release()
    {
        Save();
        StateChanged = null;
        if (Instance == this)
        {
            Instance = null;
        }
        base.Release();
    }

#if UNITY_EDITOR
    /// <summary>
    /// 디버그 도구에서 아이템 수량을 소비하지 않고 자유 이용권을 활성화합니다.
    /// </summary>
    public bool DebugTryActivateFreePass(int durationSeconds, out string error)
    {
        return TryActivateFreePass(durationSeconds, false, out error);
    }

    public bool DebugTryModifyState(Action<PlayerProgressState> modifier, out string error)
    {
        error = null;

        if (m_State == null || Availability != SaveAvailability.Ready)
        {
            error = "Save state is unavailable.";
            return false;
        }

        PlayerProgressState candidate = m_State.DeepClone();
        modifier?.Invoke(candidate);

        if (!TrySave(candidate, out error))
        {
            return false;
        }

        m_State = candidate;
        return true;
    }
#endif
}
