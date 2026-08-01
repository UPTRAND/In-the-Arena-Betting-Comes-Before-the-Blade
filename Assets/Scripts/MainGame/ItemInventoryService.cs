
using UnityEngine;

namespace InTheArena.MainGame
{
    public class ItemInventoryService
    {
        private readonly SaveManager m_SaveManager;

        public ItemInventoryService(SaveManager saveManager)
        {
            m_SaveManager = saveManager;
        }

        // 로비?�서 구매 �??�용 (SaveManager 직접 조작, ?�자???�보)
        public bool TryBuyItemFromLobby(ItemData itemData)
        {
            if (itemData == null)
            {
                return false;
            }

            int price = itemData.PriceGold;
            if (m_SaveManager.Data.gold >= price)
            {
                m_SaveManager.Data.gold -= price;
                m_SaveManager.Data.itemCounts[(int)itemData.ItemType]++;
                m_SaveManager.Save();
                return true;
            }
            return false;
        }

        public bool TryUseItemFromLobby(ItemData itemData)
        {
            if (itemData == null)
            {
                return false;
            }

            int typeIdx = (int)itemData.ItemType;
            if (m_SaveManager.Data.itemCounts[typeIdx] > 0)
            {
                m_SaveManager.Data.itemCounts[typeIdx]--;
                m_SaveManager.Save();
                return true;
            }
            return false;
        }

        public int GetLobbyItemCount(ItemData itemData)
        {
            if (itemData == null)
            {
                return 0;
            }
            
            return m_SaveManager.Data.itemCounts[(int)itemData.ItemType];
        }

        // ?�테?��? 진행 �?구매 �??�용 (StagePlayerState 캐시 조작)
        public bool TryBuyItemFromStage(ItemData itemData, StagePlayerState playerState)
        {
            if (itemData == null)
            {
                return false;
            }

            if (playerState == null)
            {
                return false;
            }

            int price = itemData.PriceGold;
            if (playerState.Gold >= price)
            {
                playerState.Gold -= price;
                playerState.ItemCounts[(int)itemData.ItemType]++;
                return true;
            }
            return false;
        }

        public bool TryUseItemFromStage(ItemData itemData, StagePlayerState playerState)
        {
            if (itemData == null)
            {
                return false;
            }

            if (playerState == null)
            {
                return false;
            }

            int typeIdx = (int)itemData.ItemType;
            if (playerState.ItemCounts[typeIdx] > 0)
            {
                playerState.ItemCounts[typeIdx]--;
                return true;
            }
            return false;
        }

        public int GetStageItemCount(ItemData itemData, StagePlayerState playerState)
        {
            if (itemData == null)
            {
                return 0;
            }

            if (playerState == null)
            {
                return 0;
            }

            return playerState.ItemCounts[(int)itemData.ItemType];
        }
    }
}

