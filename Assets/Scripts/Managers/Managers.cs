#if UNITY_6000_0_OR_NEWER
using System;
using System.Collections.Generic;
using System.Threading;
using UnityEngine;

[DisallowMultipleComponent]
public class Managers : MonoBehaviour
{
    private static Managers _instance;
    public static Managers Instance => _instance;

    [Header("Registered Managers")]
    [SerializeField] private List<Manager_Base> _allManagers = new List<Manager_Base>();

    [Header("Application Settings")]
    [SerializeField] private int _targetFrameRate = 60;
    [SerializeField] private int _vSyncCount = 0;

    private CancellationTokenSource _startupCts;
    private bool _released;
    public bool IsInitialized { get; private set; }

    private void Awake()
    {
        if (!ReferenceEquals(_instance, null) && _instance != null && _instance != this)
        {
            Debug.LogWarning("[Managers] 중복된 매니저 인스턴스가 발견되어 파괴합니다.");
            Destroy(gameObject);
            return;
        }

        _instance = this;
        DontDestroyOnLoad(gameObject);

        Application.quitting += OnApplicationQuitting;
        ApplyPerformanceSettings();
    }

    private async void Start()
    {
        _startupCts = new CancellationTokenSource();
        await InitializeManagersAsync(_startupCts.Token);
    }

    /// <summary>
    /// 질문 답변: OnApplicationFocus는 모바일에서 반드시 필요합니다.
    /// 안드로이드 시스템 권한 팝업, 상단 바 내려옴, 인앱 결제 창 등 '포커스만 잃고 앱은 완전히 일시정지되지 않은 상태'를 감지합니다.
    /// </summary>
    private void OnApplicationFocus(bool hasFocus)
    {
        NotifyFocusChanged(hasFocus);
        if (hasFocus)
        {
            ApplyPerformanceSettings();
        }
        else
        {
            // 오버레이가 켜졌을 때 사운드 일시정지 또는 게임 내 타이머 정지 등의 처리가 필요합니다.
        }
    }

    /// <summary>
    /// 안드로이드 필수 추가: 홈 버튼을 누르거나 작업 전환기로 이동할 때 호출됩니다.
    /// 안드로이드는 유저가 앱을 밀어서 종료(Swipe-Kill)하면 Quitting 이벤트가 오지 않고 프로세스가 즉시 살해되므로,
    /// 반드시 Pause(true) 시점에 중요 데이터를 저장해야 합니다.
    /// </summary>
    private void OnApplicationPause(bool pauseStatus)
    {
        NotifyPauseChanged(pauseStatus);
        if (pauseStatus)
        {
            Debug.Log("[Managers] 안드로이드 백그라운드 전환: Manager와 Pool 상태를 유지합니다.");
        }
        else
        {
            ApplyPerformanceSettings();
        }
    }

    private void ApplyPerformanceSettings()
    {
        if (QualitySettings.vSyncCount != _vSyncCount)
            QualitySettings.vSyncCount = _vSyncCount;

        if (Application.targetFrameRate != _targetFrameRate)
            Application.targetFrameRate = _targetFrameRate;
    }

    private async Awaitable InitializeManagersAsync(CancellationToken token)
    {
        try
        {
            _allManagers.Sort();

            foreach (var manager in _allManagers)
            {
                token.ThrowIfCancellationRequested();

                if (ReferenceEquals(manager, null) || manager == null)
                {
                    continue;
                }

                if (!manager.TryInitialize())
                {
                    Debug.LogError($"[Managers] {manager.gameObject.name} 초기화 실패.");
                    return;
                }

                // 매니저가 많을 경우 매 프레임 대기하면 초기화(로딩)가 과도하게 길어짐
                // 씬 로딩바 단계에서 순차 처리가 필요하다면 유지하되, 무조건적인 NextFrame 대기는 오히려 프레임 드랍을 유발
                await Awaitable.NextFrameAsync(token);
            }

            IsInitialized = true;
            Debug.Log("[Managers] 모든 매니저 초기화 완료.");
        }
        catch (OperationCanceledException)
        {
            Debug.Log("[Managers] 초기화 취소됨.");
        }
        catch (Exception ex)
        {
            Debug.LogException(ex);
        }
    }

    private void NotifyPauseChanged(bool paused)
    {
        for (int i = 0; i < _allManagers.Count; i++)
        {
            var manager = _allManagers[i];
            if (ReferenceEquals(manager, null) || manager == null) continue;
            try
            {
                manager.OnApplicationPauseChanged(paused);
            }
            catch (Exception ex) { Debug.LogException(ex); }
        }
    }

    private void NotifyFocusChanged(bool hasFocus)
    {
        for (int i = 0; i < _allManagers.Count; i++)
        {
            var manager = _allManagers[i];
            if (ReferenceEquals(manager, null) || manager == null) continue;
            try { manager.OnApplicationFocusChanged(hasFocus); }
            catch (Exception ex) { Debug.LogException(ex); }
        }
    }

    private void ReleaseAllManagers()
    {
        if (_released) return;
        _released = true;
        for (int i = _allManagers.Count - 1; i >= 0; i--)
        {
            var manager = _allManagers[i];
            if (ReferenceEquals(manager, null) || manager == null) continue;
            try { manager.Release(); }
            catch (Exception ex) { Debug.LogException(ex); }
        }
        IsInitialized = false;
    }

    private void OnApplicationQuitting()
    {
        Debug.Log("[Managers] Application Quitting...");
        ReleaseAllManagers();
    }

    private void OnDestroy()
    {
        Application.quitting -= OnApplicationQuitting;

        if (_startupCts != null)
        {
            _startupCts.Cancel();
            _startupCts.Dispose();
            _startupCts = null;
        }

        ReleaseAllManagers();
        if (_instance == this) _instance = null;
    }
}
#endif
