using System;

namespace InTheArena.Save
{
    public sealed class PlayerProgressState
    {
        public int ClearedStageNumber { get; private set; }
        public int Gold { get; private set; }
        public int Hearts { get; private set; }
        public int Stars { get; private set; }
        public int SelectedStageDifficulty { get; private set; }
        public long LastHeartRecoveryUtcTicks { get; private set; }

        public PlayerProgressState()
        {
        }

        private PlayerProgressState(PlayerProgressState other)
        {
            ClearedStageNumber = other.ClearedStageNumber;
            Gold = other.Gold;
            Hearts = other.Hearts;
            Stars = other.Stars;
            SelectedStageDifficulty = other.SelectedStageDifficulty;
            LastHeartRecoveryUtcTicks = other.LastHeartRecoveryUtcTicks;
        }

        public PlayerProgressState DeepClone()
        {
            return new PlayerProgressState(this);
        }

        public void CopyFromPayload(PlayerSavePayload payload)
        {
            if (payload == null) return;
            ClearedStageNumber = payload.clearedStageNumber;
            Gold = payload.gold;
            Hearts = payload.hearts;
            Stars = payload.stars;
            SelectedStageDifficulty = payload.selectedStageDifficulty;
            LastHeartRecoveryUtcTicks = payload.lastHeartRecoveryUtcTicks;
        }

        public PlayerSavePayload ToPayload()
        {
            return new PlayerSavePayload
            {
                clearedStageNumber = ClearedStageNumber,
                gold = Gold,
                hearts = Hearts,
                stars = Stars,
                selectedStageDifficulty = SelectedStageDifficulty,
                lastHeartRecoveryUtcTicks = LastHeartRecoveryUtcTicks
            };
        }

        public void SetClearedStageNumber(int val) => ClearedStageNumber = val;
        public void SetGold(int val) => Gold = val;
        public void SetHearts(int val) => Hearts = val;
        public void SetStars(int val) => Stars = val;
        public void SetSelectedStageDifficulty(int val) => SelectedStageDifficulty = val;
        public void SetLastHeartRecoveryUtcTicks(long val) => LastHeartRecoveryUtcTicks = val;
    }
}
