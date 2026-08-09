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

        protected override void Awake()
        {
            base.Awake();
            if (m_SettingsButton != null)
            {
                m_SettingsButton.onClick.AddListener(UI_OptionsPopup.Show);
            }
        }

        protected override void OnDestroy()
        {
            if (m_SettingsButton != null)
            {
                m_SettingsButton.onClick.RemoveListener(UI_OptionsPopup.Show);
            }

            base.OnDestroy();
        }

        public override void OnOpened() { base.OnOpened(); Refresh(); }
        private void Update() { if (BIsOpened && Time.unscaledTime >= m_NextRefresh) Refresh(); }
        public void Refresh()
        {
            m_NextRefresh = Time.unscaledTime + 1f;
            SaveManager save = SaveManager.Instance;
            if (save == null) return;
            save.RefreshHearts();
            m_GoldText.text = save.Gold.ToString();
            m_StarText.text = save.Stars.ToString();
            m_HeartText.text = save.Hearts >= SaveManager.MaxHearts ? $"{save.Hearts}/{SaveManager.MaxHearts}" : $"{save.Hearts}/{SaveManager.MaxHearts} · {save.GetRemainingHeartTime():mm\\:ss}";
        }
    }
}
#endif
