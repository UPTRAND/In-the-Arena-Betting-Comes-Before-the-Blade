#if UNITY_6000_0_OR_NEWER
using System.Collections;
using InTheArena.UI;
using UnityEngine;

public sealed class LobbyUIFlowController : MonoBehaviour
{
    private UI_Base m_CurrentPanel;
    private UI_LobbyNavigationBar m_Navigation;
    private System.Action<LobbyTab> m_TabSelectedHandler;

    private IEnumerator Start()
    {
        yield return null;

        UI_LobbyHeader header = UIManager.Instance?.GetElement<UI_LobbyHeader>() ?? FindFirstObjectByType<UI_LobbyHeader>(FindObjectsInactive.Include);
        m_Navigation = UIManager.Instance?.GetElement<UI_LobbyNavigationBar>() ?? FindFirstObjectByType<UI_LobbyNavigationBar>(FindObjectsInactive.Include);
        UI_LobbyStagePanel stage = UIManager.Instance?.GetElement<UI_LobbyStagePanel>() ?? FindFirstObjectByType<UI_LobbyStagePanel>(FindObjectsInactive.Include);
        UI_LobbyUnitPanel units = UIManager.Instance?.GetElement<UI_LobbyUnitPanel>() ?? FindFirstObjectByType<UI_LobbyUnitPanel>(FindObjectsInactive.Include);
        UI_LobbySocialPanel social = UIManager.Instance?.GetElement<UI_LobbySocialPanel>() ?? FindFirstObjectByType<UI_LobbySocialPanel>(FindObjectsInactive.Include);

        header?.Open();
        yield return null;

        m_Navigation?.Open();
        yield return null;

        m_Navigation?.SetSelected(LobbyTab.Stage);

        if (m_Navigation != null)
        {
            m_TabSelectedHandler = tab => Show(tab == LobbyTab.Stage ? (UI_Base)stage : tab == LobbyTab.Units ? units : social);
            m_Navigation.TabSelected -= m_TabSelectedHandler;
            m_Navigation.TabSelected += m_TabSelectedHandler;
        }

        Show(stage);
    }

    private void OnDestroy()
    {
        if (m_Navigation != null && m_TabSelectedHandler != null)
        {
            m_Navigation.TabSelected -= m_TabSelectedHandler;
        }
    }

    private void Show(UI_Base panel)
    {
        if (panel == null || panel == m_CurrentPanel) return;
        m_CurrentPanel?.Close();
        panel.Open();
        m_CurrentPanel = panel;
    }
}
#endif
