#if UNITY_6000_0_OR_NEWER
using System.Threading;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace InTheArena.UI
{
    [DisallowMultipleComponent]
    public sealed class UI_StageResultPanel : UI_Base
    {
        [Header("Stage Result")]
        [SerializeField] private TMP_Text m_TitleText;
        [SerializeField] private TMP_Text m_CurrentCallText;
        [SerializeField] private TMP_Text m_TargetCallText;
        [SerializeField] private Button m_ReturnToLobbyButton;

        private AwaitableCompletionSource m_CompletionSource;

        protected override void Awake()
        {
            base.Awake();
            if (m_ReturnToLobbyButton != null)
                m_ReturnToLobbyButton.onClick.AddListener(OnReturnToLobbyClicked);
        }

        private void OnReturnToLobbyClicked()
        {
            DisableInput();
            m_CompletionSource?.TrySetResult();
        }

        public void Prepare(
            bool isClear,
            int currentCall,
            int targetCall)
        {
            if (m_TitleText != null)
            {
                m_TitleText.text = isClear ? "STAGE CLEAR" : "GAME OVER";
                m_TitleText.color = isClear ? new Color(0.3f, 1f, 0.45f) : new Color(1f, 0.3f, 0.3f);
            }
            if (m_CurrentCallText != null) m_CurrentCallText.text = $"Current  {currentCall} Call";
            if (m_TargetCallText != null) m_TargetCallText.text = $"Target  {targetCall} Call";

            m_CompletionSource = new AwaitableCompletionSource();
            
            if (!gameObject.activeSelf)
                Open();
            else
                Enable();

            if (CanvasGroup != null)
            {
                CanvasGroup.alpha = 1f;
                CanvasGroup.interactable = false;
                CanvasGroup.blocksRaycasts = false;
            }
        }

        public void EnableInput()
        {
            if (CanvasGroup != null)
            {
                CanvasGroup.interactable = true;
                CanvasGroup.blocksRaycasts = true;
            }
        }

        public void DisableInput()
        {
            if (CanvasGroup != null)
            {
                CanvasGroup.interactable = false;
                CanvasGroup.blocksRaycasts = false;
            }
        }

        public async Awaitable WaitForCompletionAsync(CancellationToken token)
        {
            AwaitableCompletionSource source = m_CompletionSource 
                ?? throw new System.InvalidOperationException("Prepare must be called before waiting.");

            using CancellationTokenRegistration registration = token.Register(static state =>
            {
                ((AwaitableCompletionSource)state).TrySetResult();
            }, source);

            await source.Awaitable;
            token.ThrowIfCancellationRequested();
        }

        public override void OnClosed()
        {
            m_CompletionSource?.TrySetResult();
            m_CompletionSource = null;
            base.OnClosed();
        }

        protected override void OnDestroy()
        {
            if (m_ReturnToLobbyButton != null)
                m_ReturnToLobbyButton.onClick.RemoveListener(OnReturnToLobbyClicked);
            m_CompletionSource?.TrySetResult();
            base.OnDestroy();
        }
    }
}
#endif
