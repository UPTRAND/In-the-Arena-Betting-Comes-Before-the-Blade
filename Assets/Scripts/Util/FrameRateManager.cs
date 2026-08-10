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

    private static void Apply()
    {
        QualitySettings.vSyncCount = 0;
        Application.targetFrameRate = TargetFrameRate;
    }
}
