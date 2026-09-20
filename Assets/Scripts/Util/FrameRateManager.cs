using UnityEngine;

/// <summary>
/// Keeps the application target frame rate at 60 FPS.
/// </summary>
public static class FrameRateManager
{
    private const int TargetFrameRate = 60;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    private static void Initialize()
    {
        Application.focusChanged -= OnFocusChanged;
        Application.focusChanged += OnFocusChanged;
        Apply();
    }

    private static void OnFocusChanged(bool hasFocus)
    {
        if (hasFocus)
            Apply();
    }

    /// <summary>프로젝트의 프레임 속도 정책을 한 곳에서 적용합니다.</summary>
    public static void Apply()
    {
        QualitySettings.vSyncCount = 0;
        Application.targetFrameRate = TargetFrameRate;
    }
}
