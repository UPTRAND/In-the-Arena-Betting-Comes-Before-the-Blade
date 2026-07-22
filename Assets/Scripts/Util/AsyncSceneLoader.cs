#if UNITY_6000_0_OR_NEWER
using System;
using System.Threading;
using UnityEngine;
using UnityEngine.SceneManagement;
using DG.Tweening;

[DisallowMultipleComponent]
public class AsyncSceneLoader : MonoBehaviour
{
    private static AsyncSceneLoader _instance;
    public static AsyncSceneLoader Instance => _instance;

    public const string LOADING_SCENE_NAME = "Loading";

    /// <summary>
    /// 현재 비동기 씬 로딩이 진행 중인지 여부
    /// </summary>
    public static bool IsLoading { get; private set; }

    /// <summary>
    /// 로딩 진행률 (0.0 ~ 1.0) - Loading 씬의 프로그레스 바 UI와 연동할 수 있습니다.
    /// </summary>
    public static float LoadingProgress { get; private set; }

    private CancellationTokenSource _cts;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    private static void AutoInitialize()
    {
        EnsureInstanceExists();
    }

    private static void EnsureInstanceExists()
    {
        // [High Safety] 유니티 가짜 Null 및 순수 C# 참조 동시 검사
        if (!ReferenceEquals(_instance, null) && _instance != null) return;

        var go = new GameObject("[SceneLoader]");
        _instance = go.AddComponent<AsyncSceneLoader>();
        DontDestroyOnLoad(go);
    }

    private void Awake()
    {
        if (!ReferenceEquals(_instance, null) && _instance != null && _instance != this)
        {
            Destroy(gameObject);
            return;
        }

        _instance = this;
        DontDestroyOnLoad(gameObject);
        _cts = new CancellationTokenSource();
    }

    #region Single-Line Public API
    /// <summary>
    /// [한 줄 편의성 API] 씬 이름을 전달하여 즉시 비동기 로딩 전환을 수행합니다.
    /// 사용 예: SceneLoader.LoadScene("DungeonScene");
    /// </summary>
    public static void LoadScene(string targetSceneName)
    {
        if (string.IsNullOrEmpty(targetSceneName))
        {
            Debug.LogError("[SceneLoader] 로딩할 타겟 씬 이름이 올바르지 않습니다.");
            return;
        }

        if (IsLoading)
        {
            Debug.LogWarning("[SceneLoader] 이미 다른 씬 로딩이 진행 중입니다.");
            return;
        }

        EnsureInstanceExists();

        // 씬 존재 여부 예외 방어
        if (!Application.CanStreamedLevelBeLoaded(targetSceneName))
        {
            Debug.LogError($"[SceneLoader] Build Settings에 등록되지 않은 씬입니다: {targetSceneName}");
            return;
        }

        if (!Application.CanStreamedLevelBeLoaded(LOADING_SCENE_NAME))
        {
            Debug.LogError($"[SceneLoader] 중간 로딩 씬({LOADING_SCENE_NAME})이 Build Settings에 없습니다.");
            return;
        }

        _instance.StartLoadSequenceAsync(targetSceneName, _instance._cts.Token);
    }
    #endregion

    /// <summary>
    /// Unity 6000 Awaitable 기반 비동기 씬 로딩 시퀀스
    /// </summary>
    private async void StartLoadSequenceAsync(string targetSceneName, CancellationToken token)
    {
        IsLoading = true;
        LoadingProgress = 0f;

        try
        {
            // 1. 씬 전환 전 기존 씬의 모든 DOTween 애니메이션 안전 해제 (메모리 누수 방지)
            DOTween.KillAll();

            // 2. "Loading" 중간 씬으로 우선 비동기 이동
            AsyncOperation loadingSceneOp = SceneManager.LoadSceneAsync(LOADING_SCENE_NAME);
            while (!loadingSceneOp.isDone)
            {
                token.ThrowIfCancellationRequested();
                await Awaitable.NextFrameAsync(token);
            }

            // 3. 목적지 씬 비동기 로딩 개시 (0.9 단계에서 자동 전환 대기)
            AsyncOperation targetSceneOp = SceneManager.LoadSceneAsync(targetSceneName);
            targetSceneOp.allowSceneActivation = false;

            while (targetSceneOp.progress < 0.9f)
            {
                token.ThrowIfCancellationRequested();

                // 0.0 ~ 0.9 구간을 0.0 ~ 1.0 진행률로 정규화
                LoadingProgress = Mathf.Clamp01(targetSceneOp.progress / 0.9f);
                await Awaitable.NextFrameAsync(token);
            }

            // 비동기 준비 완료시 진행률 100% 도달
            LoadingProgress = 1.0f;
            await Awaitable.NextFrameAsync(token);

            // 4. 로딩 완료 즉시 씬 활성화
            targetSceneOp.allowSceneActivation = true;

            while (!targetSceneOp.isDone)
            {
                token.ThrowIfCancellationRequested();
                await Awaitable.NextFrameAsync(token);
            }

            // 5. 전환 완료 후 DOTween 잔여 상태 정리
            DOTween.KillAll();
        }
        catch (OperationCanceledException)
        {
            Debug.Log("[SceneLoader] 씬 로딩 작업이 취소되었습니다.");
        }
        catch (Exception ex)
        {
            Debug.LogError($"[SceneLoader] 씬 로딩 중 예외 발생: {ex.Message}");
            Debug.LogException(ex);
        }
        finally
        {
            IsLoading = false;
            LoadingProgress = 0f;
        }
    }

    private void OnDestroy()
    {
        if (_cts != null)
        {
            _cts.Cancel();
            _cts.Dispose();
            _cts = null;
        }

        if (ReferenceEquals(_instance, this))
        {
            _instance = null;
        }

        // [High Safety] DOTween 킬
        DOTween.KillAll();
    }
}
#endif