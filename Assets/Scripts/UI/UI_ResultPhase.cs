#if UNITY_6000_0_OR_NEWER
using System;
using InTheArena.MainGame;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace InTheArena.UI
{
    [DisallowMultipleComponent]
    public sealed class UI_ResultPhase : UI_Base
    {
        [Header("Result Phase UI")]
        [SerializeField] private TMP_Text m_ResultText;
        [SerializeField] private TMP_Text m_RewardText;
        [SerializeField] private Button m_ContinueButton;

        private CombatResultSnapshot m_CombatResult;
        private BetSettlement m_Settlement;
        private int m_CurrentCall;

        public event Action ContinueClicked;

        protected override void Awake()
        {
            base.Awake();
            if (m_ContinueButton != null)
                m_ContinueButton.onClick.AddListener(OnContinueButtonClicked);
        }

        public void Configure(CombatResultSnapshot combatResult, BetSettlement settlement, int currentCall)
        {
            m_CombatResult = combatResult;
            m_Settlement = settlement;
            m_CurrentCall = currentCall;

            if (m_ContinueButton != null)
            {
                m_ContinueButton.interactable = true;
            }
        }

        public void Refresh()
        {
            bool isBetWin = m_Settlement != null && m_Settlement.IsWin;
            if (m_ResultText != null)
            {
                m_ResultText.text = $"{GetCombatResultLabel()} | BET {(isBetWin ? "WIN" : "LOSE")}";
                m_ResultText.color = isBetWin ? new Color(0.3f, 1f, 0.45f) : new Color(1f, 0.3f, 0.3f);
            }

            if (m_RewardText != null)
            {
                if (isBetWin)
                {
                    m_RewardText.text = $"x{m_Settlement.Multiplier} / +{m_Settlement.PayoutCall} Call\nCurrent: {m_CurrentCall} Call";
                    m_RewardText.color = Color.green;
                }
                else
                {
                    int wagerCall = m_Settlement?.WagerCall ?? 0;
                    m_RewardText.text = $"-{wagerCall} Call\nCurrent: {m_CurrentCall} Call";
                    m_RewardText.color = Color.red;
                }
            }
        }

        private string GetCombatResultLabel()
        {
            return m_CombatResult?.Winner switch
            {
                Team.Red => "RED WIN",
                Team.Blue => "BLUE WIN",
                _ => "DRAW"
            };
        }

        private void OnContinueButtonClicked()
        {
            ContinueClicked?.Invoke();
        }

        protected override void OnDestroy()
        {
            if (m_ContinueButton != null)
                m_ContinueButton.onClick.RemoveListener(OnContinueButtonClicked);
            ContinueClicked = null;
            base.OnDestroy();
        }
    }
}
#endif
