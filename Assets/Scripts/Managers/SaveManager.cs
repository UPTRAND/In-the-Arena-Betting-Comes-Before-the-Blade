using System;
using UnityEngine;
using InTheArena.Save;

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
            Debug.LogError("[SaveManager] ?몄씠釉??곗씠?곌? ?먯긽?섏뿀?듬땲??");
            return;
        }

        if (result.Status == SaveLoadStatus.IoFailure)
        {
            m_State = null;
            Availability = SaveAvailability.IoFailure;
            Debug.LogError("[SaveManager] ?몄씠釉??곗씠??濡쒕뱶 以?IO ?ㅻ쪟媛 諛쒖깮?덉뒿?덈떎.");
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

    public bool TrySpendHeart()
    {
        var refreshResult = RefreshHearts();
        if (refreshResult == HeartRefreshResult.Unavailable || refreshResult == HeartRefreshResult.SaveFailed)
            return false;

        if (m_State == null || m_State.Hearts <= 0 || Availability != SaveAvailability.Ready) return false;

        var copy = m_State.DeepClone();
        if (copy.Hearts == MaxHearts) copy.SetLastHeartRecoveryUtcTicks(m_Clock.UtcNow.Ticks);
        copy.SetHearts(copy.Hearts - 1);

        if (TrySave(copy, out _))
        {
            m_State = copy;
            return true;
        }
        return false;
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
            return true;
        }
        return false;
    }

    public override void OnApplicationPauseChanged(bool paused) { if (paused) Save(); }
    public override void Release() { Save(); if (Instance == this) Instance = null; base.Release(); }

#if UNITY_EDITOR
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
