#if UNITY_6000_0_OR_NEWER
using System;
using System.Threading;
using UnityEngine;
using DG.Tweening;

/// <summary>
/// MainGame 씬 내부 생명주기에 종속되는 씬 전역 컨트롤러입니다.
/// DontDestroyOnLoad 대상이 아니므로 씬 파괴 시 함께 안전하게 정지됩니다.
/// </summary>
[DisallowMultipleComponent]
public class RoundManager : MonoBehaviour
{
    private static RoundManager _instance;

    /// <summary>
    /// MainGame 씬 내부에서만 유효한 싱글톤 프로퍼티입니다.
    /// 다른 씬에서는 null을 반환합니다.
    /// </summary>
    public static RoundManager Instance
    {
        get
        {
            // [High Safety] 유니티 가짜 Null 감지
            if (ReferenceEquals(_instance, null) || _instance == null)
            {
                return null;
            }
            return _instance;
        }
    }

    [Header("Scene-Bound Phases")]
    [SerializeField] private BettingPhase m_BettingPhase;
    [SerializeField] private RoundPhaseBase m_CombatPhase;
    [SerializeField] private RoundPhaseBase m_ResultPhase;

    private RoundContext m_Context;
    private CancellationTokenSource m_SceneCts;

    public RoundContext Context => m_Context;
    public bool IsRunning { get; private set; }

    private void Awake()
    {
        if (!ReferenceEquals(_instance, null) && _instance != null && _instance != this)
        {
            Debug.LogWarning("[RoundManager] 중복 씬 컨트롤러가 감지되어 파괴합니다.");
            Destroy(gameObject);
            return;
        }

        _instance = this;
        m_Context = new RoundContext();
    }

    private void Start()
    {
        m_SceneCts = new CancellationTokenSource();
        StartGameLoopAsync(m_SceneCts.Token);
    }

    /// <summary>
    /// MainGame 씬의 메인 라운드 비동기 루프
    /// </summary>
    private async void StartGameLoopAsync(CancellationToken token)
    {
        IsRunning = true;

        try
        {
            m_Context.CurrentRound = 1;
            m_Context.CurrentCall = 100;

            while (m_Context.CurrentRound <= m_Context.MaxRounds)
            {
                token.ThrowIfCancellationRequested();

                // 1. 배팅 페이즈
                if (m_BettingPhase != null)
                {
                    m_BettingPhase.InitializePhase(m_Context);
                    await m_BettingPhase.EnterPhaseAsync(token);
                    await m_BettingPhase.ExitPhaseAsync(token);
                }

                // 2. 전투 페이즈
                if (m_CombatPhase != null)
                {
                    m_CombatPhase.InitializePhase(m_Context);
                    await m_CombatPhase.EnterPhaseAsync(token);
                    await m_CombatPhase.ExitPhaseAsync(token);
                }

                // 3. 결과 페이즈
                if (m_ResultPhase != null)
                {
                    m_ResultPhase.InitializePhase(m_Context);
                    await m_ResultPhase.EnterPhaseAsync(token);
                    await m_ResultPhase.ExitPhaseAsync(token);
                }

                // 승패 판정
                if (m_Context.CurrentCall >= m_Context.TargetCall)
                {
                    OnGameClear();
                    return;
                }

                m_Context.CurrentRound++;
            }

            if (m_Context.CurrentCall >= m_Context.TargetCall)
            {
                OnGameClear();
            }
            else
            {
                OnGameOver();
            }
        }
        catch (OperationCanceledException)
        {
            Debug.Log("[RoundManager] MainGame 씬 종료에 따라 라운드 루프가 안전하게 취소되었습니다.");
        }
        catch (Exception ex)
        {
            Debug.LogException(ex);
        }
        finally
        {
            IsRunning = false;
        }
    }

    private void OnGameClear()
    {
        Debug.Log("[RoundManager] GAME CLEAR!");
    }

    private void OnGameOver()
    {
        Debug.Log("[RoundManager] GAME OVER!");
    }

    private void OnDestroy()
    {
        // 씬 언로드 시 비동기 루프 취소
        if (m_SceneCts != null)
        {
            m_SceneCts.Cancel();
            m_SceneCts.Dispose();
            m_SceneCts = null;
        }

        // 씬 언로드 시 트윈 정리
        transform.DOKill();

        if (ReferenceEquals(_instance, this))
        {
            _instance = null;
        }
    }
}
#endif