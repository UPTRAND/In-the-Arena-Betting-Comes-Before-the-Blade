using System;
using System.Runtime.CompilerServices;

[assembly: InternalsVisibleTo("Assembly-CSharp-Editor")]

namespace InTheArena.Save
{
    // Legacy PlayerData (V1)
    [Serializable]
    public sealed class LegacyPlayerDataV1
    {
        public int clearedStageNumber;
        public int gold;
        public int hearts;
        public int stars;
        public long lastHeartRecoveryUtcTicks;
        public int[] itemCounts;
    }
}
