#if UNITY_6000_0_OR_NEWER
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace InTheArena.UI
{
    public sealed class UI_LobbyHeader : UI_Base
    {
        [SerializeField] private TMP_Text m_GoldText;
        [SerializeField] private TMP_Text m_HeartText;
        [SerializeField] private TMP_Text m_StarText;
        [SerializeField] private Button m_SettingsButton;
        private float m_NextRefresh;

        public override void OnOpened() { base.OnOpened(); Refresh(); }
        private void Update() { if (BIsOpened && Time.unscaledTime >= m_NextRefresh) Refresh(); }
        public void Refresh()
        {
            m_NextRefresh = Time.unscaledTime + 1f;
            SaveManager save = SaveManager.Instance;
            if (save == null || save.Data == null) return;
            save.RefreshHearts();
            m_GoldText.text = save.Data.gold.ToString();
            m_StarText.text = save.Data.stars.ToString();
            m_HeartText.text = save.Data.hearts >= SaveManager.MaxHearts ? $"{save.Data.hearts}/{SaveManager.MaxHearts}" : $"{save.Data.hearts}/{SaveManager.MaxHearts} · {save.GetRemainingHeartTime():mm\\:ss}";
        }
    }
}
#endif
