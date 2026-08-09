#if UNITY_6000_0_OR_NEWER
using DG.Tweening;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace InTheArena.UI
{
    public sealed class UI_LobbyHeader : UI_Base
    {
        [SerializeField] private TMP_Text m_GoldText;
        [SerializeField] private TMP_Text m_HeartText;
        [SerializeField] private TMP_Text m_TimerText;
        [SerializeField] private RectTransform m_TimerBox;
        [SerializeField] private TMP_Text m_StarText;
        [SerializeField] private Button m_SettingsButton;
        private float m_NextRefresh;
        private bool m_HasTimerState;
        private bool m_HasInitializedTimerState;
        private Tween m_TimerBoxTween;

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
            m_TimerBoxTween?.Kill();
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
            bool needsTimer = save.Hearts < SaveManager.MaxHearts;
            m_HeartText.text = $"{save.Hearts}/{SaveManager.MaxHearts}";
            if (m_TimerText != null)
            {
                m_TimerText.text = needsTimer ? save.GetRemainingHeartTime().ToString(@"mm\:ss") : string.Empty;
            }
            RefreshTimerBox(needsTimer);
        }

        private void RefreshTimerBox(bool needsTimer)
        {
            if (m_TimerBox == null) return;

            float targetY = needsTimer ? -50f : 0f;
            if (!m_HasInitializedTimerState)
            {
                m_HasInitializedTimerState = true;
                m_HasTimerState = needsTimer;
                Vector2 position = m_TimerBox.anchoredPosition;
                position.y = targetY;
                m_TimerBox.anchoredPosition = position;
                return;
            }

            if (m_HasTimerState == needsTimer) return;
            m_HasTimerState = needsTimer;
            m_TimerBoxTween?.Kill();
            m_TimerBoxTween = m_TimerBox
                .DOAnchorPosY(targetY, 0.3f)
                .SetEase(Ease.OutCubic)
                .SetUpdate(true);
        }
    }
}
#endif
