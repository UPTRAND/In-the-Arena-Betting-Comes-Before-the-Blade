#if UNITY_6000_0_OR_NEWER
using InTheArena.MainGame;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace InTheArena.UI
{
    [DisallowMultipleComponent]
    public sealed class UI_CombatHUD : UI_Base
    {
        [Header("Combat HUD")]
        [SerializeField] private TMP_Text m_TeamACountText;
        [SerializeField] private TMP_Text m_TeamBCountText;
        [SerializeField] private TMP_Text m_RoundTimerText;
        [SerializeField] private Button m_SpeedToggleButton;
        [SerializeField] private TMP_Text m_SpeedText;

        private CombatPhase m_CombatPhase;

        protected override void Awake()
        {
            base.Awake();
            if (m_SpeedToggleButton != null)
                m_SpeedToggleButton.onClick.AddListener(OnSpeedToggleClicked);
        }

        public void BindAndShow(CombatPhase combatPhase)
        {
            m_CombatPhase = combatPhase;
            if (!BIsOpened) Open();
            Enable();
            Refresh();
        }

        public void UnbindAndHide()
        {
            m_CombatPhase = null;
            if (BIsOpened) Close();
        }

        private void Update()
        {
            if (m_CombatPhase != null)
                Refresh();
        }

        private void Refresh()
        {
            if (m_CombatPhase == null) return;

            if (m_TeamACountText != null)
                m_TeamACountText.text = $"Red: {m_CombatPhase.RedAliveCount}";
            if (m_TeamBCountText != null)
                m_TeamBCountText.text = $"Blue: {m_CombatPhase.BlueAliveCount}";
            if (m_RoundTimerText != null)
            {
                int seconds = Mathf.CeilToInt(m_CombatPhase.RemainingCombatTime);
                int minutes = seconds / 60;
                seconds %= 60;
                m_RoundTimerText.text = $"{minutes:00}:{seconds:00}";
            }
            if (m_SpeedText != null)
                m_SpeedText.text = $"Speed\nx{m_CombatPhase.CurrentSpeed:0}";
            if (m_SpeedToggleButton != null)
                m_SpeedToggleButton.interactable =
                    !m_CombatPhase.IsPhaseCompleted &&
                    !m_CombatPhase.IsFinalEliminationPlaying;
        }

        private void OnSpeedToggleClicked()
        {
            if (m_CombatPhase == null ||
                m_CombatPhase.IsPhaseCompleted ||
                m_CombatPhase.IsFinalEliminationPlaying)
                return;

            m_CombatPhase.ToggleCombatSpeed();
            Refresh();
        }

        protected override void OnDestroy()
        {
            if (m_SpeedToggleButton != null)
                m_SpeedToggleButton.onClick.RemoveListener(OnSpeedToggleClicked);
            m_CombatPhase = null;
            base.OnDestroy();
        }
    }
}
#endif
