#if UNITY_6000_0_OR_NEWER
using System;
using UnityEngine;

namespace InTheArena.MainGame
{
    public enum ItemCategory
    {
        Betting,
        Combat
    }

    public enum ItemType
    {
        None = 0,
        // 배팅 아이템
        AdditionalBetTicket = 1,
        Insurance = 2,
        RerollTicket = 3,
        // 전투 아이템
        Meteor = 11,
        Mercenary = 12,
        TimeExtension = 13
    }

    [CreateAssetMenu(fileName = "New Item Data", menuName = "InTheArena/Item Data")]
    public class ItemData : ScriptableObject
    {
        [SerializeField] private ItemType m_ItemType;
        [SerializeField] private ItemCategory m_Category;
        [SerializeField] private string m_ItemName;
        [SerializeField] private int m_PriceGold;
        [SerializeField] private Sprite m_Icon;
        [TextArea]
        [SerializeField] private string m_Description;

        public ItemType ItemType 
        { 
            get 
            { 
                return m_ItemType; 
            } 
        }

        public ItemCategory Category 
        { 
            get 
            { 
                return m_Category; 
            } 
        }

        public string ItemName 
        { 
            get 
            { 
                return m_ItemName; 
            } 
        }

        public int PriceGold 
        { 
            get 
            { 
                return m_PriceGold; 
            } 
        }

        public Sprite Icon 
        { 
            get 
            { 
                return m_Icon; 
            } 
        }

        public string Description 
        { 
            get 
            { 
                return m_Description; 
            } 
        }
    }
}
#endif
