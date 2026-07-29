using System;
using UnityEngine;

[Serializable]
public sealed class PlayerData
{
    public int clearedStageNumber;
    public int gold;
    public int hearts;
    public int stars;
    public long lastHeartRecoveryUtcTicks;
}

[DisallowMultipleComponent]
public class SaveManager : Manager_Base
{
    private const string PlayerDataKey = "InTheArena.PlayerData.v1";
    public const int MaxHearts = 5;
    public const int HeartRecoverySeconds = 300;
    public static SaveManager Instance { get; private set; }
    [SerializeField] private int m_DefaultClearedStageNumber;
    [SerializeField] private int m_DefaultGold;
    [SerializeField] private int m_DefaultHearts = MaxHearts;
    [SerializeField] private int m_DefaultStars;
    [SerializeField] private ushort m_InitializationOrder = 5;
    public override ushort InitializationOrder => m_InitializationOrder;
    public PlayerData Data { get; private set; }

    private void Awake() { if (Instance != null && Instance != this) { Destroy(gameObject); return; } Instance = this; }
    protected override bool Init() { Load(); RefreshHearts(); return true; }

    public void Load()
    {
        if (PlayerPrefs.HasKey(PlayerDataKey)) Data = JsonUtility.FromJson<PlayerData>(PlayerPrefs.GetString(PlayerDataKey));
        if (Data == null)
        {
            Data = new PlayerData { clearedStageNumber = Mathf.Max(0, m_DefaultClearedStageNumber), gold = Mathf.Max(0, m_DefaultGold), hearts = Mathf.Clamp(m_DefaultHearts, 0, MaxHearts), stars = Mathf.Max(0, m_DefaultStars), lastHeartRecoveryUtcTicks = DateTime.UtcNow.Ticks };
            Save();
        }
        Data.hearts = Mathf.Clamp(Data.hearts, 0, MaxHearts);
    }

    public void Save() { if (Data == null) return; PlayerPrefs.SetString(PlayerDataKey, JsonUtility.ToJson(Data)); PlayerPrefs.Save(); }

    public bool RefreshHearts()
    {
        if (Data == null) return false;
        DateTime now = DateTime.UtcNow;
        if (Data.hearts >= MaxHearts) { Data.lastHeartRecoveryUtcTicks = now.Ticks; return false; }
        DateTime last = new DateTime(Data.lastHeartRecoveryUtcTicks <= 0 ? now.Ticks : Data.lastHeartRecoveryUtcTicks, DateTimeKind.Utc);
        int recovered = Mathf.FloorToInt((float)(Math.Max(0, (now - last).TotalSeconds) / HeartRecoverySeconds));
        if (recovered <= 0) return false;
        Data.hearts = Mathf.Min(MaxHearts, Data.hearts + recovered);
        Data.lastHeartRecoveryUtcTicks = Data.hearts == MaxHearts ? now.Ticks : last.AddSeconds(recovered * HeartRecoverySeconds).Ticks;
        Save(); return true;
    }

    public TimeSpan GetRemainingHeartTime()
    {
        RefreshHearts();
        if (Data == null || Data.hearts >= MaxHearts) return TimeSpan.Zero;
        DateTime last = new DateTime(Data.lastHeartRecoveryUtcTicks, DateTimeKind.Utc);
        return TimeSpan.FromSeconds(Mathf.Clamp(HeartRecoverySeconds - (float)Math.Max(0, (DateTime.UtcNow - last).TotalSeconds), 0, HeartRecoverySeconds));
    }

    public bool TrySpendHeart()
    {
        RefreshHearts();
        if (Data == null || Data.hearts <= 0) return false;
        if (Data.hearts == MaxHearts) Data.lastHeartRecoveryUtcTicks = DateTime.UtcNow.Ticks;
        Data.hearts--; Save(); return true;
    }

    public void GrantStageClearReward(int stageNumber, int goldReward = 100)
    {
        if (Data == null) return;
        Data.clearedStageNumber = Mathf.Max(Data.clearedStageNumber, stageNumber);
        Data.stars++;
        Data.gold = Mathf.Max(0, Data.gold + goldReward);
        Save();
    }

    public override void OnApplicationPauseChanged(bool paused) { if (paused) Save(); }
    public override void Release() { Save(); if (Instance == this) Instance = null; base.Release(); }
}