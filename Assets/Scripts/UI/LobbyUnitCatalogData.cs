using System;
using System.Collections.Generic;
using UnityEngine;
using InTheArena.Unit;

namespace InTheArena.UI
{
    [Serializable]
    public sealed class LobbyUnitRegion
    {
        [SerializeField] private string m_DisplayName;
        [SerializeField] private List<UnitData> m_Units;

        public string DisplayName => m_DisplayName;
        public IReadOnlyList<UnitData> Units => m_Units;
    }

    [CreateAssetMenu(fileName = "LobbyUnitCatalog", menuName = "In The Arena/UI/Lobby Unit Catalog")]
    public sealed class LobbyUnitCatalogData : ScriptableObject
    {
        [SerializeField] private List<LobbyUnitRegion> m_Regions;

        public IReadOnlyList<LobbyUnitRegion> Regions => m_Regions;
    }
}
