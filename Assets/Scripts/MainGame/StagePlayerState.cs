
using System;

namespace InTheArena.MainGame
{
    public class StagePlayerState
    {
        public int Gold { get; set; }
        public int[] ItemCounts { get; private set; }

        public StagePlayerState()
        {
            ItemCounts = new int[20];
        }

        public void CopyFrom(PlayerData data)
        {
            if (data != null)
            {
                Gold = data.gold;
                if (data.itemCounts != null)
                {
                    Array.Copy(data.itemCounts, ItemCounts, data.itemCounts.Length);
                }
            }
        }
        
        public void ApplyTo(PlayerData data)
        {
            if (data != null)
            {
                data.gold = Gold;
                if (data.itemCounts != null)
                {
                    Array.Copy(ItemCounts, data.itemCounts, ItemCounts.Length);
                }
            }
        }
    }
}

