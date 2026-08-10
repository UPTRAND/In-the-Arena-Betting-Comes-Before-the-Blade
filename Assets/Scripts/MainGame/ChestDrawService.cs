#if UNITY_6000_0_OR_NEWER
using System;
using System.Collections.Generic;

namespace InTheArena.MainGame
{
    public enum ChestTier { Bronze, Silver, Gold }

    public readonly struct ChestReward
    {
        public ChestReward(ChestTier tier, ItemData item, int amount) { Tier = tier; Item = item; Amount = amount; }
        public ChestTier Tier { get; }
        public ItemData Item { get; }
        public int Amount { get; }
    }

    public interface IChestRandom { float Next01(); int Range(int maxExclusive); }
    public sealed class UnityChestRandom : IChestRandom
    {
        public float Next01() => UnityEngine.Random.value;
        public int Range(int maxExclusive) => UnityEngine.Random.Range(0, maxExclusive);
    }

    public static class ChestDrawService
    {
        public static bool TryDraw(IReadOnlyList<ItemData> items, IChestRandom random, out ChestReward reward)
        {
            reward = default;
            if (items == null || items.Count == 0 || random == null) return false;
            var valid = new List<ItemData>();
            for (int i = 0; i < items.Count; i++)
                if (items[i] != null && items[i].ItemType != ItemType.None) valid.Add(items[i]);
            if (valid.Count == 0) return false;

            float roll = random.Next01();
            ChestTier tier = roll < .5f ? ChestTier.Bronze : roll < .8f ? ChestTier.Silver : ChestTier.Gold;
            reward = new ChestReward(tier, valid[random.Range(valid.Count)], (int)tier + 1);
            return true;
        }
    }
}
#endif
