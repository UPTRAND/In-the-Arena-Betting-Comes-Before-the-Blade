using InTheArena.Unit;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace InTheArena.UI
{
    public sealed class UI_LobbyUnitPanel : UI_Base
    {
        [Header("Data")]
        [SerializeField] private LobbyUnitCatalogData m_CatalogData;

        [Header("Prefabs")]
        [SerializeField] private UI_LobbyUnitRegionSection m_RegionSectionPrefab;
        [SerializeField] private UI_LobbyUnitButton m_UnitButtonPrefab;

        [Header("UI Reference")]
        [SerializeField] private RectTransform m_ScrollContent;

        private UI_UnitDescription_Popup m_DescriptionPopup;
        private bool m_IsBuilt;

        protected override void Awake()
        {
            base.Awake();
            EnsureBuilt();
        }

        public override void OnOpened()
        {
            base.OnOpened();

            m_DescriptionPopup ??= UIManager.Instance?.GetElement<UI_UnitDescription_Popup>();
        }

        private void EnsureBuilt()
        {
            if (m_IsBuilt) return;

            if (m_CatalogData == null || m_RegionSectionPrefab == null || m_UnitButtonPrefab == null || m_ScrollContent == null)
            {
                Debug.LogError("[UI_LobbyUnitPanel] Required reference is missing.", this);
                return;
            }

            foreach (var region in m_CatalogData.Regions)
            {
                var section = Instantiate(m_RegionSectionPrefab, m_ScrollContent);
                section.Bind(region.DisplayName);

                foreach (UnitData unitData in region.Units)
                {
                    if (unitData == null) continue;
                    
                    var button = Instantiate(m_UnitButtonPrefab, section.UnitRoot);
                    button.Bind(unitData, OnUnitSelected);
                }
            }

            m_IsBuilt = true;
        }

        private void OnUnitSelected(UnitData data)
        {
            if (data == null)
                return;

            m_DescriptionPopup ??= UIManager.Instance?.GetElement<UI_UnitDescription_Popup>();
            m_DescriptionPopup?.Show(data);
        }
    }
}