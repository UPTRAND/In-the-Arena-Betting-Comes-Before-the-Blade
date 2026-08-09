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
    public static AsyncSceneLoader Instance => _instance;

    public const string LOADING_SCENE_NAME = "Loading";

    /// <summary>
    /// 로딩 상태. 하위 호환성을 위해 LoadingProgressService의 값을 반환합니다.
    /// </summary>
    public static bool IsLoading => LoadingProgressService.Instance != null && LoadingProgressService.Instance.IsLoading;

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
    public static async Awaitable LoadSceneAsync(string targetSceneName, CancellationToken token = default)
    {
        if (string.IsNullOrEmpty(targetSceneName))
        {
            throw new ArgumentException("[SceneLoader] 대상 씬 이름이 올바르지 않습니다.");
        }

        if (LoadingProgressService.Instance.IsLoading)
        {
            Debug.LogWarning("[SceneLoader] 이미 다른 로딩이 진행 중입니다.");
            return;
        }

        EnsureInstanceExists();

        if (!Application.CanStreamedLevelBeLoaded(targetSceneName))
        {
            throw new InvalidOperationException($"[SceneLoader] Build Settings에 없는 씬입니다: {targetSceneName}");
        }

        if (!Application.CanStreamedLevelBeLoaded(LOADING_SCENE_NAME))
        {
            throw new InvalidOperationException($"[SceneLoader] 중간 로딩 씬({LOADING_SCENE_NAME})이 Build Settings에 없습니다.");
        }

        await _instance.StartLoadSequenceAsync(targetSceneName, token);
    }

    private async Awaitable StartLoadSequenceAsync(string targetSceneName, CancellationToken token)
    {
        using var session = LoadingProgressService.Instance.BeginSession();
        AsyncOperation targetSceneOp = null;

        try
        {
            await ScreenFaderTransition.FadeOutAsync(LOADING_ENTRY_FADE_SECONDS, token);

            // 1. 씬 전환 전 필요시 DOTween KillAll 등 정리
            // (주의: DDOL 씬 요소에 영향이 갈 수 있으나 레거시 호환을 위해 유지)
            DOTween.KillAll();

            // 2. "Loading" 씬 비동기 로드
            AsyncOperation loadingSceneOp = SceneManager.LoadSceneAsync(LOADING_SCENE_NAME);
            
            // Loading 씬으로 전환되는 도중에는 취소하지 않습니다.
            while (!loadingSceneOp.isDone)
            {
                await Awaitable.NextFrameAsync();
            }

            await ScreenFaderTransition.FadeInAsync(LOADING_ENTRY_FADE_SECONDS, token);
            float loadingSceneEnteredAt = Time.realtimeSinceStartup;
            session.Report(0.1f);

            // 3. 목적지 씬 비동기 로드 (0.9 단계에서 대기)
            targetSceneOp = SceneManager.LoadSceneAsync(targetSceneName);
            targetSceneOp.allowSceneActivation = false;

            while (targetSceneOp.progress < 0.9f)
            {
                token.ThrowIfCancellationRequested();

                // 0.0 ~ 0.9 값을 0.1 ~ 1.0으로 매핑 (Loading 씬 진입분 10% 제외)
                float normalized = targetSceneOp.progress / 0.9f;
                session.Report(Mathf.Lerp(0.1f, 1f, normalized));
                await Awaitable.NextFrameAsync(token);
            }

            // 4. 로딩 준비 완료 후 100% 진행도 적용 및 1프레임 시각적 대기
            session.Report(1.0f);
            await Awaitable.NextFrameAsync(token); // UI에 100% 렌더링될 기회 부여

            float remainingDisplayTime = MIN_LOADING_DISPLAY_SECONDS - (Time.realtimeSinceStartup - loadingSceneEnteredAt);
            if (remainingDisplayTime > 0f)
            {
                await Awaitable.WaitForSecondsAsync(remainingDisplayTime, token);
            }

            await ScreenFaderTransition.FadeOutAsync(LOADING_EXIT_FADE_SECONDS, token);

            // 5. 목적지 씬 활성화
            targetSceneOp.allowSceneActivation = true;

            // 목적지 씬 활성화 중에도 취소하지 않습니다.
            while (!targetSceneOp.isDone)
            {
                await Awaitable.NextFrameAsync();
            }

            await ScreenFaderTransition.FadeInAsync(LOADING_EXIT_FADE_SECONDS, token);

            // 6. 전환 완료 시 추가 정리
            DOTween.KillAll();

            // 7. 성공적 완료
            session.Complete();
        }
        catch (OperationCanceledException)
        {
            // 데이터 로딩 및 `allowSceneActivation = false` 진입 이전에는 정상 취소됨.
            // 단, 이미 `targetSceneOp` 가 시작되었다면 강제로 활성화를 마무리합니다.
            if (targetSceneOp != null && !targetSceneOp.isDone && !targetSceneOp.allowSceneActivation)
            {
                Debug.LogWarning("[SceneLoader] 씬 로드 진행 중 취소가 발생하여 강제로 씬 활성화를 마무리합니다.");
                targetSceneOp.allowSceneActivation = true;
                while (!targetSceneOp.isDone)
                {
                    await Awaitable.NextFrameAsync(); // 이미 취소된 토큰 전달 안 함
                }
                session.Complete();
            }
            
            throw; // 취소 발생 사실은 상위 호출자에 전달
        }
        catch (Exception ex)
        {
            Debug.LogError($"[SceneLoader] 씬 로딩 중 오류 발생: {ex.Message}");
            throw;
        }
        // finally는 using var session 블록에 의해 Dispose 됨
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
