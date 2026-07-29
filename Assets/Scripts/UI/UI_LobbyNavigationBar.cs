#if UNITY_6000_0_OR_NEWER
using System;
using UnityEngine;
using UnityEngine.UI;

namespace InTheArena.UI
{
    public enum LobbyTab { Units, Stage, Social }
    public sealed class UI_LobbyNavigationBar : UI_Base
    {
        [SerializeField] private Button m_UnitsButton;
        [SerializeField] private Button m_StageButton;
        [SerializeField] private Button m_SocialButton;
        public event Action<LobbyTab> TabSelected;
        protected override void Awake() { base.Awake(); m_UnitsButton.onClick.AddListener(() => Select(LobbyTab.Units)); m_StageButton.onClick.AddListener(() => Select(LobbyTab.Stage)); m_SocialButton.onClick.AddListener(() => Select(LobbyTab.Social)); }
        public void Select(LobbyTab tab) { SetSelected(tab); TabSelected?.Invoke(tab); }
        public void SetSelected(LobbyTab tab) { m_UnitsButton.interactable = tab != LobbyTab.Units; m_StageButton.interactable = tab != LobbyTab.Stage; m_SocialButton.interactable = tab != LobbyTab.Social; }
    }
}
#endif
