#if UNITY_6000_0_OR_NEWER
using System;
using System.Threading;
using UnityEngine;
using UnityEngine.SceneManagement;
using DG.Tweening;

namespace InTheArena.MainGame
{
    /// <summary>
    /// 스테이지 전체 운영 관리
    /// Lobby -> Loading -> MainGame 씬 전환 및 라운드 진행 제어
    /// Manager_Base 상속으로 통합 관리 가능
    /// </summary>
    [DisallowMultipleComponent]
    public class StageManager : Manager_Base
    {
        private static StageManager _instance;

        /// <summary>
        /// 싱글톤 인스턴스 (씬 전환 시에도 유지)
        /// </summary>
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
        private int m_CurrentRoundIndex = 0;

        public StageData CurrentStageData => m_CurrentStageData;
        public RoundContext Context => m_Context;
        public bool IsStageRunning => m_IsStageRunning;
        public int CurrentRoundIndex => m_CurrentRoundIndex;

        public StagePlayerState PlayerState { get; private set; }

        private void Awake()
        {
            if (!ReferenceEquals(_instance, null) && _instance != null && _instance != this)
            {
                Debug.LogWarning("[StageManager] 중복 StageManager 인스턴스 감지 - 기존 인스턴스 파괴");
                Destroy(gameObject);
                return;
            }

            _instance = this;
            // StageManager는 DontDestroyOnLoad된 Managers 루트의 자식이다.
            // 자식 GameObject에 별도로 DontDestroyOnLoad를 호출하면 Unity가 오류를 낸다.
            m_Context = new RoundContext();
        }

        public override bool Setup() => true;

        protected override bool Init()
        {
            return true;
        }

        public override void Release()
        {
            Cleanup();
            base.Release();
        }

        protected override void OnDestroy()
        {
            base.OnDestroy();
            if (ReferenceEquals(_instance, this))
            {
                _instance = null;
            }
            Cleanup();
        }

        /// <summary>
        /// 스테이지 시작 코루틴
        /// 1. Lobby에서 Stage 선택 후 Loading 씬으로 이동
        /// 2. Loading 씬에서 MainGame 씬 비동기 로딩 + 데이터 로드
        /// 3. 로드 완료 후 MainGame으로 전환
        /// </summary>
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

            using var session = InTheArena.Util.LoadingProgressService.Instance?.BeginSession();

            try
            {
                session?.Report(0f);

                // 1. Loading 씬으로 이동
                Debug.Log($"[StageManager] {stageData.FullStageName} 스테이지 시작 - Loading 씬 로드 중...");
                await SceneManager.LoadSceneAsync(m_LoadingSceneName, LoadSceneMode.Single).ToAwaitable();

                session?.Report(0.1f);

                // 2. 데이터 로드 (Loading 씬에서 진행 표시)
                m_Context.Clear();
                m_Context.InitializeStage(stageData);
                
                PlayerState = new StagePlayerState();
                if (SaveManager.Instance != null)
                {
                    PlayerState.CopyFrom(SaveManager.Instance.Data);
                }

                // 로딩 진행도 시뮬레이션 (실제로는 AssetBundle/Addressables 로드)
                await LoadStageDataAsync(new Progress<float>(p => {
                    session?.Report(Mathf.Lerp(0.1f, 0.8f, p));
                }), m_StageCts.Token);

                // 3. MainGame 씬 로드
                Debug.Log("[StageManager] MainGame 씬 로드 중...");
                AsyncOperation mainGameOp = SceneManager.LoadSceneAsync(m_MainGameSceneName, LoadSceneMode.Single);
                mainGameOp.allowSceneActivation = false;

                // MainGame 로드 시작 시점부터는 취소를 검사하지 않습니다 (무조건 진입 보장)
                while (mainGameOp.progress < 0.9f)
                {
                    float normalized = mainGameOp.progress / 0.9f;
                    session?.Report(Mathf.Lerp(0.8f, 1f, normalized));
                    await Awaitable.NextFrameAsync(); // token 미전달
                }

                session?.Report(1f);
                await Awaitable.NextFrameAsync(); // token 미전달

                mainGameOp.allowSceneActivation = true;
                while (!mainGameOp.isDone)
                {
                    await Awaitable.NextFrameAsync(); // token 미전달
                }

                session?.Complete();

                // 4. MainGame 씬 초기화 완료 대기
                await WaitForMainGameReadyAsync(m_StageCts.Token);

                // 5. 라운드 루프 시작
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
                Cleanup();
            }
        }

        private async Awaitable RecoverToLobbyAsync()
        {
            try
            {
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
            // StageData, RoundData, UnitData 등 로드
            // 실제로는 Addressables/AssetBundle 사용 권장
            
            // StageData 검증
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

            // 각 라운드 데이터 검증
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
                // Loading UI 업데이트 이벤트 발생 가능
                await Awaitable.NextFrameAsync(token);
            }

            Debug.Log("[StageManager] 모든 데이터 로드 완료");
        }

        private async Awaitable WaitForMainGameReadyAsync(CancellationToken token)
        {
            // RoundManager가 씬에 존재하고 초기화될 때까지 대기
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

            // RoundManager에 컨텍스트 전달
            RoundManager.Instance.InitializeContext(
                m_Context,
                m_CurrentStageData,
                PlayerState);
        }

        private async Awaitable RunStageLoopAsync(CancellationToken token)
        {
            int totalRounds = m_CurrentStageData.TotalRounds;

            while (m_CurrentRoundIndex < totalRounds)
            {
                token.ThrowIfCancellationRequested();

                m_Context.CurrentRound = m_CurrentRoundIndex + 1;
                Debug.Log($"[StageManager] Round {m_Context.CurrentRound} / {totalRounds} 시작");

                // 라운드 진행 (RoundManager가 처리)
                await RoundManager.Instance.RunRoundAsync(m_CurrentRoundIndex, token);

                // 게임 클리어/게임 오버 체크
                if (CheckStageClear())
                {
                    SaveManager.Instance?.GrantStageClearReward(m_CurrentStageData.StageNum);
                    Debug.Log("[StageManager] STAGE CLEAR!");
                    await ShowResultPanelAsync(true, token);
                    break;
                }


                if (CheckGameOver())
                {
                    Debug.Log("[StageManager] GAME OVER!");
                    await ShowResultPanelAsync(false, token);
                    break;
                }

                // 다음 라운드로
                m_CurrentRoundIndex++;

                // Result -> next Betting: 1-second fade-out transition.
                await ScreenFaderTransition.FadeOutAsync(1f, token);
            }

            // 로비로 돌아가기
            await ReturnToLobbyAsync();
        }

        private bool CheckStageClear()
        {
            if (m_Context.CurrentCall >= m_CurrentStageData.TargetCall)
            {
                if (PlayerState != null)
                {
                    if (SaveManager.Instance != null)
                    {
                        if (SaveManager.Instance.Data != null)
                        {
                            PlayerState.ApplyTo(SaveManager.Instance.Data);
                            SaveManager.Instance.Save();
                        }
                    }
                }
                return true;
            }
            return false;
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
            
            panel.Prepare(isClear, m_Context.CurrentCall, m_CurrentStageData.TargetCall);

            try
            {
                await ScreenFaderTransition.FadeInAsync(1f, token);
                panel.EnableInput();
                await panel.WaitForCompletionAsync(token);
            }
            finally
            {
                if (panel != null)
                {
                    panel.Close();
                }
            }
        }

        private async Awaitable ReturnToLobbyAsync()
        {
            // 데이터 메모리 해제
            m_CurrentStageData = null;
            m_Context?.Clear();
            
            if (m_StageCts != null && m_StageCts.IsCancellationRequested)
                return;

            // Lobby 씬으로 이동
            var op = SceneManager.LoadSceneAsync(m_LobbySceneName, LoadSceneMode.Single);
            if (op != null)
            {
                await op.ToAwaitable();
            }
        }

        /// <summary>
        /// 외부에서 현재 스테이지 데이터 설정 (Lobby에서 호출)
        /// </summary>
        public void SetCurrentStageData(StageData stageData)
        {
            m_CurrentStageData = stageData;
        }

        private void Cleanup()
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

            m_Context?.Clear();
            PlayerState = null;
        }
    }
}
#endif
