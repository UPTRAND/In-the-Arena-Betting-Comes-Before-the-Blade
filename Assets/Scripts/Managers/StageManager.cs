#if UNITY_6000_0_OR_NEWER
using System;
using System.Threading;
using UnityEngine;
using UnityEngine.SceneManagement;
using DG.Tweening;
using InTheArena.UI;

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
        private const float LoadingEntryFadeSeconds = 0.5f;
        private const float LoadingExitFadeSeconds = 0.3f;
        private const float MinLoadingDisplaySeconds = 1.5f;

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

        public async Awaitable StartStageAsync(StageData stageData, CancellationToken token = default)
        {
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

            m_StageCts?.Cancel();
            m_StageCts?.Dispose();

            m_StageCts = token.CanBeCanceled
                ? CancellationTokenSource.CreateLinkedTokenSource(token) : new CancellationTokenSource();

            m_CurrentStageData = stageData;
            m_IsStageRunning = true;
            m_CurrentRoundIndex = 0;
            SetStageClearCommitState(StageClearCommitState.None);
            m_PendingStageClearCandidate = null;
            m_LastStageClearSaveError = null;

            try
            {
                using (var session = InTheArena.Util.LoadingProgressService.Instance?.BeginSession())
                {
                session?.Report(0f);
                await ScreenFaderTransition.FadeOutAsync(LoadingEntryFadeSeconds, m_StageCts.Token);
                Debug.Log($"[StageManager] {stageData.FullStageName} 스테이지 시작 - Loading 씬 로드 중...");
                await SceneManager.LoadSceneAsync(m_LoadingSceneName, LoadSceneMode.Single).ToAwaitable();

                await ScreenFaderTransition.FadeInAsync(LoadingEntryFadeSeconds, m_StageCts.Token);
                float loadingSceneEnteredAt = Time.realtimeSinceStartup;
                session?.Report(0.1f);

                m_Context.Clear();
                m_Context.InitializeStage(stageData);

                PlayerState = new StagePlayerState();
                if (SaveManager.Instance != null)
                {
                    PlayerState.Gold = SaveManager.Instance.Gold;
                }

                await LoadStageDataAsync(new Progress<float>(p => {
                    session?.Report(Mathf.Lerp(0.1f, 0.8f, p));
                }), m_StageCts.Token);

                Debug.Log("[StageManager] MainGame 씬 로드 중...");
                AsyncOperation mainGameOp = SceneManager.LoadSceneAsync(m_MainGameSceneName, LoadSceneMode.Single);
                mainGameOp.allowSceneActivation = false;

                while (mainGameOp.progress < 0.9f)
                {
                    float normalized = mainGameOp.progress / 0.9f;
                    session?.Report(Mathf.Lerp(0.8f, 1f, normalized));
                    await Awaitable.NextFrameAsync();
                }

                session?.Report(1f);
                await Awaitable.NextFrameAsync();

                float remainingDisplayTime = MinLoadingDisplaySeconds - (Time.realtimeSinceStartup - loadingSceneEnteredAt);
                if (remainingDisplayTime > 0f)
                {
                    await Awaitable.WaitForSecondsAsync(remainingDisplayTime, m_StageCts.Token);
                }

                await ScreenFaderTransition.FadeOutAsync(LoadingExitFadeSeconds, m_StageCts.Token);

                mainGameOp.allowSceneActivation = true;
                while (!mainGameOp.isDone)
                {
                    await Awaitable.NextFrameAsync();
                }

                // Keep the loading overlay closed until the stage header and opening intro are primed.
                await WaitForMainGameReadyAsync(m_StageCts.Token);
                await ScreenFaderTransition.FadeInAsync(LoadingExitFadeSeconds, m_StageCts.Token);

                session?.Complete();
                }

                await RunStageLoopAsync(m_StageCts.Token);
            }
            catch (OperationCanceledException)
            {
                Debug.LogWarning("[StageManager] 스테이지 취소됨");
                await RecoverToLobbyAsync();
            }
            catch (Exception ex)
            {
                Debug.LogException(ex);
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

        public void ReturnToLobbyFromOptions()
        {
            if (m_IsReturningToLobby || !Application.isPlaying || SceneManager.GetActiveScene().name == m_LobbySceneName)
                return;

            ReturnToLobbyFromOptionsInternal();
        }

        private async void ReturnToLobbyFromOptionsInternal()
        {
            m_IsReturningToLobby = true;

            try
            {
                m_StageCts?.Cancel();
                await Awaitable.NextFrameAsync();
                await AsyncSceneLoader.LoadSceneAsync(m_LobbySceneName);
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
                QueueLobbyRewardPresentation();
                SetStageClearCommitState(StageClearCommitState.Committed);
            }
        }

        private async Awaitable RecoverToLobbyAsync()
        {
            try
            {
                if (!Application.isPlaying)
                    return;

                if (SceneManager.GetActiveScene().name != m_LobbySceneName)
                {
                    Debug.LogWarning("[StageManager] 오류/취소 복구를 위해 Lobby로 이동합니다.");
                    var op = SceneManager.LoadSceneAsync(m_LobbySceneName, LoadSceneMode.Single);
                    if (op != null)
                    {
                        await op.ToAwaitable();
                    }
                }
            }
            catch (Exception ex)
            {
                Debug.LogException(ex);
            }
        }

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
                    
                    // Commit before showing the result so the player can safely leave immediately.
                    ProcessPendingStageClearSave();
                }

                if (m_StageClearCommitState == StageClearCommitState.Pending || m_StageClearCommitState == StageClearCommitState.Failed)
                {
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
                        await ShowResultPanelAsync(true, token);
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
            return m_Context.CurrentCall <= 0 || m_CurrentRoundIndex >= m_CurrentStageData.TotalRounds - 1;
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
        }
    }
}
#endif
