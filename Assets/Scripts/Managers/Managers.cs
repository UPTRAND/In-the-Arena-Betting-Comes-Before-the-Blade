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
    [Tooltip("초기화할 매니저들을 Inspector에서 할당하거나 동적으로 추가합니다.")]
    [SerializeField] private List<Manager_Base> _allManagers = new List<Manager_Base>();

    [Header("Application Settings")]
    [SerializeField] private int _targetFrameRate = 60;
    [SerializeField] private int _vSyncCount = 0;

    private CancellationTokenSource _startupCts;
    public bool IsInitialized { get; private set; }

    /// <summary>
    /// 싱글톤 설정 및 파괴 방지
    /// </summary>
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
    /// 기존 Update()에서 매 프레임 확인하던 Vsync/FPS 로직을 최적화.
    /// 앱의 포커스 상태가 변경될 때만 실행하여 네이티브-매니지드 오버헤드를 제거합니다.
    /// </summary>
    private void OnApplicationFocus(bool hasFocus)
    {
        if (hasFocus)
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

    /// <summary>
    /// Unity 6000의 Awaitable을 사용한 최적화된 비동기 매니저 초기화 로직입니다.
    /// IEnumerator(Coroutine) 사용 시 발생하는 GC 할당을 방지합니다.
    /// </summary>
    private async Awaitable InitializeManagersAsync(CancellationToken token)
    {
        try
        {
            // 1. ManagerBase의 CompareTo(순서) 기반으로 정렬
            _allManagers.Sort();

            foreach (var manager in _allManagers)
            {
                // [High Safety] 진행 중 앱이 종료되거나 오브젝트가 파괴되면 즉시 중단
                token.ThrowIfCancellationRequested();

                // 가짜 Null(파괴된 오브젝트) 검증
                if (ReferenceEquals(manager, null) || manager == null)
                {
                    continue;
                }

                if (!manager.TryInitialize())
                {
                    Debug.LogError($"[Managers] {manager.gameObject.name} 초기화 실패. 전체 초기화 시퀀스를 중단합니다.");
                    return;
                }

                // 다음 프레임까지 대기하여 메인 스레드 블로킹 방지
                await Awaitable.NextFrameAsync(token);
            }

            IsInitialized = true;
            Debug.Log("[Managers] 모든 매니저 초기화 완료.");
        }
        catch (OperationCanceledException)
        {
            Debug.Log("[Managers] 매니저 초기화가 취소되었습니다 (앱 종료 또는 파괴).");
        }
        catch (Exception ex)
        {
            Debug.LogException(ex);
        }
    }

    /// <summary>
    /// 앱 종료 시 모든 매니저의 Release를 호출하여 데이터를 안전하게 저장/해제합니다.
    /// </summary>
    private void OnApplicationQuitting()
    {
        Debug.Log("[Managers] Application Quitting... 매니저 리소스를 해제합니다.");

        for (int i = _allManagers.Count - 1; i >= 0; i--)
        {
            var manager = _allManagers[i];
            if (!ReferenceEquals(manager, null) && manager != null)
            {
                try
                {
                    manager.Release();
                }
                catch (Exception ex)
                {
                    Debug.LogException(ex);
                }
            }
        }
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
    }
}
#endif