#if UNITY_6000_0_OR_NEWER
using System.Collections.Generic;

namespace InTheArena.MainGame
{
    /// <summary>
    /// 이번 라운드에서 사용 완료된 아이템 종류만 보관하는 도메인 상태입니다.
    /// UI, 팝업, 타기팅 또는 트랜잭션 수명은 소유하지 않습니다.
    /// </summary>
    public sealed class RoundItemUsageState
    {
        private readonly HashSet<ItemType> m_UsedItemTypes = new HashSet<ItemType>();

        public bool HasUsed(ItemType itemType)
        {
            return itemType != ItemType.None && m_UsedItemTypes.Contains(itemType);
        }

        public bool TryMarkUsed(ItemType itemType)
        {
            if (itemType == ItemType.None)
            {
                return false;
            }

            return m_UsedItemTypes.Add(itemType);
        }

        internal bool TryUnmarkUsed(ItemType itemType)
        {
            if (itemType == ItemType.None)
            {
                return false;
            }

            return m_UsedItemTypes.Remove(itemType);
        }

        public void Reset()
        {
            m_UsedItemTypes.Clear();
        }
    }
}
#endif
