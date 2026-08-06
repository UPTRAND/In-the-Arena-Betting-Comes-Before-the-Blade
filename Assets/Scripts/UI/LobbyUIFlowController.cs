#if UNITY_6000_0_OR_NEWER
using System.Collections;
using InTheArena.UI;
using UnityEngine;

public sealed class LobbyUIFlowController : MonoBehaviour
{
    private UI_Base m_CurrentPanel;
    private GameObject m_CurrentPanelObject;
    private UI_LobbyNavigationBar m_Navigation;
    private System.Action<LobbyTab> m_TabSelectedHandler;

    private IEnumerator Start()
    {
        yield return null;

        UI_LobbyHeader header = UIManager.Instance?.GetElement<UI_LobbyHeader>() ?? FindAnyObjectByType<UI_LobbyHeader>(FindObjectsInactive.Include);
        m_Navigation = UIManager.Instance?.GetElement<UI_LobbyNavigationBar>() ?? FindAnyObjectByType<UI_LobbyNavigationBar>(FindObjectsInactive.Include);
        UI_LobbyStagePanel stage = UIManager.Instance?.GetElement<UI_LobbyStagePanel>() ?? FindAnyObjectByType<UI_LobbyStagePanel>(FindObjectsInactive.Include);
        UI_LobbyUnitPanel units = UIManager.Instance?.GetElement<UI_LobbyUnitPanel>() ?? FindAnyObjectByType<UI_LobbyUnitPanel>(FindObjectsInactive.Include);
        UI_LobbySocialPanel social = UIManager.Instance?.GetElement<UI_LobbySocialPanel>() ?? FindAnyObjectByType<UI_LobbySocialPanel>(FindObjectsInactive.Include);
        GameObject unitsObject = units != null ? units.gameObject : FindSceneObject("UI_LobbyUnitPanel");
        GameObject socialObject = social != null ? social.gameObject : FindSceneObject("UI_LobbySocialPanel");

        header?.Open();
        yield return null;

        m_Navigation?.Open();
        yield return null;

        m_Navigation?.SetSelected(LobbyTab.Stage);

        if (m_Navigation != null)
        {
            m_TabSelectedHandler = tab =>
            {
                if (tab == LobbyTab.Stage)
                    Show(stage);
                else if (tab == LobbyTab.Units)
                    Show(units != null ? (Object)units : unitsObject);
                else
                    Show(social != null ? (Object)social : socialObject);
            };
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
        if (m_CurrentPanelObject != null && m_CurrentPanelObject != panel.gameObject)
            m_CurrentPanelObject.SetActive(false);

        panel.Open();
        m_CurrentPanel = panel;
        m_CurrentPanelObject = panel.gameObject;
    }

    private void Show(Object panel)
    {
        if (panel is UI_Base uiPanel)
            Show(uiPanel);
        else if (panel is GameObject gameObject)
            Show(gameObject);
    }

    private void Show(GameObject panel)
    {
        if (panel == null || panel == m_CurrentPanelObject) return;

        m_CurrentPanel?.Close();
        if (m_CurrentPanelObject != null)
            m_CurrentPanelObject.SetActive(false);

        panel.SetActive(true);
        m_CurrentPanel = panel.GetComponent<UI_Base>();
        m_CurrentPanelObject = panel;
    }

    private static GameObject FindSceneObject(string objectName)
    {
        Transform[] transforms = Resources.FindObjectsOfTypeAll<Transform>();
        foreach (Transform target in transforms)
        {
            if (target == null || target.name != objectName)
                continue;

            GameObject gameObject = target.gameObject;
            if (gameObject.scene.IsValid())
                return gameObject;
        }

        return null;
    }
}
#endif
