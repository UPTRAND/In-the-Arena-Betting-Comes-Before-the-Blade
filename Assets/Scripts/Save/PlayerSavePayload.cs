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
        public long lastHeartRecoveryUtcTicks;
    }
}
