#if UNITY_6000_0_OR_NEWER
using System;
using System.Threading;
using UnityEngine;
using DG.Tweening;

namespace InTheArena.MainGame
{
    /// <summary>
    /// MainGame 씬의 라운드 전체 흐름을 관리
    /// BettingPhase -> CombatPhase -> ResultPhase 순서로 진행
    /// </summary>
    [DisallowMultipleComponent]
    public class RoundManager : MonoBehaviour
    {
        private static RoundManager _instance;

        /// <summary>
        /// MainGame 씬의 라운드 매니저 싱글톤 인스턴스
        /// 씬 언로드 시 null 반환
        /// </summary>
        public static RoundManager Instance
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

        [Header("Phase References")]
        [SerializeField] private BettingPhase m_BettingPhase;
        [SerializeField] private CombatPhase m_CombatPhase;
        [SerializeField] private ResultPhase m_ResultPhase;

        private RoundContext m_Context;
        private StageData m_CurrentStageData;
        private CancellationTokenSource m_RoundCts;
        private int m_CurrentRoundIndex = 0;
        private bool m_IsRoundRunning = false;

        public RoundContext Context => m_Context;
        public StageData CurrentStageData => m_CurrentStageData;
        public bool IsRoundRunning => m_IsRoundRunning;
        public int CurrentRoundIndex => m_CurrentRoundIndex;

        private void Awake()
        {
            if (!ReferenceEquals(_instance, null) && _instance != null && _instance != this)
            {
                Debug.LogWarning("[RoundManager] 중복 RoundManager 인스턴스 감지 - 기존 인스턴스 파괴");
                Destroy(gameObject);
                return;
            }

            _instance = this;
            m_Context = new RoundContext();
        }

        /// <summary>
                /// StageManager에서 호출하여 컨텍스트와 스테이지 데이터 초기화
                /// </summary>
                public void InitializeContext(RoundContext context, StageData stageData)
                {
                    m_Context = context ?? new RoundContext();
                    m_CurrentStageData = stageData;
                    m_Context.CurrentStageData = stageData;  // Context에도 스테이지 데이터 설정
                    Debug.Log($"[RoundManager] 컨텍스트 초기화 완료 - 스테이지: {stageData?.FullStageName}");
                }

        /// <summary>
        /// 지정된 라운드 실행
        /// </summary>
        public async Awaitable RunRoundAsync(int roundIndex, CancellationToken token)
        {
            if (m_CurrentStageData == null)
            {
                Debug.LogError("[RoundManager] 스테이지 데이터가 설정되지 않았습니다.");
                return;
            }

            if (roundIndex >= m_CurrentStageData.RoundDatas.Count)
            {
                Debug.LogError($"[RoundManager] 라운드 인덱스 {roundIndex}가 범위를 벗어났습니다. (최대: {m_CurrentStageData.RoundDatas.Count - 1})");
                return;
            }

            m_CurrentRoundIndex = roundIndex;
            m_RoundCts = CancellationTokenSource.CreateLinkedTokenSource(token);
            m_IsRoundRunning = true;

            try
                        {
                            // 1. 라운드 데이터 설정
                            SetupRoundData(roundIndex);

                            // 2. Betting Phase
                            Debug.Log($"[RoundManager] Round {roundIndex + 1} - Betting Phase 시작");
                            m_BettingPhase.InitializePhase(m_Context);
                            await m_BettingPhase.EnterPhaseAsync(m_RoundCts.Token);
                            await m_BettingPhase.ExitPhaseAsync(m_RoundCts.Token);

                            token.ThrowIfCancellationRequested();

                            // 3. Combat Phase
                            Debug.Log($"[RoundManager] Round {roundIndex + 1} - Combat Phase 시작");
                            m_CombatPhase.InitializePhase(m_Context);
                            await m_CombatPhase.EnterPhaseAsync(m_RoundCts.Token);
                            await m_CombatPhase.ExitPhaseAsync(m_RoundCts.Token);

                            token.ThrowIfCancellationRequested();

                            // 4. Result Phase
                            Debug.Log($"[RoundManager] Round {roundIndex + 1} - Result Phase 시작");
                            m_ResultPhase.InitializePhase(m_Context);
                            await m_ResultPhase.EnterPhaseAsync(m_RoundCts.Token);

                // 결과 데이터 수집
                m_Context.IsRoundCompleted = true;
                m_Context.DidTeamAWin = m_ResultPhase.IsWin();
                int rewardCall = m_ResultPhase.GetRewardCall();
                
                if (m_Context.DidTeamAWin)
                {
                    m_Context.CurrentCall += rewardCall;
                }
                else
                {
                    m_Context.CurrentCall = Mathf.Max(0, m_Context.CurrentCall - rewardCall);
                }

                await m_ResultPhase.ExitPhaseAsync(m_RoundCts.Token);

                m_Context.IsRoundCompleted = true;
            }
            catch (OperationCanceledException)
            {
                Debug.Log($"[RoundManager] Round {roundIndex + 1} 취소됨");
            }
            catch (Exception ex)
            {
                Debug.LogException(ex);
            }
            finally
            {
                m_IsRoundRunning = false;
                
                if (m_RoundCts != null)
                {
                    m_RoundCts.Dispose();
                    m_RoundCts = null;
                }
            }
        }

        private void SetupRoundData(int roundIndex)
        {
            var roundData = m_CurrentStageData.RoundDatas[roundIndex];
            
            // 컨텍스트에 라운드 데이터 적용
            m_Context.CurrentRound = roundIndex + 1;
            m_Context.MaxRounds = m_CurrentStageData.TotalRounds;
            m_Context.TargetCall = m_CurrentStageData.TargetCall;
            
            // 유닛 데이터 설정
            m_Context.TeamAUnitDatas = roundData.GetTeamAUnits();
            m_Context.TeamBUnitDatas = roundData.GetTeamBUnits();
            
            // 베팅 기본값
            m_Context.TeamABetRatio = Mathf.RoundToInt(roundData.DefaultBetRatioA);
            m_Context.TeamBBetRatio = Mathf.RoundToInt(roundData.DefaultBetRatioB);
            m_Context.CurrentRoundRule = roundData.SpecialRule;

            // 특별 규칙 적용
            ApplyRoundRule(roundData.SpecialRule);
        }

        private void ApplyRoundRule(RoundRule rule)
        {
            switch (rule)
            {
                case RoundRule.DoubleDamage:
                    // 모든 유닛 데미지 2배 - Unit에서 처리
                    break;
                case RoundRule.HalfHeal:
                    // 회복량 50% 감소
                    break;
                case RoundRule.NoSkills:
                    // 스킬 사용 불가 - AI/Skill에서 처리
                    break;
                case RoundRule.SpeedUp:
                    // 공격/이동 속도 2배 - Unit 스탯에서 처리
                    break;
                case RoundRule.SuddenDeath:
                    // 체력 1로 설정
                    break;
            }
        }

        /// <summary>
        /// 현재 라운드 강제 종료
        /// </summary>
        public void ForceEndRound()
        {
            if (m_RoundCts != null)
            {
                m_RoundCts.Cancel();
            }
        }

        private void OnDestroy()
        {
            if (ReferenceEquals(_instance, this))
            {
                _instance = null;
            }
        }
    }
}
#endif