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
        public new static StageManager Instance
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
        
        public new bool IsInitialized { get; private set; }

        private RoundContext m_Context;
        private CancellationTokenSource m_StageCts;
        private bool m_IsStageRunning = false;
        private int m_CurrentRoundIndex = 0;

        public StageData CurrentStageData => m_CurrentStageData;
        public RoundContext Context => m_Context;
        public bool IsStageRunning => m_IsStageRunning;
        public int CurrentRoundIndex => m_CurrentRoundIndex;

        private void Awake()
        {
            if (!ReferenceEquals(_instance, null) && _instance != null && _instance != this)
            {
                Debug.LogWarning("[StageManager] 중복 StageManager 인스턴스 감지 - 기존 인스턴스 파괴");
                Destroy(gameObject);
                return;
            }

            _instance = this;
            DontDestroyOnLoad(gameObject);
            m_Context = new RoundContext();
        }

        public override bool Setup() => true;

        protected override bool Init()
        {
            IsInitialized = true;
            return true;
        }

        public new bool TryInitialize()
        {
            if (IsInitialized) return true;

            if (!Setup())
            {
                Debug.LogError($"[{gameObject.name}] StageManager Setup Failed.");
                return false;
            }

            IsInitialized = Init();
            return IsInitialized;
        }

        public new void Release()
        {
            IsInitialized = false;
            Cleanup();
        }

        /// <summary>
        /// 스테이지 시작 파이프라인
        /// 1. Lobby에서 Stage 선택 시 Loading 씬으로 이동
        /// 2. Loading 씬에서 MainGame 씬 비동기 로딩 + 데이터 로드
        /// 3. 로드 완료 시 MainGame으로 전환
        /// </summary>
        public async Awaitable StartStageAsync(StageData stageData, CancellationToken token = default)
        {
            if (stageData == null || !stageData.IsValid())
            {
                Debug.LogError("[StageManager] 유효하지 않은 스테이지 데이터입니다.");
                return;
            }

            m_CurrentStageData = stageData;
            m_StageCts = CancellationTokenSource.CreateLinkedTokenSource(token);
            m_IsStageRunning = true;
            m_CurrentRoundIndex = 0;

            try
            {
                // 1. Loading 씬으로 이동
                Debug.Log($"[StageManager] {stageData.FullStageName} 스테이지 시작 - Loading 씬 로드 중...");
                await SceneManager.LoadSceneAsync(m_LoadingSceneName, LoadSceneMode.Single).ToAwaitable();

                // 2. 데이터 로드 (Loading 씬에서 진행 표시)
                m_Context.Clear();
                m_Context.CurrentCoin = stageData.InitialCoin;
                
                // 로딩 진행도 시뮬레이션 (실제로는 AssetBundle/Addressables 로드)
                await LoadStageDataAsync(m_StageCts.Token);

                // 3. MainGame 씬 로드
                Debug.Log("[StageManager] MainGame 씬 로드 중...");
                await SceneManager.LoadSceneAsync(m_MainGameSceneName, LoadSceneMode.Single).ToAwaitable();

                // 4. MainGame 씬 초기화 완료 대기
                await WaitForMainGameReadyAsync(m_StageCts.Token);

                // 5. 라운드 루프 시작
                await RunStageLoopAsync(m_StageCts.Token);
            }
            catch (OperationCanceledException)
            {
                Debug.Log("[StageManager] 스테이지 취소됨");
            }
            catch (Exception ex)
            {
                Debug.LogException(ex);
            }
            finally
            {
                Cleanup();
            }
        }

        private async Awaitable LoadStageDataAsync(CancellationToken token)
        {
            // StageData, RoundData, UnitData 등 로드
            // 실제로는 Addressables/AssetBundle 사용 권장
            float progress = 0f;
            
            // StageData 검증
            if (!m_CurrentStageData.IsValid())
            {
                throw new InvalidOperationException("스테이지 데이터 검증 실패");
            }

            // 각 라운드 데이터 검증
            for (int i = 0; i < m_CurrentStageData.RoundDatas.Count; i++)
            {
                token.ThrowIfCancellationRequested();
                
                var roundData = m_CurrentStageData.RoundDatas[i];
                if (!roundData.IsValid())
                {
                    throw new InvalidOperationException($"라운드 {i + 1} 데이터 검증 실패");
                }

                progress = (float)(i + 1) / m_CurrentStageData.RoundDatas.Count;
                // Loading UI 업데이트 이벤트 발생 가능
                await Awaitable.NextFrameAsync();
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
            RoundManager.Instance.InitializeContext(m_Context, m_CurrentStageData);
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

                // 라운드 결과 처리
                ProcessRoundResult();

                // 게임 클리어/오버 체크
                if (CheckStageClear())
                {
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
                
                // 라운드 간 짧은 대기
                await Awaitable.WaitForSecondsAsync(1f);
            }

            // 로비로 돌아가기
            await ReturnToLobbyAsync();
        }

        private void ProcessRoundResult()
        {
            if (!m_Context.IsRoundCompleted) return;

            if (m_Context.DidTeamAWin)
            {
                // 승리: 베팅한 만큼 1.5배 획득 (단순화: TeamA 베팅 비율 * 콜 * 1.5)
                int reward = Mathf.RoundToInt(m_Context.CurrentCall * (m_Context.TeamABetRatio / 100f) * 1.5f);
                m_Context.CurrentCoin += reward;
                Debug.Log($"[StageManager] 라운드 승리! +{reward} 코인 (현재: {m_Context.CurrentCoin})");
            }
            else
            {
                // 패배: 베팅한 코인 잃음 (TeamA 베팅분)
                int loss = Mathf.RoundToInt(m_Context.CurrentCall * (m_Context.TeamABetRatio / 100f));
                m_Context.CurrentCoin -= loss;
                Debug.Log($"[StageManager] 라운드 패배! -{loss} 코인 (현재: {m_Context.CurrentCoin})");
            }
        }

        private bool CheckStageClear()
        {
            return m_Context.CurrentCoin >= m_CurrentStageData.TargetCall;
        }

        private bool CheckGameOver()
        {
            return m_Context.CurrentCoin <= 0 || m_CurrentRoundIndex >= m_CurrentStageData.TotalRounds - 1;
        }

        private async Awaitable ShowResultPanelAsync(bool isClear, CancellationToken token)
        {
            // UIManager를 통해 결과 패널 표시
            if (UIManager.Instance != null)
            {
                await UIManager.Instance.ShowStageResultAsync(isClear, m_Context.CurrentCoin, m_CurrentStageData.TargetCall, token);
            }
            
            // 사용자 입력 대기 (로비로 돌아가기 버튼 클릭)
            await WaitForLobbyReturnAsync(token);
        }

        private async Awaitable WaitForLobbyReturnAsync(CancellationToken token)
        {
            // UI에서 로비 버튼 클릭 시 완료되는 Awaitable 대기
            // 구현은 UIManager에서 처리
            while (!token.IsCancellationRequested)
            {
                await Awaitable.NextFrameAsync();
            }
        }

        private async Awaitable ReturnToLobbyAsync()
        {
            // 데이터 메모리 해제
            m_CurrentStageData = null;
            m_Context?.Clear();
            
            // Lobby 씬으로 이동
            await SceneManager.LoadSceneAsync(m_LobbySceneName, LoadSceneMode.Single).ToAwaitable();
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
            
            if (m_StageCts != null)
            {
                m_StageCts.Cancel();
                m_StageCts.Dispose();
                m_StageCts = null;
            }

            m_Context?.Clear();
        }

        private new void OnDestroy()
        {
            if (ReferenceEquals(_instance, this))
            {
                _instance = null;
            }
            Cleanup();
        }
    }
}
#endif