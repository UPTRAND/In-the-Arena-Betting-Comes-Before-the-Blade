#if UNITY_6000_0_OR_NEWER
using System;
using System.Threading;
using UnityEngine;
using UnityEngine.SceneManagement;
using DG.Tweening;
using InTheArena.Util;

[DisallowMultipleComponent]
public class AsyncSceneLoader : MonoBehaviour
{
    private const float LOADING_ENTRY_FADE_SECONDS = 0.5f;
    private const float LOADING_EXIT_FADE_SECONDS = 0.3f;
    private const float MIN_LOADING_DISPLAY_SECONDS = 1.5f;

    private static AsyncSceneLoader _instance;
    private static bool s_TransitionInProgress;
    private static int s_RequestVersion;
    public static AsyncSceneLoader Instance => _instance;

    public const string LOADING_SCENE_NAME = "Loading";

    /// <summary>
    /// 로딩 상태. 하위 호환성을 위해 LoadingProgressService의 값을 반환합니다.
    /// </summary>
    public static bool IsLoading => s_TransitionInProgress ||
        (LoadingProgressService.Instance != null && LoadingProgressService.Instance.IsLoading);

    /// <summary>
    /// 로딩 진행도. 하위 호환성을 위해 LoadingProgressService의 값을 반환합니다.
    /// </summary>
    public static float LoadingProgress => LoadingProgressService.Instance != null ? LoadingProgressService.Instance.Progress : 0f;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    private static void AutoInitialize()
    {
        EnsureInstanceExists();
    }

    private static void EnsureInstanceExists()
    {
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
    }

    #region Single-Line Public API
    /// <summary>
    /// 기존 하위 호환용 단일 호출 API. 내부적으로 LoadSceneAsync의 예외를 관찰하는 Fire-and-forget 래퍼입니다.
    /// </summary>
    public static void LoadScene(string targetSceneName)
    {
        LoadSceneInternal(targetSceneName);
    }

    private static async void LoadSceneInternal(string targetSceneName)
    {
        try
        {
            await LoadSceneAsync(targetSceneName, CancellationToken.None);
        }
        catch (OperationCanceledException)
        {
            Debug.Log("[SceneLoader] 씬 로딩이 취소되었습니다.");
        }
        catch (Exception ex)
        {
            Debug.LogError($"[SceneLoader] 씬 로딩 중 예외 발생: {ex.Message}");
            Debug.LogException(ex);
        }
    }
    #endregion

    /// <summary>
    /// 핵심 비동기 로딩 로직. Awaitable을 반환합니다.
    /// 취소는 씬 전환 시퀀스 시작 전 또는 안전한 데이터 처리 구간에서만 적용되며, 씬 비동기 작업이 시작된 이후에는 전환 완료를 우선합니다.
    /// </summary>
    public static async Awaitable LoadSceneAsync(
        string targetSceneName,
        CancellationToken token = default,
        Func<IProgress<float>, CancellationToken, Awaitable> prepareData = null,
        Func<CancellationToken, Awaitable> prepareScene = null,
        string loadingSceneName = LOADING_SCENE_NAME)
    {
        token.ThrowIfCancellationRequested();
        if (string.IsNullOrEmpty(targetSceneName) || !Application.CanStreamedLevelBeLoaded(targetSceneName))
        {
            throw new ArgumentException("Build Settings에 없는 대상 씬입니다: " + targetSceneName);
        }
        if (!Application.CanStreamedLevelBeLoaded(loadingSceneName))
        {
            throw new ArgumentException("Build Settings에 없는 로딩 씬입니다: " + loadingSceneName);
        }

        EnsureInstanceExists();
        int requestVersion = ++s_RequestVersion;
        while (s_TransitionInProgress)
        {
            await Awaitable.NextFrameAsync(token);
        }
        if (requestVersion != s_RequestVersion)
        {
            throw new OperationCanceledException("새 씬 전환 요청으로 대체되었습니다.");
        }
        token.ThrowIfCancellationRequested();
        s_TransitionInProgress = true;
        try
        {
            if (SceneManager.GetActiveScene().name == targetSceneName && prepareData == null && prepareScene == null)
            {
                // 씬 활성화 이전 취소로 남은 오버레이도 같은 전환 소유권에서 해제합니다.
                await ScreenFaderTransition.FadeInAsync(LOADING_EXIT_FADE_SECONDS, token);
                return;
            }
            await _instance.StartLoadSequenceAsync(targetSceneName, loadingSceneName, token, prepareData, prepareScene);
        }
        finally
        {
            s_TransitionInProgress = false;
        }
    }

    /// <summary>모든 화면의 로딩·활성화·취소 정리를 하나의 직렬 전환으로 실행합니다.</summary>
    private async Awaitable StartLoadSequenceAsync(
        string targetSceneName,
        string loadingSceneName,
        CancellationToken token,
        Func<IProgress<float>, CancellationToken, Awaitable> prepareData,
        Func<CancellationToken, Awaitable> prepareScene)
    {
        using var session = LoadingProgressService.Instance.BeginSession();
        AsyncOperation pendingScene = null;
        try
        {
            await ScreenFaderTransition.FadeOutAsync(LOADING_ENTRY_FADE_SECONDS, token);
            DOTween.KillAll();
            pendingScene = SceneManager.LoadSceneAsync(loadingSceneName);
            await CompleteSceneOperationAsync(pendingScene);
            await ScreenFaderTransition.FadeInAsync(LOADING_ENTRY_FADE_SECONDS, token);
            float enteredAt = Time.realtimeSinceStartup;
            session.Report(0.1f);

            void ReportDataProgress(float progress)
            {
                session.Report(Mathf.Lerp(0.1f, 0.8f, Mathf.Clamp01(progress)));
            }
            if (prepareData != null)
            {
                await prepareData(new Progress<float>(ReportDataProgress), token);
            }
            token.ThrowIfCancellationRequested();

            pendingScene = SceneManager.LoadSceneAsync(targetSceneName);
            pendingScene.allowSceneActivation = false;
            while (pendingScene.progress < 0.9f)
            {
                session.Report(Mathf.Lerp(0.8f, 1f, pendingScene.progress / 0.9f));
                await Awaitable.NextFrameAsync(token);
            }
            session.Report(1f);
            while (Time.realtimeSinceStartup - enteredAt < MIN_LOADING_DISPLAY_SECONDS)
            {
                await Awaitable.NextFrameAsync(token);
            }

            await ScreenFaderTransition.FadeOutAsync(LOADING_EXIT_FADE_SECONDS, token);
            await CompleteSceneOperationAsync(pendingScene);
            token.ThrowIfCancellationRequested();
            if (prepareScene != null)
            {
                await prepareScene(token);
            }
            await ScreenFaderTransition.FadeInAsync(LOADING_EXIT_FADE_SECONDS, token);
            session.Complete();
        }
        finally
        {
            // 취소·예외 어느 경로에서도 Unity의 활성화 대기 큐를 막아 두지 않습니다.
            await CompleteSceneOperationAsync(pendingScene);
        }
    }

    /// <summary>시작된 Unity 씬 작업은 취소할 수 없으므로 다음 전환 전에 활성화를 마칩니다.</summary>
    private static async Awaitable CompleteSceneOperationAsync(AsyncOperation operation)
    {
        if (operation == null || operation.isDone || !Application.isPlaying)
        {
            return;
        }
        operation.allowSceneActivation = true;
        while (!operation.isDone && Application.isPlaying)
        {
            await Awaitable.NextFrameAsync();
        }
    }

    private void OnDestroy()
    {
        // 중복 인스턴스가 파괴될 때는 전역 KillAll을 수행하지 않습니다.
        if (ReferenceEquals(_instance, this))
        {
            _instance = null;
            // [High Safety] DOTween 킬
            DOTween.KillAll();
        }
    }
}
#endif
