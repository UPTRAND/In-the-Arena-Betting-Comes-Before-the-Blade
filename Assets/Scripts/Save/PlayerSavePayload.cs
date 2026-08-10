using System;

namespace InTheArena.Save
{
    [Serializable]
    public sealed class PlayerSavePayload
    {
        public int clearedStageNumber;
        public int gold;
        public int hearts;
        public int stars;
        public int selectedStageDifficulty;
        public long lastHeartRecoveryUtcTicks;
        public ItemCountPayload[] itemCounts;
    }

    [Serializable]
    public sealed class ItemCountPayload
    {
        public int itemType;
        public int count;
    }
}
