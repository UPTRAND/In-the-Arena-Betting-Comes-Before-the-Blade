#if UNITY_6000_0_OR_NEWER
using System;
using System.Threading;
using UnityEngine;
using UnityEngine.SceneManagement;
using DG.Tweening;
using InTheArena.UI;
using InTheArena.Events.Core;

namespace InTheArena.MainGame
{
    public enum StageClearCommitState
    {
        None,
        Pending,
        Saving,
        Failed,
        Committed,
        GivenUp
    }

    [DisallowMultipleComponent]
    public class StageManager : Manager_Base
    {
        private const float LoadingExitFadeSeconds = 0.3f;

        private static StageManager _instance;

        public static StageManager Instance
        {
            get
            {
                if (ReferenceEquals(_instance, null) || _instance == null)
                {
                    return null;
                }
                return _instance;
            }
        }

        [Header("Stage Settings")]
        [SerializeField] private StageData m_CurrentStageData;

        [Header("Scene Names")]
        [SerializeField] private string m_LobbySceneName = "Lobby";
        [SerializeField] private string m_LoadingSceneName = "Loading";
        [SerializeField] private string m_MainGameSceneName = "MainGame";

        [Header("Manager_Base Interface")]
        [Tooltip("초기화 순서 (낮을수록 먼저 초기화)")]
        [SerializeField] private ushort m_InitializationOrder = 10;
        public override ushort InitializationOrder => m_InitializationOrder;

        private RoundContext m_Context;
        private CancellationTokenSource m_StageCts;
        private bool m_IsStageRunning = false;
        private bool m_IsReturningToLobby;
        private int m_CurrentRoundIndex = 0;

        private StageClearCommitState m_StageClearCommitState = StageClearCommitState.None;
        private string m_LastStageClearSaveError = null;
        private InTheArena.Save.PlayerProgressState m_PendingStageClearCandidate = null;
        private StageClearedEvent m_PendingStageClearedEvent;
        private const int StageClearGoldReward = 100;
        private const int StageClearStarReward = 1;

        public event Action<StageClearCommitState> OnStageClearCommitStateChanged;

        public StageData CurrentStageData => m_CurrentStageData;
        public RoundContext Context => m_Context;
        public bool IsStageRunning => m_IsStageRunning;
        public int CurrentRoundIndex => m_CurrentRoundIndex;

        public StagePlayerState PlayerState { get; private set; }

        public StageClearCommitState StageClearCommitState => m_StageClearCommitState;
        public string LastStageClearSaveError => m_LastStageClearSaveError;

        private void SetStageClearCommitState(StageClearCommitState newState)
        {
            if (m_StageClearCommitState != newState)
            {
                m_StageClearCommitState = newState;
                OnStageClearCommitStateChanged?.Invoke(newState);
            }
        }

        private void Awake()
        {
            if (!ReferenceEquals(_instance, null) && _instance != null && _instance != this)
            {
                Debug.LogWarning("[StageManager] 중복 StageManager 인스턴스 감지 - 기존 인스턴스 파괴");
                Destroy(gameObject);
                return;
            }

            _instance = this;
            m_Context = new RoundContext();
        }

        public override bool Setup() => true;

        protected override bool Init()
        {
            return true;
        }

        public override void Release()
        {
            CleanupRuntimeResources();
            ClearStageProgressState();
            base.Release();
        }

        protected override void OnDestroy()
        {
            base.OnDestroy();
            if (ReferenceEquals(_instance, this))
            {
                _instance = null;
            }
            CleanupRuntimeResources();
            ClearStageProgressState();
        }

        /// <summary>스테이지를 한 번에 하나만 실행하고 취소 시 미완료 씬 로드를 마무리합니다.</summary>
        public async Awaitable StartStageAsync(StageData stageData, CancellationToken token = default)
        {
            if (m_IsStageRunning || m_IsReturningToLobby)
            {
                return;
            }

            if (stageData == null || !stageData.IsValid())
            {
                Debug.LogError("[StageManager] 유효하지 않은 스테이지 데이터입니다.");
                return;
            }

            if (InTheArena.Util.LoadingProgressService.Instance != null && InTheArena.Util.LoadingProgressService.Instance.IsLoading)
            {
                Debug.LogWarning("[StageManager] 이미 다른 로딩이 진행 중입니다.");
                return;
            }

            if (EventManager.Instance != null && !EventManager.Instance.TryStartStage(stageData.StageNum, out _))
            {
                Debug.LogError("[StageManager] 도전 시작 표식을 저장하지 못해 스테이지 시작을 중단합니다.");
                return;
            }

            m_StageCts?.Cancel();
            m_StageCts?.Dispose();

            if (token.CanBeCanceled)
            {
                m_StageCts = CancellationTokenSource.CreateLinkedTokenSource(token);
            }
            else
            {
                m_StageCts = new CancellationTokenSource();
            }

            m_CurrentStageData = stageData;
            m_IsStageRunning = true;
            m_CurrentRoundIndex = 0;
            SetStageClearCommitState(StageClearCommitState.None);
            m_PendingStageClearCandidate = null;
            m_PendingStageClearedEvent = null;
            m_LastStageClearSaveError = null;

            try
            {
                await AsyncSceneLoader.LoadSceneAsync(
                    m_MainGameSceneName,
                    m_StageCts.Token,
                    PrepareStageDataAsync,
                    WaitForMainGameReadyAsync,
                    m_LoadingSceneName);

                await RunStageLoopAsync(m_StageCts.Token);
            }
            catch (OperationCanceledException)
            {
                Debug.LogWarning("[StageManager] 스테이지 취소됨");
                StageEndReason reason = m_IsReturningToLobby ? StageEndReason.UserExit : StageEndReason.Error;
                EventManager.Instance?.RecordStageEnded(reason);
                await RecoverToLobbyAsync();
            }
            catch (Exception ex)
            {
                Debug.LogException(ex);
                EventManager.Instance?.RecordStageEnded(StageEndReason.Error);
                await RecoverToLobbyAsync();
            }
            finally
            {
                CleanupRuntimeResources();
                // If we failed to save or are pending, we DO NOT clear the stage progress yet.
                // We preserve it for recovery.
                if (m_StageClearCommitState != StageClearCommitState.Failed && m_StageClearCommitState != StageClearCommitState.Pending)
                {
                    ClearStageProgressState();
                }
            }
        }

        /// <summary>옵션의 연속 입력을 합쳐 스테이지 종료 후 로비로 이동합니다.</summary>
        public void ReturnToLobbyFromOptions()
        {
            if (m_IsReturningToLobby || !Application.isPlaying ||
                (!m_IsStageRunning && SceneManager.GetActiveScene().name == m_LobbySceneName))
            {
                return;
            }

            ReturnToLobbyFromOptionsInternal();
        }

        /// <summary>이전 스테이지의 finally 정리가 끝난 뒤 다음 화면을 엽니다.</summary>
        private async void ReturnToLobbyFromOptionsInternal()
        {
            m_IsReturningToLobby = true;

            try
            {
                m_StageCts?.Cancel();
                while (m_IsStageRunning && Application.isPlaying)
                {
                    await Awaitable.NextFrameAsync();
                }
                if (Application.isPlaying)
                {
                    await AsyncSceneLoader.LoadSceneAsync(m_LobbySceneName);
                }
            }
            catch (OperationCanceledException)
            {
                // The loader owns cancellation after the return transition starts.
            }
            catch (Exception ex)
            {
                Debug.LogException(ex);
            }
            finally
            {
                m_IsReturningToLobby = false;
            }
        }

        public bool RetryStageClearSave()
        {
            if (m_StageClearCommitState != StageClearCommitState.Failed)
            {
                return false;
            }

            SetStageClearCommitState(StageClearCommitState.Saving);
            if (SaveManager.Instance != null)
            {
                if (SaveManager.Instance.TryCommitPendingStageClear(m_PendingStageClearCandidate, out string error))
                {
                    EventManager.Instance?.NotifyStageClearCommitted(m_PendingStageClearedEvent);
                    QueueLobbyRewardPresentation();
                    SetStageClearCommitState(StageClearCommitState.Committed);
                    return true;
                }
                else
                {
                    m_LastStageClearSaveError = error;
                    SetStageClearCommitState(StageClearCommitState.Failed);
                    return false;
                }
            }
            else
            {
                m_LastStageClearSaveError = "SaveManager is unavailable.";
                SetStageClearCommitState(StageClearCommitState.Failed);
                return false;
            }
        }

        public bool GiveUpStageClearSave()
        {
            if (m_StageClearCommitState != StageClearCommitState.Failed)
                return false;

            m_PendingStageClearCandidate = null;
            m_PendingStageClearedEvent = null;
            EventManager.Instance?.RecordStageEnded(StageEndReason.UserExit);
            SetStageClearCommitState(StageClearCommitState.GivenUp);
            return true;
        }

        private void ProcessPendingStageClearSave()
        {
            if (m_StageClearCommitState != StageClearCommitState.Pending)
                return;

            SetStageClearCommitState(StageClearCommitState.Saving);

            if (SaveManager.Instance == null)
            {
                m_LastStageClearSaveError = "SaveManager is unavailable.";
                SetStageClearCommitState(StageClearCommitState.Failed);
                return;
            }

            if (m_PendingStageClearCandidate == null)
            {
                m_LastStageClearSaveError = "No pending candidate data.";
                SetStageClearCommitState(StageClearCommitState.Failed);
                return;
            }

            bool success = SaveManager.Instance.TryCommitPendingStageClear(m_PendingStageClearCandidate, out string error);
            if (!success)
            {
                Debug.LogError($"[StageManager] 스테이지 클리어 저장 실패: {error}");
                m_LastStageClearSaveError = error;
                SetStageClearCommitState(StageClearCommitState.Failed);
            }
            else
            {
                EventManager.Instance?.NotifyStageClearCommitted(m_PendingStageClearedEvent);
                QueueLobbyRewardPresentation();
                SetStageClearCommitState(StageClearCommitState.Committed);
            }
        }

        /// <summary>옵션 복귀는 해당 요청에 맡기고 그 외 실패만 공용 로더로 복구합니다.</summary>
        private async Awaitable RecoverToLobbyAsync()
        {
            if (!Application.isPlaying || m_IsReturningToLobby)
            {
                return;
            }

            try
            {
                if (SceneManager.GetActiveScene().name != m_LobbySceneName)
                {
                    await AsyncSceneLoader.LoadSceneAsync(m_LobbySceneName);
                }
                await ScreenFaderTransition.FadeInAsync(LoadingExitFadeSeconds);
            }
            catch (Exception ex)
            {
                Debug.LogException(ex);
            }
        }

        /// <summary>공용 로딩 화면 안에서 스테이지 상태와 데이터만 준비합니다.</summary>
        private async Awaitable PrepareStageDataAsync(IProgress<float> progress, CancellationToken token)
        {
            m_Context.Clear();
            m_Context.InitializeStage(m_CurrentStageData);
            PlayerState = new StagePlayerState();
            if (SaveManager.Instance != null)
            {
                PlayerState.Gold = SaveManager.Instance.Gold;
            }
            await LoadStageDataAsync(progress, token);
        }

        /// <summary>스테이지에 포함된 라운드를 확인하고 준비 진행도를 전달합니다.</summary>
        private async Awaitable LoadStageDataAsync(IProgress<float> progressReporter, CancellationToken token)
        {
            if (!m_CurrentStageData.IsValid())
            {
                throw new InvalidOperationException("스테이지 데이터 검증 실패");
            }

            int roundCount = m_CurrentStageData.RoundDatas.Count;
            if (roundCount == 0)
            {
                progressReporter?.Report(1f);
                return;
            }

            for (int i = 0; i < roundCount; i++)
            {
                token.ThrowIfCancellationRequested();

                var roundData = m_CurrentStageData.RoundDatas[i];
                if (!roundData.IsValid())
                {
                    throw new InvalidOperationException($"라운드 {i + 1} 데이터 검증 실패");
                }

                float currentProgress = (float)(i + 1) / roundCount;
                progressReporter?.Report(currentProgress);
                await Awaitable.NextFrameAsync(token);
            }

            Debug.Log("[StageManager] 모든 데이터 로드 완료");
        }

        private async Awaitable WaitForMainGameReadyAsync(CancellationToken token)
        {
            float timeout = 10f;
            float elapsed = 0f;

            while (RoundManager.Instance == null && elapsed < timeout)
            {
                token.ThrowIfCancellationRequested();
                await Awaitable.WaitForSecondsAsync(0.1f);
                elapsed += 0.1f;
            }

            if (RoundManager.Instance == null)
            {
                throw new TimeoutException("MainGame 씬에서 RoundManager를 찾을 수 없습니다.");
            }

            RoundManager.Instance.InitializeContext(
                m_Context,
                m_CurrentStageData,
                PlayerState);
        }

        private async Awaitable RunStageLoopAsync(CancellationToken token)
        {
            int totalRounds = m_CurrentStageData.TotalRounds;
            int autoRetryCount = 0;
            bool clearPanelPrepared = false;

            while (m_CurrentRoundIndex < totalRounds)
            {
                token.ThrowIfCancellationRequested();

                if (m_StageClearCommitState == StageClearCommitState.None)
                {
                    m_Context.CurrentRound = m_CurrentRoundIndex + 1;
                    Debug.Log($"[StageManager] Round {m_Context.CurrentRound} / {totalRounds} 시작");
                    await RoundManager.Instance.RunRoundAsync(m_CurrentRoundIndex, token);
                    token.ThrowIfCancellationRequested();
                }

                if (m_StageClearCommitState == StageClearCommitState.None && CheckStageClear())
                {
                    SetStageClearCommitState(StageClearCommitState.Pending);

                    if (SaveManager.Instance != null)
                    {
                        m_PendingStageClearCandidate = SaveManager.Instance.CreatePendingStageClearCandidate(PlayerState, m_CurrentStageData.StageNum, StageClearGoldReward, StageClearStarReward);
                        m_PendingStageClearedEvent = EventManager.Instance?.ApplyStageClearedToCandidate(
                            m_PendingStageClearCandidate,
                            m_CurrentStageData.StageNum);
                        if (m_PendingStageClearCandidate == null)
                        {
                            m_LastStageClearSaveError = "Failed to create candidate (Invalid state).";
                            SetStageClearCommitState(StageClearCommitState.Failed);
                        }
                    }
                    else
                    {
                        m_LastStageClearSaveError = "SaveManager is unavailable.";
                        SetStageClearCommitState(StageClearCommitState.Failed);
                    }
                    
                    // 저장 시도 전에 실패·재시도 상태를 표시할 화면을 준비합니다.
                    await ShowResultPanelAsync(true, token);
                    clearPanelPrepared = true;
                }

                if (m_StageClearCommitState == StageClearCommitState.Pending || m_StageClearCommitState == StageClearCommitState.Failed)
                {
                    if (!clearPanelPrepared)
                    {
                        await ShowResultPanelAsync(true, token);
                        clearPanelPrepared = true;
                    }
                    ProcessPendingStageClearSave();

                    if (m_StageClearCommitState == StageClearCommitState.Failed)
                    {
                        // Bounded Auto Retry up to 3 times
                        if (autoRetryCount < 3)
                        {
                            autoRetryCount++;
                            Debug.LogWarning($"[StageManager] 자동 재시도 {autoRetryCount}/3 수행 중...");
                            await Awaitable.WaitForSecondsAsync(1f, token);
                            if (m_StageClearCommitState != StageClearCommitState.Failed)
                            {
                                continue;
                            }
                            SetStageClearCommitState(StageClearCommitState.Pending);
                            continue;
                        }

                        // After auto-retries, wait for external retry UI or manual action
                        await Awaitable.WaitForSecondsAsync(0.5f, token);
                        continue;
                    }
                }

                if (m_StageClearCommitState == StageClearCommitState.GivenUp)
                {
                    if (UIManager.Instance != null)
                    {
                        UIManager.Instance.GetStageResultPanel()?.Close();
                    }
                    
                    break;
                }

                if (m_StageClearCommitState == StageClearCommitState.Committed)
                {
                    if (UIManager.Instance != null)
                    {
                        if (!clearPanelPrepared)
                        {
                            await ShowResultPanelAsync(true, token);
                        }
                        var panel = UIManager.Instance.GetStageResultPanel();
                        if (panel != null)
                        {
                            await panel.WaitForCompletionAsync(token);
                            panel.Close();
                        }
                    }
                    
                    break;
                }

                if (CheckGameOver())
                {
                    Debug.Log("[StageManager] GAME OVER!");
                    EventManager.Instance?.RecordStageEnded(StageEndReason.Defeated);
                    await ShowResultPanelAsync(false, token);
                    if (UIManager.Instance != null)
                    {
                        var panel = UIManager.Instance.GetStageResultPanel();
                        if (panel != null)
                        {
                            await panel.WaitForCompletionAsync(token);
                            panel.Close();
                        }
                    }
                    break;
                }

                m_CurrentRoundIndex++;
                await ScreenFaderTransition.FadeOutAsync(1f, token);
            }

            await ReturnToLobbyAsync();

            // Once returned to lobby, clear the stage progress state to fully clean up
            if (m_StageClearCommitState == StageClearCommitState.Committed || m_StageClearCommitState == StageClearCommitState.GivenUp)
            {
                ClearStageProgressState();
            }
        }

        private bool CheckStageClear()
        {
            return m_Context.CurrentCall >= m_CurrentStageData.TargetCall;
        }

        private bool CheckGameOver()
        {
            return m_Context.CurrentCall < BettingRules.WagerStepCall ||
                   m_CurrentRoundIndex >= m_CurrentStageData.TotalRounds - 1;
        }

        private async Awaitable ShowResultPanelAsync(bool isClear, CancellationToken token)
        {
            if (UIManager.Instance == null) return;
            var panel = UIManager.Instance.GetStageResultPanel();
            if (panel == null)
            {
                Debug.LogError("[StageManager] StageResultPanel을 찾을 수 없습니다.");
                return;
            }

            Debug.Log($"[StageManager] Stage Result - Clear: {isClear}, CurrentCall: {m_Context.CurrentCall}, TargetCall: {m_CurrentStageData.TargetCall}");

            int initialCall = m_Context.CurrentStageData != null ? m_Context.CurrentStageData.InitialCall : 0;
            panel.Prepare(isClear, initialCall, m_Context.CompletedRoundSettlements);
            if (isClear && m_StageClearCommitState != StageClearCommitState.Committed)
            {
                if (m_StageClearCommitState == StageClearCommitState.Failed)
                {
                    panel.SetMode(StageResultPanelMode.SaveFailed, m_LastStageClearSaveError);
                }
                else
                {
                    panel.SetMode(StageResultPanelMode.Saving);
                }
            }
            
            // ScreenFaderTransition may cause issues if called concurrently, but in this sequence it's called once at end of round
            await ScreenFaderTransition.FadeInAsync(1f, token);
            panel.PlayResultAnimation();
        }

        private async Awaitable ReturnToLobbyAsync()
        {
            m_CurrentStageData = null;
            m_Context?.Clear();

            if (!Application.isPlaying)
                return;

            if (m_StageCts != null && m_StageCts.IsCancellationRequested)
                return;

            await AsyncSceneLoader.LoadSceneAsync(m_LobbySceneName);
        }

        private static void QueueLobbyRewardPresentation()
        {
            SaveManager save = SaveManager.Instance;
            if (save == null) return;
            StageClearRewardPresentation.Queue(new StageClearRewardPresentation.RewardData(
                Mathf.Max(0, save.Gold - StageClearGoldReward), save.Gold,
                Mathf.Max(0, save.Stars - StageClearStarReward), save.Stars));
        }

        public void SetCurrentStageData(StageData stageData)
        {
            m_CurrentStageData = stageData;
        }

        private void CleanupRuntimeResources()
        {
            m_IsStageRunning = false;
            PoolManager.Instance?.ClearStage();

            if (m_StageCts != null)
            {
                CancellationTokenSource cts = m_StageCts;
                m_StageCts = null;
                cts.Cancel();
                cts.Dispose();
            }
        }

        private void ClearStageProgressState()
        {
            m_Context?.Clear();
            PlayerState = null;
            m_PendingStageClearCandidate = null;
            m_PendingStageClearedEvent = null;
        }
    }
}
#endif
