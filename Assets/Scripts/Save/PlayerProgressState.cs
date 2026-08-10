using System;
using System.Collections.Generic;
using InTheArena.MainGame;

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
        private readonly Dictionary<ItemType, int> m_ItemCounts = new Dictionary<ItemType, int>();

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
            foreach (var pair in other.m_ItemCounts) m_ItemCounts[pair.Key] = pair.Value;
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
            m_ItemCounts.Clear();
            if (payload.itemCounts == null) return;
            foreach (ItemCountPayload entry in payload.itemCounts)
            {
                if (entry == null || !Enum.IsDefined(typeof(ItemType), entry.itemType)) continue;
                ItemType type = (ItemType)entry.itemType;
                if (type != ItemType.None && entry.count > 0) m_ItemCounts[type] = entry.count;
            }
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
                ,itemCounts = ToItemCountPayload()
            };
        }

        public void SetClearedStageNumber(int val) => ClearedStageNumber = val;
        public void SetGold(int val) => Gold = val;
        public void SetHearts(int val) => Hearts = val;
        public void SetStars(int val) => Stars = val;
        public void SetSelectedStageDifficulty(int val) => SelectedStageDifficulty = val;
        public void SetLastHeartRecoveryUtcTicks(long val) => LastHeartRecoveryUtcTicks = val;
        public int GetItemCount(ItemType type) => type == ItemType.None || !m_ItemCounts.TryGetValue(type, out int count) ? 0 : count;
        public void SetItemCount(ItemType type, int count)
        {
            if (type == ItemType.None) return;
            if (count <= 0) m_ItemCounts.Remove(type); else m_ItemCounts[type] = count;
        }

        private ItemCountPayload[] ToItemCountPayload()
        {
            var entries = new List<ItemCountPayload>();
            foreach (var pair in m_ItemCounts)
                if (pair.Value > 0) entries.Add(new ItemCountPayload { itemType = (int)pair.Key, count = pair.Value });
            return entries.ToArray();
        }
    }
}
