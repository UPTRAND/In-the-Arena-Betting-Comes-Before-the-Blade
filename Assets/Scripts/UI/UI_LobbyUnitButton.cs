using System;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using InTheArena.Unit;
using InTheArena.MainGame;

namespace InTheArena.UI
{
    public sealed class UI_LobbyUnitButton : MonoBehaviour
    {
        [SerializeField] private Button m_Button;
        [SerializeField] private Image m_Icon;
        [SerializeField] private TMP_Text m_NameText;

        private UnitData m_UnitData;
        private Action<UnitData> m_ClickHandler;

        private void Awake()
        {
            if (m_Button != null)
                m_Button.onClick.AddListener(OnClicked);
        }

        public void Bind(UnitData unitData, Action<UnitData> clickHandler)
        {
            m_UnitData = unitData;
            m_ClickHandler = clickHandler;

            if (m_NameText != null)
                m_NameText.text = unitData.DisplayName;
            if (m_Icon != null)
                m_Icon.sprite = unitData.GetPortrait(Team.Blue);
        }

        private void OnClicked()
        {
            if (m_UnitData != null)
                m_ClickHandler?.Invoke(m_UnitData);
        }

        private void OnDestroy()
        {
            if (m_Button != null)
                m_Button.onClick.RemoveListener(OnClicked);
        }
    }
}
