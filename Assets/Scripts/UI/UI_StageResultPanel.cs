#if UNITY_6000_0_OR_NEWER
using System.Threading;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using InTheArena.MainGame;

namespace InTheArena.UI
{
    public enum StageResultPanelMode
    {
        Saving,
        SaveFailed,
        ClearCompleted,
        GameOver
    }

    [DisallowMultipleComponent]
    public sealed class UI_StageResultPanel : UI_Base
    {
        [Header("Stage Result")]
        [SerializeField] private TMP_Text m_TitleText;
        [SerializeField] private TMP_Text m_CurrentCallText;
        [SerializeField] private TMP_Text m_TargetCallText;
        [SerializeField] private Button m_ReturnToLobbyButton;
        
        [Header("Error State")]
        [SerializeField] private TMP_Text m_ErrorText;
        [SerializeField] private Button m_RetryButton;

        private AwaitableCompletionSource m_CompletionSource;
        private bool m_IsEventSubscribed = false;

        protected override void Awake()
        {
            base.Awake();
            if (m_ReturnToLobbyButton != null)
                m_ReturnToLobbyButton.onClick.AddListener(OnReturnToLobbyClicked);
            if (m_RetryButton != null)
                m_RetryButton.onClick.AddListener(OnRetryClicked);
        }

        private void OnEnable()
        {
            SubscribeEvent();
        }

        private void OnDisable()
        {
            UnsubscribeEvent();
        }

        private void SubscribeEvent()
        {
            if (!m_IsEventSubscribed && StageManager.Instance != null)
            {
                StageManager.Instance.OnStageClearCommitStateChanged += HandleCommitStateChanged;
                m_IsEventSubscribed = true;
            }
        }

        private void UnsubscribeEvent()
        {
            if (m_IsEventSubscribed && StageManager.Instance != null)
            {
                StageManager.Instance.OnStageClearCommitStateChanged -= HandleCommitStateChanged;
                m_IsEventSubscribed = false;
            }
        }

        private void HandleCommitStateChanged(StageClearCommitState state)
        {
            if (state == StageClearCommitState.Failed)
            {
                SetMode(StageResultPanelMode.SaveFailed, StageManager.Instance.LastStageClearSaveError);
            }
            else if (state == StageClearCommitState.Saving)
            {
                SetMode(StageResultPanelMode.Saving);
            }
            else if (state == StageClearCommitState.Committed)
            {
                SetMode(StageResultPanelMode.ClearCompleted);
            }
        }

        public void SetMode(StageResultPanelMode mode, string errorStr = null)
        {
            if (m_ErrorText != null) m_ErrorText.gameObject.SetActive(mode == StageResultPanelMode.Saving || mode == StageResultPanelMode.SaveFailed);
            if (m_RetryButton != null) m_RetryButton.gameObject.SetActive(mode == StageResultPanelMode.SaveFailed);
            if (m_ReturnToLobbyButton != null) m_ReturnToLobbyButton.gameObject.SetActive(mode == StageResultPanelMode.ClearCompleted || mode == StageResultPanelMode.GameOver);

            if (mode == StageResultPanelMode.Saving)
            {
                if (m_ErrorText != null) m_ErrorText.text = "저장 중...";
                DisableCompletionInput();
            }
            else if (mode == StageResultPanelMode.SaveFailed)
            {
                if (m_ErrorText != null) m_ErrorText.text = $"저장에 실패했습니다.\n{errorStr}";
                DisableCompletionInput();
                // Retry button operates regardless of CompletionInput block (it's part of the panel, interactability should be enabled globally, but completion logic blocked)
                EnableInput();
            }
            else if (mode == StageResultPanelMode.ClearCompleted)
            {
                if (m_TitleText != null)
                {
                    m_TitleText.text = "STAGE CLEAR";
                    m_TitleText.color = new Color(0.3f, 1f, 0.45f);
                }
                EnableInput();
                EnableCompletionInput();
            }
            else if (mode == StageResultPanelMode.GameOver)
            {
                if (m_TitleText != null)
                {
                    m_TitleText.text = "GAME OVER";
                    m_TitleText.color = new Color(1f, 0.3f, 0.3f);
                }
                EnableInput();
                EnableCompletionInput();
            }
        }

        private void OnReturnToLobbyClicked()
        {
            // Only allow completion if button is active and we are in a valid state (interactable handled by UI, but double check)
            if (m_ReturnToLobbyButton.gameObject.activeSelf)
            {
                DisableInput();
                m_CompletionSource?.TrySetResult();
            }
        }
        
        private void OnRetryClicked()
        {
            if (StageManager.Instance != null && m_RetryButton.gameObject.activeSelf)
            {
                StageManager.Instance.RetryStageClearSave();
            }
        }

        public void Prepare(
            bool isClear,
            int currentCall,
            int targetCall)
        {
            if (m_CurrentCallText != null) m_CurrentCallText.text = $"Current  {currentCall} Call";
            if (m_TargetCallText != null) m_TargetCallText.text = $"Target  {targetCall} Call";

            m_CompletionSource = new AwaitableCompletionSource();
            
            if (!gameObject.activeSelf)
                Open();
            else
                Enable();
                
            SubscribeEvent();

            // Set initial mode based on current manager state
            if (isClear)
            {
                if (StageManager.Instance != null)
                {
                    HandleCommitStateChanged(StageManager.Instance.StageClearCommitState);
                }
                else
                {
                    SetMode(StageResultPanelMode.ClearCompleted);
                }
            }
            else
            {
                SetMode(StageResultPanelMode.GameOver);
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

        private void DisableCompletionInput()
        {
            if (m_ReturnToLobbyButton != null) m_ReturnToLobbyButton.interactable = false;
        }

        private void EnableCompletionInput()
        {
            if (m_ReturnToLobbyButton != null) m_ReturnToLobbyButton.interactable = true;
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
            UnsubscribeEvent();
            base.OnClosed();
        }

        protected override void OnDestroy()
        {
            if (m_ReturnToLobbyButton != null)
                m_ReturnToLobbyButton.onClick.RemoveListener(OnReturnToLobbyClicked);
            if (m_RetryButton != null)
                m_RetryButton.onClick.RemoveListener(OnRetryClicked);
            m_CompletionSource?.TrySetResult();
            UnsubscribeEvent();
            base.OnDestroy();
        }
    }
}
#endif
