#if UNITY_6000_0_OR_NEWER
using System;
using System.Threading;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.SceneManagement;
using DG.Tweening;
using InTheArena.MainGame;

namespace InTheArena.UI
{
    /// <summary>
    /// 테스트용 스테이지 선택 버튼 UI
    /// Lobby 씬에서 특정 스테이지를 선택하여 MainGame 씬으로 전환
    /// </summary>
    [DisallowMultipleComponent]
    public class UI_StageButtonTest : UI_Base
    {
        [Header("Stage Selection")]
        [Tooltip("이동할 스테이지 데이터 (에디터에서 할당)")]
        [SerializeField] private StageData m_TargetStageData;

        [Header("UI References")]
        [SerializeField] private Text m_StageNameText;
        [SerializeField] private Text m_StageDescText;
        [SerializeField] private Button m_StartButton;
        [SerializeField] private GameObject m_LoadingOverlay;

        [Header("Transition Settings")]
        [SerializeField] private float m_TransitionDuration = 0.5f;
        [SerializeField] private Ease m_TransitionEase = Ease.OutCubic;

        private CancellationTokenSource m_ButtonCts;

        public override void OnOpened()
        {
            base.OnOpened();
            UpdateStageInfo();
        }

        public override void OnClosed()
        {
            base.OnClosed();
            m_ButtonCts?.Cancel();
            m_ButtonCts?.Dispose();
        }

        protected override void Awake()
        {
            base.Awake();
            if (m_StartButton != null)
                m_StartButton.onClick.AddListener(OnStartButtonClicked);
        }

        private void UpdateStageInfo()
        {
            if (m_TargetStageData != null)
            {
                if (m_StageNameText != null)
                    m_StageNameText.text = m_TargetStageData.FullStageName;

                if (m_StageDescText != null)
                    m_StageDescText.text =
                        $"Region: {m_TargetStageData.Region} | Round: {m_TargetStageData.TotalRounds} | " +
                        $"Target: {m_TargetStageData.TargetCall} Call";
            }
        }

        private void OnStartButtonClicked()
        {
            if (m_TargetStageData == null)
            {
                Debug.LogWarning("[UI_StageButtonTest] 스테이지 데이터가 할당되지 않았습니다.");
                return;
            }

            var stageManager = StageManager.Instance;
            if (stageManager == null)
            {
                Debug.LogError("[UI_StageButtonTest] StageManager 인스턴스를 찾을 수 없습니다.");
                return;
            }

            // 1. 중복 클릭 방지 및 로딩 UI 표시 (씬 전환 전까지 유효)
            if (m_StartButton != null)
                m_StartButton.interactable = false;

            if (m_LoadingOverlay != null)
                m_LoadingOverlay.SetActive(true);

            Debug.Log($"[UI_StageButtonTest] {m_TargetStageData.FullStageName} 스테이지 시작 요청");

            // 2. 핵심: UI 내부에서 await하지 않고 StageManager를 직접 실행합니다.
            // CancellationToken.None을 전달하여 UI의 파괴 여부와 관계없이 StageManager가 씬 전환을 완수하도록 합니다.
            _ = stageManager.StartStageAsync(m_TargetStageData, CancellationToken.None);
        }

        private async Awaitable StartStageAsync(CancellationToken token)
        {
            if (m_LoadingOverlay != null)
                m_LoadingOverlay.SetActive(true);

            if (m_StartButton != null)
                m_StartButton.interactable = false;

            try
            {
                var stageManager = StageManager.Instance;
                if (stageManager == null)
                {
                    Debug.LogError("[UI_StageButtonTest] StageManager 인스턴스를 찾을 수 없습니다.");
                    return;
                }

                Debug.Log($"[UI_StageButtonTest] {m_TargetStageData.FullStageName} 스테이지 시작 요청");

                // StageManager를 통해 스테이지 시작 (Loading -> MainGame 씬 전환 포함)
                await stageManager.StartStageAsync(m_TargetStageData, token);
            }
            catch (OperationCanceledException)
            {
                Debug.Log("[UI_StageButtonTest] 스테이지 시작 취소됨");
            }
            catch (Exception ex)
            {
                Debug.LogException(ex);
            }
            finally
            {
                if (m_LoadingOverlay != null)
                    m_LoadingOverlay.SetActive(false);

                if (m_StartButton != null)
                    m_StartButton.interactable = true;
            }
        }

        public void SetTargetStage(StageData stageData)
        {
            m_TargetStageData = stageData;
            UpdateStageInfo();
        }

        protected override void OnDestroy()
        {
            m_ButtonCts?.Cancel();
            m_ButtonCts?.Dispose();

            if (!ReferenceEquals(transform, null))
            {
                transform.DOKill();
            }
        }
    }
}
#endif
